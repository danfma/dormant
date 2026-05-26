<!-- SPECKIT START -->
For additional context about technologies, project structure, conventions, and
important decisions for the active feature, read the current plan:
`specs/003-linq-dql-grammar/plan.md` (with `research.md`, `data-model.md`,
`contracts/`, and `quickstart.md` in the same directory). 003 is a **front-end
grammar replacement** built on `specs/002-immutable-command-dml/` (the immutable,
command-driven direction — its runtime semantics are preserved unchanged): the DQL
unit surface becomes a **LINQ-/SQL-hybrid, brace-delimited grammar** with explicit
aliases and C#/TypeScript operators. Units are `query name(...) { from E u where
u.x == p select u }` (reads) and `mutation name(...) { insert|update|delete E u …
[returning …] }` (writes); unit names are snake_case → PascalCase C# methods;
entities PascalCase; type keywords lowercase (`string bool int long double decimal
uuid datetime date json` + `optional T`). Operators: `== != < <= > >= && || !`,
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
