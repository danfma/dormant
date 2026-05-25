# Phase 1 Data Model: Dormant ORM

**Feature**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25

Two layers of "model" exist in Dormant and must not be confused:

1. **The compiler/build model** — the internal types the source generator builds from the DSL (schema
   AST, query AST, emission descriptors). These are build-time-only, equatable, and never shipped.
2. **The runtime/user model** — the conceptual entities the spec enumerates (Entity, Link, Session,
   Migration, …) realized as generated types + the `Dormant.Abstractions` kernel.

Both are described below. Fields are conceptual (the public shape is fixed in `contracts/`).

---

## A. Build-time model (source generator)

### SchemaModel
The parsed, validated schema. Equatable aggregate root of generation.
- `Modules: EquatableArray<ModuleModel>`
- `Entities: EquatableArray<EntityModel>` (ordered by ordinal name)
- `Diagnostics: EquatableArray<DiagnosticInfo>`
- `IsValid: bool`
- **Validation**: links must target defined entities; required-link cycles flagged; duplicate
  property/link names flagged; every diagnostic carries a `LocationInfo` (FR-004, SC-009).

### EntityModel
- `Name`
- `Namespace` — `PascalCaseEachPart(rootNamespace + folders + module)` (FR-046), not the bare module
- `DbSchema` — the module name; the database schema the table lives in (FR-045)
- `TableName` — schema-qualified (`<DbSchema>.<table>`)
- `Properties: EquatableArray<PropertyModel>`
- `References: EquatableArray<ReferenceModel>`
- `Key: KeyModel` (identity columns)
- `ConcurrencyToken: PropertyModel?` (optimistic concurrency, FR-015)

### PropertyModel
- `Name`, `ColumnName`
- `ValueType: ValueTypeRef` (scalar | collection | native — see ValueTypeRef)
- `IsNullable: bool`
- `IsConcurrencyToken: bool`

### ValueTypeRef
- `Kind: { Scalar, Array, Tuple, NamedTuple, Json, Native }`
- `ClrType: string` (build-time-known target .NET type)
- `ProviderType: string?` (e.g. `jsonb`, `geometry` — set for `Native`)
- `ProviderScope: string?` (provider directive scope, FR-042)

### ReferenceModel (formerly LinkModel)
- `Name`, `Target: string` (entity name); `KeyType: string?` (for `Map`)
- `Kind: { Ref, Set, List, Bag, Map }` → `Ref<T>` / `RefSet<T>` / `RefList<T>` / `RefBag<T>` / `RefMap<K,V>` (FR-049).
  Syntax: `name: Target` (single), `name: Set<Target>` / `List<…>` / `Bag<…>` / `Map<Key, Target>` (FR-047)
