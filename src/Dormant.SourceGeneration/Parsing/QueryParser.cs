using System.Collections.Generic;
using System.Globalization;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Result of parsing one DormantQL query file (<c>.dql</c>).</summary>
/// <param name="ModuleName">The declared module name (or <c>Dormant.Generated</c> when omitted).</param>
/// <param name="Queries">The parsed queries.</param>
/// <param name="Diagnostics">Syntax diagnostics collected during parsing.</param>
internal readonly record struct QueryParseResult(
    string ModuleName,
    IReadOnlyList<QueryModel> Queries,
    IReadOnlyList<DiagnosticInfo> Diagnostics);

/// <summary>
/// Hand-written recursive-descent parser for the DormantQL v1 query MVP (FR-006/FR-013): a module
/// declaration and <c>query Name(params) = select Entity [{ fields }] [filter …] [order by …]
/// [limit …] [offset …];</c>. Filters are conjunctive own-column comparisons; path navigation, nested
/// shapes, optional parameters, and DML are later slices. Emits located ORM001 diagnostics and recovers
/// to the next query boundary.
/// </summary>
internal sealed class QueryParser
{
    private readonly string _filePath;
    private readonly List<Token> _tokens;
    private readonly List<DiagnosticInfo> _diagnostics = [];
    private int _pos;

    private QueryParser(string filePath, List<Token> tokens)
    {
        _filePath = filePath;
        _tokens = tokens;
    }

    public static QueryParseResult Parse(string filePath, string text)
    {
        var parser = new QueryParser(filePath, Lexer.Tokenize(text));
        return parser.ParseFile();
    }

    private Token Current => _tokens[_pos];

    private bool IsKeyword(string keyword) =>
        Current.Kind == TokenKind.Identifier && Current.Text == keyword;

    private QueryParseResult ParseFile()
    {
        var moduleName = "Dormant.Generated";
        var queries = new List<QueryModel>();

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
            else
            {
                Error($"unexpected '{Describe(Current)}'; expected 'query'");
                RecoverToQueryEnd();
            }
        }

