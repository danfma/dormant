<!-- SPECKIT START -->
For additional context about technologies, project structure, conventions, and
important decisions for the active feature, read the current plan:
`specs/012-edgeql-constraints/plan.md` (with `research.md`, `data-model.md`,
`contracts/`, `quickstart.md`).

**Feature 012 – EdgeQL-Style Constraints** replaces DormantQL's ad-hoc trailing type-modifiers
(`primary`, `concurrency`, `db("…")`) with a uniform **constraint system** modeled on Gel/EdgeDB:
named constraints in a `{ constraint …; }` block on a member or at the entity level, from a standard
library with familiar names (`exclusive`→`unique`, `expression`→`check`, `max_len_value`→`max_length`,
…). Constraints can span multiple members (`constraint unique on (a, b)`), carry boolean expressions
(`constraint check (…)`), and pin the SQL constraint name via `as {name}`. Adds **custom scalar
types** (`scalar X extending str { … }`) and **entity inheritance/composition** (`abstract entity` +
`extending`, flattened into one table). `primary`/`concurrency` become constraints; the old modifier
syntax is **removed (clean break, MAJOR)** with a migration guide. Touches lexer→parser→`SchemaModel`
→validator(ORM029+)→`SqlIr`(`ConstraintDef`)→per-dialect DDL renderers (PG full; SQLite `regex`
fallback)→`EntityBindingEmitter`, plus the 011 grammar and all in-repo `.dqls`. Phased P-A…P-F;
constraint IR + DDL is the high-risk core. The background plans below remain completed-feature context:

**Feature 011 – DQL Syntax Highlighting** turns the DormantQL DSL into a first-class citizen in editors. It delivers:
- A portable grammar (Tree-sitter primary + TextMate secondary) maintained in `tooling/grammar/`.
- A proper VS Code extension (initial focus) with automatic activation for `.dql` and `.dqls`.
- Support for Zed (via Tree-sitter queries) immediately after VS Code.
- Best-effort repository viewing (GitHub etc.) via `.gitattributes` + planned future Linguist contribution.
- Explicit design for future expansion to JetBrains Rider and other editors.

