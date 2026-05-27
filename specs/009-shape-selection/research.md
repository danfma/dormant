# Phase 0 Research: Shape Selection + Flat Immutable Entities

Decisions resolving the Technical Context unknowns. Format: decision / rationale / alternatives.

## R1. Single-round-trip nested reads = SQL JSON aggregation

**Decision**: A shaped read compiles to **one** SELECT whose select-list builds the result as JSON.
Nested to-one → a scalar subquery returning a JSON object; nested to-many → a scalar subquery
returning a JSON array. Per dialect:
- **PostgreSQL**: `jsonb_build_object('k', expr, …)` for objects; `(select coalesce(jsonb_agg(jsonb_build_object(...) [order by …]), '[]'::jsonb) from child where child.fk = root.pk)` for arrays.
- **SQLite**: `json_object('k', expr, …)` for objects; `(select coalesce(json_group_array(json_object(...)), json('[]')) from child where child.fk = root.pk)` for arrays. (Inner ordering handled in the subquery; SQLite preserves `json_group_array` input order via the subquery's `order by`.)

The whole result row can be emitted as a **single JSON column** (the entire shape wrapped in one
`json_object`/`jsonb_build_object`), so even the root scalars travel as JSON — uniform materialization
(R2). The dialect framework (005) renders the function spellings; only the JSON-builder spelling
differs between dialects.

**Rationale**: One round-trip, no N+1, no parent-row duplication, no client-side grouping/dedup, and
arbitrary nesting depth — exactly how EdgeDB compiles shapes to Postgres. Both target engines ship the
needed JSON functions (PG native; SQLite JSON1 is compiled into the `e_sqlite3` bundle Dormant already
uses). Fits "build-time SQL, choose variant by `session.Dialect`".

**Alternatives considered**: (a) JOIN + flat rows + client-side grouping — rejected: parent-row fan-out,
complex dedup materialization, and nested-collection-within-collection becomes unwieldy. (b) N+1 /
per-level queries — rejected: violates the single-round-trip guarantee and Dormant's perf stance.

## R2. AOT nested materialization = generator-emitted `Utf8JsonReader` parsers

**Decision**: Dormant generates, per shaped result type, a `Utf8JsonReader`-based parse method that
reads the JSON column into the immutable projection record (and its nested records/lists). The shaped
`CompiledQuery<T>` materializer reads the single JSON column (`reader.GetValue<string>(0)` /
byte span) and invokes the emitted parser.

**Rationale**: Source generators do **not** see each other's generated output within one compilation,
so `System.Text.Json` source generation (`JsonSerializerContext`) cannot serialize Dormant-generated
record types — STJ's generator never sees them. A hand-emitted `Utf8JsonReader` parser keeps Dormant
in full control end-to-end: zero runtime reflection, AOT-clean, no boxing of scalar fields, low
allocation (no `JsonDocument`). `System.Text.Json` is in-box on .NET 10; `Utf8JsonReader` is a ref
struct safe under AOT.

**Alternatives considered**: (a) STJ source-gen context — rejected (generator-visibility above).
(b) STJ reflection-based `JsonSerializer.Deserialize` — rejected (reflection/AOT-trim warnings, boxing).
(c) Reuse the positional `IFieldReader` for nested data — not applicable; nesting arrives as one JSON
column, not as flat ordinals. Flat (non-shape) queries keep the existing positional `IFieldReader`
path unchanged.

## R3. Relationship navigation (`a.writer.name`) = SqlIr JOIN + qualified columns

**Decision**: Extend `SqlIr` with a real relational shape: a FROM that carries joins, column refs that
are alias-qualified, and select-items that are expressions (not bare strings). Navigation in a
predicate/expression resolves the relationship chain to JOIN(s) on the FK columns. Net-new IR nodes:
`Join`, `QualifiedColumn`, and an expression hierarchy for select-items.

**Rationale**: Current `SqlIr.SelectStatement` is single-table with bare-string columns and
single-column `SqlCondition` — it cannot express joins or navigation. Navigation is a prerequisite for
shapes and for filtering by related fields. Building the relational IR once serves both.

**Alternatives considered**: Correlated subquery for every navigation — rejected for predicates (a join
is clearer/faster); subqueries are still used for the *nested-shape* select-items (R1).

## R4. To-many declaration + backlink resolution

**Decision**: To-many is an **explicit schema collection member** (`articles: Set<Article>` on
`Author`) kept as build-time metadata only (no runtime entity member). The join key is resolved by
**backlink**: find the to-one on the child that targets the parent (e.g. `Article.writer: Author`) and
join `child.writer_id = parent.id`. When more than one candidate inverse exists, an explicit
disambiguation is required (diagnostic), matching EdgeQL's named-link model.

**Rationale**: Matches the maintainer's clarification (explicit collection + EdgeQL-style backlink).
Explicit declaration is discoverable and avoids ambiguous auto-inference; backlink reuses the child's
already-declared FK so the collection side needs no extra column.

**Alternatives considered**: Pure inference from any inverse FK — rejected (ambiguous with multiple FKs
to the same target). A dedicated join table for every to-many — rejected (FK backlink is the common,
cheaper case; many-to-many with a link entity is a later concern).

## R5. Read-side `with` = CTE (single query)

**Decision**: Read-side `with name = (query)` bindings render as SQL CTEs (`WITH name AS (…), … SELECT
…`) inside the single shaped query; later bindings may reference earlier ones (cascading). This is
distinct from feature 003's write-side `with`, which executes a separate statement per binding.

**Rationale**: Preserves the single-round-trip guarantee (FR-008/009) and lets each source be filtered
independently before composition in the final select. CTEs are supported by both PG and SQLite.

**Alternatives considered**: Multiple round-trips assembled in memory — rejected by clarification
(breaks single-round-trip). Inline subqueries only — viable but CTEs read better and enable cascading
references; renderer can still inline if a dialect needs it.

## R6. Entity flattening + FK scalar (blast radius)

**Decision**: `EntityEmitter` stops emitting `Ref/RefSet/RefList/RefBag/RefMap` members. For each
to-one relationship it emits the FK id scalar as an init-only `required`/optional property (e.g.
`Guid WriterId` / `Guid? ManagerId`), name = `<ref>_id`-backed via naming convention. To-many
collection declarations emit **no** entity member (metadata only). The wrapper types are deleted from
`Dormant.Abstractions.Entities`. The materializer ctor reads the FK scalar like any other column.

**Rationale**: Delivers FR-015/016; makes the canonical entity a faithful flat row; relationships move
to schema metadata + shapes. Landing this first (P-A) isolates the MAJOR breaking change.

**Blast radius**: every generated entity, the conformance schema/tests, the quickstart sample, the
generator snapshots, and any consumer reading wrapper members. The benchmark `Product` (scalars only)
is unaffected.

## R7. Constitution Principle III amendment (governance)

**Decision**: The Principle III clause "Links MUST be modeled with explicit, type-checked
loaded/unloaded states" is superseded by this feature; the no-partial-data guarantee is delivered by
projection/shape types. Amend the constitution via `/speckit-constitution` as part of P-G. Treat as a
clarifying refinement (the principle's intent — never read unfetched data — is preserved), versioned
per the constitution's own rules.

**Rationale**: The plan must not silently contradict the constitution; the amendment is an explicit,
reviewed step. Keeps governance honest.

## R8. Implementation refinement (post-P-B): to-one shapes via JOIN, JSON only for to-many

**Decision**: When implementing shape selection (US1), do **to-one** nested shapes with a `JOIN` +
**flat positional materialization** into nested records — reusing the P-B relational IR
(`JoinedSelectStatement`, qualified `ColumnExpr`) and the existing `IFieldReader` path. Reserve the
JSON-aggregation machinery (R1/R2) for **to-many** only (where multiple child rows require an array).

- to-one: `SELECT a.title, w.name FROM article a [INNER|LEFT] JOIN author w ON a.writer_id = w.id` →
  materialize `new Card(reader.GetValue<string>(0), new CardWriter(reader.GetValue<string>(1)))`.
  Required ref ⇒ `INNER JOIN` (nested record non-null). Optional ref ⇒ `LEFT JOIN` + a target-PK
  null-probe column ⇒ nullable nested record (null when the probe is null).
- to-many: still JSON (`json_group_array`/`jsonb_agg`) as a scalar subquery column, parsed by the
  emitted `Utf8JsonReader` materializer.

**Rationale**: to-one via JOIN is far simpler than JSON (no hand-rolled JSON parser, no
generator-emitted `Utf8JsonReader` for the to-one case), AOT-clean, no boxing, and reuses code already
shipped in P-B. A shaped query mixing to-one + to-many becomes a hybrid: flat positional columns for
the to-one parts + one JSON column per to-many part. This lets US1 land incrementally — **to-one shape
slice first** (no JSON), then the to-many/JSON slice.

**Sequencing for US1**: (1) lexer already has `{ } : ,`; (2) add a `SelectShape` AST to QueryModel
(keep `ProjectionFields` for the flat form); (3) parse `select alias { field, ref: { … } }` (root-shape;
detect by alias-then-`{`); (4) validate to-one nodes (cycle/backlink only matter for to-many); (5) emit
a `JoinedSelectStatement` whose items are the depth-first flattened shape columns + a JOIN per to-one
node, generate the nested immutable records, and a recursive positional materializer assigning ordinals
in flatten order; (6) conformance (`Article.writer`) + per-dialect snapshot. Defer to-many to the next slice.

## Resolved unknowns

| Unknown | Resolution |
|---------|-----------|
| Nested single round-trip | R1 — JSON aggregation, per-dialect functions, whole row as one JSON column |
| AOT nested materialization | R2 — generator-emitted `Utf8JsonReader` parsers (not STJ source-gen) |
| Navigation/join model | R3 — net-new relational SqlIr (joins, qualified cols, expression select-items) |
| To-many + backlink | R4 — explicit collection metadata + inverse-FK backlink, disambiguation diagnostic |
| Read `with` execution | R5 — CTEs in the single query, cascading |
| Entity flatten + FK scalar | R6 — drop wrappers, emit FK id scalar, delete wrapper types, migrate consumers |
| Constitution III | R7 — amend via /speckit-constitution in P-G |
