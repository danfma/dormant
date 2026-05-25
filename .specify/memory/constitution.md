<!--
SYNC IMPACT REPORT
==================
Version change: 2.0.0 → 2.0.1
Rationale: PATCH. Wording/metadata only. The lone inspirational reference to a third-party query
language in this report's rationale is reworded to a trademark-safe form; the project's own
schema/query language is named "DormantQL". No principle, normative rule, or governance semantics
changed. (Supersedes the 2.0.0 report; full history is in version control.)

Modified principles: None (wording only)
Added principles: None
Removed sections: None

Prior change recorded for context — 2.0.0 (MAJOR):
  - II. ABI Stability & Compatibility → II. Interface & Compatibility Stability (redefined for a
    managed library: public API + assembly/package compatibility + generated-code contract + DSL
    language stability)
  - Added III. Statically-Known, Safe-by-Default Data Access; renumbered former III→IV, IV→V, V→VI
  - Compatibility & Performance Standards reframed (SemVer over API/package/generated-code/DSL;
    PostgreSQL primary reference provider; build-time SQL generation). The project is a managed
    .NET 10 ORM built on source generators and its own schema/query DSL (DormantQL), PostgreSQL-primary.

Templates requiring updates:
  - .specify/templates/plan-template.md ✅ (Constitution Check gate is generic; no change needed)
  - .specify/templates/spec-template.md ✅ (no constitution-specific coupling)
  - .specify/templates/tasks-template.md ✅ (sample tasks remain generic)
  - Feature artifacts under specs/001-orm-aot-sourcegen/ ✅ (already use the DormantQL naming)

Deferred TODOs: None
-->

# Dormant Constitution

## Core Principles

### I. Developer Experience First (DX)

The primary user of Dormant is the developer who integrates it. Every public surface
MUST optimize for the integrator's clarity, discoverability, and time-to-first-success.

- Public APIs and the schema/query DSL MUST be self-explanatory: names describe intent, signatures
  and declarations reveal contracts, and the common path requires no reading of internals.
- Every public symbol and DSL construct MUST ship with documentation and at least one runnable example.
- Error messages and diagnostics MUST be actionable: state what failed, why, and the next step the
  caller can take, with a source location where applicable.
- Breaking the integrator's mental model is a defect, even when behavior is technically correct.

Rationale: Adoption and correct usage are bounded by how quickly a developer can understand and trust
the library. DX is the outcome the other principles serve.

### II. Interface & Compatibility Stability

Dormant is a managed .NET library whose contract spans four surfaces: the public API, the
assembly/package compatibility, the code emitted by its source generators, and the schema/query DSL
language. All four are contracts, not implementation details.

- The public API, the shape and semantics of generated code that consumer code binds to, and the DSL
  grammar/semantics MUST NOT change incompatibly within a MAJOR version.
- Backward-incompatible changes to any of these surfaces REQUIRE a MAJOR version bump and a documented
  migration path.
- New capabilities MUST be added in a backward-compatible way (additive APIs, additive generated
  members, additive/optional DSL syntax) whenever technically feasible.
- Every change touching the public API, generated-code contract, or DSL MUST be reviewed against the
  published compatibility baseline; an unexplained diff in that baseline blocks merge.

Rationale: Consumers depend not only on the API they call but on the code we generate into their
projects and on the DSL they author. Silent changes to any of these break builds or behavior, so
stability across all four surfaces is non-negotiable.

### III. Statically-Known, Safe-by-Default Data Access

Data access MUST be safe by construction: the type system, not runtime discipline, prevents the
classic partially-loaded-data bugs.

- The result type of every query MUST be fully known at build time; it MUST NOT depend on runtime
  values. Only data values and predicates may vary at runtime.
- A query returns either a full entity type or a projection type. A projection MUST be a distinct
  generated type containing exactly the requested fields and links — never a partially-populated
  entity. Accessing a field absent from the result type MUST be a compile-time error.
- Links (relationships) MUST be modeled with explicit, type-checked loaded/unloaded states so that
  unfetched related data cannot be read as if present.
- There MUST be no implicit lazy loading. Related data is retrieved only when explicitly requested
  (declared in a fetch shape or resolved on demand through an explicit call).

Rationale: The most common ORM defects come from data that looks loaded but is not. Encoding load
state in the type system eliminates that class of bug and is what makes Dormant safe by default.

### IV. First-Class Tooling

Tooling is part of the product, not an afterthought. The workflows of building, authoring,
inspecting, diagnosing, and upgrading Dormant MUST be supported by maintained tools.

