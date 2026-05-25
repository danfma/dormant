<!-- SPECKIT START -->
For additional context about technologies, project structure, conventions, and
important decisions for the active feature, read the current plan:
`specs/001-orm-aot-sourcegen/plan.md` (with `research.md`, `data-model.md`,
`contracts/`, and `quickstart.md` in the same directory).

Key conventions: .NET 10 / C# 14; AOT-first (zero library trimming/AOT warnings,
no runtime reflection or query compilation on hot paths, no boxing); build-time SQL
via a Roslyn incremental source generator; ValueTask-first async (await-once
discipline) + IAsyncEnumerable streaming; feature-first layout behind a Ports &
Adapters boundary (Dormant.Abstractions = ports/kernel, Dormant.Core = engine,
adapters = Provider.PostgreSql / Spatial.PostgreSql / Tool).

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
`name: [multi] Type[?]` (value type ⇒ property, else ⇒ link; required by default,
`?` optional, `multi` collection; no `->` arrow, no `single`). Non-nullable members
emit C# `required` (not `= default!`); materialize via a [SetsRequiredMembers] ctor
invoked through [UnsafeAccessor]. (The committed US1 generator predates these and
needs a revision pass — see plan.md Phase notes.)
<!-- SPECKIT END -->
