# Phase 0 Research: Project Documentation & Developer README

## Decision: Use plain Markdown in `README.md` and `docs/`

**Rationale**: The user explicitly requested a root README and a `docs` folder. The repository does not yet
have documentation infrastructure, and the immediate need is trustworthy content for GitHub and local readers.
Plain Markdown is reviewable, portable, and requires no new dependency or build step.

**Alternatives considered**:

- Static-site generator: rejected for this feature because it adds tooling before there is a content base.
- Generated docs from source comments: rejected because the primary knowledge source is SpecKit design
  artifacts, not public API XML documentation yet.
- Single large README: rejected because P2/P3 scenarios need deeper, navigable material.

## Decision: Organize docs by reader journey

**Rationale**: The feature has three audiences: evaluators, integrators, and curious contributors. A journey
structure lets the README stay short while deeper docs remain discoverable:

- Evaluate: `README.md`, `docs/index.md`, `docs/status.md`
- First success: `docs/getting-started.md`
- Use correctly: `docs/guides/*`
- Understand/contribute: `docs/architecture.md`, `docs/design-decisions.md`, `docs/speckit-sources.md`

**Alternatives considered**:

- Organize by SpecKit feature number: rejected because external developers should not need to know project
  history before understanding the product.
- Organize by source project: useful for contributors but weaker for first-time usage.

## Decision: Make capability status explicit

**Rationale**: SpecKit artifacts include implemented, planned, and deferred capabilities. Documentation must
avoid implying that all designed features are already shipped. Use a small vocabulary:

- **Implemented**: present in current source/tests/spec baseline.
- **Planned**: specified in current SpecKit artifacts but not completed.
- **Deferred**: intentionally out of current scope or moved to a future feature.
- **Illustrative**: example shows intended syntax/convention but is not end-to-end verified by this docs work.

**Alternatives considered**:

- Hide planned/deferred work: rejected because the project is early and prospective developers need status.
- Use "stable/experimental" only: rejected because that blurs implementation status with compatibility.

## Decision: Treat SpecKit artifacts as source-of-truth citations

**Rationale**: FR-011 and SC-005 require content to be derived from existing SpecKit artifacts. The docs should
include a `speckit-sources.md` page mapping major claims to artifacts, and individual pages should mention
their source basis where useful without becoming cluttered.

**Alternatives considered**:

- Footnote every sentence: rejected as too noisy for developer documentation.
- Rely on commit history only: rejected because SpecKit artifacts are already curated design records.

## Decision: Use minimal DormantQL examples based on current samples and grammar contracts

**Rationale**: Examples are the highest drift risk. They should mirror the current sample schema and query
units, use only documented grammar forms, and avoid obsolete `002` syntax. Required forms:

- `.dqls` schema module with `entity`, `name: TypeExpr[?]`, `primary`, `concurrency`, and `Set<T>`.
- `.dql` units with `query`/`mutation`, aliases, alias-qualified members, lowercase parameter types,
  symbolic operators, `returning`, and documented `with` value flow only where status is clear.

**Alternatives considered**:

- Invent richer examples: rejected because unsupported claims and grammar drift would be likely.
- Show C# generated output exhaustively: rejected for README/getting-started; use focused snippets and link
  to the generated-code guide.

## Decision: Add Todo and Scheduling as ASP.NET Core API sample applications

**Rationale**: The updated spec requires examples beyond the minimal quickstart so developers can recognize
DormantQL in familiar problem spaces, and the clarification specifies that these must be new ASP.NET API
sample applications. Todo/task-list and Scheduling APIs should be small enough to review quickly, and they
should demonstrate schema plus read/write DormantQL units behind HTTP endpoints without implying a runtime
scheduling engine.

**Alternatives considered**:

- Add only documentation snippets: rejected because the clarification asks for new sample API applications.
- Add only prose descriptions of Todo/Scheduling: rejected because FR-015 requires concrete DormantQL snippets.
- Model recurrence, reminders, or calendar integrations: rejected because those capabilities are not grounded in
  current SpecKit artifacts and would overstate product scope.

## Decision: Document provider status conservatively

**Rationale**: Current source contains PostgreSQL provider and spatial companion projects. `005` specifies
SQLite + dialect framework as planned and NMemory as deferred. Public docs should therefore describe:

- PostgreSQL as the primary/reference provider.
- SQLite as planned per the provider-support feature, not present unless implemented before docs land.
- NMemory as deferred/non-AOT future work.
- Docker requirement for PostgreSQL provider tests.

**Alternatives considered**:

- Present SQLite as available because it is specified: rejected until code exists.
- Omit NMemory: rejected because `005` explicitly resolves its status and AOT caveat.

## Decision: Validate documentation without new tooling first

**Rationale**: The feature should not add a docs dependency unless implementation discovers a real need.
Manual and shell-based checks are enough for this initial docs set:

- List Markdown links and verify relative targets exist.
- Review examples against `specs/003-linq-dql-grammar/contracts/dql-grammar.md`.
- Review status claims against source files and SpecKit artifacts.
- Run `./build.sh build` or `./build.sh all` when environment allows.

**Alternatives considered**:

- Add `markdown-link-check`: rejected as a new Node dependency for a small docs set.
- Add a custom validator now: deferred until documentation volume justifies it.
