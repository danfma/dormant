# Feature Specification: EdgeQL-Style Constraints

**Feature Branch**: `012-edgeql-constraints`

**Created**: 2026-05-29

**Status**: Draft

**Input**: User description: "Vamos uniformizar as constraints como no EdgeQL e não como type modifiers, como foi feito hoje. A única alteração seria a possibilidade de usar um `as {name}` para mapear a constraint para uma constraint SQL. Também precisamos definir um conjunto mínimo de constraints com base no EdgeQL mesmo. Vale atentar que algumas constraints podem ser aplicadas sobre múltiplos fields e até terem expressões mais complexas. A única diferença é que podemos dar nomes mais semânticos/conhecidos para algumas, como por exemplo: exclusive -> unique. Além disso, também temos o suporte a tipos escalares, herança e composição. Em termos de schema e definição, deveríamos copiar o máximo que pudermos do original."

## Context & Motivation

Today DormantQL expresses data rules as **trailing type modifiers** on a member
(e.g. `id: uuid primary;`, `version: int concurrency;`). This is ad-hoc: each new rule needs a
new keyword, modifiers do not compose, they cannot span multiple fields, they cannot carry
parameters or arbitrary expressions, and there is no uniform way to name the resulting database
constraint.

This feature replaces that ad-hoc model with a **uniform, EdgeQL-inspired constraint system**: a
schema author declares named constraints in a constraint block attached to a member or to an
entity, drawing from a defined standard library of constraints. Where it helps adoption, the
standard constraints use familiar/semantic names (e.g. EdgeQL's `exclusive` becomes **`unique`**).
The system also introduces **custom scalar types** that carry constraints, and **inheritance and
composition** so constraints and fields can be reused across entities. The schema surface should
mirror the original EdgeQL model as closely as is reasonable.

## Clarifications

### Session 2026-05-29

- Q: Os marcadores atuais `primary`/`concurrency` migram pra sintaxe de constraint ou continuam markers? → **Migrar tudo p/ constraint.** `primary` e `concurrency` passam a ser declarados como constraints (DormantQL-specific, sem equivalente EdgeQL), unificando toda regra de membro num único mecanismo.
- Q: Scalar types (US4) e herança/composição (US5) entram nesta feature ou em follow-up? → **Incluir tudo agora.** US1–US5 fazem parte desta feature.
- Q: A sintaxe antiga de modifiers é removida ou mantida como alias deprecado? → **Quebra limpa (MAJOR).** A sintaxe de modifier antiga é removida; só a nova sintaxe de constraint vale. Guia de migração é parte da entrega.

### Session 2026-05-29 (2nd round, post-analyze)

- Q: Como expressar nome de coluna no DB (antigo `db("…")`)? → **`annotation` (não constraint).** Dentro do bloco do membro, metadata vai como `annotation column("email_col")` — separando metadata de validação (idiomático EdgeQL, que tem `annotation` de 1ª classe). Abre porta p/ `annotation deprecated`, `description`, etc.
- Q: Sintaxe de args de constraint/annotation? → **Function-call estilo atributo C#** (`constraint range(min = 1, max = 2)`, `annotation column("email_col")`): args posicionais OU nomeados (`nome = valor` dentro dos parênteses); parêntese **opcional** p/ zero-arg (`constraint unique;`); `on (…)` e `as …` permanecem sufixos fora dos parênteses. Permite constraints multi-arg numa só declaração.
- Q: `check` pode navegar relacionamentos? → **Não.** Operador de navegação foi descartado em favor de aliases; `check` referencia só colunas da própria entidade e renderiza como expressão **literal** no DDL (sem placeholder/alias).
- Q: Constraints em refs/coleções? → **Fora de escopo (v1).** Constraints só em value members (+ entity-level sobre value members). Ref/coleção fica p/ depois.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare validation constraints on a field (Priority: P1)

A schema author attaches one or more named constraints to a single member using a constraint block,
instead of stacking bespoke modifier keywords. The standard library covers the common rules
(uniqueness, length, numeric range, pattern, allowed values, and an arbitrary boolean expression).

**Why this priority**: This is the core uniformization the feature exists to deliver. It replaces
the unscalable modifier approach with one consistent, composable mechanism and unblocks every other
story. On its own it already lets authors express the rules they most need.

**Independent Test**: Write an entity whose member declares `unique` plus a length and a range
constraint; confirm the schema compiles, the rules are reflected in the generated database
schema/DDL, and violating rows are rejected by the database.

**Acceptance Scenarios**:

1. **Given** a member `email: str { constraint unique; constraint max_length(255); }`, **When** the
   schema is built, **Then** the generated schema enforces both a uniqueness rule and a maximum
   length on that column.
2. **Given** a member with `constraint min(0)` on a numeric field, **When** a row with a negative
   value is written, **Then** the database rejects it.
3. **Given** a member with two constraints declared, **When** the author reads the member back,
   **Then** the constraint block is the single, uniform place those rules live (no trailing
   modifier keywords for them).
4. **Given** an unknown constraint name, **When** the schema is built, **Then** a source-located
   diagnostic names the offending constraint and lists the available standard constraints.

---

### User Story 2 - Multi-field and expression constraints at the entity level (Priority: P2)

A schema author declares a constraint that spans **several members** (e.g. a composite uniqueness
rule) or that evaluates an **arbitrary boolean expression** over the entity's members, by attaching
the constraint to the entity rather than to a single member.

**Why this priority**: Composite uniqueness and cross-field checks are common real-world rules that
the modifier model could never express. They build directly on US1's mechanism but apply at a
broader scope.

**Independent Test**: Declare an entity with `constraint unique on (first_name, last_name)` and a
`constraint check (start_date <= end_date)`; confirm the composite uniqueness and the cross-field
check are both enforced by the generated database schema.

**Acceptance Scenarios**:

1. **Given** an entity-level `constraint unique on (a, b)`, **When** two rows share the same `(a, b)`
   pair, **Then** the second write is rejected.
2. **Given** an entity-level `constraint check (<boolean expression over members>)`, **When** a row
   violates the expression, **Then** the write is rejected.
3. **Given** a multi-field constraint, **When** the schema is built, **Then** the referenced members
   must exist and a missing member produces a source-located diagnostic.

---

### User Story 3 - Map a constraint to a named SQL constraint with `as` (Priority: P2)

A schema author appends `as {name}` to a constraint declaration to pin the **name of the underlying
SQL constraint** that gets generated, instead of relying on an auto-generated name.

**Why this priority**: Stable, human-chosen constraint names matter for migrations, for matching an
existing database, and for readable error messages and operational tooling. It is a small,
orthogonal addition on top of US1/US2.

**Independent Test**: Declare `constraint unique as users_email_key;` and confirm the generated
database constraint carries exactly that name.

**Acceptance Scenarios**:

1. **Given** `constraint unique as users_email_key`, **When** the schema is generated, **Then** the
   resulting database constraint is named `users_email_key`.
2. **Given** two constraints in the same module that request the same `as` name, **When** the schema
   is built, **Then** a source-located diagnostic reports the collision.
3. **Given** a constraint without `as`, **When** the schema is generated, **Then** a deterministic,
   stable default name is produced.

---

### User Story 4 - Custom scalar types that carry constraints (Priority: P3)

A schema author defines a **named scalar type** that extends a base scalar (e.g. `str`, `int`) and
attaches constraints to it. Members typed with that scalar inherit its constraints, so a rule like
"a username is at most 30 characters and matches a pattern" is defined once and reused.

**Why this priority**: Scalar types remove duplication and give domain concepts a name, but the core
value (US1–US3) is deliverable without them.

**Independent Test**: Define `scalar Username extending str { constraint max_length(30); }`, type a
member as `Username`, and confirm the member enforces the scalar's constraints without restating
them.

**Acceptance Scenarios**:

1. **Given** a scalar type with constraints and a member typed with it, **When** the schema is built,
   **Then** the member enforces the scalar's constraints.
2. **Given** a member that also declares its own constraints, **When** the schema is built, **Then**
   the member's constraints apply in addition to the scalar's.

---

### User Story 5 - Inheritance and composition of entities (Priority: P3)

A schema author defines an **abstract entity** (or reusable base) carrying members and constraints,
and other entities **extend/compose** it, inheriting those members and constraints. Mirrors the
EdgeQL model of abstract types and `extending`.

**Why this priority**: Inheritance/composition reduces duplication across entities (e.g. shared
`created_at`/`updated_at` plus their constraints) but is the least urgent slice and the most
involved.

**Independent Test**: Define an abstract entity with a timestamp member and a constraint, extend it
from a concrete entity, and confirm the concrete entity has the inherited member and constraint.

**Acceptance Scenarios**:

1. **Given** an abstract base with a member and a constraint, **When** a concrete entity extends it,
   **Then** the concrete entity exposes the inherited member and enforces the inherited constraint.
2. **Given** an entity extending multiple bases, **When** the schema is built, **Then** members and
   constraints are composed and a name conflict produces a source-located diagnostic.

---

### Edge Cases

- A constraint references a member that does not exist, or a multi-field constraint lists a member
  twice.
- Two constraints (or two `as` names) collide within the same scope.
- A constraint is applied to a member whose type does not support it (e.g. `max_length` on a numeric
  field, `regex` on a non-string).
- A custom scalar's constraints conflict with a member-level constraint of the same kind (e.g. two
  different `max_length`).
