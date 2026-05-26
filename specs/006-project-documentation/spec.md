# Feature Specification: Project Documentation & Developer README

**Feature Branch**: `006-project-documentation`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "Gere uma documentação para o projeto (numa pasta docs) e então um README na pasta principal apresentando o projeto para possíveis Devs (tudo em inglês). Se tiver alguma ferramenta ou skill para documentação que seja interessante, seria a hora para utilizar. Monte a documentação com base nos recursos e descrições criadas pelo SpecKit."

## Overview

Dormant is an AOT-first .NET 10 ORM whose schema and queries are authored in a dedicated
language (DormantQL) and compiled to SQL at build time by a Roslyn source generator. The
project has accumulated rich design knowledge across five SpecKit features (001–005:
ORM/AOT source generation, immutable command DML, the LINQ-/SQL-hybrid DQL grammar, raw
string SQL emission, and the SQLite + dialect-framework direction), plus a governing
constitution. None of this is yet surfaced to an outside developer: there is no README and
no `docs/` folder. A developer discovering the repository today cannot tell what Dormant is,
why it exists, or how to use it.

This feature produces public-facing, English-language documentation that presents Dormant to
prospective developers — both evaluators deciding whether to adopt it and integrators who
have decided to try it. The content is synthesized from existing SpecKit artifacts
(spec/plan/research/data-model/contracts/quickstart files, the constitution, and project
context) so that the documentation reflects the project's real, decided design rather than
invented claims.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Evaluator understands the project from the README (Priority: P1)

A developer arrives at the repository root (e.g., via the GitHub project page) with no prior
knowledge. From the README alone they learn what Dormant is, the problem it solves, what makes
it distinct (AOT-first, build-time SQL, no runtime reflection, type-safe partial-load-free
results, the DormantQL schema/query language), the current maturity/status, and where to go
next for deeper detail.

**Why this priority**: The README is the single highest-traffic entry point and the first (often
only) artifact an evaluator reads. If it fails, nothing else is seen. It is the minimum viable
deliverable and stands alone as value even if `docs/` did not exist.

**Independent Test**: Open the repository root, read only `README.md`, and confirm a developer
unfamiliar with the project can state in their own words what Dormant is, its key
differentiators, its current status, and where to find installation and deeper docs — without
opening any source file.

**Acceptance Scenarios**:

1. **Given** a developer who has never seen the project, **When** they read the README top to
   bottom, **Then** they can describe the project's purpose, its primary differentiators, and
   its current status without reading source code.
2. **Given** the README, **When** the developer looks for next steps, **Then** they find clear
   links into the `docs/` folder (getting started, guides, architecture) and any prerequisites
   (target framework, required tooling such as a container runtime for provider tests).
3. **Given** the README presents a code/usage snippet, **When** the developer compares it to the
   authored DormantQL grammar and generated-API conventions described in the SpecKit artifacts,
   **Then** the snippet is consistent with them (correct keywords, casing, member syntax).

---

### User Story 2 - Integrator follows the docs to first success (Priority: P2)

A developer who has decided to try Dormant opens the `docs/` folder and follows a guided path:
install/reference the library, define a schema module in DormantQL, author a read query and a
write mutation, understand how names map to generated C# (snake_case unit → PascalCase method,
schema/folder → namespace), and run it against a provider. They reach a working first result.

**Why this priority**: Converts interest into adoption. It depends on P1 existing (the README
points here) but delivers distinct, standalone value: a path from zero to a running query.

**Independent Test**: Following only the `docs/` getting-started content, a developer can author
a minimal schema + query/mutation and understand the expected generated output and run model,
with every DormantQL construct used matching the grammar defined in the SpecKit specs.

**Acceptance Scenarios**:

1. **Given** the getting-started guide, **When** a developer follows it step by step, **Then**
   each step is actionable and ordered (prerequisites → install → define schema → author
   units → generate → run) with no forward references to undefined concepts.
2. **Given** a DormantQL example in the docs, **When** it is checked against the grammar in
   `specs/003-linq-dql-grammar`, **Then** the syntax is valid (units `query`/`mutation`,
   brace-delimited, alias-qualified members, lowercase type keywords, supported operators) and
   removed/forbidden 002 forms do not appear.
