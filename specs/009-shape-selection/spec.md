# Feature Specification: Shape Selection (EdgeQL-style) + Flat Immutable Entities

**Feature Branch**: `009-shape-selection`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "EdgeQL-style shape selection blended with LINQ, replacing the runtime relationship wrappers (Ref/RefSet/...) with flat immutable entities plus nested projection types."

## Overview

Dormant is now an immutable, command/query-authored data layer: no change tracking, no lazy
loading, no partially-loaded entities. The runtime relationship wrappers on generated entities
(`Ref<T>`, `RefSet<T>`, `RefList<T>`, `RefBag<T>`, `RefMap<K,V>`) are a leftover from the earlier
mutable-ORM direction — on reads they are never populated (entities materialize with relationships
permanently "unloaded", there is no load/include mechanism, and the foreign-key value is not even
exposed), so relationships are effectively write-only today.

This feature replaces that model. Relationships stay **declared in the schema** (they power joins
and typed navigation in queries), but the way a developer pulls related data becomes an **explicit
shape on the query's `select`** — an EdgeQL-style shape block blended with Dormant's existing
LINQ-flavored query grammar. The result type *is* the requested shape (nested immutable projection
records), which delivers the "never read data you didn't ask for" guarantee through the type system
without any load-state wrapper. Generated entities become **flat immutable rows of their own
columns**, exposing foreign-key id scalars so manual follow-up is always possible.

## Clarifications

### Session 2026-05-27

