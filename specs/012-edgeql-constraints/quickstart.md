# Quickstart: DormantQL Constraints, Scalar Types & Inheritance

How to declare data rules in DormantQL after Feature 012. Constraints replace the old trailing
modifiers; the model mirrors Gel/EdgeDB.

## 1. Constraints on a member

```dqls
entity User {
  id: Uuid { constraint primary; }
  email: String {
    annotation column("email_addr");          # metadata: DB column name (replaces old db("…"))
    constraint unique as users_email_key;
    constraint max_length(255);
    constraint regex("^[^@]+@[^@]+$");
  }
  age: Int { constraint range(min = 0, max = 130); }   # one multi-arg constraint, named args
  version: Int { constraint concurrency; }
}
```

- One `{ … }` block per member holds `constraint`s and `annotation`s.
- Constraints/annotations use a **function-call, C#-attribute-style** form: `name(args)` with
  positional or named arguments (`range(min = 1, max = 2)`); parentheses are **optional** for
  zero-argument ones (`constraint unique;`, `constraint primary;`).
- `as {name}` pins the database constraint name (else a deterministic name is generated).
- `annotation column("…")` sets the DB column name (metadata, not validation).
- Constraints/annotations apply to **value members and the entity level** only — not to references
  or collections (v1).

## 2. Multi-field & expression constraints (entity level)

```dqls
entity Booking {
  id: Uuid { constraint primary; }
  room: String;
  start_at: DateTime;
  end_at: DateTime;

  constraint unique on (room, start_at) as bookings_room_slot;
  constraint check (start_at <= end_at);
}
```

- `on (a, b)` → composite constraint over several members.
- `check (…)` accepts the same boolean expressions as a query `where`, over this entity's members.

## 3. Custom scalar types

```dqls
scalar Username extending String {
  constraint min_length(3);
  constraint max_length(30);
  constraint regex("^[a-z0-9_]+$");
}

scalar Status extending String {
  constraint one_of("Open", "Closed", "Merged");
}

entity Account {
  id: Uuid { constraint primary; }
  handle: Username;          # inherits Username's constraints
  status: Status;
}
```

A member typed with a scalar inherits the scalar's constraints; member-level constraints add to them.

## 4. Inheritance & composition

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

- `abstract entity` defines reusable members + constraints and emits **no table**.
- `extending` flattens inherited members and constraints into the concrete entity's single table.
- Multiple bases compose; conflicting names produce a diagnostic.

## 5. Standard constraint & annotation reference

**Constraints**: `unique`, `check`, `one_of`, `max`, `min`, `max_exclusive`, `min_exclusive`,
`max_length`, `min_length`, `length`, `range(min=, max=)`, `regex`, plus DormantQL's `primary` and
`concurrency`. Composite uniqueness uses the entity-level form `constraint unique on (a, b)` (there is
no per-field grouping key).

**Annotations**: `column("…")` (DB column name). Extensible to `description`, `deprecated`, … later.

See `contracts/constraint-dsl-contract.md` for argument/scope rules.

## 6. Migration from the old syntax (BREAKING)

The old trailing modifiers are removed. Rewrite:

| Old (removed) | New |
|---------------|-----|
| `id: Uuid primary;` | `id: Uuid { constraint primary; }` |
| `version: Int concurrency;` | `version: Int { constraint concurrency; }` |
| `email: String db("email_addr");` | `email: String { annotation column("email_addr"); }` |

The compiler flags leftover legacy modifiers with a source-located diagnostic pointing to the new
form. All bundled sample/test schemas have been migrated; do the same in your `.dqls` before
upgrading.

## Provider notes

- **PostgreSQL** (primary): all constraints enforced, including `regex` (`~`).
- **SQLite**: all enforced except `regex` where no faithful `GLOB`/`LIKE` equivalent exists — that
  case emits a build warning and is not enforced at the database level.