        return new QueryParseResult(moduleName, queries, _diagnostics);
    }

    // query := 'query' IDENT '(' params? ')' '=' 'select' IDENT projection? clauses ';'
    private QueryModel? ParseQuery()
    {
        _pos++; // 'query'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a query name after 'query'");
            RecoverToQueryEnd();
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.LeftParen, "'(' to open the parameter list"))
        {
            RecoverToQueryEnd();
            return null;
        }

        var parameters = ParseParameters();

        if (!Expect(TokenKind.Equals, "'=' before the query body") ||
            !ExpectKeyword("select"))
        {
            RecoverToQueryEnd();
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name after 'select'");
            RecoverToQueryEnd();
            return null;
        }

        var rootEntity = Current.Text;
        _pos++;

        var projection = ParseProjection();
        var filters = ParseFilters();
        var orderBy = ParseOrderBy();
        var limit = ParseLimitClause("limit");
        var offset = ParseLimitClause("offset");

        Expect(TokenKind.Semicolon, "';' to close the query");

        return new QueryModel(
            name,
            rootEntity,
            new EquatableArray<QueryParameter>([.. parameters]),
            new EquatableArray<string>([.. projection]),
            new EquatableArray<FilterCondition>([.. filters]),
            new EquatableArray<OrderTerm>([.. orderBy]),
            limit,
            offset);
    }

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

            parameters.Add(new QueryParameter(paramName, dslType, clrType));

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
        }

        Expect(TokenKind.RightParen, "')' to close the parameter list");
        return parameters;
    }

    // projection := '{' IDENT (',' IDENT)* '}'   (absent ⇒ full entity)
    private List<string> ParseProjection()
    {
        var fields = new List<string>();
        if (Current.Kind != TokenKind.LeftBrace)
        {
            return fields;
        }

        _pos++; // '{'
        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a field name in the projection");
                break;
            }

            fields.Add(Current.Text);
            _pos++;

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the projection");
        return fields;
    }

    // filterClause := 'filter' cond ('and' cond)*  ; cond := '.' IDENT op IDENT
    private List<FilterCondition> ParseFilters()
    {
        var conditions = new List<FilterCondition>();
        if (!IsKeyword("filter"))
        {
            return conditions;
        }

        _pos++; // 'filter'
        while (true)
        {
            var condition = ParseCondition();
            if (condition is not null)
            {
                conditions.Add(condition);
            }

            if (IsKeyword("and"))
            {
                _pos++;
                continue;
            }

            break;
        }

        return conditions;
    }

    private FilterCondition? ParseCondition()
    {
        var column = ParseColumnPath();
        if (column is null)
        {
            return null;
        }

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
        return new FilterCondition(column, op.Value, paramName);
    }

    // A column reference: '.' IDENT. Path navigation ('.a.b') is rejected in the MVP.
    private string? ParseColumnPath()
    {
        if (!Expect(TokenKind.Dot, "'.' before a column reference"))
        {
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a column name after '.'");
            return null;
        }

        var column = Current.Text;
        _pos++;

        if (Current.Kind == TokenKind.Dot)
        {
            Error("path navigation across references is not supported in v1; filter on a column of the selected entity");
            while (Current.Kind == TokenKind.Dot)
            {
                _pos++;
                if (Current.Kind == TokenKind.Identifier)
                {
                    _pos++;
                }
            }
        }

        return column;
    }

    private CompareOp? ParseOperator()
    {
        switch (Current.Kind)
        {
            case TokenKind.Equals: _pos++; return CompareOp.Eq;
            case TokenKind.LeftAngle: _pos++; return CompareOp.Lt;
            case TokenKind.RightAngle: _pos++; return CompareOp.Gt;
            case TokenKind.LessEqual: _pos++; return CompareOp.Le;
            case TokenKind.GreaterEqual: _pos++; return CompareOp.Ge;
            case TokenKind.Identifier when Current.Text == "like": _pos++; return CompareOp.Like;
            case TokenKind.Identifier when Current.Text == "ilike": _pos++; return CompareOp.ILike;
            default:
                Error($"expected a comparison operator but found '{Describe(Current)}'");
                return null;
        }
    }

    // orderClause := 'order' 'by' term (',' term)* ; term := '.' IDENT ('asc'|'desc')?
    private List<OrderTerm> ParseOrderBy()
    {
        var terms = new List<OrderTerm>();
        if (!IsKeyword("order"))
        {
            return terms;
        }

        _pos++; // 'order'
        if (!ExpectKeyword("by"))
        {
            return terms;
        }

        while (true)
        {
            var column = ParseColumnPath();
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

        return terms;
    }

    // limitClause := keyword (NUMBER | IDENT)
    private LimitValue? ParseLimitClause(string keyword)
    {
        if (!IsKeyword(keyword))
        {
            return null;
        }

        _pos++; // keyword
        if (Current.Kind == TokenKind.Number)
        {
            var text = Current.Text;
            _pos++;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? new LimitValue(IsParameter: false, ParameterName: string.Empty, Literal: value)
                : Fail($"invalid integer literal '{text}' after '{keyword}'");
        }

        if (Current.Kind == TokenKind.Identifier)
        {
            var paramName = Current.Text;
            _pos++;
            return new LimitValue(IsParameter: true, paramName, Literal: 0);
        }

        Error($"expected an integer or parameter after '{keyword}'");
        return null;
    }

    private LimitValue? Fail(string message)
    {
        Error(message);
        return null;
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

    private void RecoverToQueryEnd()
    {
        while (Current.Kind is not (TokenKind.Semicolon or TokenKind.EndOfFile))
        {
            _pos++;
        }

        if (Current.Kind == TokenKind.Semicolon)
        {
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

    private void Error(string message) =>
        _diagnostics.Add(new DiagnosticInfo(
            DiagnosticDescriptors.SyntaxError,
            LocationOf(Current),
            new EquatableArray<string>([message])));

    private LocationInfo LocationOf(Token token) => new(
        _filePath,
        new TextSpan(token.Start, token.Length == 0 ? 1 : token.Length),
        new LinePositionSpan(
            new LinePosition(token.Line, token.Column),
            new LinePosition(token.Line, token.Column + (token.Length == 0 ? 1 : token.Length))));

    private static string Describe(Token token) =>
        token.Kind == TokenKind.EndOfFile ? "end of file" : token.Text;
}