- Q: How is a to-many relationship declared now that the runtime collection wrappers are removed? → A: Explicit collection declaration in the schema (build-time metadata, e.g. `articles: Set<Article>`), **plus** support for EdgeQL-style backlink navigation (navigate the inverse of a declared to-one to reach the many side).
- Q: How does read-side `with name = (query)` composition execute? → A: As a single query — `with` bindings compile to CTEs/subqueries and resolve in one round-trip (distinct from 003's write-side `with`, which runs separate statements).
- Q: What result type does a `select` with a shape block return? → A: Always a distinct generated projection type; `select <alias>` with no shape block returns the flat entity (invariant: shape block ⇒ projection, no block ⇒ entity).
- Q: How does `into MyDto` match a shape to a user-owned record? → A: Structurally, by member name (case-insensitive) and assignable type, order-independent; missing/extra/mismatched members are a build-time error.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fetch an object with its related objects in one shot (Priority: P1)

A developer authoring a DormantQL query unit wants one object and its related objects (one level or
many, to-one and to-many) returned together, shaped exactly as requested, from a single query — the
EdgeQL "select the tree you want" experience, with Dormant's LINQ-style `from`/`where`/`order by`.

**Why this priority**: This is the heart of the feature and the reason relationships exist in the
schema at all. Without it the product is a flat row mapper that Dapper already covers. A single
root-shaped read is the MVP.

**Independent Test**: Author a query that selects a root entity with a nested to-one and a nested
to-many shape; run it; confirm the returned object tree matches the shape, the nested collection is
fully materialized, and it was satisfied in a single database round-trip.

**Acceptance Scenarios**:

1. **Given** an entity with a to-one relationship, **When** a query selects the root with a nested
   shape for that relationship (`select a { title, writer: { name } }`), **Then** the result is a
   typed object whose `Writer` is a nested record containing exactly `name`.
2. **Given** an entity with a to-many relationship, **When** a query requests it in the shape
   (`select a { title, tags: { label } }`), **Then** the result's `tags` is a fully materialized
   list of nested records, retrieved in the same single round-trip (no per-row follow-up queries).
3. **Given** a shape that omits a field, **When** consuming code tries to read that field off the
   result type, **Then** it is a build-time error (the field is not part of the result type).
4. **Given** a nested to-many shape with an inner `order by`, **When** the query runs, **Then** the
   nested collection is ordered as specified.
5. **Given** a to-one relationship whose value is absent, **When** selected in a shape, **Then** the
   nested result is null; **Given** a to-many with no matches, **Then** the collection is empty (not
   null).

---

### User Story 2 - Assemble a new response object from several sources (Priority: P2)

A developer wants to build a brand-new response object that pulls fields from more than one object
(not just a single root tree) — e.g. a headline from one entity, an author name navigated through a
relationship, and a nested block from a second source — optionally filtering each source
independently.

**Why this priority**: Real read models frequently combine data from multiple entities into one DTO.
It builds on the shaping engine from US1 but is a distinct, independently valuable capability.

**Independent Test**: Author a query with two in-scope sources and a free-composition select that
names fields drawn from both; run it; confirm a single result type is returned with the composed
fields.

**Acceptance Scenarios**:

1. **Given** two in-scope sources, **When** the select is a free composition
   (`select { headline = a.title, authorName = a.writer.name, related = b { ... } }`), **Then** the
   result is a new typed object with exactly those named members.
2. **Given** sources that need different filters, **When** each is expressed as a cascading `with`
   binding and combined in the final select, **Then** each source is filtered independently and the
   final result composes them.
3. **Given** a `with` binding that itself depends on another `with` binding, **When** the query is
   authored, **Then** the bindings cascade (a later binding may reference an earlier one).

---

### User Story 3 - Project into a user-owned record (Priority: P3)

A developer wants the result materialized into their own plain record type (a Clean-Architecture
boundary type they own), instead of an auto-generated result type.

**Why this priority**: Useful for layering/ownership, but the auto-generated result type already
delivers value; this is an ergonomic addition.

**Independent Test**: Author a query whose shape targets a user-owned record via an `into` form;
confirm the result is that user type and that a structural mismatch is reported at build time.

**Acceptance Scenarios**:

1. **Given** a user-owned record matching the shape, **When** the query uses `into MyDto`, **Then**
   results materialize as `MyDto`.
2. **Given** a user record that does not match the shape, **When** building, **Then** a clear
   build-time error describes the mismatch.

---

### Edge Cases

- **Cyclic / self-referential shapes** (e.g. `Employee.manager: Employee` selected recursively): the
  system must produce a clear build-time diagnostic rather than infinite generation or silent
  truncation. Depth itself is not artificially capped.
- **Database/query limits** (very deep or very wide shapes that exceed an underlying engine limit):
  surface an actionable build-time (or author-time) diagnostic, never a silent or runtime-only
  failure.
- **Duplicate member names** in a free composition: build-time error.
- **Empty to-many**: yields an empty collection, never null; **absent to-one**: yields null.
- **Selecting a relationship without a shape block** in a context that needs one (or selecting a
  scalar with a shape block): build-time error with a clear message.
- **`select a` with no shape**: returns the flat entity (own columns incl. FK id scalars), no nested
  data.
- **Existing code that read `Ref<T>`/`RefSet<T>` members**: those members no longer exist — this is a
  breaking change to the generated-code contract and must be called out as MAJOR.

## Requirements *(mandatory)*

### Functional Requirements

#### Shape selection (reads)

- **FR-001**: A query's `select` MUST support a **root-object shape**: a root alias followed by a
  brace block listing scalar fields and nested relationship shapes
  (`select a { field, toOneRef: { ... }, toManyRef: { ... } }`).
- **FR-002**: A query's `select` MUST support a **free composition**: a brace block of named members
  whose values are expressions over any in-scope source, including navigated relationship members and
  nested shape blocks (`select { name = expr, nested = b { ... } }`).
- **FR-003**: `select <alias>` with no shape block MUST return the flat entity — its own columns,
  including foreign-key id scalars — and no relationship data.
- **FR-004**: Nested shapes MUST be expressible to arbitrary depth; the system MUST NOT impose an
  arbitrary fixed depth cap, but MUST detect cycles/self-references and emit a clear build-time
  diagnostic.
- **FR-005**: A nested shape node MUST support an inner `order by`. Nested `filter`/`limit` on a shape
  node are out of scope for this version; independent per-source filtering MUST be achievable via
  `with` clauses (FR-009).
- **FR-006**: The result type of a query MUST be fully determined at build time by its shape;
  accessing a field absent from the shape MUST be a build-time error. There MUST be no implicit lazy
  loading and no partially-populated entity.
- **FR-007**: A to-one relationship in a shape MUST materialize as a nested immutable record (or null
  when absent); a to-many relationship MUST materialize as a fully populated read-only collection of
  nested immutable records (empty, never null, when there are no matches).
- **FR-008**: A shaped read — including nested to-many collections — MUST be satisfied in a **single
  database round-trip** (no N+1 / no per-parent follow-up queries).

#### Composition

- **FR-009**: `with name = (query)` bindings MUST be supported and MUST cascade — a later binding may
  reference an earlier one — and be combinable in the final `select` so that multiple sources, each
  independently filtered, compose into one result. Read-side `with` composition MUST resolve in a
  **single query / one round-trip** (bindings realized as CTEs/subqueries) — this is distinct from the
  write-side `with` of feature 003, which executes a separate statement per binding.
- **FR-010**: A free composition MUST be able to draw members from two or more distinct in-scope
  sources into a single result type; duplicate member names MUST be a build-time error.

#### Result types

- **FR-011**: A `select` carrying a shape block MUST always yield a distinct generated immutable
  projection type (with nested immutable types for nested shapes), even when the shape lists only
  scalar fields. `select <alias>` with no shape block MUST return the flat entity. Invariant: shape
  block ⇒ projection type; no block ⇒ entity type.
- **FR-012**: A query MUST optionally bind its shape to a **user-owned plain record** via an `into`
  form. Matching MUST be structural — by member name (case-insensitive) and assignable type,
  order-independent; missing, extra, or type-incompatible members MUST be a build-time error.

#### Typed navigation & relationships

- **FR-013**: Relationships MUST remain declared in the schema and MUST drive (a) typed navigation in
  predicates and expressions (`a.writer.name`) and (b) nested shapes — both generating the appropriate
  join.
- **FR-013a**: To-many relationships MUST be expressible by an explicit collection declaration in the
  schema (build-time metadata, e.g. `articles: Set<Article>`), with no runtime wrapper member on the
  entity.
- **FR-013b**: The schema/grammar MUST support EdgeQL-style **backlink navigation** — reaching the
  many side of a relationship by navigating the inverse of a declared to-one (the collection side does
  not require its own foreign key; it is the inverse of the target's to-one).
- **FR-014**: Navigating or shaping a relationship that is not declared in the schema MUST be a
  build-time error.

#### Entity model change (breaking)

- **FR-015**: Generated entities MUST become flat immutable rows of their own columns. The runtime
  relationship wrappers (`Ref`, `RefSet`, `RefList`, `RefBag`, `RefMap`) MUST be removed from generated
  entities.
- **FR-016**: Generated entities MUST expose the foreign-key id scalar for each to-one relationship
  (e.g. a `writer_id`-backed property) so a caller can always perform a manual follow-up read.
- **FR-017**: This change to the generated-entity surface is backward-incompatible and MUST be
  released as a MAJOR version with a documented migration path (shape selection replaces wrapper
  access).
- **FR-018**: Write units (`insert`/`update`/`delete`) MUST continue to set foreign keys by binding
  scalar/id values; removing the wrappers MUST NOT change how writes assign relationships.

#### Cross-cutting guarantees

- **FR-019**: Shaped queries MUST behave equivalently across the supported dialects (PostgreSQL and
  SQLite) — the same authored query returns the same shape and the same logical results.
- **FR-020**: Result materialization MUST require no runtime reflection and MUST avoid boxing of
  value-type columns, consistent with Dormant's AOT-first, build-time-SQL guarantees.

### Key Entities *(include if feature involves data)*

- **Query unit**: An authored read with LINQ-style logic (`from`/`where`/`order by`), optional
  cascading `with` bindings, and a terminal shaped `select`.
- **Shape**: Either a root-object shape (rooted at one alias) or a free composition (a new object).
- **Shape node**: A member of a shape — a scalar field, a to-one nested shape, or a to-many nested
  shape (the latter may carry an inner `order by`).
- **Result type**: The immutable type a query yields — auto-generated (default) or a user-owned record
  bound via `into`. Nested shapes yield nested immutable types.
- **Entity**: A flat immutable row of its own columns, including foreign-key id scalars. No relationship
  wrappers.
- **Relationship (schema)**: A declared link between entities; build-time metadata that powers joins,
  navigation, and shapes. Not a runtime property wrapper.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A query selecting a root object plus nested to-one and to-many related objects returns
  the full tree in a **single** database round-trip (verifiable by observing exactly one query is
  issued).
- **SC-002**: Attempting to read a field that was not included in a query's shape fails at build time,
  not at runtime, in 100% of cases.
- **SC-003**: A to-many shape returns a fully materialized collection; an empty match returns an empty
  collection (never null) and an absent to-one returns null.
- **SC-004**: The same authored shaped query returns equivalent shapes and logical results on both
  supported databases.
- **SC-005**: A single query can assemble a result object from members of at least two distinct
  sources (free composition), and per-source filters can be applied via cascading `with` bindings.
- **SC-006**: A developer can retrieve an object graph nested several levels deep with one authored
  query and no per-level boilerplate.
- **SC-007**: A cyclic/self-referential or limit-exceeding shape produces a clear build-time
  diagnostic — never an infinite build, a silent truncation, or a runtime-only failure.
- **SC-008**: After the change, a by-key entity read returns the entity's own columns including its
  foreign-key id scalars, and exposes no relationship-wrapper members.

## Assumptions

- The existing 003 LINQ-/SQL-hybrid grammar (aliases, `from`/`where`/`order by`, operators
  `== != < <= > >= && || !`, `with` blocks, brace-delimited `insert`/`update set`) is the base this
  shape grammar extends; the read `select` is what changes from a projection expression to a shape.
- PostgreSQL and SQLite are the supported dialects; the dialect framework (feature 005) selects the
  per-dialect SQL for shaped reads at build time.
- Single-round-trip nested materialization is achieved by aggregating nested shapes server-side (the
  per-dialect mechanism is an implementation/plan concern, not part of this spec).
- Writes (insert/update/delete) are unchanged except that relationship members on entities are no
  longer runtime wrappers; "object-as-parameter" binding for writes is a separate future concern.
- The no-partial-data guarantee (Constitution Principle III) is now delivered by projection/shape
  types; the principle's "links modeled with loaded/unloaded states" clause is superseded by this
  feature and will be revised in the constitution.
- Auto-generated result type identity/naming (for the default, non-`into` case) follows Dormant's
  existing generated-naming conventions; exact names are an implementation detail.
