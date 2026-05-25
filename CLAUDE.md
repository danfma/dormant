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
<!-- SPECKIT END -->
