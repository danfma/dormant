; DormantQL highlight queries (Feature 011). Canonical source — kept in sync with the
; Zed copy under tooling/zed-dormantql/languages/dormantql/highlights.scm.
; Ordering matters: Zed/Tree-sitter use first-match-wins, so specific captures come
; BEFORE the generic (identifier) @variable fallback at the end.

; Keywords
[
  "module"
  "entity"
  "query"
  "mutation"
  "from"
  "where"
  "select"
  "insert"
  "update"
  "delete"
  "with"
  "returning"
  "set"
  "order"
  "by"
  "asc"
  "desc"
  "into"
  "primary"
  "optional"
  "concurrency"
] @keyword

; Operators
(binary_operator) @operator
"=" @operator

; Literals
(string_literal) @string
(number_literal) @number
(boolean_literal) @constant.builtin.boolean

; Comments
(comment) @comment

; Types / entity names (strong distinction — FR-003 priority)
(primitive_type) @type.builtin
(type_identifier) @type
(entity_definition (identifier) @type)
(into_clause (qualified_identifier) @type)

; Declared member / field names
(member_declaration name: (identifier) @property)
(shape_field name: (identifier) @property)

; Parameters
(parameter name: (identifier) @variable.parameter)

; Generic identifier fallback (aliases, values, references) — MUST be last
(identifier) @variable

; Punctuation
[
  ";"
  ":"
  "."
  ","
  "?"
] @punctuation.delimiter

[
  "{"
  "}"
  "("
  ")"
  "<"
  ">"
] @punctuation.bracket