Key decisions from clarify: web/repository highlighting stays in scope; grammar must be designed for extensibility from day one; implementation order is VS Code extension first, then Zed; the grammar is owned and maintained in our repository rather than depending on external projects. All of this is in service of Principle IV (First-Class Tooling) and Principle I (Developer Experience First). Background completed features remain below.
Feature 009 turns the query `select` into an **EdgeQL-style shape** blended with the
LINQ grammar and **flattens entities** by removing the runtime relationship wrappers
(`Ref`/`RefSet`/…). Shaped reads return nested immutable projections (shape = type);
nested to-one/to-many is fetched in **one round-trip** via **SQL JSON aggregation**
(PG `jsonb_build_object`/`jsonb_agg`, SQLite `json_object`/`json_group_array`) rendered
per dialect; nested rows materialize through **generator-emitted `Utf8JsonReader`
parsers** (no reflection — STJ source-gen can't see generated types). Relationships
become **schema-only metadata** driving joins, navigation (`a.writer.name`), shapes, and
EdgeQL **backlinks**; entities expose the **FK id scalar** (`WriterId`). Also: free
composition `select { x = a.f, y = b.g }`, read-side `with` as **CTEs** (single query),
`into` user records (structural match). `SqlIr` is currently a flat single-table builder
→ joins/subqueries/CTEs/JSON are **net-new IR**. **Breaking (MAJOR)**; Principle III
"links loaded/unloaded" clause to be amended (`/speckit-constitution`). Phased P-A…P-G;
to-many/JSON is the high-risk core. The background plans below remain completed-feature
context:
Feature 008 turns the placeholder `tests/Dormant.Benchmarks` into a real
**BenchmarkDotNet** suite comparing **Dormant vs Dapper, EF Core, Insight.Database**
across five operations (read-by-key, filtered read, insert, update, delete) over one
shared **in-memory SQLite** DB — Dormant owns the DDL (`DormantSqlite.EnsureCreatedAsync`),
peers bind to the same table; `MemoryDiagnoser` + Dormant baseline; single-command run
(`dotnet run -c Release --project tests/Dormant.Benchmarks`) + CI `Dry`-job smoke. The
project ref swaps PG → `Dormant.Provider.Sqlite`; not AOT (peers use reflection); Insight
via inline-SQL APIs (provider shim as fallback — primary risk). The background plans
below remain completed-feature context:
`specs/005-sqlite-nmemory-providers/plan.md` — Feature 005 generalizes the PostgreSQL-only `SqlRenderer` into a **multi-variant
dialect framework** (per-dialect renderers over the existing neutral `SqlIr`,
rendered at build time) and adds the **SQLite** provider (`Dormant.Provider.Sqlite`,
AOT-clean via `Microsoft.Data.Sqlite.Core` + `bundle_e_sqlite3` + explicit
`Batteries_V2.Init()`). Generated code selects the SQL variant by `session.Dialect`
(`enum DialectId { PostgreSql, Sqlite }`) — a branch over const strings, no runtime
SQL compilation; PG output stays byte-identical. NMemory is deferred. Cross-provider
parity proven by one parameterized conformance suite (PG via Testcontainers + SQLite
in-memory). The predecessor plans below remain the completed-feature background:
`specs/003-linq-dql-grammar/plan.md` — done + green: the cutover (LINQ grammar) and
`returning` for insert/update/delete. **Remaining (post-2026-05-26 clarify, see
plan.md "Post-clarify design" + FR-020/021/022)**: ref → `<ref>_id` FK column +
`alias.ref = expr` (FR-020); a **`with name = (expr)` block + single terminal
`select`** that binds each expression's result object (ref/FK context → target PK)
and executes each binding as its own SQL statement in the transaction — portable,
**not** CTE-bound (FR-021/022); this supersedes the old "multi-command" framing.
Sibling `specs/004-raw-string-sql/plan.md` (done): generated SQL emitted as C#
multi-line **raw string literals** (`"""…"""`) — value byte-identical. 003 is a **front-end
grammar replacement** built on `specs/002-immutable-command-dml/` (the immutable,
command-driven direction — its runtime semantics are preserved unchanged): the DQL
unit surface becomes a **LINQ-/SQL-hybrid, brace-delimited grammar** with explicit
aliases and C#/TypeScript operators. Units are `query name(...) { from E u where
u.x == p select u }` (reads) and `mutation name(...) { insert|update|delete E u …
[returning …] }` (writes); unit names are snake_case → PascalCase C# methods;
entities PascalCase; value types **PascalCase** (Feature 012): `String Char Byte Short Int Long
Float Double Decimal Bool Uuid DateTime Date Time Json` (+ `optional T`); old lowercase aliases
removed (ORM003). Operators: `== != < <= > >= && || !`,
assignment `=`; members alias-qualified (`u.email`). Mutation result is inferred
(insert→id, update/delete→affected count) and optionally shaped by `returning`
(mirrors `select`) or a trailing read; multi-command blocks flow values via `with`.
Removed 002 forms (`command`, `= …;`, leading-dot, `:=`, `and`/`or`) MUST NOT parse.
Only the generator front end (lexer/parsers/models/naming + the two emitters'
mapping) changes; the SQL IR/renderer, immutable entities, session, and provider
are reused from 002. `specs/001-orm-aot-sourcegen/` remains the deeper return point.

Key conventions: .NET 10 / C# 14; AOT-first (zero library trimming/AOT warnings,
no runtime reflection or query compilation on hot paths, no boxing); build-time SQL
via a Roslyn incremental source generator; ValueTask-first async (await-once
discipline) + IAsyncEnumerable streaming; feature-first layout with dependencies
pointing one direction inward (Dormant.Abstractions = stable kernel, Dormant.Core =
engine, adapters = Provider.PostgreSql / Spatial.PostgreSql / Tool). Use semantic
folder/namespace names, NOT architectural labels: there is no `Ports` namespace —
abstraction interfaces are grouped by capability (Abstractions.Providers, .Mapping,
.Migrations, .Native; plus .Sessions, .Links, .Querying).

Testing: TUnit (source-generated, AOT-native; runs on Microsoft.Testing.Platform,
so test projects are `Exe` and `dotnet test` works). Use TUnit's built-in
assertions (Shouldly only if they prove insufficient). Provider/connectivity and
provider-specific behavior are verified against a REAL provider in ephemeral Docker
via Testcontainers — never mocks; a Docker daemon is required. Generator tests use
Verify (`Verify.TUnit` + `Verify.SourceGenerators`) snapshots + cacheability checks.

DormantQL conventions: a module maps to a DB schema (schema-qualified DDL/SQL).
Generated namespace = PascalCaseEachPart(project RootNamespace + schema-file folders
+ module) — e.g. schema/app.dqls in Dormant.Sample.Quickstart → namespace
Dormant.Sample.Quickstart.Schema.App (NOT the bare module). Member syntax is
`name: TypeExpr[?]`: value type ⇒ property; single ref `name: Target`; collections
`Set<T>`/`List<T>`/`Bag<T>`/`Map<K,V>`. Relationship types (kernel): `Ref<T>`,
`RefSet<T>`, `RefList<T>`, `RefBag<T>`, `RefMap<K,V>` (renamed from Link/LinkSet) —
each a readonly struct with an Unloaded sentinel. Single-ref optionality is the
nullability of the type arg (orthogonal to load-state): required `owner: User` →
`Ref<User>`; optional `manager: User?` → `Ref<User?>` (so `Ref<T> where T : class?`);
collections take no element `?` (`Set<User>` → `RefSet<User>`). Properties + single
refs are required by default (`?` optional → C# `required`); collections default to the
Unloaded sentinel (`= RefSet<T>.Unloaded`), NEVER `= []`. Non-nullable members emit
C# `required`; materialize via a generated [SetsRequiredMembers] ctor on the entity
partial (ordinary setters; public parameterless ctor kept for required-init) — NOT
UnsafeAccessor/backing-field (fragile). Reads via public getters. Entities get
PK identity Equals/GetHashCode by default (opt out `[NoIdentityEquality]`).
Projections may target user-owned plain records (no Dormant types) — the Clean-Arch
boundary. (The committed kernel/generator predate the Ref rename + collections +
equality — revision pending; see plan.md Phase notes.)
<!-- SPECKIT END -->

## Commands
- Build: `dotnet build Dormant.slnx`
- Test: `dotnet test Dormant.slnx`
- Lint: `dotnet format Dormant.slnx --verify-no-changes`
- CI: GitHub Actions (`.github/workflows/ci.yml`)