- The project MUST provide tooling to verify API, generated-code, and DSL compatibility between
  releases, and to manage database schema migrations (create, apply, roll back, inspect status).
- The schema/query DSL MUST ship with, at minimum, a defined syntax and source-located diagnostics;
  richer editor integration (language server/IntelliSense) is a planned enhancement.
- Build, test, benchmark, and documentation generation MUST be reproducible via a single documented
  entry point and MUST run in CI.
- A tool the workflow depends on MUST be maintained and tested like shipped code.

Rationale: DX, compatibility guarantees, and safe data access are only credible if enforced and
observed by tooling rather than by discipline alone.

### V. Performance by Default

Performance is a feature owned on behalf of the end users of the applications that embed Dormant.
The default configuration MUST be fast; speed MUST NOT require expert tuning.

- The library MUST be fully compatible with Native AOT and full trimming, with zero
  library-originated trimming/AOT warnings in consuming applications.
- Mapping and query execution MUST NOT rely on runtime code generation or runtime reflection on hot
  paths, and MUST NOT require first-use runtime warm-up; required metadata, accessors, and SQL are
  produced at build time.
- Materialization MUST avoid boxing of value-type columns and MUST avoid hidden costs on the common
  path (no surprise allocations, copies, or synchronization not required by the contract).
- Each release MUST declare explicit, measurable performance budgets (latency, throughput, memory,
  binary size) for its hot paths; a regression beyond budget blocks merge unless explicitly accepted
  and recorded.

Rationale: End users never see the API, only its cost. An AOT-first ORM justifies its existence
largely through performance and low memory use, so regressions are treated as defects.

### VI. Quality & Testing Discipline (NON-NEGOTIABLE)

The guarantees above (DX, compatibility, safe data access, performance) only hold if they are
continuously verified.

- Public behavior MUST be covered by automated tests; compatibility baselines and performance budgets
  MUST have dedicated checks in CI.
- A change that alters public behavior, any compatibility surface (API, generated code, DSL), or a
  performance budget MUST update the corresponding tests and baselines in the same change.
- CI MUST be green before merge; failing or skipped verification is not "done".
- Bugs are reproduced with a failing test before they are fixed.

Rationale: Without enforced verification, the other principles degrade into aspirations. Testing is
the mechanism that makes the constitution real.

## Compatibility & Performance Standards

- Versioning follows Semantic Versioning (MAJOR.MINOR.PATCH) applied to ALL compatibility surfaces:
  the public API, assembly/package compatibility, the generated-code contract, and the DSL language.
  Incompatible changes to any surface are MAJOR; additive, backward-compatible capabilities are MINOR;
  fixes that preserve all surfaces are PATCH.
- A published compatibility baseline (public API surface, generated-code contract, and DSL grammar)
  MUST be checked into the repository and updated only by intentional, reviewed changes.
- PostgreSQL is the primary reference provider. The provider boundary MUST be designed so additional
  relational providers can be derived later without breaking consumers.
- All SQL on the core query path is generated at build time; runtime query compilation on that path is
  disallowed.
- Each release MUST document its supported targets (.NET 10+) and its performance budgets.
- Deprecation precedes removal: a public, generated, or DSL element MUST be marked deprecated for at
  least one MINOR release, with a documented replacement, before removal in a MAJOR release.

## Development Workflow

- Every change to a compatibility surface (public API, generated-code contract, DSL) or a performance
  budget REQUIRES review by at least one maintainer and an explicit note describing the compatibility
  and performance impact.
- The build, test, benchmark, compatibility, and migration tooling MUST be runnable locally through
  the documented entry point and MUST run in CI on every change.
- Public changes MUST be accompanied by updated documentation and examples in the same change.
- Complexity that violates a principle MUST be justified in writing (what need it serves, and why the
  simpler compliant alternative was rejected) or it MUST be removed.

## Governance

- This constitution supersedes other project practices. When a guideline conflicts with it, the
  constitution wins until amended.
- Amendments REQUIRE a documented proposal, maintainer approval, a version bump per the rules below,
  and propagation to dependent templates and tooling.
- Versioning policy for this constitution:
  - MAJOR: backward-incompatible governance or principle removals/redefinitions.
  - MINOR: a new principle or section, or materially expanded guidance.
  - PATCH: clarifications and wording fixes with no semantic change.
- Compliance is verified at review time: pull requests MUST confirm they uphold the principles, and
  violations MUST be either fixed or recorded in the change's complexity justification.

**Version**: 2.0.1 | **Ratified**: 2026-05-24 | **Last Amended**: 2026-05-25
