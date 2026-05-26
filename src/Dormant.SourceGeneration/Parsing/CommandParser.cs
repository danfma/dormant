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

        // v1 MVP: only `insert` is implemented; update/delete are a later slice.
        if (!IsKeyword("insert"))
        {
            Error($"expected 'insert' (update/delete commands are a later slice) but found '{Describe(Current)}'");
            RecoverToStatementEnd();
            return null;
        }

        _pos++; // 'insert'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name after 'insert'");
            RecoverToStatementEnd();
            return null;
        }

        var entity = Current.Text;
        _pos++;

        var assignments = ParseAssignments();
        Expect(TokenKind.Semicolon, "';' to close the command");

        return new CommandModel(
            name,
            CommandKind.Insert,
            entity,
            new EquatableArray<QueryParameter>([.. parameters]),
            new EquatableArray<Assignment>([.. assignments]));
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