- Inheritance introduces the same member or constraint name from two different bases.
- A `check` expression references a relationship/navigation path (rejected — navigation operator was
  removed in favor of aliases; `check` is restricted to the entity's own columns).
- A constraint or annotation is declared on a reference/collection member (out of scope v1).
- An unknown annotation name, or an annotation given the wrong argument shape.
- Migration of existing schemas that still use the old `primary`/`concurrency`/`db("…")` forms.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The schema language MUST allow attaching one or more named constraints to a single
  member via a constraint block, replacing bespoke trailing modifier keywords for validation rules.
- **FR-002**: The schema language MUST allow attaching constraints to an entity as a whole, including
  constraints that reference **multiple members**.
- **FR-003**: The system MUST define a **minimum standard constraint library** based on the EdgeQL
  standard constraints, using familiar/semantic names where appropriate. The minimum set MUST cover:
  uniqueness (EdgeQL `exclusive` → **`unique`**), an arbitrary boolean **`check`/expression**
  constraint, maximum/minimum length, exact length, inclusive numeric max/min, exclusive numeric
  max/min, a **regular-expression/pattern** constraint, and an **allowed-values** (`one_of`)
  constraint. (Exact final names to be confirmed in clarification.)
- **FR-004**: Standard constraints that take parameters (length, range, pattern, allowed values) MUST
  accept those parameters in a **function-call, C#-attribute-style** form: positional or named
  arguments inside parentheses (`range(min = 1, max = 2)`, `max_length(255)`), parentheses optional
  for zero-argument constraints (`unique`, `primary`). Named arguments allow multi-value constraints
  in one declaration.
- **FR-005**: A constraint declaration MUST support an optional `as {name}` clause that sets the name
  of the underlying database constraint; without it, a deterministic default name MUST be generated.
- **FR-006**: Entity-level constraints MUST support an **arbitrary boolean expression** over the
  entity's **own members** (cross-field checks). The expression MUST NOT navigate relationships
  (the navigation operator is removed; only alias/column-qualified members of the entity are valid);
  it is rendered as a literal expression in the generated DDL.
- **FR-007**: The schema language MUST support **custom scalar types** that extend a base scalar and
  carry constraints; members typed with a custom scalar MUST inherit its constraints.
- **FR-008**: The schema language MUST support **inheritance/composition** of entities (abstract
  bases + extending), with inherited members and constraints composed into the derived entity.
- **FR-009**: All constraint, scalar, and inheritance errors (unknown constraint, type-incompatible
  constraint, missing referenced member, duplicate/`as`-name collision, inheritance conflict) MUST
  produce **source-located, actionable diagnostics** (Principle I/IV).
- **FR-010**: Constraints declared in the schema MUST be reflected in the **generated database
  schema/DDL** so the database enforces them, consistently across supported providers (PostgreSQL
  primary; SQLite where the provider supports the rule, with documented fallbacks where it does not).
- **FR-011**: The schema definition surface MUST mirror the EdgeQL constraint model as closely as is
  reasonable, so authors familiar with EdgeQL can transfer their knowledge.
- **FR-012**: The old trailing-modifier syntax for member rules MUST be **removed** (clean break);
  only the new constraint syntax is accepted. This is a breaking (MAJOR) change to the schema DSL and
  MUST ship with a documented migration guide (Principle II).
- **FR-013**: The existing `primary` (identity / primary key) and `concurrency` (optimistic-lock
  token) markers MUST be re-expressed as **constraints** in the new uniform syntax (e.g.
  `constraint primary;`, `constraint concurrency;`), so every per-member rule lives in one
  mechanism. These are DormantQL-specific constraints with no EdgeQL equivalent; they retain their
  identity/concurrency semantics and keep their names.
- **FR-014**: Custom scalar types (US4) and entity inheritance/composition (US5) ARE in scope for
  this feature's first delivery, alongside the core constraint uniformization (US1–US3).
- **FR-015**: The schema language MUST support **annotations** as metadata distinct from constraints,
  declared in the same member/entity/scalar block via the same function-call form
  (`annotation column("email_col")`). The minimum annotation set MUST include **`column`** (the
  database column-name override that replaces the removed `db("…")` modifier). The model MUST be
  extensible to further annotations (e.g. `description`, `deprecated`) without rework. An unknown
  annotation name or wrong argument shape MUST produce a source-located diagnostic.
- **FR-016**: Constraints and annotations are supported on **value members and at the entity level**
  only (v1). Declaring them on a reference/collection member is out of scope and MUST produce a
  source-located diagnostic rather than silently failing to parse.

### Proposed Standard Constraint Mapping (EdgeQL → DormantQL)

Source of truth for the model: Gel/EdgeDB standard constraints
(`https://docs.geldata.com/reference/stdlib/constraints`). DormantQL copies the model and renames a
few for familiarity. Final names confirmed in clarification; this is the proposed minimum set.

| EdgeQL (`std`)  | DormantQL (proposed) | Parameters            | Applies to        |
|-----------------|----------------------|-----------------------|-------------------|
| `exclusive`     | **`unique`**         | none (or `on (…)`)    | member / entity   |
| `expression`    | **`check`**          | boolean expression    | member / entity   |
| `one_of`        | `one_of`             | value list            | member            |
| `max_value`     | **`max`**            | value (inclusive)     | member            |
| `min_value`     | **`min`**            | value (inclusive)     | member            |
| `max_ex_value`  | **`max_exclusive`**  | value (exclusive)     | member            |
| `min_ex_value`  | **`min_exclusive`**  | value (exclusive)     | member            |
| `max_len_value` | **`max_length`**     | length                | string member     |
| `min_len_value` | **`min_length`**     | length                | string member     |
| `len_value`     | **`length`**         | exact length          | string member     |
| `regexp`        | **`regex`**          | pattern               | string member     |
| — (DormantQL)   | **`primary`**        | none                  | member (identity) |
| — (DormantQL)   | **`concurrency`**    | none                  | member (lock token)|

`primary` and `concurrency` have no EdgeQL counterpart (Gel handles `id` implicitly); DormantQL
keeps explicit identity/concurrency but now expresses them through the same `constraint` mechanism
(FR-013).

Illustrative DormantQL surface (mirrors EdgeQL; exact grammar finalized in planning):

```dqls
scalar Status extending str {
  constraint one_of("Open", "Closed", "Merged");
}

abstract entity Timestamped {
  created_at: datetime;
  updated_at: datetime;
}

entity User extending Timestamped {
  id: uuid { constraint primary; }
  email: str {
    annotation column("email_addr");        # metadata: DB column name (replaces old db("…"))
    constraint unique as users_email_key;
    constraint max_length(255);
    constraint regex("^[^@]+@[^@]+$");
  }
  age: int { constraint range(min = 0, max = 130); }   # multi-arg constraint, named args
  first_name: str;
  last_name: str;
  status: Status;
  version: int { constraint concurrency; }

  constraint unique on (first_name, last_name) as users_full_name_key;
  constraint check (created_at <= updated_at);
}
```

Constraints use a **function-call, C#-attribute-style** form (`name(args)`, positional or named;
parentheses optional for zero-arg). Annotations (`annotation name(args)`) carry metadata and are
distinct from constraints.

