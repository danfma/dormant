# Phase 1 Data Model: Shape Selection + Flat Immutable Entities

Three layers change: the **parse model (AST)**, the **SqlIr**, and the **generated runtime types**.

## 1. Parse model (AST) — `Parsing/QueryModel.cs`

Today a query is `{ RootEntity, ProjectionFields: string[], Filters, OrderBy, Limit/Offset,
IsProjection }`. New model:

- **QueryUnit**
  - `Name`
  - `Parameters` (unchanged)
  - `Sources: QuerySource[]` — one or more in-scope sources (a `from E alias`, a join, or a `with`
    binding result). Replaces the implicit single root.
  - `With: WithBinding[]` — cascading read-side bindings (a later one may reference an earlier).
  - `Where`, `OrderBy`, `Limit`, `Offset` — as today, but columns may be **navigation paths**.
  - `Select: SelectShape`
  - `Into: IntoTarget?` — optional user-owned record binding.

- **SelectShape** (one of):
  - `RootShape { Alias, Nodes: ShapeNode[] }` — `select a { … }`.
  - `FreeComposition { Members: NamedMember[] }` — `select { name = expr, nested = b { … } }`.
  - `BareEntity { Alias }` — `select a` (no block) ⇒ the flat entity (no projection type).

- **ShapeNode** (one of):
  - `ScalarField { Name }` — a column of the node's entity.
  - `ToOneShape { RefName, Nodes: ShapeNode[] }` — nested object.
  - `ToManyShape { CollectionName, Nodes: ShapeNode[], OrderBy: OrderTerm[]? }` — nested list (inner
    `order by` allowed; inner filter/limit deferred).

- **NamedMember** `{ Name, Value }` where `Value` is an expression (navigation path / literal) or a
  nested shape rooted at a source.
- **WithBinding** `{ Name, Query: QueryUnit }`.
- **IntoTarget** `{ TypeName }` (resolved structurally at emit time).
- **NavigationPath** `{ Alias, Segments: string[] }` — e.g. `a.writer.name` ⇒ alias `a`, segments
  `[writer, name]`; each non-terminal segment is a declared to-one.

**Validation (SchemaValidator)**: resolve every navigation segment against schema relationships
(unknown ref/field ⇒ ORM diagnostic); resolve to-many via backlink (ambiguous inverse ⇒ diagnostic);
detect shape cycles (self-reference beyond a node already on the path ⇒ build-time diagnostic);
duplicate member names in a free composition ⇒ diagnostic; `into` structural mismatch ⇒ diagnostic.

## 2. SqlIr additions — `Ir/SqlIr.cs` (net-new)

The flat builder grows a small relational/expression core. Existing flat `SelectStatement` stays for
the no-shape path (byte-identical output).

- **FromItem** `{ TableRef Table, string Alias }`
- **Join** `{ FromItem Target, JoinKind Kind, SqlExpr On }` (INNER/LEFT)
- **QualifiedColumn** `{ string Alias, string Column }` : `SqlExpr`
- **SqlExpr** hierarchy (select-items + conditions + JSON builders):
  - `ColumnExpr(QualifiedColumn)`
  - `ParamExpr(int Index, bool Json)`
  - `BinaryExpr(SqlExpr L, string Op, SqlExpr R)` (predicates)
  - `FuncExpr(string CanonicalFunc, SqlExpr[] Args)` (dialect spells the function)
  - `JsonObjectExpr(IReadOnlyList<(string Key, SqlExpr Value)>)` — object builder
  - `JsonArrayAggExpr(ScalarSubquery Source)` — array aggregation
  - `ScalarSubquery(ShapedSelect Inner)` — a correlated subquery returning one (JSON) value
- **ShapedSelect** `{ IReadOnlyList<FromItem> From, IReadOnlyList<Join> Joins,
  IReadOnlyList<(string? Key, SqlExpr Expr)> Items, IReadOnlyList<SqlExpr> Where,
  IReadOnlyList<SqlOrder> OrderBy, SqlLimit? Limit/Offset, IReadOnlyList<Cte> With }`
- **Cte** `{ string Name, ShapedSelect Query }`

A shaped query's top-level result is a `ShapedSelect` whose single select-item is a
`JsonObjectExpr` (the whole shape), so execution returns one JSON column.

**Renderers** (`Ir/Dialects/*`): each `ISqlDialectRenderer` gains rendering for joins, qualified
columns, CTEs, and the JSON builders. Only the JSON-builder/aggregate spelling differs:

| Canonical IR | PostgreSQL | SQLite |
|--------------|-----------|--------|
| `JsonObjectExpr` | `jsonb_build_object('k', v, …)` | `json_object('k', v, …)` |
| `JsonArrayAggExpr` | `coalesce(jsonb_agg(elem [order by …]), '[]'::jsonb)` | `coalesce(json_group_array(elem), json('[]'))` |

## 3. Generated runtime types

### Entity (flattened) — `Schema/EntityEmitter.cs`

```csharp
public partial class Article : IEquatable<Article>
{
    public Article() { }
    [SetsRequiredMembers] internal Article(IFieldReader reader)
    {
        Id = reader.GetValue<Guid>(0);
        Title = reader.GetValue<string>(1);
        WriterId = reader.GetValue<Guid>(2);   // FK scalar, was Ref<Author> Writer
    }
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required Guid WriterId { get; init; }   // optional ref => Guid? ManagerId
    // NO Ref<Author> Writer; NO RefSet<...> collections
}
```

- To-one relationship ⇒ FK id scalar property (`WriterId`), required/optional mirroring the ref's
  nullability. To-many collection declaration ⇒ **no** entity member.
- `Dormant.Abstractions.Entities.{Ref,RefSet,RefList,RefBag,RefMap}` are **deleted**.

### Shaped projection + nested records + parser — `Query/QueryEmitter.cs`

For `query article_card(id) { from Article a where a.id == id select a { title, writer: { name }, tags: { label } } }`:

```csharp
public sealed record ArticleCard(string Title, ArticleCardWriter Writer, IReadOnlyList<ArticleCardTag> Tags);
public sealed record ArticleCardWriter(string Name);
public sealed record ArticleCardTag(string Label);
```

- Result type = the shape (FR-011: shape block ⇒ projection always). Nested records named by path
  (`{Method}{PathSegments}`); exact naming per existing conventions.
- The query method returns `IAsyncEnumerable<ArticleCard>` (or a single, per cardinality), backed by a
  `CompiledQuery<ArticleCard>` whose statement selects one JSON column and whose materializer calls a
  generated `Utf8JsonReader` parser:

```csharp
static reader => ArticleCardJson.Parse(reader.GetValue<string>(0)); // emitted Utf8JsonReader parser
```

### `into` target

`select a { … } into MyDto` ⇒ no generated record; the emitted parser targets `MyDto` after a
build-time structural check (member name case-insensitive + assignable type; mismatch = diagnostic).

## Key entities (recap)

- **Query unit / Shape / Shape node / Result type / Entity / Relationship (schema)** — as defined in
  spec.md Key Entities. This file binds them to concrete model/IR/emitted-type changes.
