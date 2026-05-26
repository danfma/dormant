# Requirements Quality Checklist: Provider/Dialect Architecture, AOT & Parity

**Purpose**: Reviewer-sanity validation of REQUIREMENTS quality (not implementation) for the dialect
abstraction, AOT/performance NFRs, and cross-provider parity — "unit tests for the English".
**Created**: 2026-05-26
**Feature**: [spec.md](../spec.md)
**Depth**: Reviewer sanity | **Audience**: PR reviewer

## Provider / Dialect Architecture

- [ ] CHK001 - Are the obligations a new provider MUST implement (connect, schema apply, command + query execution) enumerated as discrete requirements rather than narrated? [Completeness, Spec §FR-001/§FR-004]
- [ ] CHK002 - Is "execution-strategy abstraction" defined with concrete, testable obligations (what the boundary must/must not assume) rather than as a descriptive phrase? [Clarity, Spec §FR-004]
- [ ] CHK003 - Is the build-time-vs-runtime split (render one variant per dialect at build time; select by session dialect at runtime; no runtime SQL compilation) stated unambiguously as a requirement? [Clarity, Spec §FR-003]
- [ ] CHK004 - Is the "structured SQL representation (IR)" specified precisely enough that a reviewer can judge whether it is provider-neutral (i.e., what it may and may NOT contain)? [Measurability, Spec §Key Entities]
- [ ] CHK005 - Is "a non-SQL execution strategy" defined concretely enough to evaluate SC-004's claim that the boundary "admits" it? [Measurability, Spec §SC-004]

## AOT & Performance (Non-Functional Requirements)

- [ ] CHK006 - Is "zero library-originated AOT/trim warnings" paired with an objective verification method (the AOT gate) in the requirements, not just asserted? [Measurability, Spec §FR-006/§SC-002]
- [ ] CHK007 - Is "no first-call warm-up" stated as a testable criterion rather than an adjective? [Clarity, Spec §FR-006]
- [ ] CHK008 - Is SC-005's "a fraction of the PostgreSQL/Testcontainers time" quantified with a threshold or reference baseline, or is it unquantified? [Ambiguity, Spec §SC-005]
- [ ] CHK009 - Is the AOT scope explicitly bounded to core + dialect framework + SQLite, and explicitly excluding deferred non-AOT providers, with no contradictory statements? [Consistency, Spec §FR-006/§Out of Scope]

## Cross-Provider Parity & Testing

- [ ] CHK010 - Is "results equivalent to PostgreSQL" / "100% of covered behaviors match" backed by an explicit, enumerated set of in-scope behaviors? [Clarity/Measurability, Spec §SC-001]
- [ ] CHK011 - Does the spec enumerate which authored-DQL behaviors the parity suite must cover (schema apply, CRUD, `returning`, `with`-block, optional-filter queries)? [Completeness, Spec §US1/§SC-001]
- [ ] CHK012 - Are "real engine, never mocks" and "single parameterized cross-provider suite from one source" stated as binding requirements (not just rationale)? [Clarity, Spec §FR-007]
- [ ] CHK013 - Is the in-memory "clean store per case" expectation captured as a requirement rather than only an edge-case aside? [Coverage, Spec §Edge Cases]

## Edge Cases & Error Contracts

- [ ] CHK014 - Is the "clear, located/provider-named diagnostic or runtime error" for an unsupported capability specified with enough detail to test it (which capabilities trigger it, what the error shape is)? [Ambiguity, Spec §FR-009]
- [ ] CHK015 - Does the spec define behavior when a dialect cannot represent an authored type/operation, distinguishing a surfaced error from silently-wrong results? [Coverage/Edge Case, Spec §FR-009]
- [ ] CHK016 - Are provider-specific dialect divergences (placeholders, type names, `RETURNING`, JSON storage, identifier quoting, schema handling) enumerated so reviewers can confirm none are left undefined? [Completeness, Spec §Edge Cases/§FR-003]

## Consistency & Conflicts

- [ ] CHK017 - Does SC-006's "0 changes to the generated-code contract" reconcile with the spec's allowance for the one-time dialect-framework seam (which alters generated output)? [Conflict, Spec §SC-006/§FR-008]
- [ ] CHK018 - Is the consumer-facing surface that FR-008/SC-006 protect (public API, DSL, generated method signatures) distinguished in requirements from internal generated-code shape that may evolve? [Clarity, Spec §FR-008/§SC-006]
- [ ] CHK019 - Are the terms "provider", "dialect", "execution strategy", and "IR" used consistently across spec, plan, and contracts without drift? [Consistency, Spec §Key Entities]

## Dependencies & Assumptions

- [ ] CHK020 - Is the assumption "SQLite is AOT-friendly via managed + native SQLite" stated together with how it is validated (the AOT smoke gate)? [Assumption, Spec §Assumptions]
- [ ] CHK021 - Is the minimum SQLite engine capability the requirements depend on (e.g., `RETURNING` support) captured explicitly rather than left implicit? [Gap, Spec §Assumptions]
