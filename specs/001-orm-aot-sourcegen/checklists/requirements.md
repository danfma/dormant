# Specification Quality Checklist: Dormant — AOT-First, Schema-DSL ORM for .NET 10 (DormantQL DSL)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-24
**Updated**: 2026-05-25
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- All three original clarifications resolved through discussion and recorded in the spec's
  Clarifications section (authoring surface → DSL; relationship depth → first-class links + nested
  fetch, no implicit lazy/inheritance v1; providers → PostgreSQL primary).
- Direction pivoted to a schema-DSL-first ORM (DormantQL). Core invariant: result type of any
  query is known at build time; only values/predicates vary at runtime (safe-by-default; projections
  are distinct types, never partial entities).
- Two-phase plan recorded in Out of Scope: Phase 1 = schema + links + shapes/projections + optional
  params + basic filtering; Phase 2 = richer expression sub-language + DSL language-server/IntelliSense.
- CONSTITUTION FOLLOW-UP — RESOLVED: constitution amended to **v2.0.0** (ratified 2026-05-25).
  Principle II redefined from native ABI to managed-interface + generated-code + DSL compatibility;
  new Principle III "Statically-Known, Safe-by-Default Data Access" added. Spec now aligns.
- 2026-05-25 clarify pass (Clarifications + FR-009/FR-014 + entities): mutable entities with session
  snapshot diff; links on full entities carry explicit loaded/unloaded type-state
  (`Link<T>`/`LinkSet<T>`); explicit on-demand link load in v1 (no implicit lazy).
- 2026-05-25 specify pass: DormantQL query/DML surface detailed and scoped into
  Tier A (v1) vs Tier B (Phase 2) — see new "DSL Language Surface" section, FR-030..FR-035, SC-011,
  and expanded Out of Scope. `**` deep-splat permanently excluded (no build-time-known result type).
- 2026-05-25 type-system + relationships (verified vs the reference docs): added FR-036 (v1 property value
  types; first-class `map<K,V>` deferred to Phase 2) and FR-037 (many-to-many via multi links in v1;
  join entity for edge data; backlinks/`@prop` are Phase 2). Dictionaries in v1 = JSON property or
  key/value child entity. The reference language has no native map type.
- 2026-05-25 native types & functions pass: added US8 + FR-038..FR-044 + SC-012..SC-014 + Key Entities
  (Native Type Binding, Native Function/Operator, Provider Directive) + DSL surface subsection.
  Per-provider native types/functions are a deliberate, non-portable departure: typed function catalog
  **and** raw typed SQL-fragment escape; explicit provider directive + build-time portability
  diagnostic; v1 = mechanism + built-in `jsonb` in core PostgreSQL, GIS (PostGIS) as an optional
  companion package. Build-time-known-type, no-boxing, and AOT guarantees preserved.
- VALIDATION NOTE: SC-012/SC-013 intentionally name provider features (`jsonb`, PostGIS) — these are
  user-facing capabilities the feature exists to deliver, not implementation leakage, and are
  consistent with the spec's existing provider-specific criteria (SC-001 Native AOT, SC-007 PostgreSQL
  benchmark). "Technology-agnostic SC" item kept passing on that basis.
- 2026-05-25 language refinement pass: added FR-045 (module → DB schema), FR-046 (generated namespace =
  PascalCaseEachPart(rootNamespace + folders + module)), FR-047 (unified `name: [multi] Type[?]` member
  syntax — arrows + `single` removed, required-by-default, `?` optional), FR-048 (C# `required` instead
  of `= default!`). Updated DSL surface, dsl-grammar/generated-code/data-model contracts, quickstart.
- DRIFT FLAG (spec ↔ code) — RESOLVED 2026-05-25: US1 generator revised to FR-046 (namespace formula via
  `AnalyzerConfigOptions` RootNamespace/ProjectDir), FR-047 (`name: [multi] Type[?]` member syntax;
  lowercase-unknown ⇒ ORM003, PascalCase-unknown ⇒ ORM002), FR-048 (C# `required`). Sample/tests/quickstart
  updated; sample builds + runs in namespace `Dormant.Sample.Quickstart.Schema.App`; SourceGen 8/8 + Core
  6/6 green. Remaining: FR-045 schema-qualified DDL/SQL lands in US5; T031 accessors/materialization in US2.
- 2026-05-25 relationship model pass: Link→Ref rename + NHibernate collection vocabulary (Ref<T>,
  RefSet/RefList/RefBag/RefMap), added FR-049 (collection semantics), FR-050 (projection into user-owned
  plain records — Dormant-free domain boundary), FR-051 (entity identity/PK equality by default, opt-out).
  Relationships default to an Unloaded sentinel (not `= []`), `required` only when mandatory (FR-047/048
  revised). spec.md (clarifications, FR-009/047/048/049/050/051, Key Entities, DSL example) updated.
- DRIFT FLAG (spec ↔ code + docs) — OPEN: propagation pending — (a) docs: contracts/public-api.md
  (Link→Ref + Ref* types + projection-records), contracts/dsl-grammar.md (Set/List/Bag/Map), generated-code.md
  (Ref + equality + projection records), data-model.md (Link→Ref model), quickstart.md, CLAUDE.md; (b) code:
  rename `Link<T>`/`LinkSet<T>` → `Ref<T>` + add RefSet/RefList/RefBag/RefMap in Dormant.Abstractions, update
  generator emit (collection kinds, Unloaded initializers, PK equality), adapter/sample/tests. Apply in the
  next /speckit-implement pass before continuing US2.
- 2026-05-25 materialization revision: FR-048 reworked — drop fragile `[UnsafeAccessor]`-to-backing-field
  materialization in favor of a generated `[SetsRequiredMembers]` ctor on the entity partial (ordinary
  setters) + public getters for reads. Spec/research/contracts/plan/CLAUDE updated. DRIFT (code): the
  committed `EntityBindingEmitter` still uses UnsafeAccessor — T107 applies the switch next /speckit-implement.
- 2026-05-25 US3 MVP query slice: DormantQL `.dql` queries → `ISession` extension methods carrying
  build-time SQL on `CompiledQuery<T>`. Full-entity + flat scalar projection; conjunctive own-column
  filter + order by + limit/offset. `CompiledQuery<T>` gained a public ctor (Statement + Materialize);
  Session query execution wired; ORM010/011/012 added. Generator 11/11, Core 7/7, PostgreSQL 7/7;
  sample emits AppQueries. DEFERRED (next passes): nested-link single-round-trip via JSON aggregation
  (one JsonSerializerContext/assembly, snake_case — research §6), link loading (T058), user-owned-record
  projection (T104/FR-050), path-nav + optional-param query grammar (US4).