3. **Given** the naming/mapping guide, **When** a developer reads it, **Then** the documented
   rules (unit → method, entity casing, namespace = PascalCaseEachPart(RootNamespace + schema
   folders + module), member `name: TypeExpr[?]`, relationship types, required-by-default)
   match the conventions stated in the project context and data-model artifacts.

---

### User Story 3 - Curious developer or contributor explores design and rationale (Priority: P3)

A developer wanting depth — to assess long-term fit or to contribute — reads architecture and
design documentation: the layered, feature-first structure and inward dependency direction; the
abstraction groupings by capability (Providers, Mapping, Migrations, Native, Sessions, Links,
Querying); the build-time SQL generation pipeline (IR → per-dialect rendering); the
constitution's governing principles; and how the design evolved across SpecKit features 001–005.

**Why this priority**: Valuable for trust, contribution, and retention, but not required for a
first impression or first success. It builds on P1/P2 context.

**Independent Test**: From the architecture docs alone, a developer can draw the high-level
component map, name the dependency direction, explain why SQL is generated at build time, and
summarize the project's guiding principles — all consistent with the constitution and SpecKit
plans.

**Acceptance Scenarios**:

1. **Given** the architecture documentation, **When** a developer reads it, **Then** they can
   identify the major layers/projects (Abstractions kernel, Core engine, Provider/Spatial/Tool
   adapters) and the one-directional inward dependency rule.
2. **Given** the design-decisions documentation, **When** a developer reads it, **Then** the
   key decisions (AOT-first, no runtime reflection/compilation on hot paths, build-time SQL,
   immutable command-driven runtime, dialect abstraction with deferred NMemory) are explained
   with their rationale, traceable to the constitution and SpecKit artifacts.

---

### Edge Cases

- **Documentation drift**: Source design changes after docs are written. Documentation MUST be
  traceable to its SpecKit sources so reviewers can detect and correct divergence; examples
  should be minimal enough to stay valid.
- **Unverified runtime claims**: A getting-started example cannot be end-to-end executed in this
  documentation effort (e.g., provider/container not available). Such examples MUST be clearly
  marked as illustrative and kept faithful to the documented grammar/conventions rather than
  presented as verified runs.
- **Conflicting or superseded design notes**: Where SpecKit artifacts contain superseded framing
  (e.g., the old "multi-command" wording replaced by the `with` block; the Link→Ref rename
  predated by committed code), documentation MUST present the current decided design and not the
  obsolete version.
- **Aspirational vs. shipped features**: Some capabilities are designed but not fully implemented.
  Documentation MUST distinguish what is available now from what is planned/deferred so an
  evaluator is not misled.
- **Internationalization**: Project context and prior discussion include Portuguese; all produced
  documentation MUST be in English.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository root MUST contain a `README.md` that presents Dormant to a developer
  with no prior knowledge, covering: what it is, the problem it solves, its primary
  differentiators, its current status/maturity, and where to go next.
- **FR-002**: The README MUST link to the `docs/` folder and orient the reader to its main
  sections (getting started, usage guides, architecture/design).
- **FR-003**: A `docs/` folder MUST exist at the repository root containing the deeper
  documentation, organized into discoverable sections rather than a single flat file.
- **FR-004**: Documentation MUST include a getting-started path that orders prerequisites,
  installation/reference, schema definition, authoring read and write units, generation, and
  running, with no forward references to undefined concepts.
- **FR-005**: Documentation MUST explain the DormantQL schema language (modules → schema, member
  syntax `name: TypeExpr[?]`, value/ref/collection members, relationship types, required-by-default
  and optionality rules) consistent with the project context and data-model artifacts.
- **FR-006**: Documentation MUST explain the DormantQL query/mutation grammar (003): `query` and
  `mutation` units, brace-delimited blocks, explicit aliases, alias-qualified members, lowercase
  type keywords, supported operators, `returning`, and `with` value flow — and MUST NOT present
  removed 002 forms (`command`, `= …;`, leading-dot, `:=`, `and`/`or`) as valid.
- **FR-007**: Documentation MUST explain the naming/mapping rules from authored units to generated
  C# (unit snake_case → PascalCase method; entities PascalCase; namespace =
  PascalCaseEachPart(RootNamespace + schema folders + module); mutation result inference).
