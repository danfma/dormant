# Migration Guide — Feature 012 (EdgeQL-Style Constraints + PascalCase Types)

Feature 012 is a **breaking (MAJOR)** change to the DormantQL schema language. This guide lists every
change and how to migrate your `.dqls` / `.dql` files. The compiler flags removed syntax with
source-located diagnostics (`ORM003`, `ORM035`) pointing at the new form.

## 1. Value types are now PascalCase

The lowercase type aliases are removed. Rename every value type:

| Old (removed) | New |
|---------------|-----|
| `str` / `string` | `String` |
| `int` / `int32` | `Int` |
| `int64` / `long` | `Long` |
| `int16` | `Short` |
| `float32` / `float` | `Float` |
| `float64` / `double` | `Double` |
| `decimal` | `Decimal` |
| `bool` / `boolean` | `Bool` |
| `uuid` | `Uuid` |
| `datetime` | `DateTime` |
| `date` | `Date` |
| `json` | `Json` |
| `bytes` | `Array<Byte>` (planned) |

A leftover lowercase type now reports **ORM003**. Built-in type names are reserved (an entity may not
be named `Date`, `String`, etc.).

## 2. Member rules move from modifiers to constraint blocks

The trailing `primary` / `concurrency` / `db("…")` modifiers are removed. Use a `{ … }` block:

| Old (removed) | New |
|---------------|-----|
| `id: uuid primary;` | `id: Uuid { constraint primary; }` |
| `version: int concurrency;` | `version: Int { constraint concurrency; }` |
| `email: str db("email_col");` | `email: String { annotation column("email_col"); }` |

A leftover modifier reports **ORM035** with the suggested new form. A member with no rules keeps the
plain `name: Type;` form.

## 3. Constraints (function-call / C#-attribute style)

Declared inside a member block or at the entity level. Optional parentheses for zero-arg; positional
or named arguments.

```dqls
entity User {
  id: Uuid { constraint primary; }
  email: String {
    constraint unique as users_email_key;
    constraint max_length(255);
    constraint regex("^[^@]+@[^@]+$");
  }
  age: Int { constraint range(min = 0, max = 130); }
  status: String { constraint one_of("Active", "Closed"); }

  constraint unique on (email) as users_email_alt;   # entity-level / multi-field
  constraint check (age >= 0);                        # cross-field boolean expression
}
```

Standard library: `unique`, `check`, `one_of`, `max`, `min`, `max_exclusive`, `min_exclusive`,
`max_length`, `min_length`, `length`, `range`, `regex`, plus `primary` / `concurrency`.

- `as {name}` pins the SQL constraint name (else a deterministic default). Duplicate `as` names in a
  module report **ORM032**.
- A type-incompatible constraint reports **ORM030**; an unknown constraint **ORM029**; a missing
  referenced member **ORM031**.
- `check` references the entity's own columns only — there is no relationship navigation.

## 4. Annotations (metadata)

```dqls
title: String { annotation column("note_title"); }
```

`column("…")` sets the database column name (replaces `db("…")`). Unknown annotation names report
**ORM036**. Constraints/annotations on reference/collection members are not supported in v1 (ORM036).

## 5. Custom scalar types

```dqls
scalar Username extending String {
  constraint min_length(3);
  constraint max_length(30);
}

entity Account { id: Uuid { constraint primary; } handle: Username; }
```

A member typed with a scalar inherits the scalar's constraints. Scalars must be declared **before**
use. An unknown base reports **ORM033**.

## 6. Inheritance & composition

```dqls
abstract entity Timestamped {
  created_at: DateTime;
  updated_at: DateTime;
  constraint check (created_at <= updated_at);
}

entity Article extending Timestamped {
  id: Uuid { constraint primary; }
  title: String { constraint max_length(200); }
}
```

`abstract entity` emits no table; `extending` flattens inherited members + constraints into the
concrete entity's single table. Bases must be declared before use. A duplicate member name or unknown
base reports **ORM034**.

## Provider notes

- **PostgreSQL** (primary): every constraint kind enforced, including `regex` (`~`).
- **SQLite**: all enforced except `regex` (no native REGEXP) — omitted at the database level.
