# Feature Specification: Dormant — AOT-First, Schema-DSL ORM for .NET 10 (DormantQL DSL)

**Feature Branch**: `001-orm-aot-sourcegen`

**Created**: 2026-05-24

**Status**: Draft

**Input**: User description: "Construa um novo ORM .NET baseado na API e em um subconjunto das funcionalidades do NHibernate (...) com foco em AOT, source-gen, performance e baixo consumo de memória, tooling de migração/execução similar ao EF Core, e uma DSL própria de entidades inspirada no EdgeDB/Gel convertida em tipos parciais." Refined through discussion into a schema-DSL-first ORM (not a database server) with its own schema/query language, **DormantQL** (`.dqls` schema files, `.dql` query files): PostgreSQL is the primary provider (others derived later), the DSL is the primary surface for schema and queries (LINQ is not the primary query surface), all generated SQL is produced at build time, the object-graph "feel" comes from first-class links and single-round-trip nested fetches, and a core invariant holds throughout: **the result type of any query is fully known at build time; only values and predicates vary at runtime**.

## Clarifications

### Session 2026-05-25

- Q: Mapping/authoring surface for v1? → A: The schema DSL is the primary surface. No LINQ provider and no fluent/attribute mapping as the primary path in v1.
- Q: Depth of the relationship feature set? → A: First-class links plus single-round-trip nested fetch. No implicit lazy loading and no inheritance-mapping strategies in v1.
- Q: Database providers in v1? → A: PostgreSQL is the primary (reference) provider; other relational providers are derived later and out of scope for v1.
- Q: Dynamic queries? → A: Optional/conditional parameters are supported with the result type held statically fixed. Runtime-decided result shapes are out of scope for v1.
- Q: Full entity vs projection? → A: Both are distinct, build-time-generated types. A projection is its own type, never a partially-populated entity. Accessing a field that was not fetched is a compile error.
- Q: Phasing of the DSL? → A: Phase 1 covers schema, links, fetch shapes/projections, optional parameters, and basic filtering/ordering/pagination. A richer expression sub-language (computed expressions, polymorphic queries, set operations, aggregates) is Phase 2.
- Q: Phasing of DSL tooling? → A: Phase 1 ships syntax + located diagnostics. Editor language-server/IntelliSense integration is Phase 2.
- Q: How do entities mutate and how does the session detect changes? → A: Generated entities are mutable; the session holds an identity map and a per-entity loaded snapshot, and detects changes by diffing current state against the snapshot at commit (no proxies, no per-property dirty flags). Column-level granularity comes from the snapshot diff.
- Q: How is an unfetched link represented on a full entity? → A: Each link member is a generated typed wrapper (e.g. `Link<T>` / `LinkSet<T>`) encoding explicit loaded/unloaded state; the loaded value is reachable only after the unloaded case is handled, so unfetched data cannot be read as if present. Projections keep their own mechanism (the field is simply absent from the type).
- Q: Is explicit on-demand loading of an unloaded link in scope for v1? → A: Yes. An unloaded link can be filled by an explicit session call that issues a query and transitions the wrapper to loaded. This is the only non-fetch-shape retrieval path; loading is never implicit.
- Q: What is the v1 vs Phase 2 scope of the DormantQL query/DML surface? → A: v1 (Tier A) = shaped select (entity/projection), forward path navigation, single/multi-link nested fetch in one round-trip, filter, order by, limit/offset, required+optional parameters with coalesce, core predicates (=, comparisons, like/ilike, in, exists, ??), single-result narrowing, and basic insert/update/delete. Phase 2 (Tier B) = computed expressions, polymorphism, backlinks, link properties, set ops, aggregates, for-union, upsert, nested insert, +=/-=, group by, free objects. The `**` deep-splat is excluded permanently; a schema-resolved `*` single-splat is allowed.
- Q: Does the DSL support dictionaries/maps and many-to-many relationships? → A: Many-to-many is a v1 capability via multi-valued links (bidirectional = a multi link per side; edge data = an explicit join entity; backlinks and `@prop` link properties are Phase 2 sugar). Dictionaries have no first-class type in v1 — model as a JSON property (opaque) or a key/value child entity (queryable); a first-class typed `map<K,V>` is deferred to Phase 2. v1 property value types are enumerated in FR-036.
- Q: Should the DSL support database-native types and functions per provider (non-portable)? → A: Yes, by design. Native types (e.g. PostgreSQL `jsonb`) map to build-time-known .NET representations; native functions/operators are invoked via declared typed signatures **and** a raw typed SQL fragment escape (both keep the result type static). Native constructs are explicitly provider-scoped through a per-provider directive, and targeting an unsupported provider is a build-time located diagnostic (never silent). v1 ships the mechanism + built-in `jsonb` in the core PostgreSQL provider; GIS (PostGIS) rides the same mechanism as an optional companion package. AOT, no-boxing, and build-time-known-type guarantees are preserved (FR-038..FR-044).
- Q: How is provider connectivity and provider-specific behavior verified in tests? → A: Against a real provider instance (never mocks or an in-memory fake), provisioned ephemerally in Docker via Testcontainers. This is the required mechanism for the integration-style acceptance scenarios (US2, US5, US8) and the provider-dependent success criteria (SC-003, SC-010, SC-012, SC-013); CI must provide a Docker daemon for these tests.
- Q: What does a DormantQL module map to in the database? → A: A module maps to a database schema. Generated tables and all SQL/DDL are qualified by the module's schema (the module name is the DB schema name), and migration tooling creates the schema as needed (FR-045).
- Q: What namespace do generated .NET types use? → A: NOT the bare module name. The namespace is `PascalCaseEachPart(root namespace + relative folder segments of the schema file + module name)`. Example: `schema/app.dqls` in project `Dormant.Sample.Quickstart` → `Dormant.Sample.Quickstart.Schema.App` (FR-046). This reads naturally to a .NET developer.
- Q: What is the member/link declaration syntax? → A: A unified `name: [multi] Type[?]` form (Kotlin/TypeScript-like); the `-> ` arrow and the `single`/`multi` prefix forms are removed. A member whose type is a value type is a property, otherwise a link. Members are **required by default**; a trailing `?` makes them optional/nullable; `multi` marks a multi-valued link. So `email: str` (required), `email: str?` (optional), `author: User` (required single link), `author: User?` (optional single link), `posts: multi Post` (multi link) (FR-047).
- Q: How are non-nullable generated members expressed in C#? → A: With the C# `required` modifier (not a `= default!` initializer). Nullable members are optional. The materializer populates required members through an accessor path that bypasses object-initializer enforcement (FR-048).

