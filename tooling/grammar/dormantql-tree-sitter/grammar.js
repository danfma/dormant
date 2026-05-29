// Tree-sitter grammar for DormantQL (Feature 011) — canonical source of truth.
// Designed for syntax highlighting: it favors recognizing the FR-003 semantic categories
// (keywords, types/entity names, comments, strings, numbers, params, aliases, punctuation)
// over full semantic validation. `word` makes DSL keywords reserved so they highlight
// distinctly from identifiers.
module.exports = grammar({
  name: 'dormantql',

  word: $ => $.identifier,

  extras: $ => [
    /\s+/,
    $.comment,
  ],

  rules: {
    source_file: $ => repeat(choice(
      $.module_declaration,
      $.entity_definition,
      $.query_definition,
      $.mutation_definition,
    )),

    module_declaration: $ => seq('module', $.identifier, ';'),

    // --- Schema (.dqls) ---------------------------------------------------

    entity_definition: $ => seq(
      'entity',
      $.identifier,
      '{',
      repeat($.member_declaration),
      '}',
    ),

    // Unified member: `name: TypeExpr[?] [modifier...] ;`. A single rule covers both value
    // fields (`email: str`) and single refs (`author: User`) — they are syntactically
    // identical, so distinguishing them is a semantic concern, not a highlighting one.
    member_declaration: $ => seq(
      field('name', $.identifier),
      ':',
      $.type_expression,
      optional('?'),
      repeat($.member_modifier),
      ';',
    ),

    member_modifier: $ => choice('primary', 'concurrency'),

    type_expression: $ => choice(
      $.primitive_type,
      seq($.type_identifier, optional($.generic_arguments)),
    ),

    type_identifier: $ => $.identifier,

    primitive_type: $ => choice(
      'uuid', 'string', 'str', 'int', 'long', 'double', 'decimal',
      'bool', 'boolean', 'datetime', 'date', 'json',
    ),

    generic_arguments: $ => seq(
      '<',
      $.type_expression,
      repeat(seq(',', $.type_expression)),
      '>',
    ),

    // --- Operations (.dql) ------------------------------------------------

    query_definition: $ => seq(
      'query',
      $.identifier,
      optional($.parameter_list),
      '{',
      repeat($.query_statement),
      '}',
    ),

    mutation_definition: $ => seq(
      'mutation',
      $.identifier,
      optional($.parameter_list),
      '{',
      repeat($.mutation_statement),
      '}',
    ),

    parameter_list: $ => seq(
      '(',
      optional(seq($.parameter, repeat(seq(',', $.parameter)))),
      ')',
    ),

    parameter: $ => seq(
      field('name', $.identifier),
      ':',
      optional('optional'),
      $.type_expression,
    ),

    query_statement: $ => choice(
      $.from_clause,
      $.where_clause,
      $.select_clause,
      $.order_by_clause,
      $.with_clause,
    ),

    mutation_statement: $ => choice(
      $.insert_statement,
      $.update_statement,
      $.delete_statement,
      $.with_clause,
      $.returning_clause,
    ),

    from_clause: $ => seq('from', $.identifier, optional($.identifier)),
    where_clause: $ => seq('where', $.expression),

    // `select w` (full entity), `select a { … }` (root-object shape, 009 US1),
    // `select { … }` (free composition, 009 US2), with an optional `into` target (009 US3).
    select_clause: $ => seq(
      'select',
      choice(
        $.shape_expression,
        seq($.expression, optional($.shape_expression)),
      ),
      optional($.into_clause),
    ),

    into_clause: $ => seq('into', $.qualified_identifier),
    qualified_identifier: $ => seq($.identifier, repeat(seq('.', $.identifier))),

    order_by_clause: $ => seq(
      'order', 'by',
      $.order_term,
      repeat(seq(',', $.order_term)),
    ),
    order_term: $ => seq($.expression, optional(choice('asc', 'desc'))),

    insert_statement: $ => seq(
      'insert',
      $.identifier,
      optional($.identifier),
      '{',
      repeat($.assignment),
      '}',
    ),

    update_statement: $ => seq(
      'update',
      $.identifier,
      optional($.identifier),
      optional($.where_clause),
      'set',
      '{',
      repeat($.assignment),
      '}',
    ),

    delete_statement: $ => seq(
      'delete',
      $.identifier,
      optional($.identifier),
      optional($.where_clause),
    ),

    with_clause: $ => seq(
      'with',
      $.identifier,
      '=',
      '(',
      choice($.insert_statement, $.update_statement, $.delete_statement, $.expression),
      ')',
    ),

    returning_clause: $ => seq('returning', choice($.shape_expression, $.expression)),

    // Fields may be separated by commas (009 shapes) or just whitespace (terse projections);
    // a trailing comma is tolerated.
    shape_expression: $ => seq('{', repeat(seq($.shape_field, optional(','))), '}'),
    shape_field: $ => choice(
      seq(field('name', $.identifier), '=', $.expression),        // free composition: x = path
      seq(field('name', $.identifier), ':', $.shape_expression),  // nested shape: writer: { … }
      $.expression,                                               // bare column or path: title / u.id
    ),

    assignment: $ => seq(
      choice($.member_expression, $.identifier),
      '=',
      $.expression,
    ),

    // --- Expressions ------------------------------------------------------

    expression: $ => choice(
      $.member_expression,
      $.identifier,
      $.string_literal,
      $.number_literal,
      $.boolean_literal,
      $.parenthesized_expression,
      $.binary_expression,
    ),

    parenthesized_expression: $ => seq('(', $.expression, ')'),

    binary_expression: $ => prec.left(1, seq($.expression, $.binary_operator, $.expression)),

    member_expression: $ => prec.left(2, seq($.expression, '.', $.identifier)),

    binary_operator: $ => choice('==', '!=', '<=', '>=', '<', '>', '&&', '||'),

    // --- Tokens -----------------------------------------------------------

    identifier: $ => /[a-zA-Z_][a-zA-Z0-9_]*/,

    string_literal: $ => token(seq('"', repeat(choice(/[^"\\]/, /\\./)), '"')),
    number_literal: $ => /\d+(\.\d+)?/,
    boolean_literal: $ => choice('true', 'false'),

    comment: $ => token(choice(
      seq('#', /.*/),
      seq('//', /.*/),
      seq('/*', /[^*]*\*+([^\/*][^*]*\*+)*/, '/'),
    )),
  },
});
