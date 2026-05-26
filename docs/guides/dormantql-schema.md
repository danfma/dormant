# DormantQL Schema Guide

DormantQL schema files use the `.dqls` extension. They describe modules, entities, scalar members, references, relationship collections, primary keys, and concurrency members.

## Module

```dql
module app;
```

A module maps to a database schema and contributes to the generated namespace. With a root namespace and schema folder, generated code uses a PascalCase namespace path.

## Entity

```dql
entity User {
  id: uuid primary;
  email: str;
  created_at: datetime;
  bio: str?;
  posts: Set<Post>;
  version: int concurrency;
}
```

Entity names are PascalCase. Member names are snake_case in DormantQL and become PascalCase members in generated C#.

## Member Syntax

Members use:

```dql
name: TypeExpr;
```

By default, value members are required. Add `?` to mark an optional value:

```dql
bio: str?;
```

The sample schema uses scalar types such as `uuid`, `str`, `datetime`, and `int`.

## Primary Keys And Concurrency

```dql
id: uuid primary;
version: int concurrency;
```

The primary member is the entity identity. A concurrency member participates in optimistic concurrency behavior for write paths.

## References

A single reference is written as the target entity type:

```dql
author: User;
```

Generated entities use explicit relationship load-state types, such as `Ref<T>`, so a relationship can be unloaded without pretending to be present.

## Collections

Relationship collections use collection type expressions:

```dql
posts: Set<Post>;
```

The designed relationship collection families are:

- `Set<T>` for unordered unique relationships.
- `List<T>` for ordered relationships.
- `Bag<T>` for unordered relationships that may contain duplicates.
- `Map<K, V>` for keyed relationships.

Generated relationship members use corresponding `RefSet<T>`, `RefList<T>`, `RefBag<T>`, and `RefMap<TKey, TValue>` load-state types.

## Current Sample

```dql
module app;

entity User {
  id: uuid primary;
  email: str;
  created_at: datetime;
  bio: str?;
  posts: Set<Post>;
  version: int concurrency;
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;
}
```

This is copied from `samples/Dormant.Sample.Quickstart/schema/app.dqls`.
