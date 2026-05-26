# Data Model: Project Documentation & Developer README

This feature does not add runtime entities. These are documentation-domain entities used to structure the
implementation and review.

## Entity: Documentation Set

**Purpose**: The complete public documentation surface for Dormant.

**Fields**:

- `root_readme`: `README.md`
- `docs_root`: `docs/`
- `pages`: collection of Documentation Page
- `source_artifacts`: collection of SpecKit Source Artifact
- `examples`: collection of DormantQL Example
- `sample_api_applications`: collection of Sample API Application
- `links`: collection of Documentation Link

**Validation rules**:

- Must include exactly one root README.
- Must include `docs/index.md`.
- Must be written in English.
- Must distinguish implemented, planned, deferred, and illustrative capabilities.
- Must include Todo/task-list and Scheduling task ASP.NET Core API sample applications.

## Entity: Documentation Page

**Purpose**: One Markdown page with a defined reader intent.

**Fields**:

- `path`: repository-relative Markdown path
- `audience`: evaluator, integrator, contributor, or mixed
- `purpose`: short statement of what the page helps the reader do
- `required_topics`: list of topics the page must cover
- `source_basis`: SpecKit source artifacts used by the page

**Planned pages**:

| Path | Audience | Purpose |
|------|----------|---------|
| `README.md` | Evaluator | Explain what Dormant is, why it exists, current status, and next docs links. |
| `docs/index.md` | Mixed | Provide the docs table of contents and reading paths. |
| `docs/getting-started.md` | Integrator | Walk from prerequisites to minimal schema/query/mutation and expected generated surface. |
| `docs/status.md` | Evaluator/integrator | Label implemented, planned, and deferred capabilities. |
| `docs/guides/dormantql-schema.md` | Integrator | Explain schema modules, entities, members, refs, collections, nullability, naming. |
| `docs/guides/queries-and-mutations.md` | Integrator | Explain current `.dql` units, clause order, operators, result inference, removed forms. |
| `docs/guides/naming-and-generated-code.md` | Integrator | Explain namespace/method/entity/result naming and generated C# expectations. |
| `docs/architecture.md` | Contributor | Explain packages/layers, inward dependencies, generator pipeline, provider boundary. |
| `docs/design-decisions.md` | Contributor/evaluator | Explain key decisions and rationale from SpecKit/constitution. |
| `docs/speckit-sources.md` | Contributor/reviewer | Map claims and pages to source artifacts for traceability. |

## Entity: SpecKit Source Artifact

**Purpose**: Authoritative design input.

**Fields**:

- `path`: repository-relative file path
- `feature`: feature number/name or constitution/project context
- `claim_categories`: grammar, generated code, architecture, provider status, build/test, governance

**Primary artifacts**:

- `.specify/memory/constitution.md`
- `specs/001-orm-aot-sourcegen/*`
- `specs/002-immutable-command-dml/*`
- `specs/003-linq-dql-grammar/*`
- `specs/004-raw-string-sql/*`
- `specs/005-sqlite-nmemory-providers/spec.md`
- `samples/Dormant.Sample.Quickstart/schema/app.dqls`
- `samples/Dormant.Sample.Quickstart/schema/app.dql`
- `build.sh`, `global.json`, `Dormant.slnx`, `Directory.Packages.props`

**Validation rules**:

- Every major capability claim in docs must be traceable to at least one source artifact.
- When artifacts conflict, later clarified/implemented artifacts win over superseded framing.

## Entity: Capability Status

**Purpose**: A label that prevents aspirational docs from reading as shipped docs.

**Fields**:

- `name`: capability name
- `status`: Implemented, Planned, Deferred, or Illustrative
- `source_basis`: source artifacts supporting the label
- `notes`: short caveat, if needed

**Validation rules**:

- SQLite provider and dialect framework are Planned unless implemented before docs land.
- NMemory is Deferred.
- PostgreSQL provider and current DormantQL grammar are Implemented where source/tests support them.
- Unexecuted examples in this docs feature are marked Illustrative when appropriate.
- Todo/task-list and Scheduling task sample APIs must be buildable sample projects unless provider/runtime
  prerequisites prevent full execution in the local environment.

## Entity: DormantQL Example

**Purpose**: Schema/query/mutation snippets embedded in docs.

**Fields**:

- `page_path`: page containing the example
- `language`: `dqls`, `dql`, `csharp`, or shell
- `source_basis`: sample/spec artifact used
- `status`: verified, illustrative, or planned
- `constructs_used`: list of grammar constructs

**Validation rules**:

- Must not use removed `002` forms: `command`, `= ...;` unit assignment, leading-dot members, `:=`,
  `and`/`or` keyword connectives, `::`, or `->`.
- `.dql` examples must use `query`/`mutation`, explicit aliases, alias-qualified members, lowercase parameter
  type keywords, and supported operators.
- `.dqls` examples must use `name: TypeExpr[?]` member syntax and required-by-default nullability rules.
- C# snippets must avoid implying unimplemented package names or APIs unless clearly illustrative.

## Entity: Sample API Application

**Purpose**: A compact ASP.NET Core API sample project that shows DormantQL in a recognizable problem space
beyond the minimal quickstart.

**Fields**:

- `name`: sample label shown to readers
- `project_path`: repository-relative path under `samples/`
- `docs_page_path`: documentation page describing or routing to the sample
- `api_surface`: HTTP endpoints exposed by the sample
- `schema_files`: DormantQL `.dqls` files included by the sample project
- `unit_files`: DormantQL `.dql` files containing at least one query and one mutation
- `build_status`: build verified or caveated
- `runtime_caveats`: provider/container prerequisites, if any
- `scope_notes`: what the sample intentionally does not claim
- `source_basis`: grammar/spec/sample artifacts used to validate the example

**Required sample API applications**:

| Name | Project path | Intent | Required scope boundary |
|------|--------------|--------|-------------------------|
| Todo/task-list API | `samples/Dormant.Sample.TodoApi/` | Show a small task/item domain with completion state and simple ownership or timestamps as appropriate. | Do not imply task-management product features beyond the shown API, schema, and units. |
| Scheduling task API | `samples/Dormant.Sample.SchedulingApi/` | Show a time-oriented task/job/appointment-style domain with planned time and status. | Do not imply recurrence, background execution, notifications, authentication, or calendar integrations. |

**Validation rules**:

- Each required sample API must include one schema file and at least one query unit plus one mutation unit.
- Each DormantQL file must satisfy the DormantQL Example validation rules.
- Each sample API project must build in the standard local workflow, or document the provider/environment
  prerequisite that prevents runtime verification.
- Scheduling APIs must represent scheduled data, not a runtime scheduler.

## Entity: Documentation Link

**Purpose**: A relative Markdown link between docs pages or sections.

**Fields**:

- `source_path`: page containing the link
- `target`: relative path and optional anchor
- `target_exists`: boolean checked during implementation

**Validation rules**:

- All links from README into `docs/` must target existing files.
- All cross-links among docs pages must target existing files.
- Section anchors should be avoided unless they add real navigational value; file-level links are easier to
  keep valid.

## Entity: Traceable Claim

**Purpose**: A documentation statement about Dormant capabilities, status, architecture, or constraints.

**Fields**:

- `claim`: concise statement
- `page_path`: page where it appears
- `source_basis`: one or more source artifacts
- `status_label`: optional Capability Status

**Validation rules**:

- No unsupported/invented claims.
- Claims about AOT, build-time SQL, no runtime reflection/compilation, immutable command-driven writes,
  grammar, provider status, or testing must be traceable.
