# Contract: Query Shape Grammar + Generated Surface

The user-facing contract: the authored shape grammar (a LINQ-logic / EdgeQL-shape blend) and the
shape of the code generated from it. This is a compatibility surface (DSL + generated code) — changes
here follow the constitution's versioning rules.

## Grammar (informal)

```
query <name>(<params>) {
  [ with <bind> = ( <query> ) ]*            # cascading read-side bindings (CTEs)
  from <Entity> <alias> [ , <Entity> <alias> ]*
  [ where <predicate> ]
  [ order by <nav> (asc|desc) [, ...] ]
  [ limit <n|param> ] [ offset <n|param> ]
  select <select-shape>
  [ into <UserRecord> ]
}

select-shape :=
    <alias>                                  # bare entity (flat row + FK id scalars)
  | <alias> { <shape-node> [, <shape-node>]* }   # root-object shape
  | { <named-member> [, <named-member>]* }       # free composition

shape-node :=
    <field>                                  # scalar column of the node entity
  | <toOneRef> : { <shape-node>* }           # nested object
  | <toManyCollection> : { <shape-node>* } [ order by <field> (asc|desc) ]   # nested list

named-member := <name> = <expr>              # expr: navigation path / literal / nested `<alias> { … }`
predicate    := uses == != < <= > >= && || ! and alias-qualified navigation (a.writer.name)
```

- **Bare `select a`** returns the flat entity (own columns incl. FK id scalars), no relationships.
- **Root shape `select a { … }`** always yields a generated projection type, even if all-scalar.
- **Free composition `select { … }`** assembles a new type from members of one or more sources.
- **Navigation** `a.writer.name` is valid only for relationships declared in the schema; the
  non-terminal segments must be declared to-one references.
- **To-many** appears only as a nested shape node (`tags: { … }`), resolved via backlink (the child's
  inverse to-one). Inner `order by` allowed; inner filter/limit are not in this version.
- **`with`** bindings cascade and compose in the final select; they resolve within the single query
  (CTEs), not as separate statements.

## Generated surface contract

- One `IAsyncEnumerable<TResult>` (or single) extension method per query on `ISession`, name = the
  unit's snake_case → PascalCase, as today.
- `TResult` is:
  - the **flat entity** for bare `select a`;
  - an **auto-generated immutable record** for a shape block (nested shapes ⇒ nested immutable records;
    to-many ⇒ `IReadOnlyList<TNested>`), OR
  - the **user record** named in `into`.
- A field not present in the requested shape is **not a member** of `TResult` ⇒ reading it is a
  compile error (FR-006).
- A to-one with no row ⇒ `null` nested record; a to-many with no rows ⇒ empty `IReadOnlyList`
  (never `null`) (FR-007).

## Execution contract

- A shaped read (any depth, including to-many) issues **exactly one** database command (FR-008).
- The SQL is generated at build time, one variant per dialect (`session.Dialect`); nested data is
  produced by JSON aggregation in that single statement.
- Materialization uses a generator-emitted `Utf8JsonReader` parser: no runtime reflection, no scalar
  boxing.
- The same authored query yields equivalent shape + logical results on PostgreSQL and SQLite (FR-019).

## Diagnostics (build-time)

- Unknown entity / column / parameter (existing ORM010/011/012 family extended to navigation paths).
- Navigating/shaping an undeclared relationship.
- Ambiguous to-many backlink (multiple candidate inverse FKs) ⇒ require explicit disambiguation.
- Shape cycle / self-reference beyond support ⇒ clear diagnostic (no infinite generation).
- Duplicate member names in a free composition.
- `into` structural mismatch (missing/extra/type-incompatible member).
- A field that would exceed a database/query limit (very wide/deep shape) ⇒ actionable diagnostic.

## Migration (breaking — MAJOR)

- `entity.Writer` (`Ref<Author>`) and collection wrappers (`entity.Posts` as `RefSet<Post>`) **no
  longer exist**. Replacements:
  - read the FK id scalar (`entity.WriterId`) and/or
  - author a shaped query that nests the related object(s).
- Writes are unchanged: mutations still set FKs by binding scalar/id values.