## User Scenarios & Testing _(mandatory)_

### User Story 1 - Model a domain in the schema DSL with first-class links (Priority: P1)

A .NET developer describes their domain in a concise schema DSL — entities, properties, and the
**links** (relationships) between them — and the build produces strongly-typed **partial** entity
types reflecting that schema, which the developer extends in separate files.

**Why this priority**: The schema is the source of truth from which entities, queries, and
migrations all derive. Nothing else exists without it; it is the smallest standalone slice of value.

**Independent Test**: Write a schema with two entities and a link between them, build, and confirm
compiling partial types with the declared properties and a navigable link are generated; add a
hand-written partial with custom members and confirm it coexists and survives regeneration.

**Acceptance Scenarios**:

1. **Given** a valid schema, **When** the project builds, **Then** partial entity types with the
   declared properties and links are generated.
2. **Given** generated partials, **When** the developer adds a separate partial file with custom
   members, **Then** the project compiles and custom members coexist with generated ones.
3. **Given** an edited schema, **When** the developer rebuilds, **Then** generated members update and
   hand-written partials are left untouched.
4. **Given** a schema error (e.g., a link to an undefined entity), **When** the build runs, **Then** a
   clear, source-located diagnostic is reported and no generated output hides the error.
5. **Given** the same schema input, **When** generation runs repeatedly, **Then** the generated output
   is identical (deterministic).

---

### User Story 2 - Persist and load full entities through a session (Priority: P1)

A developer opens a session (unit of work), creates / loads / updates / deletes mapped entities
within an explicit transaction against PostgreSQL, and the session tracks changes and persists only
what changed.

**Why this priority**: Persistence of full entities is the core ORM round-trip. Together with US1
and US3 it forms the minimum usable ORM.

**Independent Test**: Insert an entity and commit, confirm the row exists; load it, modify one
field, commit, and confirm only that column changed; delete it and confirm the row is gone.

**Acceptance Scenarios**:

1. **Given** an open session, **When** a new full entity is saved and committed, **Then** a row with
   the mapped values exists.
2. **Given** a loaded entity that is modified, **When** the unit of work commits, **Then** only the
   changed columns are written.
3. **Given** a loaded entity, **When** it is deleted and committed, **Then** the row no longer exists.
4. **Given** two sessions modifying the same row, **When** both commit, **Then** an optimistic
   concurrency conflict is reported rather than one silently overwriting the other.

---

### User Story 3 - Query returning exact result types: entity or projection, with nested links (Priority: P1)

A developer writes a query in the DSL that returns **either a full entity or a projection shape**.
A projection is its own generated type containing exactly the requested fields and nested links;
related data declared in the shape is fetched in a **single database round-trip**. A field that was
not requested is **not present on the result type** — accessing it is a compile error, not a runtime
surprise.

**Why this priority**: This is the product's core differentiator and its safe-by-default guarantee.
It eliminates the classic partially-loaded-entity bug and delivers the object-graph "feel".

**Independent Test**: Query a projection of `{ id, name, posts: { title } }`; confirm the result type
exposes exactly those members, that `posts` is populated in one round-trip, and that referencing a
non-requested field (e.g., `email`) fails to compile.

**Acceptance Scenarios**:

1. **Given** a query requesting a full entity, **When** it runs, **Then** the result is the entity
   type with all its mapped columns populated.
2. **Given** a query requesting a projection shape, **When** it runs, **Then** the result is a
   distinct generated type containing exactly the requested fields and nested links.
3. **Given** a projection that includes nested links, **When** it runs, **Then** the nested data is
   retrieved in a single database round-trip.
4. **Given** a projection result, **When** code references a field not included in the shape, **Then**
   the project fails to compile (the field does not exist on the type).
5. **Given** any query, **When** the project is built, **Then** the SQL is produced at build time (no
   runtime query compilation).

---

### User Story 4 - Dynamic queries via optional parameters with a statically fixed result type (Priority: P2)

A developer writes one query that accepts **optional/conditional parameters** (e.g., an optional
filter, an optional sort) so the executed SQL adapts at runtime, while the **result type stays the
same** regardless of which parameters are supplied.

**Why this priority**: Conditional search/filter is a common business need; doing it without losing
static typing is a concrete advantage. It builds on US3 but is separable.

