using System.Collections.Generic;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>
/// Result of parsing one DormantQL unit file (<c>.dql</c>) in a single pass: both <c>query</c> and
/// <c>mutation</c> units share the file. (003 unifies the prior <c>QueryParser</c>/<c>CommandParser</c>.)
/// </summary>
/// <param name="ModuleName">The declared module name (or <c>Dormant.Generated</c> when omitted).</param>
/// <param name="Queries">The parsed read units (<c>query</c>).</param>
/// <param name="Commands">The parsed write units (<c>mutation</c>).</param>
/// <param name="Diagnostics">Diagnostics collected during parsing.</param>
internal readonly record struct UnitParseResult(
    string ModuleName,
    IReadOnlyList<QueryModel> Queries,
    IReadOnlyList<CommandModel> Commands,
    IReadOnlyList<DiagnosticInfo> Diagnostics
);

/// <summary>
/// Hand-written recursive-descent parser for the 003 LINQ-/SQL-hybrid DormantQL unit grammar (one pass per
/// <c>.dql</c> file). A unit is <c>('query'|'mutation') snake_name '(' params ')' '{' body '}'</c>; statements
/// are newline-separated with an optional, tolerated <c>;</c>; <c>#</c> starts a line comment (handled by the
/// lexer). Members are alias-qualified (<c>alias.col</c>) and the alias is validated against the subject's
/// declared alias. Removed 002 forms (<c>command</c>, <c>= …</c>, leading-dot, <c>:=</c>, <c>and</c>/<c>or</c>)
/// produce a located migration diagnostic (ORM020) instead of a generic parse error. Produces the existing
/// <see cref="QueryModel"/> / <see cref="CommandModel"/> shapes so the 002 emitters need only small tweaks.
/// </summary>
internal sealed class UnitParser
{
    private readonly string _filePath;
    private readonly List<Token> _tokens;
    private readonly List<DiagnosticInfo> _diagnostics = [];
    private readonly HashSet<string> _bindingNames = new(System.StringComparer.Ordinal);
    private int _pos;

    private UnitParser(string filePath, List<Token> tokens)
    {
        _filePath = filePath;
        _tokens = tokens;
    }

    public static UnitParseResult Parse(string filePath, string text)
    {
        var parser = new UnitParser(filePath, Lexer.Tokenize(text));
        return parser.ParseFile();
    }

    private Token Current => _tokens[_pos];

    private Token Peek() => _tokens[System.Math.Min(_pos + 1, _tokens.Count - 1)];

    private bool IsKeyword(string keyword) =>
        Current.Kind == TokenKind.Identifier && Current.Text == keyword;

    private UnitParseResult ParseFile()
    {
        var moduleName = "Dormant.Generated";
        var queries = new List<QueryModel>();
        var commands = new List<CommandModel>();

        if (IsKeyword("module"))
        {
            _pos++;
            if (Current.Kind == TokenKind.Identifier)
            {
                moduleName = Current.Text;
                _pos++;
            }
            else
            {
                Error("expected a module name after 'module'");
            }

            Expect(TokenKind.Semicolon, "';' after the module declaration");
        }

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (IsKeyword("query"))
            {
                var query = ParseQuery();
                if (query is not null)
                {
                    queries.Add(query);
                }
            }
            else if (IsKeyword("mutation"))
            {
                var command = ParseMutation();
                if (command is not null)
                {
                    commands.Add(command);
                }
            }
            else if (IsKeyword("command"))
            {
                // Removed 002 form (FR-015): name the construct + replacement, then recover. Consume the
                // 'command' keyword FIRST (parity with ParseQuery/ParseMutation) — otherwise RecoverToUnitEnd
                // sees 'command' as a unit boundary and returns without advancing, spinning this loop forever.
                Removed(
                    "'command' was removed; write 'mutation name(...) { insert|update|delete … }' instead"
                );
                _pos++;
                RecoverToUnitEnd();
            }
            else
            {
                Error($"unexpected '{Describe(Current)}'; expected 'query' or 'mutation'");
                RecoverToUnitEnd();
            }
        }

