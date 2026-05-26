using System.Collections.Generic;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Result of parsing the commands in one DormantQL file (<c>.dql</c>).</summary>
/// <param name="ModuleName">The declared module name (or <c>Dormant.Generated</c> when omitted).</param>
/// <param name="Commands">The parsed commands.</param>
/// <param name="Diagnostics">Syntax diagnostics collected during parsing.</param>
internal readonly record struct CommandParseResult(
    string ModuleName,
    IReadOnlyList<CommandModel> Commands,
    IReadOnlyList<DiagnosticInfo> Diagnostics);

/// <summary>
/// Hand-written recursive-descent parser for the DormantQL v1 command MVP (FR-002): a module declaration and
/// <c>command Name(params) = insert Entity { col := expr, … };</c>. Values are parameters, literals, or
/// native calls (<c>datetime::now()</c>). Coexists with queries in the same file: <c>query</c> blocks are
/// skipped here (the query parser handles them). Emits located ORM001 diagnostics and recovers to the next
/// statement boundary. (update/delete + nested + `with` arrive in later slices.)
/// </summary>
internal sealed class CommandParser
{
    private readonly string _filePath;
    private readonly List<Token> _tokens;
    private readonly List<DiagnosticInfo> _diagnostics = [];
    private int _pos;

    private CommandParser(string filePath, List<Token> tokens)
    {
        _filePath = filePath;
        _tokens = tokens;
    }

    public static CommandParseResult Parse(string filePath, string text)
    {
        var parser = new CommandParser(filePath, Lexer.Tokenize(text));
        return parser.ParseFile();
    }

    private Token Current => _tokens[_pos];

    private bool IsKeyword(string keyword) =>
        Current.Kind == TokenKind.Identifier && Current.Text == keyword;

    private CommandParseResult ParseFile()
    {
        var moduleName = "Dormant.Generated";
        var commands = new List<CommandModel>();

        if (IsKeyword("module"))
        {
            _pos++;
            if (Current.Kind == TokenKind.Identifier)
            {
                moduleName = Current.Text;
                _pos++;
            }

            Expect(TokenKind.Semicolon, "';' after the module declaration");
        }

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (IsKeyword("command"))
            {
                var command = ParseCommand();
                if (command is not null)
                {
                    commands.Add(command);
                }
            }
            else if (IsKeyword("query"))
            {
                RecoverToStatementEnd(); // queries are handled by the query parser
            }
            else
            {
                Error($"unexpected '{Describe(Current)}'; expected 'command' or 'query'");
                RecoverToStatementEnd();
            }
        }

        return new CommandParseResult(moduleName, commands, _diagnostics);
    }

    // command := 'command' IDENT '(' params? ')' '=' 'insert' IDENT '{' assignments '}' ';'
    private CommandModel? ParseCommand()
    {
        _pos++; // 'command'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a command name after 'command'");
            RecoverToStatementEnd();
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.LeftParen, "'(' to open the parameter list"))
        {
            RecoverToStatementEnd();
            return null;
        }

        var parameters = ParseParameters();

        if (!Expect(TokenKind.Equals, "'=' before the command body"))
        {
            RecoverToStatementEnd();
            return null;
        }

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
            RecoverToStatementEnd();
            return null;
        }

        _pos++; // kind keyword
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name");
            RecoverToStatementEnd();
            return null;
        }

        var entity = Current.Text;
        _pos++;

        var assignments = new List<Assignment>();
        var filters = new List<FilterCondition>();
        if (kind == CommandKind.Insert)
        {
            assignments = ParseAssignments();
        }
        else
        {
            filters = ParseFilters();
            if (kind == CommandKind.Update)
            {
                ExpectKeyword("set");
                assignments = ParseAssignments();
            }
        }

        Expect(TokenKind.Semicolon, "';' to close the command");

        return new CommandModel(
            name,
            kind,
            entity,
            new EquatableArray<QueryParameter>([.. parameters]),
            new EquatableArray<Assignment>([.. assignments]),
            new EquatableArray<FilterCondition>([.. filters]));
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

    // '{' (IDENT ':=' value (',' IDENT ':=' value)*)? '}'
    private List<Assignment> ParseAssignments()
    {
        var assignments = new List<Assignment>();
        if (!Expect(TokenKind.LeftBrace, "'{' to open the command body"))
        {
            return assignments;
        }

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a column name in the command body");
                break;
            }

            var column = Current.Text;
            _pos++;
            if (!Expect(TokenKind.Assign, "':=' between the column and its value"))
            {
                break;
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

        Expect(TokenKind.RightBrace, "'}' to close the command body");
        return assignments;
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
                return new CommandValue(CommandValueKind.Parameter, param);
            default:
                Error($"expected a value (parameter, literal, or native call) but found '{Describe(Current)}'");
                return null;
        }
    }

    // filter := 'filter' cond ('and' cond)* ; cond := '.' IDENT op IDENT
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
            if (!Expect(TokenKind.Dot, "'.' before a column reference"))
            {
                break;
            }

            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a column name after '.'");
                break;
            }

            var column = Current.Text;
            _pos++;

            var op = ParseOperator();
            if (op is null)
            {
                break;
            }

            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a parameter on the right side of the comparison");
                break;
            }

            var param = Current.Text;
            _pos++;
            conditions.Add(new FilterCondition(column, op.Value, param));

            if (IsKeyword("and"))
            {
                _pos++;
                continue;
            }

            break;
        }

        return conditions;
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

    private Token Peek() => _tokens[System.Math.Min(_pos + 1, _tokens.Count - 1)];

    private void RecoverToStatementEnd()
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