**Independent Test**: Define a query with two optional filter parameters; run it with neither, one,
and both supplied; confirm the result type is identical across all runs and that each run returns the
correctly filtered rows.

**Acceptance Scenarios**:

1. **Given** a query with optional parameters, **When** a parameter is omitted, **Then** the
   corresponding filter/sort is absent from the executed query and the result type is unchanged.
2. **Given** the same query, **When** parameters are supplied, **Then** the executed query reflects
   them and the result type is still unchanged.
3. **Given** any combination of optional parameters, **When** the project builds, **Then** the single
   statically-known result type is generated once and reused for all combinations.

---

### User Story 5 - Evolve the schema with migration and execution tooling (Priority: P2)

A developer uses command-line tooling to create migrations from schema changes, apply them to a
PostgreSQL database, roll them back, and inspect migration status — a workflow comparable in scope
to mainstream .NET ORM tooling.

**Why this priority**: Schema evolution is required for real use and is an explicit goal; it follows
the core runtime.

**Independent Test**: From a schema, generate and apply an initial migration and confirm the database
schema matches; change the schema, generate and apply an incremental migration; roll it back and
confirm the previous state is restored.

**Acceptance Scenarios**:

1. **Given** a schema with no prior migrations, **When** a migration is created, **Then** an artifact
   describing the full initial schema is produced.
2. **Given** an applied migration and a changed schema, **When** a new migration is created, **Then**
   it contains only the incremental difference.
3. **Given** pending migrations, **When** they are applied, **Then** the database schema matches the
   current schema definition.
4. **Given** an applied migration, **When** it is rolled back, **Then** the database returns to the
   prior migration's state.
5. **Given** a migration with a destructive operation (data loss), **When** it is generated/applied,
   **Then** the tooling flags it rather than performing it silently.
6. **Given** any state, **When** status is requested, **Then** applied and pending migrations are
   reported.

---

### User Story 6 - Ship a Native AOT / fully-trimmed application (Priority: P2)

A developer publishes an application using the ORM with Native AOT and full trimming; it starts,
maps, queries, and migrates with no library-originated trimming/AOT warnings and no first-use
runtime warm-up.

**Why this priority**: AOT-first is a defining constraint; it must be proven end-to-end as its own
deliverable.

**Independent Test**: Publish the US2/US3 sample as Native AOT with trimming, run the same scenarios,
and confirm identical results with zero library-originated warnings and no warm-up step.

**Acceptance Scenarios**:

1. **Given** an application using the ORM, **When** published with Native AOT and full trimming,
   **Then** there are no trimming/AOT warnings attributable to the library.
2. **Given** the AOT-published application, **When** it runs the full scenario suite, **Then** results
   are identical to the non-trimmed run.
3. **Given** the AOT-published application, **When** it starts cold, **Then** the first query requires
   no runtime code-generation/warm-up step.

---

### User Story 7 - Extend the ORM with custom behavior (Priority: P3)

A developer registers custom type/value handlers and naming/mapping conventions without modifying
the library, and the extensions preserve AOT compatibility and the performance guarantees.

**Why this priority**: Extensibility is an explicit goal ("extensível, mas simples") but refines
rather than enables the product.

**Independent Test**: Register a custom handler for a type unsupported by default, use it on a mapped
property, and confirm round-trip persistence and querying; confirm an AOT publish still has zero
library-originated warnings.

**Acceptance Scenarios**:

1. **Given** a type unsupported by default, **When** a custom type handler is registered, **Then**
   properties of that type persist and materialize correctly.
2. **Given** a custom naming convention, **When** mapping is resolved, **Then** names follow the
   convention without per-entity configuration.
3. **Given** any registered extension, **When** the application is AOT-published, **Then** no
   extension forces runtime reflection on hot paths and no library-originated warnings appear.

---

### User Story 8 - Use database-native types and functions (e.g., JSONB, GIS) (Priority: P2)

A developer uses provider-native capabilities — a `jsonb` column with containment/path operators, or
PostGIS geometry types with spatial functions — directly from the schema and query DSL, accepting
that these constructs are tied to the provider and explicitly **not** portable. Native types map to
build-time-known .NET representations and native functions are invoked with a statically known result
type, so the safe-by-default guarantee still holds.

**Why this priority**: Real PostgreSQL applications depend on first-class features (JSONB, full-text,
GIS) that a lowest-common-denominator portable surface cannot express. Supporting them from the start
is a deliberate differentiator over a strictly portable ORM.

**Independent Test**: Declare an entity with a `jsonb` property, persist and load it, and run a query
that filters using a native containment operator; confirm the query's result type is known at build
time and the AOT publish has zero library-originated warnings. Repeat a spatial query via the GIS
companion package.

**Acceptance Scenarios**:

1. **Given** an entity with a provider-native-typed property, **When** it is persisted and loaded,
   **Then** the value round-trips through its build-time-known .NET representation with no boxing of
   value-type columns.
2. **Given** a declared native function/operator, **When** it is used in a filter or projection,
   **Then** the invocation is type-checked and the query's result type is known at build time.
3. **Given** a native construct not covered by a declared signature, **When** a raw provider-SQL
   fragment with a declared result type and parameter bindings is used, **Then** it executes with
   caller values passed as parameters (not concatenated) and a statically known result type.
4. **Given** a native construct marked for a provider, **When** the build targets a provider that does
   not support it, **Then** a clear, source-located diagnostic is reported (non-portability is never
   silent).
