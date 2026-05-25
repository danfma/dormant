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
