# Quickstart: Implementing and Checking the Documentation

This quickstart is for the contributor implementing `006-project-documentation`.

## 1. Read the source material

Use these as the source of truth before writing public docs:

```sh
specs/006-project-documentation/spec.md
specs/006-project-documentation/plan.md
.specify/memory/constitution.md
specs/001-orm-aot-sourcegen/
specs/002-immutable-command-dml/
specs/003-linq-dql-grammar/
specs/004-raw-string-sql/
specs/005-sqlite-nmemory-providers/spec.md
samples/Dormant.Sample.Quickstart/schema/app.dqls
samples/Dormant.Sample.Quickstart/schema/app.dql
samples/Dormant.Sample.TodoApi/
samples/Dormant.Sample.SchedulingApi/
```

## 2. Create the docs files

Create:

```text
README.md
docs/index.md
docs/getting-started.md
docs/status.md
docs/guides/dormantql-schema.md
docs/guides/queries-and-mutations.md
docs/guides/naming-and-generated-code.md
docs/architecture.md
docs/design-decisions.md
docs/speckit-sources.md
```

Keep the README short enough for a first read. Put detail in `docs/`.

## 3. Use conservative examples

Prefer examples copied from or closely adapted from:

```text
samples/Dormant.Sample.Quickstart/schema/app.dqls
samples/Dormant.Sample.Quickstart/schema/app.dql
```

Check examples against:

```text
specs/003-linq-dql-grammar/contracts/dql-grammar.md
```

Do not use removed `002` forms.

## 4. Add the sample API applications

Create two ASP.NET Core API sample projects:

```text
samples/Dormant.Sample.TodoApi/
samples/Dormant.Sample.SchedulingApi/
```

Each sample API should include its own `.dqls` schema file and `.dql` units, with at least one query and one
mutation. Keep the APIs intentionally small:

- Todo API: task-list data, completion state, and simple ownership or timestamps as appropriate.
- Scheduling API: scheduled task/job/appointment-style data with planned time and status.

Do not add recurrence engines, background schedulers, notifications, authentication, or calendar integrations
as part of these samples.

## 5. Label capability status

At minimum, make these distinctions:

- PostgreSQL provider: implemented primary/reference provider.
- Raw string SQL literals in generated code: implemented if source/tests reflect `004`.
- SQLite provider/dialect framework: planned from `005` unless implementation has landed.
- NMemory: deferred future non-AOT provider.
- Any runtime flow that cannot be executed without provider/container setup: documented with that caveat.

## 6. Check links

List Markdown links and verify every relative file target exists:

```sh
rg -n '\]\(([^)#][^)]+)\)' README.md docs
```

For each relative target, confirm the destination file exists. Avoid anchors unless needed.

## 7. Check product and sample build context

If the local environment is ready, run:

```sh
./build.sh build
```

Confirm the new sample API projects participate in the normal build or document the exact reason they cannot.

For full verification, run:

```sh
./build.sh all
```

The full test path includes provider tests that require Docker for PostgreSQL.

## 8. Final review checklist

- README explains what Dormant is, why it exists, differentiators, status, and next docs links.
- `docs/getting-started.md` has no forward references that block first success.
- DormantQL examples use current `003` syntax.
- Todo and Scheduling sample APIs build or have documented provider/runtime prerequisites.
- Docs do not claim planned/deferred capabilities are shipped.
- Major claims are traceable in `docs/speckit-sources.md`.
- All documentation is in English.