5. **Given** an application using native types/functions (incl. the GIS companion package), **When**
   it is published with Native AOT and full trimming, **Then** there are no library-originated
   warnings and no runtime warm-up.

---

### Edge Cases

- A schema link targets an undefined entity, or required links form a cycle → build-time located
  diagnostic; no masking output.
- A query projection references a non-fetched field → compile error (field absent from the type).
- Code reads an unloaded link on a full entity → the unloaded case must be handled (the loaded value
  is not directly reachable); the link can be filled via an explicit on-demand session load.
- A migration would drop a column/table (data loss) → flagged, not applied silently.
- Two sessions modify the same row → optimistic concurrency conflict reported.
- A query returns zero rows, or a single-result query matches zero/multiple rows → defined behavior.
- A mapped column is null but the target member is non-nullable → defined behavior (diagnostic/error).
- Optional parameters supplied in an unusual combination → result type unchanged; executed SQL valid.
- Generated partials and hand-written partials declare conflicting members → defined compile-time
  behavior.
- A native function/operator is invoked with arguments that do not match its declared signature →
  compile-time error.
- A raw native SQL fragment omits its declared result type → build error (native constructs may not
  produce a runtime-decided shape).
- A native construct is targeted at a provider that does not support it → source-located build
  diagnostic (never a silent non-portable build).

## Requirements _(mandatory)_

### Functional Requirements

#### Schema DSL & generation

- **FR-001**: The system MUST provide a schema DSL for declaring entities, properties, and first-class
  links (relationships) in a concise, human-authored form.
- **FR-002**: The DSL is the primary surface for defining the model and writing queries in v1; a LINQ
  provider and fluent/attribute mapping are NOT the primary path in v1.
- **FR-003**: The system MUST generate strongly-typed **partial** entity types from the schema so
  developers extend generated types in separate files without edits being overwritten on
  regeneration.
- **FR-004**: Generation MUST be deterministic (same schema in → same output) and MUST report clear,
  source-located diagnostics for invalid schemas without emitting output that hides the error.
- **FR-005**: The system MUST expose a small, simple session/unit-of-work API recognizable as a subset
  of the NHibernate-style model (session, identity map, transactional commit), favoring simplicity
  over feature-completeness.

#### Result types, projections & safe-by-default access

- **FR-006**: Every query MUST have a result type that is fully known at build time. The result type
  MUST NOT depend on runtime values.
- **FR-007**: A query MUST be able to return either a full entity type or a projection type; a
  projection MUST be a distinct generated type containing exactly the requested fields and nested
  links — never a partially-populated entity.
- **FR-008**: Accessing a field that was not included in a query's shape MUST be a compile-time error
  (the field is absent from the result type).
- **FR-009**: The system MUST NOT perform implicit lazy loading. On a full entity, every link MUST be
  represented by a generated type that encodes explicit loaded/unloaded state (e.g. `Link<T>` /
  `LinkSet<T>`), such that unloaded related data cannot be read as if present — the loaded value is
  reachable only after the unloaded case is handled. An unloaded link MAY be retrieved on demand
  through an explicit session call that issues a query and transitions the wrapper to loaded; this
  explicit call is the only non-fetch-shape retrieval path and loading MUST never be implicit.
- **FR-010**: A fetch shape that includes nested links MUST be retrieved in a single database
  round-trip.

#### Querying & dynamic parameters

- **FR-011**: The DSL MUST support querying with filtering, ordering, projection, and pagination over
  mapped entities and their links.
- **FR-012**: The DSL MUST support optional/conditional parameters such that supplying or omitting a
  parameter changes the executed SQL while the result type remains statically fixed.
- **FR-013**: All SQL for queries MUST be produced at build time; there MUST be no runtime query
  compilation on the core query path.

#### Persistence & session

- **FR-014**: The session MUST support create, read, update, and delete of full entities within
  explicit transactions, tracking changes and persisting only modified state on commit. Generated
  entity types MUST be mutable; the session MUST track changes by diffing each loaded entity against a
  per-entity snapshot captured at load time (not via runtime proxies or per-property dirty flags), and
  the commit MUST write only the columns whose values differ from the snapshot.
- **FR-015**: The system MUST support optimistic concurrency control and report conflicts to the
  caller.

#### AOT, trimming & runtime behavior

- **FR-016**: The library MUST be fully compatible with Native AOT and full trimming: consuming
  applications MUST publish with zero library-originated trimming/AOT warnings.
- **FR-017**: Mapping and materialization MUST NOT use runtime code generation or runtime reflection
  on hot paths; required metadata and accessors MUST be available at build time.
- **FR-018**: The core CRUD and query paths MUST NOT require first-use runtime warm-up.
- **FR-019**: Materialization MUST avoid boxing of value-type columns.

#### Migration & execution tooling

- **FR-020**: The system MUST provide command-line tooling to create migrations from schema changes,
  apply pending migrations, roll back migrations, and report migration status.
- **FR-021**: Generated migrations MUST capture only the incremental difference between the previous
  and current schema state.
- **FR-022**: The tooling MUST flag potentially destructive (data-loss) migration operations rather
  than applying them silently.
- **FR-023**: The migration/execution tooling MUST run on the AOT-first toolchain without forcing
  consuming applications to abandon AOT publishing.

#### Providers

- **FR-024**: PostgreSQL MUST be the primary (reference) provider for v1. The provider boundary MUST
  be designed so additional relational providers can be derived later, but no other provider is in
  scope for v1. The provider also scopes which native types and functions are available (FR-038..FR-044).

