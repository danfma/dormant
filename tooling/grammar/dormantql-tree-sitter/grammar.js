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
      $.scalar_definition,
      $.entity_definition,
      $.query_definition,
      $.mutation_definition,
    )),

    module_declaration: $ => seq('module', $.identifier, ';'),

    // --- Schema (.dqls) ---------------------------------------------------

    // Custom scalar type (012 US4): `scalar Name extending Base { constraint…; }`.
    scalar_definition: $ => seq(
      'scalar',
      field('name', $.identifier),
      'extending',
      $.type_identifier,
      $.member_block,
    ),

    // `[abstract] entity Name [extending A, B] { … }` (012 US5).
    entity_definition: $ => seq(
      optional('abstract'),
      'entity',
      $.identifier,
      optional($.extends_clause),
      '{',
      repeat($.entity_member),
      '}',
    ),

    extends_clause: $ => seq('extending', $.identifier, repeat(seq(',', $.identifier))),

    entity_member: $ => choice(
      $.member_declaration,
      $.constraint_statement,
      $.annotation_statement,
    ),

    // `name: TypeExpr[?]` terminated by either a `{ constraint…; annotation…; }` block or ';'.
    member_declaration: $ => seq(
      field('name', $.identifier),
      ':',
      $.type_expression,
      optional('?'),
      choice($.member_block, ';'),
    ),

    member_block: $ => seq(
      '{',
      repeat(choice($.constraint_statement, $.annotation_statement)),
      '}',
    ),

    // constraint name[(args)] [on (…)] [as name] ;  (012)
    constraint_statement: $ => seq(
      'constraint',
      field('name', $.identifier),
      optional($.argument_list),
      optional($.on_clause),
      optional($.as_clause),
      ';',
    ),

    annotation_statement: $ => seq(
      'annotation',
      field('name', $.identifier),
      optional($.argument_list),
      ';',
    ),

    on_clause: $ => seq('on', '(', $.identifier, repeat(seq(',', $.identifier)), ')'),
    as_clause: $ => seq('as', $.identifier),

    argument_list: $ => seq(
      '(',
      optional(seq($.argument, repeat(seq(',', $.argument)))),
      ')',
    ),
    // Positional or named (`min = 1`); the `check` expression is just a (binary) expression argument.
    argument: $ => choice(
      seq(field('name', $.identifier), '=', $.expression),
      $.expression,
    ),

    type_expression: $ => choice(
      $.primitive_type,
      seq($.type_identifier, optional($.generic_arguments)),
    ),

    type_identifier: $ => $.identifier,

    // 012: PascalCase value-type vocabulary.
    primitive_type: $ => choice(
      'String', 'Char', 'Byte', 'Short', 'Int', 'Long', 'Float', 'Double',
      'Decimal', 'Bool', 'Uuid', 'DateTime', 'Date', 'Time', 'Json',
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