- `IsRequired: bool` (single ref: bare = required → C# `required`; `Target?` = optional; collections optional by default)
- `JoinEntity: string?` (set when m:n carries edge data via a join entity, FR-037)

### QueryModel
- `Name`, `ResultShape: ShapeModel`, `Parameters: EquatableArray<ParameterModel>`
- `SqlFragments: EquatableArray<SqlFragmentModel>` (prebuilt; some toggled by optional params)
- `Kind: { Select, Insert, Update, Delete }`
- **Validation**: every parameter typed; every referenced field exists; result type closed at build time
  (FR-006); native constructs carry a provider scope (FR-042).

### ShapeModel
- `RootEntity: string`
- `Fields: EquatableArray<ShapeField>` (scalar fields, or nested `ShapeModel` for links)
- `IsProjection: bool` (projection = distinct generated type; full entity otherwise, FR-007)

### ParameterModel
- `Name`, `ClrType`, `IsOptional: bool`, `AffectsFragments: EquatableArray<int>`

### NativeBindingModel / NativeFunctionModel
- `NativeBindingModel`: `ProviderScope`, `ProviderType`, `ClrType`, `ReadExpr`, `WriteExpr` (FR-038).
- `NativeFunctionModel`: `ProviderScope`, `Name`, `ParameterTypes: EquatableArray<string>`,
  `ReturnType`, or `RawSqlFragment` + declared `ReturnType` (FR-039/FR-040). **Validation**: raw fragment
  must declare a return type (else build error); arguments must match signature.

### Emission descriptors
`EntityEmit` (incl. `[SetsRequiredMembers]` materialization ctor), `SnapshotEmit` (snapshot struct + diff comparer; reads via public getters),
`QueryEmit` (typed method + SQL), `JsonContextEmit` (STJ `JsonSerializerContext`), `NativeEmit`.

---

## B. Runtime / user model

### Entity (generated, mutable)
A generated `partial` class. Mapped properties + link members. Mutable; the session holds its snapshot.
- **State**: `Transient` → `Persistent`(tracked) → `Removed`; `Detached` when its session closes.
- **Invariants**: never partially populated — a full entity has all mapped columns; unfetched references are
  `Unloaded` (not null-as-loaded).

### Ref<T> / RefSet<T> / RefList<T> / RefBag<T> / RefMap<K,V> (kernel, `Dormant.Abstractions`)
Reference types over a relationship on a full entity (FR-009/FR-049). Each is a `readonly struct`.
- **State**: `Unloaded` (default sentinel) | `Loaded(value)`. Default is `Unloaded`, never empty.
- **Single-ref optionality** = nullability of `T` (orthogonal to load-state): `Ref<User>` required (loaded
  non-null), `Ref<User?>` optional (loaded may be null); `Ref<T> where T : class?`. Collections take no
  element `?` (`RefSet<User>` has non-null elements).
- Reading the value requires handling `Unloaded` (e.g. `TryGetLoaded`, pattern match); raw value not
  directly reachable while unloaded.
- **Collection semantics** (FR-049): `RefSet` unordered/unique; `RefBag` unordered/dups; `RefList`
  ordered; `RefMap` keyed.
- **Transition**: `Unloaded → Loaded` only via an explicit session on-demand load, or by being fetched in
  a shape. Never implicit (FR-009).

### Projection / Shape (generated or user-owned record)
A distinct type containing exactly the requested fields + nested shapes (FR-007). Accessing a
non-requested field is impossible (the member does not exist) (FR-008). May be a generated type **or a
user-owned plain `record`/DTO with no Dormant types** (FR-050) — the dependency-free boundary for
domain/application code. Records get structural equality from the compiler.

### Entity equality (FR-051)
Generated `Equals`/`GetHashCode`: equal when same entity type with equal primary-key values; transient
(unset key) → reference equality; `[NoIdentityEquality]` opts out.

### Session / Unit of Work (kernel + Core)
- Owns: **identity map** (one instance per key), **snapshots**, the open transaction.
- **Lifecycle**: `Open` → (track/query/mutate) → `Commit`/`Rollback` → `Closed`.
- **Commit**: diff each tracked entity vs snapshot; emit INSERT/UPDATE(changed columns only)/DELETE; verify
  concurrency token; refresh snapshots (FR-014/FR-015).
- **Concurrency**: on token mismatch at commit → conflict surfaced to caller (FR-015), no silent overwrite.

### Migration
- `Id` (ordered, e.g. timestamp), `Name`, `Up`/`Down` operations, `State: { Pending, Applied }`.
- **Transitions**: `Pending → Applied` (apply), `Applied → Pending` (rollback).
- Incremental: a new migration captures only the diff vs prior schema state (FR-021). Destructive ops are
  flagged, not auto-applied (FR-022).

### Type Handler / Native Type Binding
- `ITypeBinding<T>`: read/write a column ↔ `T` with no boxing (FR-019, FR-025).
- **Native Type Binding**: a provider-scoped `ITypeBinding<T>` for a native type (`jsonb`, `geometry`)
  (FR-038); registered under a `Provider Directive` scope.

### Native Function / Operator
- Declared signature (typed params + single return type) in the native catalog, or a raw typed SQL
  fragment; both keep a statically-known return type (FR-039/FR-040). Invocations are type-checked.

### Provider / Provider Directive
- **Provider**: adapter (PostgreSQL) implementing the data-access, dialect, and migration contracts; declares the
  native types/functions available (FR-024).
- **Provider Directive**: build-time marker scoping native constructs; drives the unsupported-provider
  diagnostic (FR-042).

### Convention
- A rule deriving mapping defaults (e.g. naming) without per-member config (FR-025), applied at generation
  time.

---

## Relationships (overview)

```
SchemaModel 1─* EntityModel 1─* PropertyModel
                EntityModel 1─* ReferenceModel ─► EntityModel (target)
QueryModel ─► ShapeModel ─► (EntityModel | ProjectionType)
Session 1─* Entity (identity map)  ;  Entity 1─* Ref<T>/RefSet<T>
Provider 1─* NativeBinding ; Provider 1─* NativeFunction ; ProviderDirective scopes both
Migration *─1 SchemaModel (snapshot at migration time)
```