#### Extensibility

- **FR-025**: The system MUST provide extension points for custom type/value handlers and
  naming/mapping conventions, usable without modifying the library.
- **FR-026**: Extension points MUST preserve AOT compatibility and MUST NOT silently introduce runtime
  reflection on hot paths.

#### Tooling & diagnostics (DX)

- **FR-027**: The DSL MUST ship, in v1, with syntax definition and source-located diagnostics. Editor
  language-server/IntelliSense integration is Phase 2.
- **FR-028**: Errors (schema, query, migration, mapping) MUST be actionable: stating what failed, why,
  and the next corrective step.
- **FR-029**: Every public capability MUST ship with documentation and at least one runnable example.

#### DSL query & DML language surface (DormantQL)

- **FR-030**: The v1 query surface MUST support: shaped selection returning a full entity or a
  projection, forward path navigation over properties and links, single- and multi-link nested fetch
  retrieved in one round-trip (FR-010), `filter` predicates, `order by` (ascending/descending,
  multiple sort keys, empty-first/empty-last placement), and `limit`/`offset` pagination.
- **FR-031**: The v1 query surface MUST support required and optional query parameters. An optional
  parameter MAY change the executed SQL and the number of rows returned but MUST NOT change the result
  type (consistent with FR-006/FR-012). A coalesce/default mechanism MUST be available so that an
  omitted optional parameter cannot silently empty an otherwise non-empty result.
- **FR-032**: The v1 predicate surface MUST include equality and ordered comparison, pattern matching
  (case-sensitive and case-insensitive), set membership, existence testing, and empty/`null`
  coalescing.
- **FR-033**: Result cardinality MUST map onto the generated .NET surface: a single/optional link or
  result is exposed as an optional single value; a multi link or result is exposed as a collection.
  The DSL MUST provide an explicit single-result narrowing operation (assert-single / assert-exists
  in spirit) that converts a many-or-optional result into a required single result at the type level.
- **FR-034**: The v1 DML surface MUST support `insert` (assigning single and multi links from
  sub-queries selecting existing rows), `update` (filter + set, including replacing a link set), and
  `delete` (filter), each executed within the session/transaction model of FR-014.
- **FR-035**: The following constructs are explicitly **Phase 2** and out of scope for v1: computed
  shape expressions, polymorphic/type-intersection queries (`[is Type]`), backlinks, link properties,
  set operations (union/distinct), broader aggregates and conditional (if/else) expressions, iterative
  `for … union`, upsert (`unless conflict … else`), nested insert, incremental link mutation
  (`+=`/`-=`), grouping (`group … by`), anonymous free-object results, and a first-class typed
  dictionary/map value type (`map<K,V>`). The deep-splat (`**`) MUST NOT be supported in any phase
  because it cannot yield a build-time-known result type; a schema-resolved single-splat (`*`) MAY be
  offered since it expands at build time.

#### Property value types & relationships

- **FR-036**: The schema DSL MUST support, as v1 property value types: textual, boolean, integer,
  floating-point and high-precision decimal, arbitrary-precision integer, UUID, date/time and
  duration, binary, and JSON values, together with the collection types array, tuple, and named tuple
  (ranges where the primary provider supports them). A first-class typed dictionary/map value type
  (`map<K,V>`) is **Phase 2** (FR-035); in v1 a key→value structure MUST be modeled either as a
  JSON-typed property (opaque, not individually queryable) or as a key/value child entity reached
  through a link (queryable). Provider-native value types (e.g. `jsonb`, GIS geometry) are additionally
  available per FR-038.
- **FR-037**: The schema DSL MUST support many-to-many relationships in v1 via multi-valued links.
  Bidirectional navigation in v1 is expressed by declaring a multi link on each side; relationship
  (edge) data is carried by an explicit join entity (two single links plus its own properties).
  Backlink navigation (`.<link`) and link properties (`@prop`) are Phase 2 conveniences (FR-035) and
  MUST NOT be required to express a many-to-many relationship in v1.

#### Native (per-provider) types & functions

- **FR-038**: The schema DSL MUST allow a property to be typed as a provider-native type (e.g.
  PostgreSQL `jsonb`), mapped to a build-time-known .NET representation through an AOT-safe type
  binding. Persisting and loading such a property MUST avoid boxing of value-type columns, and the
  result type of a query over it MUST remain statically known.
- **FR-039**: The DSL MUST allow provider-native functions and operators to be invoked in predicates,
  ordering, and projected/computed values through **declared signatures** (parameter types and a
  single statically-known return type), so invocations are type-checked and the query's result type
  stays known at build time.
- **FR-040**: The DSL MUST provide a **raw provider-SQL fragment** escape for native constructs not
  covered by a declared signature. Such a fragment MUST declare its result type and MUST NOT produce a
  runtime-decided result shape (consistent with FR-006).
- **FR-041**: Raw native fragments MUST support parameter binding so caller-supplied values are passed
  as query parameters; user values MUST NOT be string-concatenated into SQL.
- **FR-042**: Native types, functions, and raw fragments MUST be explicitly **provider-scoped** via a
  per-provider directive. The build MUST emit a clear, source-located diagnostic when a native
  construct is targeted at a provider that does not support it; non-portability MUST NOT be silent.
  The directive and diagnostic mechanism MUST exist in v1 even though PostgreSQL is the only provider,
  so future providers surface gaps at build time.
