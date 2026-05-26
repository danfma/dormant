# Phase 1 Data Model: Comparative ORM Benchmarks

One representative entity, modeled three ways (Dormant-generated, plain POCO for
Dapper/Insight, EF Core entity) — all mapped to the **same** physical SQLite table.

## Entity: Product

The benchmark workload entity. Small and typical: an identity key, two text columns (one
used as the filtered-read predicate), a numeric and a money column.

| Field | DormantQL type | SQLite column | Notes |
|-------|----------------|---------------|-------|
| `id` | `uuid primary` | `id` (TEXT, PK) | Client-supplied `Guid` — no DB identity, so all libraries insert the same way (fairness) |
| `name` | `string` | `name` (TEXT) | |
| `category` | `string` | `category` (TEXT) | Predicate for the filtered multi-row read |
| `price` | `decimal` | `price` (TEXT) | Microsoft.Data.Sqlite stores `decimal` as TEXT — identical for every library |
| `quantity` | `int` | `quantity` (INTEGER) | Column mutated by the update op |

**Authority**: Dormant's source generator emits the table DDL; `DormantSqlite.EnsureCreatedAsync`
creates it. The POCO and EF entity bind to this table — they do **not** define their own DDL.
This guarantees FR-002 (identical schema, one source of truth).

### DormantQL schema (`schema/bench.dqls`)

```text
module bench;

entity Product {
  id: uuid primary;
  name: string;
  category: string;
  price: decimal;
  quantity: int;
}
```

Generated namespace: `Dormant.Benchmarks.Schema.Bench` (RootNamespace `Dormant.Benchmarks`
+ folder `schema` + module `bench`).

### Plain POCO (`Model/Product.cs`) — Dapper + Insight

A user-owned record with no Dormant types (the Clean-Arch projection boundary). Property
names match columns so Dapper/Insight map by convention.

```csharp
public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

### EF Core entity (`Model/BenchDbContext.cs`)

Same `Product` shape reused (or a parallel EF entity), configured `ToTable("Product")` with
`HasKey(p => p.Id)`. `DbContextOptions` point at a `SqliteConnection` opened on the shared
connection string. EF does **not** create the schema (`EnsureCreated` is skipped) — it binds
to Dormant's table.

## Operation parameters & result shapes

| Operation | Input | Result | Predicate / target |
|-----------|-------|--------|--------------------|
| Read-by-key | one `Guid` (a seeded id) | one `Product` | PK `id` |
| Filtered read | one `category` string | `Product[]` (~100 rows) | `category == @category` |
| Insert | new `Product` (fresh `Guid`) | rows-affected / inserted id | — |
| Update | `id` + new `quantity` | rows-affected | per-library scratch `id` |
| Delete | `id` | rows-affected | per-library scratch `id` (inserted in IterationSetup) |

## Seed dataset

- ~1,000 `Product` rows, `category` spread across ~10 distinct values (so filtered read
  returns ~100). Deterministic generation (fixed seed) for reproducibility (SC-003).
- A set of stable, per-library "scratch" ids reserved for update/delete so write benchmarks
  never collide across libraries (FR-007). Seeded once in `GlobalSetup`.

## Per-library representation map

| Concern | Dormant | Dapper | EF Core | Insight.Database |
|---------|---------|--------|---------|------------------|
| Type | generated `Product` (immutable, `required`) | `Model.Product` POCO | EF-configured `Product` | `Model.Product` POCO |
| Connection | `ISession` from `DormantSqlite.CreateSessionFactory` | `SqliteConnection` | `BenchDbContext` over `SqliteConnection` | `SqliteConnection` |
| Read-by-key | `session.GetAsync<Product>(id)` | `QueryFirstOrDefaultAsync<Product>` | `FindAsync` / `AsNoTracking().FirstOrDefault` | `SingleAsync<Product>` |
| Filtered read | generated `ProductsByCategory(cat)` (`IAsyncEnumerable`) | `QueryAsync<Product>` | `Where(...).AsNoTracking().ToListAsync` | `QueryAsync<Product>` |
| Insert | generated `CreateProduct(...)` | `ExecuteAsync(INSERT)` | `Add` + `SaveChangesAsync` | `ExecuteAsync(INSERT)` |
| Update | generated `UpdateProductQuantity(...)` | `ExecuteAsync(UPDATE)` | load + set + `SaveChangesAsync` | `ExecuteAsync(UPDATE)` |
| Delete | generated `DeleteProduct(id)` | `ExecuteAsync(DELETE)` | `Remove` + `SaveChangesAsync` | `ExecuteAsync(DELETE)` |