The expression form (`check`) references the entity's own members directly (alias/column-qualified;
no relationship navigation) — the DormantQL analogue of EdgeQL's `__subject__`.

### Key Entities *(include if data involved)*

- **Constraint**: A named rule from the standard library, optionally parameterized, attached to a
  member or an entity, optionally carrying an `as` SQL name. Has a kind (unique, check, length,
  range, pattern, allowed-values), zero or more arguments, and an optional target member list /
  expression.
- **Standard constraint library**: The defined minimum set of constraint kinds and their
  semantic names.
- **Custom scalar type**: A named type extending a base scalar, carrying a set of constraints,
  reusable as a member type.
- **Abstract/base entity**: A reusable definition (members + constraints) that concrete entities
  extend or compose.
- **Member / entity (existing)**: Gains a constraint block; entities gain entity-level constraints
  and an `extending` relationship.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every rule expressible today via member modifiers for validation purposes is
  expressible with the new constraint syntax, plus at least the full minimum standard library
  (FR-003), verified by schema examples for each constraint kind.
- **SC-002**: A schema author can declare a composite (multi-field) uniqueness rule and a cross-field
  expression check, and the database rejects rows that violate them — demonstrated end to end on the
  primary provider.
- **SC-003**: A constraint declared with `as {name}` produces a database constraint with exactly that
  name 100% of the time; constraints without `as` get stable, deterministic names that do not change
  between builds of an unchanged schema.