- **FR-043**: Native type bindings and native function/fragment invocations MUST preserve Native AOT
  and full-trimming compatibility (zero library-originated warnings) and MUST NOT introduce runtime
  reflection or runtime query compilation on the core path.
- **FR-044**: v1 MUST ship the native mechanism with the PostgreSQL provider and **built-in `jsonb`
  support**. GIS (PostGIS) types and functions MUST be supportable through the same mechanism and
  provided as an **optional companion package** with at least one runnable end-to-end example; GIS
  MUST NOT be bundled into the core provider.

#### Module mapping, namespaces & member syntax

- **FR-045**: Each DormantQL **module maps to a database schema**. Generated tables and all emitted
  SQL/DDL MUST be qualified by that schema (the module name is the database schema name), and migration
  tooling MUST create the schema when it does not exist.
- **FR-046**: Generated .NET types MUST be placed in a namespace computed as
  `PascalCaseEachPart(rootNamespace + relativeFolderSegments + moduleName)`, where `rootNamespace` is the
  consuming project's root namespace, `relativeFolderSegments` are the folders of the schema file
  relative to the project, and `moduleName` is the module. The bare module name MUST NOT be used as the
  namespace. Example: `schema/app.dqls` in project `Dormant.Sample.Quickstart` →
  `Dormant.Sample.Quickstart.Schema.App`.
- **FR-047**: Members MUST be declared with a unified `name: [multi] Type[?]` syntax. A member whose
  type is a value type is a **property**; otherwise it is a **link**. Members are **required by
  default**; a trailing `?` makes a member optional (nullable property, or optional single link); the
  `multi` keyword marks a multi-valued link. The previous arrow form (`multi name -> Type` /
  `single name -> Type`) MUST be removed; there is no `single` keyword (a bare single link is required,
  `Type?` is optional).
- **FR-048**: Generated non-nullable members MUST use the C# `required` modifier (not a `= default!`
  initializer); nullable members are optional and omit `required`. Materialization MUST populate
  required members without tripping required-initialization enforcement (e.g. via an accessor/constructor
  path that bypasses object-initializer checks), preserving the no-reflection guarantee (FR-017).

### Key Entities _(conceptual model of the system)_

- **Schema Definition (DSL)**: The human-authored declaration of entities, properties, and links; the
  single source of truth for generated types, queries, and migrations.
- **Entity**: A generated, mutable partial type representing a fully-mappable domain object; its
  loaded state is captured as a session-held snapshot for change detection.
- **Link**: A first-class relationship between entities, navigable in shapes and queries. On a full
  entity it is exposed as a typed wrapper carrying explicit loaded/unloaded state.
- **Projection / Shape**: A distinct generated type describing exactly the fields and nested links a
  query returns.
- **Query**: A DSL-authored, build-time-compiled description of data to retrieve, with a statically
  known result type and optional runtime parameters.
- **Session / Unit of Work**: The transactional boundary tracking loaded entities and batching
  changes; owns the identity map and the per-entity loaded snapshots used to diff and persist only
  changed columns on commit.
- **Migration**: A versioned, ordered, reversible schema change with applied/pending state.
- **Type Handler**: An extension defining how a .NET type is stored to and read from a column.
- **Convention**: A rule deriving mapping defaults (e.g., names) without per-member configuration.
- **Provider**: The adapter targeting a relational database (PostgreSQL primary) for SQL generation
  and schema operations; also the scope that declares which native types and functions are available.
- **Native Type Binding**: A provider-scoped mapping between a database-native column type (e.g.
  `jsonb`, `geometry`) and a build-time-known .NET representation, with AOT-safe read/write.
- **Native Function / Operator**: A provider-scoped database function or operator invocable in
  queries, either via a declared signature (typed parameters and return) or as a raw typed SQL
  fragment escape; both keep a statically known result type.
- **Provider Directive**: A per-provider marker that scopes native constructs and drives build-time
  portability diagnostics when an unsupported provider is targeted.

## DSL Language Surface (DormantQL)

The DSL has two faces: the **schema** face (entities, properties, links — covered by FR-001..FR-005)
and the **query/DML** face described here. Both faces are expressed in **DormantQL**, its own
language compiled to PostgreSQL SQL at build time. The examples below are **illustrative of intended
shape, not final syntax**. The governing invariant holds across every construct: the result type is
fully known at build time; only values and predicates vary at runtime.

A core design idea is adopted deliberately: **type and cardinality are static properties of every
query expression.** Absence is an empty result, single vs multi links are distinguished, and the
generated .NET surface mirrors that (optional single vs collection). This is precisely what makes the
build-time-known-result-type guarantee natural rather than bolted on.

### Schema declarations (v1)

A module maps to a **database schema**; generated types live in a .NET namespace derived from the
project's root namespace + the schema file's folders + the module name (FR-045/FR-046). Members use a
unified `name: [multi] Type[?]` form — **required by default**, `?` for optional, `multi` for collections
(FR-047). A member typed as a value type is a property; otherwise it is a link.

```
module app;                 # → DB schema "app"; types → <RootNamespace>.<Folders>.App

entity User {
  id: uuid primary;         # required property (C# `required`)
  email: str;               # required property
  bio: str?;                # optional property (nullable)
  manager: User?;           # optional single link
  posts: multi Post;        # multi link
  version: int concurrency; # optimistic-concurrency token
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;             # required single link
}
```

### Query constructs — v1 (Tier A)

