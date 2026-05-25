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
/// Hand-written recursive-descent parser for the DormantQL v1 schema grammar (FR-047): a module
/// declaration and entity declarations whose members use a unified <c>name: [multi] Type[?]</c> form.
/// A member typed as a known value type is a property; otherwise it is a link. Members are required by
/// default; a trailing <c>?</c> makes them optional. Emits located diagnostics (ORM001 syntax, ORM003
/// unknown type) and recovers to the next member boundary.
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
            if (Current.Kind == TokenKind.Identifier)
            {
                ParseMember(properties, links);
            }
            else
            {
                Error($"unexpected '{Describe(Current)}' in entity body");
                RecoverToMemberEnd();
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the entity body");
        return new EntityModel(
            name,
            new EquatableArray<PropertyModel>([.. properties]),
            new EquatableArray<LinkModel>([.. links]));
    }

    // member := name ':' ['multi'] Type ['?'] modifier* ';'
    private void ParseMember(List<PropertyModel> properties, List<LinkModel> links)
    {
        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.Colon, "':' between the member name and its type"))
        {
            RecoverToMemberEnd();
            return;
        }

        var isMulti = false;
        if (IsKeyword("multi"))
        {
            isMulti = true;
            _pos++;
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a type or target entity");
            RecoverToMemberEnd();
            return;
        }

        var typeToken = Current;
        var typeName = typeToken.Text;
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

        Expect(TokenKind.Semicolon, "';' after the member declaration");

        // A multi member is always a link. Otherwise: a known value type is a property; a PascalCase
        // name is a single link (validated against the entity set → ORM002 if undefined); a lowercase
        // unknown name is most likely a mistyped value type (ORM003), by the convention that value
        // types are lowercase and entities are PascalCase.
        if (!isMulti && TypeMap.TryMap(typeName, out var clrType))
        {
            properties.Add(new PropertyModel(name, typeName, clrType, isNullable, isPrimary, isConcurrency));
            return;
        }

        if (!isMulti && typeName.Length > 0 && char.IsLower(typeName[0]))
        {
            _diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnknownPropertyType,
                LocationOf(typeToken),
                new EquatableArray<string>([name, typeName])));
            return;
        }

        links.Add(new LinkModel(
            name,
            typeName,
            isMulti,
            IsRequired: !isMulti && !isNullable,
            LocationOf(typeToken)));
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