        return new UnitParseResult(moduleName, queries, commands, _diagnostics);
    }

    // query := 'query' snake_name '(' params? ')' '{' from where? (order by)* select '}'
    private QueryModel? ParseQuery()
    {
        _pos++; // 'query'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a query name after 'query'");
            RecoverToUnitEnd();
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.LeftParen, "'(' to open the parameter list"))
        {
            RecoverToUnitEnd();
            return null;
        }

        var parameters = ParseParameters();

        // Removed 002 form: `query Name(...) = select …;`.
        if (Current.Kind == TokenKind.Equals)
        {
            Removed(
                "the '= select …;' form was removed; write 'query name(...) { from Entity alias … select … }' instead"
            );
            RecoverToUnitEnd();
            return null;
        }

        if (!Expect(TokenKind.LeftBrace, "'{' to open the query body"))
        {
            RecoverToUnitEnd();
            return null;
        }

        // 'from' Entity alias
        if (!ExpectKeyword("from"))
        {
            RecoverToUnitEnd();
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name after 'from'");
            RecoverToUnitEnd();
            return null;
        }

        var entity = Current.Text;
        _pos++;

        var alias = ParseAlias(entity);
        if (alias is null)
        {
            RecoverToUnitEnd();
            return null;
        }

        var filters = new List<FilterCondition>();
        var orderBy = new List<OrderTerm>();
        var projection = new List<string>();
        SelectShape? shape = null;
        FreeComposition? composition = null;
        var seenWhere = false;
        var seenOrder = false;
        var seenSelect = false;

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (IsKeyword("where"))
            {
                if (seenOrder || seenSelect)
                {
                    ClauseOrder("where", "from → where → order by → select");
                }

                seenWhere = true;
                _pos++;
                ParseWherePredicate(alias, filters);
            }
            else if (IsKeyword("order"))
            {
                if (seenSelect)
                {
                    ClauseOrder("order by", "from → where → order by → select");
                }

                seenOrder = true;
                _pos++;
                ParseOrderBy(alias, orderBy);
            }
            else if (IsKeyword("select"))
            {
                seenSelect = true;
                _pos++;
                if (!ParseSelect(alias, projection, out shape, out composition))
                {
                    RecoverToUnitEnd();
                    return null;
                }
            }
            else if (Current.Kind == TokenKind.Semicolon)
            {
                _pos++; // tolerated statement separator
            }
            else if (DetectRemovedClause())
            {
                RecoverToUnitEnd();
                return null;
            }
            else
            {
                Error(
                    $"unexpected '{Describe(Current)}' in the query body; expected 'where', 'order by', or 'select'"
                );
                RecoverToUnitEnd();
                return null;
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the query");

        if (!seenSelect)
        {
            Error($"query '{name}' is missing a 'select' clause");
        }

        _ = seenWhere; // tracked for clause-order validation only

        return new QueryModel(
            name,
            entity,
            alias,
            new EquatableArray<QueryParameter>([.. parameters]),
            new EquatableArray<string>([.. projection]),
            new EquatableArray<FilterCondition>([.. filters]),
            new EquatableArray<OrderTerm>([.. orderBy]),
            Limit: null,
            Offset: null,
            Shape: shape,
            Composition: composition
        );
    }

    // mutation := 'mutation' snake_name '(' params? ')' '{' command '}'
    // command  := insert Entity alias '{' assignment* '}' | update Entity alias where set '{' … '}' | delete Entity alias where
    private CommandModel? ParseMutation()
    {
        _pos++; // 'mutation'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a mutation name after 'mutation'");
            RecoverToUnitEnd();
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.LeftParen, "'(' to open the parameter list"))
        {
            RecoverToUnitEnd();
            return null;
        }

        var parameters = ParseParameters();

        // Removed 002 form: `command Name(...) = insert …;` (now also if a mutation used `=`).
        if (Current.Kind == TokenKind.Equals)
        {
            Removed(
                "the '= insert …;' form was removed; write 'mutation name(...) { insert Entity alias { … } }' instead"
            );
            RecoverToUnitEnd();
            return null;
        }

        if (!Expect(TokenKind.LeftBrace, "'{' to open the mutation body"))
        {
            RecoverToUnitEnd();
            return null;
        }

        // 003 (FR-021/FR-022): zero or more `with name = ( command )` bindings, then the terminal command.
        // Each binding runs as its own statement; its result is referable downstream by name (WithRef value).
        _bindingNames.Clear();
        var bindings = new List<WithBinding>();
        while (IsKeyword("with"))
        {
            _pos++; // 'with'
            if (Current.Kind != TokenKind.Identifier || IsReserved(Current.Text))
            {
                Error($"expected a binding name after 'with' but found '{Describe(Current)}'");
                RecoverToUnitEnd();
                return null;
            }

            var bindingName = Current.Text;
            _pos++;
            if (
                !Expect(TokenKind.Equals, "'=' after the with-binding name")
                || !Expect(TokenKind.LeftParen, "'(' to open the bound command")
            )
            {
                RecoverToUnitEnd();
                return null;
            }

            var bound = ParseCommandCore(bindingName, parameters);
            if (bound is null || !Expect(TokenKind.RightParen, "')' to close the bound command"))
            {
                RecoverToUnitEnd();
                return null;
            }

            bindings.Add(new WithBinding(bindingName, bound));
            _bindingNames.Add(bindingName);
        }

        var terminal = ParseCommandCore(name, parameters);
        if (terminal is null)
        {
            RecoverToUnitEnd();
            return null;
        }

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.Semicolon)
            {
                _pos++;
                continue;
            }

            Error($"unexpected '{Describe(Current)}' after the mutation's terminal command");
            RecoverToUnitEnd();
            return null;
        }

        Expect(TokenKind.RightBrace, "'}' to close the mutation");

        return terminal with
        {
            Bindings = new EquatableArray<WithBinding>([.. bindings]),
        };
    }

    // Parses one command core — `insert|update|delete Entity alias <body> [returning]` — without the
    // enclosing mutation braces or binding parens. Shared by `with` bindings and the terminal command.
    // Returns null on error WITHOUT recovering (the caller — ParseMutation — recovers the unit once).
    private CommandModel? ParseCommandCore(string name, List<QueryParameter> parameters)
    {
        CommandKind kind;
        if (IsKeyword("insert"))
        {
            kind = CommandKind.Insert;
        }
        else if (IsKeyword("update"))
        {
            kind = CommandKind.Update;
        }
        else if (IsKeyword("delete"))
        {
            kind = CommandKind.Delete;
        }
        else
        {
            Error($"expected 'insert', 'update', or 'delete' but found '{Describe(Current)}'");
            return null;
        }

        _pos++; // kind keyword
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name");
            return null;
        }

        var entity = Current.Text;
        _pos++;

        var alias = ParseAlias(entity);
        if (alias is null)
        {
            return null;
        }

        var assignments = new List<Assignment>();
        var filters = new List<FilterCondition>();

        if (kind == CommandKind.Insert)
        {
            ParseAssignmentBlock(alias, assignments);
        }
        else
        {
            // update/delete: 'where' predicate is required, then 'set { … }' for update.
            if (!ExpectKeyword("where"))
            {
                return null;
            }

            ParseWherePredicate(alias, filters);

            if (kind == CommandKind.Update)
            {
                if (!ExpectKeyword("set"))
                {
                    return null;
                }

                ParseAssignmentBlock(alias, assignments);
            }
        }

        // Optional `returning` clause shapes the result (FR-017).
        ReturningShape? returning = null;
        if (IsKeyword("returning"))
        {
            _pos++;
            returning = ParseReturning(alias);
            if (returning is null)
            {
                return null;
            }
        }

        return new CommandModel(
            name,
            kind,
            entity,
            alias,
            new EquatableArray<QueryParameter>([.. parameters]),
            new EquatableArray<Assignment>([.. assignments]),
            new EquatableArray<FilterCondition>([.. filters]),
            returning
        );
    }

    // alias := IDENT (required; missing → ORM021). A reserved clause/keyword token cannot be an alias (so
    // `from Widget where …` reports a missing alias rather than swallowing `where`). The entity name is
    // reused only for the message.
    private string? ParseAlias(string entity)
    {
        if (Current.Kind != TokenKind.Identifier || IsReserved(Current.Text))
        {
            _diagnostics.Add(Located(DiagnosticDescriptors.MissingAlias, Current, entity));
            return null;
        }

        var alias = Current.Text;
        _pos++;
        return alias;
    }

    // Clause/structural keywords that may immediately follow a subject; they can never be an alias.
    private static bool IsReserved(string text) =>
        text
            is "where"
                or "order"
                or "select"
                or "set"
                or "from"
                or "insert"
                or "update"
                or "delete"
                or "query"
                or "mutation"
                or "returning"
                or "with"
                or "and"
                or "or";

    private List<QueryParameter> ParseParameters()
    {
        var parameters = new List<QueryParameter>();
        while (Current.Kind != TokenKind.RightParen && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a parameter name");
                break;
            }

            var paramName = Current.Text;
            _pos++;
            Expect(TokenKind.Colon, "':' between the parameter name and its type");

            // Optional parameter (FR-012/FR-031): `name: optional Type`.
            var isOptional = false;
            if (IsKeyword("optional"))
            {
                isOptional = true;
                _pos++;
            }

            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a parameter type");
                break;
            }

            var dslType = Current.Text;
            _pos++;
            var clrType = TypeMap.TryMap(dslType, out var mapped) ? mapped : string.Empty;
            if (clrType.Length == 0)
            {
                Error($"unknown parameter type '{dslType}'");
            }

            parameters.Add(new QueryParameter(paramName, dslType, clrType, isOptional));

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
        }

        Expect(TokenKind.RightParen, "')' to close the parameter list");
        return parameters;
    }

    // where := predicate ('&&' predicate)* ; predicate := member cmp-op value
    // 003 IN scope: conjunction (`&&`) only → flattened into the ANDed filter list.
    private void ParseWherePredicate(string alias, List<FilterCondition> filters)
    {
        while (true)
        {
            var condition = ParseComparison(alias);
            if (condition is not null)
            {
                filters.Add(condition);
            }

            if (Current.Kind == TokenKind.AmpAmp)
            {
                _pos++;
                continue;
            }

            // Removed 002 form: keyword connectives `and`/`or`.
            if (IsKeyword("and") || IsKeyword("or"))
            {
                Removed(
                    $"keyword connective '{Current.Text}' was removed; use '&&' (and) or '||' (or)"
                );
                break;
            }

            // TODO(003): `||` and `!` logical operators are deferred.
            if (Current.Kind == TokenKind.PipePipe || Current.Kind == TokenKind.Bang)
            {
                Error(
                    $"the '{Current.Text}' logical operator is not supported yet (only '&&' conjunction)"
                );
                break;
            }

            break;
        }
    }

    private FilterCondition? ParseComparison(string alias)
    {
        // 009 P-B: a comparison's left side may navigate to-one references (alias.ref.[ref.]*column).
        var path = ParseMemberPath(alias);
        if (path is null || path.Count == 0)
        {
            return null;
        }

        var column = path[path.Count - 1];
        var navRefs = path.GetRange(0, path.Count - 1);

        var op = ParseOperator();
        if (op is null)
        {
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a parameter on the right side of the comparison");
            return null;
        }

        var paramName = Current.Text;
        _pos++;
        return new FilterCondition(
            column,
            op.Value,
            paramName,
            new EquatableArray<string>([.. navRefs])
        );
    }

    // member-path := alias '.' IDENT ('.' IDENT)* — returns the segments after the alias (the last is the
    // terminal column; any preceding segments are to-one references to navigate). 009 P-B.
    private List<string>? ParseMemberPath(string subjectAlias)
    {
        if (Current.Kind == TokenKind.Dot)
        {
            Removed(
                "leading-dot members ('.field') were removed; use alias-qualified members like 'u.field'"
            );
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error($"expected an alias-qualified member but found '{Describe(Current)}'");
            return null;
        }

        var first = Current.Text;

        if (Peek().Kind != TokenKind.Dot)
        {
            _diagnostics.Add(Located(DiagnosticDescriptors.UnqualifiedMember, Current, first));
            _pos++;
            return null;
        }

        var aliasToken = Current;
        _pos++; // alias

        var segments = new List<string>();
        while (Current.Kind == TokenKind.Dot)
        {
            _pos++; // '.'
            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a member name after '.'");
                return null;
            }

            segments.Add(Current.Text);
            _pos++;
        }

        if (first != subjectAlias)
        {
            _diagnostics.Add(Located(DiagnosticDescriptors.UndeclaredAlias, aliasToken, first));
            return null;
        }

        return segments;
    }

    // member := alias '.' IDENT. Validates the alias matches the subject's declared alias.
    private string? ParseMemberRef(string subjectAlias)
    {
        // Removed 002 form: leading-dot member (`.email`).
        if (Current.Kind == TokenKind.Dot)
        {
            Removed(
                "leading-dot members ('.field') were removed; use alias-qualified members like 'u.field'"
            );
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error($"expected an alias-qualified member but found '{Describe(Current)}'");
            return null;
        }

        var first = Current.Text;

        // Unqualified member: `email` without `.column` after it.
        if (Peek().Kind != TokenKind.Dot)
        {
            _diagnostics.Add(Located(DiagnosticDescriptors.UnqualifiedMember, Current, first));
            _pos++;
            return null;
        }

        _pos++; // alias
        _pos++; // '.'

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a member name after '.'");
            return null;
        }

        var member = Current.Text;
        _pos++;

        if (first != subjectAlias)
        {
            _diagnostics.Add(Located(DiagnosticDescriptors.UndeclaredAlias, Current, first));
            return null;
        }

        return member;
    }

    private CompareOp? ParseOperator()
    {
        switch (Current.Kind)
        {
            case TokenKind.EqualEqual:
                _pos++;
                return CompareOp.Eq;
            case TokenKind.BangEqual:
                _pos++;
                return CompareOp.Neq;
            case TokenKind.LeftAngle:
                _pos++;
                return CompareOp.Lt;
            case TokenKind.RightAngle:
                _pos++;
                return CompareOp.Gt;
            case TokenKind.LessEqual:
                _pos++;
                return CompareOp.Le;
            case TokenKind.GreaterEqual:
                _pos++;
                return CompareOp.Ge;
            case TokenKind.Equals:
                // Removed 002 form: single '=' as comparison.
                Removed("single '=' is assignment; use '==' for equality comparison");
                return null;
            default:
                Error($"expected a comparison operator but found '{Describe(Current)}'");
                return null;
        }
    }

    // order by := 'order' 'by' term (',' term)* ; term := member ('asc'|'desc')?
    private void ParseOrderBy(string alias, List<OrderTerm> terms)
    {
        if (!ExpectKeyword("by"))
        {
            return;
        }

        while (true)
        {
            var column = ParseMemberRef(alias);
            if (column is null)
            {
                break;
            }

            var descending = false;
            if (IsKeyword("asc"))
            {
                _pos++;
            }
            else if (IsKeyword("desc"))
            {
                descending = true;
                _pos++;
            }

            terms.Add(new OrderTerm(column, descending));

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
                continue;
            }

            break;
        }
    }

    // select := alias  |  alias '{' shape-node* '}'  |  '{' member (','? member)* '}'
    // (the middle form is the 009 root-object shape; commas optional; newline-separated)
    private bool ParseSelect(
        string alias,
        List<string> projection,
        out SelectShape? shape,
        out FreeComposition? composition
    )
    {
        shape = null;
        composition = null;

        // 009 US1: root-object shape — `select alias { … }` (alias immediately before a brace).
        if (Current.Kind == TokenKind.Identifier && Peek().Kind == TokenKind.LeftBrace)
        {
            var selectedAlias = Current.Text;
            _pos++; // alias
            if (selectedAlias != alias)
            {
                _diagnostics.Add(
                    Located(DiagnosticDescriptors.UndeclaredAlias, _tokens[_pos - 1], selectedAlias)
                );
                return false;
            }

            var nodes = ParseShapeBlock();
            if (nodes is null)
            {
                return false;
            }

            shape = new SelectShape(alias, new EquatableArray<ShapeNode>([.. nodes]));
            return true;
        }

        if (Current.Kind == TokenKind.LeftBrace)
        {
            // 009 US2: free composition — `{ name = path, … }` (named members; an '=' after the first
            // member name distinguishes it from a flat `{ alias.field … }` projection).
            if (
                _pos + 2 < _tokens.Count
                && _tokens[_pos + 1].Kind == TokenKind.Identifier
                && _tokens[_pos + 2].Kind == TokenKind.Equals
            )
            {
                _pos++; // '{'
                var members = new List<CompositionMember>();
                while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
                {
                    if (Current.Kind != TokenKind.Identifier)
                    {
                        Error(
                            $"expected a composition member name but found '{Describe(Current)}'"
                        );
                        return false;
                    }

                    var name = Current.Text;
                    _pos++;
                    if (!Expect(TokenKind.Equals, "'=' after the composition member name"))
                    {
                        return false;
                    }

                    var path = ParseMemberPath(alias);
                    if (path is null || path.Count == 0)
                    {
                        return false;
                    }

                    members.Add(
                        new CompositionMember(name, alias, new EquatableArray<string>([.. path]))
                    );

                    if (Current.Kind == TokenKind.Comma)
                    {
                        _pos++;
                    }
                }

                if (!Expect(TokenKind.RightBrace, "'}' to close the composition"))
                {
                    return false;
                }

                composition = new FreeComposition(
                    new EquatableArray<CompositionMember>([.. members])
                );
                return true;
            }

            _pos++; // '{'
            while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
            {
                var member = ParseMemberRef(alias);
                if (member is null)
                {
                    return false;
                }

                projection.Add(member);

                if (Current.Kind == TokenKind.Comma)
                {
                    _pos++;
                }
            }

            return Expect(TokenKind.RightBrace, "'}' to close the projection");
        }

        // `select alias` → full entity.
        if (Current.Kind == TokenKind.Identifier)
        {
            var selected = Current.Text;
            _pos++;
            if (selected != alias)
            {
                _diagnostics.Add(
                    Located(DiagnosticDescriptors.UndeclaredAlias, _tokens[_pos - 1], selected)
                );
                return false;
            }

            return true;
        }

        Error(
            $"expected an alias or a '{{ … }}' projection after 'select' but found '{Describe(Current)}'"
        );
        return false;
    }

    // shape-block := '{' shape-node (','? shape-node)* '}' ; shape-node := IDENT | IDENT ':' shape-block
    // (009 US1; bare field/reference names — resolved against the node's entity by the validator/emitter).
    private List<ShapeNode>? ParseShapeBlock()
    {
        if (!Expect(TokenKind.LeftBrace, "'{' to open the shape"))
        {
            return null;
        }

        var nodes = new List<ShapeNode>();
        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                Error(
                    $"expected a field or reference name in the shape but found '{Describe(Current)}'"
                );
                return null;
            }

            var name = Current.Text;
            _pos++;

            if (Current.Kind == TokenKind.Colon)
            {
                _pos++; // ':'
                var children = ParseShapeBlock();
                if (children is null)
                {
                    return null;
                }

                nodes.Add(new ShapeNode(name, true, new EquatableArray<ShapeNode>([.. children])));
            }
            else
            {
                nodes.Add(new ShapeNode(name, false, new EquatableArray<ShapeNode>([])));
            }

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
        }

        return Expect(TokenKind.RightBrace, "'}' to close the shape") ? nodes : null;
    }

    // returning := alias | '{' member (','? member)* '}' | alias '.' member  (mirrors select + a scalar form)
    private ReturningShape? ParseReturning(string alias)
    {
        if (Current.Kind == TokenKind.LeftBrace)
        {
            _pos++; // '{'
            var members = new List<string>();
            while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
            {
                var member = ParseMemberRef(alias);
                if (member is null)
                {
                    return null;
                }

                members.Add(member);
                if (Current.Kind == TokenKind.Comma)
                {
                    _pos++;
                }
            }

            return Expect(TokenKind.RightBrace, "'}' to close the returning projection")
                ? new ReturningShape(
                    ReturningKind.Projection,
                    new EquatableArray<string>([.. members])
                )
                : null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error(
                $"expected an alias or a '{{ … }}' projection after 'returning' but found '{Describe(Current)}'"
            );
            return null;
        }

        // `returning alias.member` → scalar; `returning alias` → full entity.
        if (Peek().Kind == TokenKind.Dot)
        {
            var member = ParseMemberRef(alias);
            return member is null
                ? null
                : new ReturningShape(ReturningKind.Scalar, new EquatableArray<string>([member]));
        }

        var selected = Current.Text;
        _pos++;
        if (selected != alias)
        {
            _diagnostics.Add(
                Located(DiagnosticDescriptors.UndeclaredAlias, _tokens[_pos - 1], selected)
            );
            return null;
        }

        return new ReturningShape(ReturningKind.Entity, new EquatableArray<string>([]));
    }

    // '{' (member '=' value)* '}' — assignments are alias-qualified; '=' is assignment.
    private void ParseAssignmentBlock(string alias, List<Assignment> assignments)
    {
        if (!Expect(TokenKind.LeftBrace, "'{' to open the assignment block"))
        {
            return;
        }

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            // Removed 002 form: `column := value` (no alias, ':=' assignment).
            if (Current.Kind == TokenKind.Identifier && Peek().Kind == TokenKind.Assign)
            {
                Removed("':=' assignment was removed; write 'alias.column = value'");
                return;
            }

            var column = ParseMemberRef(alias);
            if (column is null)
            {
                return;
            }

            if (Current.Kind == TokenKind.Assign)
            {
                Removed("':=' assignment was removed; write 'alias.column = value'");
                return;
            }

            if (!Expect(TokenKind.Equals, "'=' between the member and its value"))
            {
                return;
            }

            var value = ParseValue();
            if (value is not null)
            {
                assignments.Add(new Assignment(column, value));
            }

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the assignment block");
    }

    // value := STRING | NUMBER | IDENT '::' IDENT '(' ')' | IDENT(param)
    private CommandValue? ParseValue()
    {
        switch (Current.Kind)
        {
            case TokenKind.String:
                var s = Current.Text;
                _pos++;
                return new CommandValue(CommandValueKind.StringLiteral, s);
            case TokenKind.Number:
                var n = Current.Text;
                _pos++;
                return new CommandValue(CommandValueKind.NumberLiteral, n);
            case TokenKind.Identifier when Peek().Kind == TokenKind.DoubleColon:
                _pos++; // namespace
                _pos++; // '::'
                if (Current.Kind != TokenKind.Identifier)
                {
                    Error("expected a native function name after '::'");
                    return null;
                }

                var func = Current.Text;
                _pos++;
                Expect(TokenKind.LeftParen, "'(' after the native function name");
                Expect(TokenKind.RightParen, "')' to close the native function call");
                return new CommandValue(CommandValueKind.NativeCall, func);
            case TokenKind.Identifier:
                var param = Current.Text;
                _pos++;
                // A bare identifier naming a `with` binding is a WithRef (FR-021); otherwise a parameter.
                return new CommandValue(
                    _bindingNames.Contains(param)
                        ? CommandValueKind.WithRef
                        : CommandValueKind.Parameter,
                    param
                );
            default:
                Error(
                    $"expected a value (parameter, literal, or native call) but found '{Describe(Current)}'"
                );
                return null;
        }
    }

    // Detects a removed 002 form appearing where a clause keyword was expected, emitting ORM020.
    private bool DetectRemovedClause()
    {
        if (IsKeyword("filter"))
        {
            Removed(
                "'filter' was removed; use 'where' with C#/TypeScript operators (== != < <= > >= &&)"
            );
            return true;
        }

        if (Current.Kind == TokenKind.Dot)
        {
            Removed(
                "leading-dot members ('.field') were removed; use alias-qualified members like 'u.field'"
            );
            return true;
        }

        return false;
    }

    private bool ExpectKeyword(string keyword)
    {
        if (IsKeyword(keyword))
        {
            _pos++;
            return true;
        }

        Error($"expected '{keyword}' but found '{Describe(Current)}'");
        return false;
    }

    private void RecoverToUnitEnd()
    {
        // Skip to the matching brace depth of the next unit boundary (or EOF). Balances braces so a stray
        // '}' inside a body doesn't terminate recovery early.
        var depth = 0;
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.LeftBrace)
            {
                depth++;
            }
            else if (Current.Kind == TokenKind.RightBrace)
            {
                _pos++;
                if (depth <= 1)
                {
                    return;
                }

                depth--;
                continue;
            }
            else if (
                depth == 0
                && (IsKeyword("query") || IsKeyword("mutation") || IsKeyword("command"))
            )
            {
                return;
            }

            _pos++;
        }
    }

    private bool Expect(TokenKind kind, string what)
    {
        if (Current.Kind == kind)
        {
            _pos++;
            return true;
        }

        Error($"expected {what} but found '{Describe(Current)}'");
        return false;
    }

    private void ClauseOrder(string clause, string canonical) =>
        _diagnostics.Add(
            new DiagnosticInfo(
                DiagnosticDescriptors.WrongClauseOrder,
                LocationOf(Current),
                new EquatableArray<string>([clause, canonical])
            )
        );

    private void Removed(string message) =>
        _diagnostics.Add(
            new DiagnosticInfo(
                DiagnosticDescriptors.RemovedSyntax,
                LocationOf(Current),
                new EquatableArray<string>([message])
            )
        );

    private void Error(string message) =>
        _diagnostics.Add(
            new DiagnosticInfo(
                DiagnosticDescriptors.SyntaxError,
                LocationOf(Current),
                new EquatableArray<string>([message])
            )
        );

    private DiagnosticInfo Located(
        Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor,
        Token token,
        params string[] args
    ) => new(descriptor, LocationOf(token), new EquatableArray<string>(args));

    private LocationInfo LocationOf(Token token) =>
        new(
            _filePath,
            new TextSpan(token.Start, token.Length == 0 ? 1 : token.Length),
            new LinePositionSpan(
                new LinePosition(token.Line, token.Column),
                new LinePosition(token.Line, token.Column + (token.Length == 0 ? 1 : token.Length))
            )
        );

    private static string Describe(Token token) =>
        token.Kind == TokenKind.EndOfFile ? "end of file" : token.Text;
}