| Construct | Illustrative form | Notes |
|-----------|-------------------|-------|
| Shaped select (entity) | `select Movie { id, title }` | Result type = the shape. |
| Shaped select (projection) | `select Movie { title, director: { name } }` | Distinct generated projection type. |
| Path navigation | `Movie.director.name` | Forward steps; cardinality follows the schema. |
| Single-link nested fetch | `{ director: { name } }` | Optional single value on the result. |
| Multi-link nested fetch | `{ actors: { name } }` | Collection on the result; one round-trip (FR-010). |
| Filter | `… filter .year = 1999` | Values vary; type does not. |
| Order by | `… order by .title desc then .year` | Multi-key, asc/desc, empty-first/last. |
| Pagination | `… offset 20 limit 10` | Int expressions; both optional. |
| Required parameter | `… filter .name = <str>$name` | Empty argument is rejected. |
| Optional parameter | `… offset <optional int>$skip` | Omission drops the clause; result type unchanged (FR-031). |
| Coalesce / default | `<optional str>$q ?? ''` | Prevents an omitted optional from emptying the result. |
| Predicates | `=`, `<`/`>`/`<=`/`>=`, `like`/`ilike`, `in`, `exists` | The v1 predicate surface (FR-032). |
| Single-result narrowing | `assertSingle(...)` / `assertExists(...)` (in spirit) | Turns many/optional into a required single value at the type level (FR-033). |

### DML constructs — v1 (Tier A)

| Construct | Illustrative form | Notes |
|-----------|-------------------|-------|
| Insert | `insert Movie { title := "...", year := 1999 }` | Properties assigned with `:=`. |
| Insert + link to existing | `insert Movie { …, director := (select Person filter .name = <str>$n) }` | Single/multi links assigned from sub-queries over existing rows. |
| Update | `update Movie filter .id = <uuid>$id set { title := <str>$t }` | Filter + set; may replace a whole link set. |
| Delete | `delete Movie filter .id = <uuid>$id` | Filter-scoped delete within the session/transaction. |

### Deferred to Phase 2 (Tier B)

Computed shape expressions (`field := expr`), polymorphic/type-intersection queries (`[is Type]`,
`[is Type].field`), backlinks (`.<link[is Type]`), link properties (`@prop`), set operations
(`union`, `distinct`), broader aggregates (`sum`, `array_agg`, …) and conditional (`if/else`)
expressions, iterative `for … union` (incl. bulk insert), upsert (`unless conflict … else`), nested
insert, incremental link mutation (`+=`/`-=`), grouping (`group … by`), anonymous free-object
results, and a first-class typed dictionary/map value type (`map<K,V>`). The deep-splat (`**`) is
excluded in every phase (it cannot yield a build-time-known result type); a schema-resolved
single-splat (`*`) may be offered because it expands at build time.

### Property value types & relationships (v1)

v1 property values cover the common scalar set (text, bool, integers, float/decimal, bigint, UUID,
date/time, duration, bytes, JSON) plus `array`, `tuple`, and named-tuple collections; ranges where the
provider supports them. There is **no first-class dictionary/map type in v1** (deferred to Phase 2) —
model a key→value structure as a JSON property (opaque) or a key/value child entity (queryable).

**Many-to-many is a v1 capability**, expressed with multi-valued links:

| Need | v1 form | Phase 2 sugar |
|------|---------|---------------|
| Basic m:n | a `multi` link | — |
| Navigable both directions | a `multi` link on each side | backlink `.<link[is …]` |
| m:n with edge data (e.g. `since`, `role`) | explicit join entity (two required single links + its own props) | link property `@prop` |

### Native (per-provider) types & functions

Unlike a strictly portable surface, the DSL exposes provider-native types and functions directly.
These are **explicitly non-portable** and provider-scoped; targeting an unsupported provider is a
build-time diagnostic, never silent. Native constructs still obey the hard invariant — each declares a
build-time-known type. Forms below are illustrative, not final syntax.

| Construct | Illustrative form | Notes |
|-----------|-------------------|-------|
| Native-typed property | `tags: jsonb` | Provider-native column type → build-time-known .NET representation. |
| Declared native function | `func st_dwithin(geometry, geometry, float64) -> bool` then `filter st_dwithin(.geom, $p, 100)` | Typed signature; invocation type-checked. |
| Native operator | `filter .tags @> <jsonb>$q` | Provider operator with a declared result type. |
| Raw typed fragment | `native(postgres, returns: bool) { ".tags @> {0}" }(<jsonb>$q)` | Escape for the long tail; declares return type; values bound as parameters. |
| Provider directive | `@provider(postgres) { … }` | Scopes native constructs; drives portability diagnostics. |

v1 ships the mechanism + built-in `jsonb` in the core PostgreSQL provider; GIS (PostGIS) rides the
same mechanism as an optional companion package.

## Success Criteria _(mandatory)_

### Measurable Outcomes

- **SC-001**: A consuming application publishes with Native AOT and full trimming with **zero**
  library-originated trimming/AOT warnings.
- **SC-002**: The result type of every query is determined at build time; attempting to access a
  field not included in a query's shape fails to compile **100%** of the time (no partial-entity
  runtime errors are possible).
- **SC-003**: A fetch shape with nested links executes in **exactly one** database round-trip
  (verifiable by counting executed statements).
- **SC-004**: Materializing a large result set introduces **no** per-row heap allocations for
  value-type columns (no boxing), verifiable via allocation measurement.
- **SC-005**: A single query with optional parameters returns the **same** result type across all
  parameter combinations, while producing correctly filtered results for each.