- **FR-008**: Documentation MUST include architecture/design content describing the feature-first
  layered structure, the one-directional inward dependency rule, the capability-grouped
  abstraction namespaces, and the build-time SQL generation pipeline.
- **FR-009**: Documentation MUST summarize the project's guiding principles (from the constitution)
  and the key design decisions with their rationale (AOT-first, build-time SQL, no runtime
  reflection/compilation on hot paths, immutable command-driven runtime, dialect abstraction).
- **FR-010**: All produced documentation (README and `docs/`) MUST be written in English.
- **FR-011**: Documentation content MUST be derived from existing SpecKit artifacts and project
  context (specs 001–005 and their plan/research/data-model/contracts/quickstart files, the
  constitution, and CLAUDE.md), and MUST NOT invent capabilities not grounded in those sources.
- **FR-012**: Documentation MUST clearly distinguish capabilities that are available now from those
  that are planned or deferred (e.g., NMemory provider).
- **FR-013**: Every DormantQL or C# example shown MUST be syntactically consistent with the
  grammar and conventions defined in the SpecKit artifacts.
- **FR-014**: Internal links between the README and `docs/` (and among `docs/` pages) MUST resolve
  to existing files/sections (no broken links).

### Key Entities *(include if feature involves data)*

- **README**: The root entry document; audience = first-time evaluator; purpose = orient and route.
- **docs/ folder**: The structured documentation set; audience = integrators and contributors;
  contains getting-started, usage guides (schema language, query/mutation grammar, naming/mapping),
  and architecture/design pages.
- **SpecKit source artifacts**: The existing specs (001–005), their supporting files
  (plan/research/data-model/contracts/quickstart), the constitution, and project context — the
  authoritative inputs from which documentation content is synthesized.
- **DormantQL examples**: Illustrative schema and query/mutation snippets embedded in the docs,
  each traceable to the grammar/conventions in the SpecKit artifacts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer unfamiliar with the project can, after reading only the README, correctly
  state what Dormant is, at least three primary differentiators, and its current status — in under
  5 minutes.
- **SC-002**: A developer following only the getting-started documentation can author a minimal
  schema plus one read query and one write mutation, and correctly anticipate the generated C#
  surface, without consulting source code.
- **SC-003**: 100% of DormantQL/C# examples in the documentation are consistent with the grammar
  and conventions defined in the SpecKit artifacts (zero invalid or forbidden-form examples).
- **SC-004**: 100% of internal links between the README and `docs/` resolve to existing
  files/sections (zero broken links).
- **SC-005**: Every documented capability is traceable to a SpecKit artifact or project-context
  statement (zero unsupported/invented capability claims), and every capability marked "available
  now" vs. "planned/deferred" is labeled accordingly.
- **SC-006**: A reviewer reading the architecture documentation can reproduce the high-level
  component map and the dependency direction with no contradictions against the constitution or
  SpecKit plans.

## Assumptions

- **Audience**: Primary audience is external/prospective .NET developers evaluating or adopting
  Dormant; secondary audience is contributors. Documentation targets developers, not non-technical
  stakeholders.
- **Language**: All documentation is in English, per the explicit request, despite Portuguese
  appearing in project discussion/context.
- **Source of truth**: SpecKit artifacts (001–005), the constitution, and CLAUDE.md are the
  authoritative inputs; where they conflict, the most recently decided design (latest clarifications)
  wins, and superseded framing is omitted.
- **No end-to-end runtime verification required**: This feature delivers documentation; it does not
  require standing up a provider/container to execute examples. Examples are validated for
  grammatical/convention consistency, and any non-executed example is marked illustrative.
- **Format**: Documentation is authored as Markdown files (README at root, pages under `docs/`),
  consistent with existing repository conventions. Selection of any documentation-site
  tooling/generator is an implementation detail deferred to planning.
- **Scope boundary**: This feature does not change product code, the generator, or the DormantQL
  grammar; it only adds documentation describing the already-decided design.
- **Status honesty**: Because parts of the design are specified but not fully implemented,
  documentation reflects current reality and marks planned/deferred items, rather than implying a
  finished, fully shipped product.
