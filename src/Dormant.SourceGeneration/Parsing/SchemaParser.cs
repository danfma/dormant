using System.Collections.Generic;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Result of parsing one DormantQL schema file.</summary>
/// <param name="ModuleName">The declared module name (or <c>Dormant.Generated</c> when omitted).</param>
/// <param name="Entities">The parsed entities.</param>
/// <param name="Diagnostics">Syntax/type diagnostics collected during parsing.</param>
internal readonly record struct ParseResult(
    string ModuleName,
    IReadOnlyList<EntityModel> Entities,
    IReadOnlyList<DiagnosticInfo> Diagnostics);

/// <summary>
/// Hand-written recursive-descent parser for the DormantQL v1 schema grammar: a module declaration
/// and entity declarations containing value properties and single/multi links. Emits located
/// diagnostics (ORM001 syntax, ORM003 unknown type) and recovers to the next member boundary.
/// </summary>
internal sealed class SchemaParser
{
    private readonly string _filePath;
    private readonly List<Token> _tokens;
    private readonly List<DiagnosticInfo> _diagnostics = [];
    private int _pos;

    private SchemaParser(string filePath, List<Token> tokens)
    {
        _filePath = filePath;
        _tokens = tokens;
    }

    public static ParseResult Parse(string filePath, string text)
    {
        var parser = new SchemaParser(filePath, Lexer.Tokenize(text));
        return parser.ParseSchema();
    }

    private Token Current => _tokens[_pos];

    private bool IsKeyword(string keyword) =>
        Current.Kind == TokenKind.Identifier && Current.Text == keyword;

    private ParseResult ParseSchema()
    {
        var moduleName = "Dormant.Generated";
        var entities = new List<EntityModel>();

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
            if (IsKeyword("entity"))
            {
                var entity = ParseEntity();
                if (entity is not null)
                {
                    entities.Add(entity);
                }
            }
            else
            {
                Error($"unexpected '{Describe(Current)}'; expected 'entity'");
                _pos++;
            }
        }

        return new ParseResult(moduleName, entities, _diagnostics);
    }

    private EntityModel? ParseEntity()
    {
        _pos++; // 'entity'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an entity name after 'entity'");
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.LeftBrace, "'{' to open the entity body"))
        {
            return null;
        }

        var properties = new List<PropertyModel>();
        var links = new List<LinkModel>();

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (IsKeyword("single") || IsKeyword("multi"))
            {
                var link = ParseLink();
                if (link is not null)
                {
                    links.Add(link);
                }
            }
            else if (Current.Kind == TokenKind.Identifier)
            {
                var property = ParseProperty();
                if (property is not null)
                {
                    properties.Add(property);
                }
            }
            else
            {
                Error($"unexpected '{Describe(Current)}' in entity body");
                RecoverToMemberEnd();
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the entity body");
        return new EntityModel(name, new EquatableArray<PropertyModel>([.. properties]), new EquatableArray<LinkModel>([.. links]));
    }

    private LinkModel? ParseLink()
    {
        var isMulti = Current.Text == "multi";
        _pos++; // 'single' | 'multi'

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a link name");
            RecoverToMemberEnd();
            return null;
        }

        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.Arrow, "'->' between the link name and its target entity"))
        {
            RecoverToMemberEnd();
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a target entity name after '->'");
            RecoverToMemberEnd();
            return null;
        }

        var target = Current.Text;
        var targetLocation = LocationOf(Current);
        _pos++;
        Expect(TokenKind.Semicolon, "';' after the link declaration");
        return new LinkModel(name, target, isMulti, targetLocation);
    }

    private PropertyModel? ParseProperty()
    {
        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.Colon, "':' between the property name and its type"))
        {
            RecoverToMemberEnd();
            return null;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a property type");
            RecoverToMemberEnd();
            return null;
        }

        var typeToken = Current;
        var dslType = typeToken.Text;
        _pos++;

        var isNullable = false;
        if (Current.Kind == TokenKind.Question)
        {
            isNullable = true;
            _pos++;
        }

        var isPrimary = false;
        var isConcurrency = false;
        while (Current.Kind == TokenKind.Identifier)
        {
            if (Current.Text == "primary")
            {
                isPrimary = true;
            }
            else if (Current.Text == "concurrency")
            {
                isConcurrency = true;
            }
            else
            {
                Error($"unexpected modifier '{Current.Text}'; expected 'primary' or 'concurrency'");
            }

            _pos++;
        }

        Expect(TokenKind.Semicolon, "';' after the property declaration");

        var clrType = string.Empty;
        if (!TypeMap.TryMap(dslType, out var mapped))
        {
            _diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnknownPropertyType,
                LocationOf(typeToken),
                new EquatableArray<string>([name, dslType])));
        }
        else
        {
            clrType = mapped;
        }

        return new PropertyModel(name, dslType, clrType, isNullable, isPrimary, isConcurrency);
    }

    private void RecoverToMemberEnd()
    {
        while (Current.Kind is not (TokenKind.Semicolon or TokenKind.RightBrace or TokenKind.EndOfFile))
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