- **SC-004**: A custom scalar type's constraints apply to every member typed with it without the
  author restating them, and an abstract entity's members/constraints appear on every entity that
  extends it — verified by examples.
- **SC-005**: All defined error cases (unknown constraint, type mismatch, missing member, name
  collision, inheritance conflict) produce a diagnostic that names the problem and its source
  location; no constraint error fails silently.
- **SC-006**: An author familiar with EdgeQL constraints can author the equivalent DormantQL schema
  without consulting a translation table for the common cases (subjective parity check against the
  EdgeQL model).

## Assumptions

- The minimum constraint set is modeled on the EdgeQL standard constraints, with semantic renames
  (at minimum `exclusive` → `unique`); the precise final names are confirmed during clarification but
  default to the familiar SQL/validation vocabulary.
- Constraints are enforced primarily at the **database** layer via generated DDL; build-time and
  runtime application-side validation beyond what the database enforces is out of scope unless a
  later clarification adds it.
- PostgreSQL is the primary reference provider; SQLite support follows the multi-dialect framework,
  with documented fallbacks where SQLite cannot express a given constraint.
- This is a breaking change to the schema DSL (MAJOR), consistent with Constitution Principle II; the
  old trailing-modifier syntax is removed (clean break) and a migration guide is part of the
  deliverable.
- `primary` and `concurrency` keep their identity/optimistic-concurrency semantics but are now
  expressed as constraints (FR-013); the capability is not removed, only its syntax is unified.
- Scalar types and entity inheritance/composition are included in this feature (FR-014), making it a
  large, multi-part delivery; user stories are independently shippable in priority order.
- The DQL syntax-highlighting grammar (Feature 011) will be updated in the same release cycle to
  cover the new constraint/scalar/inheritance syntax (Feature 011 FR-005 / SC-005).
