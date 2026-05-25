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
- 2026-05-25 naming-conventions + extension-block pass: added US9 (configurable DB naming) +
  FR-052..FR-057 (snake_case default; per-project convention; per-unit table/column/function override;
  build-time resolution; consistency across DDL/DML/queries/params/migrations; collision diagnostic) +
  SC-015 + Key Entities (Naming Convention, Name Override) + edge cases + Clarifications. Also FR-058:
  generated session extension surface MUST use C# 14 extension blocks (enables extension properties/
  static members without breaking shape). research §12/§13, contracts/generated-code.md updated.
  Tasks: US9 phase (T109-T118) + T108 (extension-block refactor of QueryEmitter). DRIFT (code): naming
  not yet implemented (binding/query emitters use raw DSL names); QueryEmitter still emits classic
  extension methods — both land in /speckit-implement (T108 then US9).
- VALIDATION NOTE: FR-047/048/049/058 name C# language features (`required`, `[SetsRequiredMembers]`,
  `Ref<T>`, extension blocks). Consistent with the spec's treatment of generated-code/API as explicit
  compatibility surfaces (Constitution II); "no implementation details" item kept passing on that basis.
- 2026-05-25 IMPLEMENTED T108 + US9: (T108) `QueryEmitter` now emits query methods inside a C# 14
  extension block `extension(ISession session){…}` (FR-058) — call sites unchanged. (US9) database
  naming: `NamingConvention` (snake_case default + verbatim) resolver in `Emit/NamingConvention.cs`;
  project config via `build_property.DormantNamingConvention` → `GeneratorConfig`; per-unit `db("…")`
  override (entity + column) via lexer string literals + `EntityModel/PropertyModel.NameOverride`;
  `EntityBindingEmitter` + `QueryEmitter` resolve names (override ?? convention) consistently;
  per-entity column collision → ORM013. Tests: generator 16/16 (5 new naming), PostgreSQL 8/8 (incl.
  `StockItem`→`stock_item`/`item_name` round-trip); existing integration DDL updated to snake_case
  table names. Build 0/0. Deferred: schema-qualified DDL + function-name resolution (US5/US8),
  cross-table collision.
- 2026-05-25 IR + plugins pass: added US10 (P3, architectural) + FR-059..FR-064 — generation over a
  structured, deterministic, value-equatable IR (language constructs + statements-to-emit), strings only
  at the output boundary; build-time plugin transform seam (deterministic order, located diagnostic on
  invalid IR); compiled query/command definition cache (FR-064) deferred per user. + SC-016/017, Key
  Entities (IR/AST, Generation Plugin, Compiled Definition Cache), edge cases, Clarifications, Out-of-Scope
  phasing (public plugin API + cache = later phase). research §14. tasks: Phase 13 (T119-T124, future).
  DRIFT (code): current emitters build SQL via string assembly — IR refactor is future work (T119-T122).
- VALIDATION NOTE: US10/FR-059..064 describe generator-internal architecture + an extensibility surface;
  framed as user value (plugin authors / extensibility / allocation). Implementation nouns (IR, AST,
  plugin) are the feature's subject, consistent with the spec's treatment of generated-code/tooling as
  compatibility surfaces (Constitution II/IV). "No implementation details" item kept passing on that basis.
- 2026-05-25 IMPLEMENTED T119+T120 (US10 IR start): added `Ir/SqlIr.cs` — structured SQL IR
  (Insert/Select/Delete statements + SqlCondition/SqlOrder/SqlLimit) + `SqlRenderer` (centralized
  quoting). Migrated `EntityBindingEmitter` (INSERT/SELECT-by-key/DELETE) + `QueryEmitter` (SELECT) to
  build IR nodes and render at the boundary — removed hand-escaped string assembly for static
  statements. Byte-identical: generator 16/16 (exact-SQL asserts unchanged), PostgreSQL 8/8, build 0/0.
  UPDATE excluded (runtime-dynamic changed-columns-only assembly) → tracked under T121. Deferred:
  transform seam (T121), plugin API + definition cache (T123/T124, later phase).
- 2026-05-25 IMPLEMENTED US5 first slice (DDL + CREATE SCHEMA): module → DB schema (snake_case via
  convention); IR gained `TableRef(schema,name)` + `CreateSchemaStatement`/`CreateTableStatement` +
  `TypeMap.ToSqlType` (DSL→PG). ALL emitted SQL now schema-qualified (binding INSERT/SELECT/UPDATE/DELETE
  + query SELECT + DDL). Bindings expose `Schema` + `CreateTableSql`; `EntityBindings.All()` enumerates;
  `Core/Migrations/SchemaInitializer.EnsureCreatedAsync` + `DormantPostgres.EnsureCreatedAsync` apply
  CREATE SCHEMA + CREATE TABLE (idempotent, one tx). Integration suite converted to provision via
  EnsureCreated (de-brittled). Tests: generator 16/16 (schema-qualified asserts), Core 7/7, PostgreSQL
  9/9 (+ MigrationApplyTests idempotency). Build 0/0. T059/T064/T065/T105 done. DEFERRED: versioned
  migration store + incremental diff (T063), rollback (T061), destructive guard (T067), CLI (T066).
- 2026-05-25 IMPLEMENTED US4 (optional-param dynamic queries): `optional` parameter keyword in
  QueryParser + `QueryParameter.IsOptional`; `QueryEmitter.EmitDynamicStatement` assembles SQL at
  runtime when a filter uses an optional param — required fragments always, optional only when the param
  is non-null; bind callback re-applies the same guards (value-type optionals via `.Value`); result type
  fixed for all combinations (FR-005/012/031, FR-013 fragment selection not compilation). Optional params
  → nullable C# params defaulted null, ordered after required. Tests: generator 17/17 (OptionalParamType),
  PostgreSQL 10/10 (OptionalParams none/one/both). Build 0/0. DEFERRED: `??` coalesce + optional
  LIMIT/OFFSET (sugar).
- 2026-05-25 IMPLEMENTED US8 first slice (json/jsonb value round-trip, FR-038 core): a `json` property
  maps to a build-time-known `string` over a PG `jsonb` column and round-trips no-boxing. Discovered PG
  won't coerce text→jsonb (42804); fixed with a native WRITE CAST — `json` columns emit `$n::jsonb` in
  INSERT (`InsertColumn.ParamCast` in the SQL IR) + UPDATE SET (`EntityBindingEmitter.ParamCast`). New
  `Document` entity + JsonbTests. Tests: generator 17/17, PostgreSQL 11/11. Build 0/0. DEFERRED (rest of
  US8): native function/operator catalog + containment `@>` (T078/T079), raw typed fragment (T080),
  portability diagnostic (T081), STJ-typed jsonb<T> (T082), GIS/EWKB companion (T083), jsonb AOT smoke (T074).
