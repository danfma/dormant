# Quickstart: Shape Selection

## Schema (relationships are metadata; entities are flat)

```text
module blog;

entity Author {
  id: uuid primary;
  name: string;
  articles: Set<Article>;     # to-many: metadata only, no entity member; backlink via Article.writer
}

entity Article {
  id: uuid primary;
  title: string;
  writer: Author;             # to-one: entity exposes WriterId scalar, no Ref wrapper
}

entity Tag {
  id: uuid primary;
  label: string;
  article: Article;
}
```

Generated entities (flat):

```csharp
public partial class Article { public required Guid Id { get; init; } public required string Title { get; init; } public required Guid WriterId { get; init; } }
public partial class Author  { public required Guid Id { get; init; } public required string Name { get; init; } }   // no Articles member
```

## Root-object shape (US1)

```text
query article_card(id: uuid) {
  from Article a
  where a.id == id
  select a {
    title,
    writer: { name },
    tags: { label } order by tag.label asc
  }
}
```

```csharp
var card = await session.ArticleCard(id).FirstOrDefaultAsync();
// card.Title, card.Writer.Name, card.Tags[i].Label
```

Result type:

```csharp
public sealed record ArticleCard(string Title, ArticleCardWriter Writer, IReadOnlyList<ArticleCardTag> Tags);
public sealed record ArticleCardWriter(string Name);
public sealed record ArticleCardTag(string Label);
```

One query is issued (JSON aggregation); `Tags` is empty (not null) when there are none; `Writer` is
null when absent.

## Free composition from multiple sources (US2)

```text
query feed_item(article_id: uuid) {
  with hot = (from Tag t where t.article == article_id order by t.label asc select t { label })
  from Article a
  where a.id == article_id
  select {
    headline   = a.title,
    authorName = a.writer.name,     # navigation generates the join
    tags       = hot                 # composed from the cascading `with`
  }
}
```

## Project into your own record (US3)

```text
query article_dto(id: uuid) {
  from Article a where a.id == id
  select a { title, writer: { name } } into ArticleDto
}
```

```csharp
public sealed record ArticleDto(string Title, ArticleAuthorDto Writer);
public sealed record ArticleAuthorDto(string Name);
```

Structural match by name + type; a mismatch fails the build.

## Bare entity (no shape)

```text
query article_by_id(id: uuid) { from Article a where a.id == id select a }
```

Returns the flat `Article` (incl. `WriterId`); fetch the author with a follow-up `GetAsync<Author>(article.WriterId)` or a shaped query.

## Validate (maps to success criteria)

1. A shaped read with nested to-one + to-many issues exactly one DB command → **SC-001**.
2. Reading a field not in the shape fails at build time → **SC-002**.
3. Empty to-many ⇒ empty list; absent to-one ⇒ null → **SC-003**.
4. Same query, same shape + logical results on PostgreSQL and SQLite → **SC-004**.
5. Free composition draws from ≥2 sources; per-source filter via `with` → **SC-005**.
6. Deep nesting with one authored query, no per-level code → **SC-006**.
7. Cyclic/over-limit shape ⇒ clear build-time diagnostic → **SC-007**.
8. Bare entity read exposes own columns + FK id scalars, no wrapper members → **SC-008**.
