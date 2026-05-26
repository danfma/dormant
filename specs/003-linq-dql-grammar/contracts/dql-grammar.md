# Contract: DormantQL Authored Grammar (units)

The authored grammar for `.dql` unit files (queries + mutations). The `.dqls` schema grammar is unchanged and
not specified here. EBNF is indicative; `#` starts a line comment; statements are newline-separated (a
trailing `;` is tolerated, never required); identifiers are case-sensitive.

## Grammar (EBNF-ish)

```ebnf
unit-file   = "module" ident ";" { unit } ;
unit        = query | mutation ;

query       = "query" snake-name "(" [ params ] ")" "{"
                "from" Entity alias
                [ "where" predicate ]
                { "order" "by" member ( "asc" | "desc" ) }
                ( "select" alias | "select" "{" member { "," member } "}" )
              "}" ;

mutation    = "mutation" snake-name "(" [ params ] ")" "{"
                { with-binding }
                command { command }
                [ trailing ]
              "}" ;

command     = insert-cmd | update-cmd | delete-cmd ;
insert-cmd  = "insert" Entity alias "{" { assignment } "}" [ returning ] ;
update-cmd  = "update" Entity alias "where" predicate "set" "{" { assignment } "}" [ returning ] ;
delete-cmd  = "delete" Entity alias "where" predicate [ returning ] ;

with-binding= "with" ident "=" expr ;
trailing    = returning
            | "from" Entity alias [ "where" predicate ] select-shape ;   # trailing read
returning   = "returning" ( alias | "{" member { "," member } "}" | member ) ;
select-shape= "select" alias | "select" "{" member { "," member } "}" ;

assignment  = member "=" expr ;                      # '=' is assignment
predicate   = or-expr ;
or-expr     = and-expr { "||" and-expr } ;
and-expr    = not-expr { "&&" not-expr } ;
not-expr    = [ "!" ] cmp ;
cmp         = ( member cmp-op expr ) | "(" predicate ")" ;
cmp-op      = "==" | "!=" | "<" | "<=" | ">" | ">=" ;

params      = param { "," param } ;
param       = ident ":" [ "optional" ] type ;
type        = "string" | "bool" | "int" | "long" | "double" | "decimal"
            | "uuid" | "datetime" | "date" | "json" ;

member      = alias "." ident ;                      # alias-qualified, required
alias       = ident ;
Entity      = PascalCaseIdent ;
snake-name  = snake_case_ident ;
expr        = param-ref | literal | with-ref | native-call ;
```

## Canonical clause order (enforced)

- **query**: `from` → `where`? → `order by`* → `select`
- **mutation command**: `insert E a { … }` | `update E a where … set { … }` | `delete E a where …`
- Optional `returning` follows each command; an optional **trailing read** or `returning` at the block tail
  determines a multi-command unit's result.

## Operators

| DQL | Meaning | SQL |
|-----|---------|-----|
| `==` | equal | `=` |
| `!=` | not equal | `<>` |
| `<` `<=` `>` `>=` | ordering | same |
| `&&` | logical and | `AND` |
| `\|\|` | logical or | `OR` |
| `!` | logical not | `NOT` |
| `=` | assignment (insert/set) | column write |

Precedence: `!` > comparison > `&&` > `||`; parentheses group.

## Result inference (no `returning`)

| Trailing | Result |
|----------|--------|
| `select alias` | full immutable entity |
| `select { … }` | distinct projection type |
| `insert` | the entity's primary-key (id) value |
| `update` / `delete` | affected-row count (`int`) |
| `returning <expr>` / trailing read | overrides default; shape mirrors `select` (entity / projection / scalar) |

## Identifier casing

- `query`/`mutation` names: `snake_case` authored → `PascalCase` C# method.
- Entities: `PascalCase`. Members/aliases: `snake_case` in DQL.
- Type keywords: lowercase.

## Diagnostics (located, FR-009)

| Condition | Message intent |
|-----------|----------------|
| Removed `002` form (`command`, `= …;`, leading `.field`, `:=`, `and`/`or`) | name the removed construct + the replacement |
| Missing / undeclared / duplicate alias | require an explicit, unique, declared alias |
| Unqualified member (`email` not `u.email`) | members must be alias-qualified |
| Unknown entity / member / parameter | name the unknown symbol + source span |
| Wrong clause order | state the canonical order |
| `insert` missing a required member | name the missing member |
| `returning`/`select` member not in shape | fixed-shape violation |

## Removed forms (MUST NOT parse — FR-015)

`command Name(...) = …;`, `query Name(...) = …;`, leading-dot members (`.email`), `:=` assignment, single-`=`
comparison, `and`/`or`/`not` keyword connectives, `::`/`->` operators.