- **SC-006**: A cold-started AOT-published application completes its first query with **no** runtime
  code-generation/warm-up step (first-query latency dominated by I/O).
- **SC-007**: For a representative CRUD-and-query benchmark on PostgreSQL, throughput is at least on
  par with, and memory allocated per operation is lower than, the mainstream baseline .NET ORM on the
  same hardware and database.
- **SC-008**: A new developer can author a schema, generate entities, and complete a full CRUD-and-
  query round-trip by following the quickstart in under **15 minutes**, using only the DSL and
  documented public APIs.
- **SC-009**: Invalid schemas produce source-located diagnostics **100%** of the time (no silent
  generation failures).
- **SC-010**: A developer can take a schema change from edit to an applied (and rollback-able)
  migration on a PostgreSQL database using only the provided command-line tooling, without
  hand-editing SQL.
- **SC-011**: Using only the DSL, a developer can express every v1 (Tier A) query construct — shaped
  select of an entity and of a projection, single- and multi-link nested fetch, filter, multi-key
  order, pagination, required and optional parameters with coalescing, and single-result narrowing —
  and every v1 DML operation (insert with links to existing rows, update, delete), with a runnable
  example covering each construct.
- **SC-012**: A `jsonb` property round-trips and is queryable through a native containment/path
  operator with the query's result type known at build time and **zero** library-originated
  AOT/trimming warnings.
- **SC-013**: A GIS (PostGIS) spatial type and at least one spatial function work end-to-end via the
  companion package in a Native-AOT-published application with **zero** library-originated warnings.
- **SC-014**: When a native construct is targeted at a provider that does not support it, the build
  reports a source-located diagnostic **100%** of the time (no silent non-portable builds).

## Out of Scope (v1)

- The Phase 2 (Tier B) DSL constructs enumerated in FR-035: computed shape expressions,
  polymorphic/type-intersection queries, backlinks, link properties, set operations (union/distinct),
  broader aggregates and conditional expressions, iterative `for … union`, upsert
  (`unless conflict … else`), nested insert, incremental link mutation (`+=`/`-=`), grouping
  (`group … by`), anonymous free-object results, and a first-class typed dictionary/map value type
  (`map<K,V>`).
- The deep-splat (`**`) result expansion — excluded in every phase, not just v1 (no build-time-known
  result type).
- Editor language-server/IntelliSense integration for the DSL (Phase 2).
- A LINQ provider as a query surface.
- Implicit lazy loading.
- Runtime-decided (dynamic) result shapes.
- A language-neutral core enabling non-.NET bindings (possible future direction; the DSL compiler and
  schema model SHOULD remain architecturally separable to keep it feasible, but it is not built in v1).
- Database-server-style centralized enforcement (access policies/row-level rules valid for all
  external clients), which an in-process library cannot guarantee.
- Relational providers other than PostgreSQL.
- Cross-provider portability or automatic translation of native (per-provider) types and functions —
  native constructs are non-portable by design (FR-038..FR-044); only the provider directive and the
  build-time portability diagnostic are in scope, not cross-provider equivalence or translation.
- GIS (PostGIS) support bundled into the core provider — GIS ships as an optional companion package
  built on the native mechanism (FR-044).

## Assumptions

- Target runtime is .NET 10; earlier runtimes are out of scope.
- The primary user is a .NET application developer integrating the library; there is no end-user UI
  for the ORM itself.
- **DormantQL**, the schema/query language, is inspired in spirit by EdgeDB's EdgeQL (concise
  declarative entities, first-class links, shape-based fetches) but is its own distinct language
  targeting generated .NET partial types and build-time SQL. "EdgeDB", "EdgeQL", and "Gel" are
  trademarks of their respective owner and are referenced here solely for descriptive comparison;
  Dormant is not affiliated with or endorsed by them.
- "Object-database feel" refers to ergonomics (objects, links, graph-shaped fetches), not to
  database-server enforcement semantics.
- "Tooling similar to EF Core" refers to the developer command-line workflow scope (migrations,
  database update, status/inspection), not command compatibility.
- Implementation techniques named in the original prompt (source generation, UnsafeAccessor, generics
  to avoid boxing) are the means to satisfy the AOT, no-runtime-reflection, no-boxing, and
  build-time-known-type outcomes; the specification fixes the outcomes, planning fixes the techniques.
- Async database access is supported for persistence and querying, per standard .NET expectations.
- PostgreSQL is the reference implementation; other providers are derived from the same provider
  boundary in later versions.
- Native (per-provider) types and functions are a deliberate, accepted departure from cross-provider
  portability: they are PostgreSQL-first in v1 and explicitly non-portable, traded for first-class
  access to features like JSONB and GIS. The build-time-known-result-type and AOT guarantees still
  hold for them.
- Provider connectivity and provider-specific behavior are verified against a real provider instance
  provisioned ephemerally via Docker (Testcontainers) — not against mocks or an in-memory fake — so the
  integration-style acceptance scenarios and provider-dependent success criteria exercise the actual
  database. A Docker daemon is required to run these tests locally and in CI.
- A DormantQL module is the unit that maps to a database schema; the generated .NET namespace is derived
  from the consuming project and file location (root namespace + folders + module, PascalCased), not from
  the bare module name — this reads naturally to a .NET developer while keeping the DB schema named after
  the module.
- Members are required by default (C# `required`) with `?` marking optional/nullable, mirroring how a
  .NET developer reads non-nullable vs nullable; this applies uniformly to properties and single links.
