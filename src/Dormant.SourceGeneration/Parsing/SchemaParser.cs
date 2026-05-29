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
    IReadOnlyList<DiagnosticInfo> Diagnostics
);

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

        // Optional explicit table name: `entity RecentPost db("recent_post") { … }` (FR-054).
        var nameOverride = TryParseDbOverride();

        if (!Expect(TokenKind.LeftBrace, "'{' to open the entity body"))
        {
            return null;
        }

        var properties = new List<PropertyModel>();
        var references = new List<ReferenceModel>();
        var entityConstraints = new List<ConstraintModel>();
        var entityAnnotations = new List<AnnotationModel>();

        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            // Entity-level constraint/annotation: `constraint …;` / `annotation …;` (not a member,
            // which would be `name :`). Distinguish by the following token not being a colon.
            if (IsKeyword("constraint") && Peek().Kind != TokenKind.Colon)
            {
                ParseConstraintStatement(entityConstraints, entityLevel: true);
            }
            else if (IsKeyword("annotation") && Peek().Kind != TokenKind.Colon)
            {
                ParseAnnotationStatement(entityAnnotations);
            }
            else if (Current.Kind == TokenKind.Identifier)
            {
                ParseMember(properties, references);
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
            new EquatableArray<ReferenceModel>([.. references]),
            nameOverride,
            IsAbstract: false,
            Extends: default,
            Constraints: new EquatableArray<ConstraintModel>([.. entityConstraints]),
            Annotations: new EquatableArray<AnnotationModel>([.. entityAnnotations])
        );
    }

    // Optional `db("name")` override (FR-054); returns the literal name or null when absent.
    private string? TryParseDbOverride()
    {
        if (
            !(
                Current.Kind == TokenKind.Identifier
                && Current.Text == "db"
                && Peek().Kind == TokenKind.LeftParen
            )
        )
        {
            return null;
        }

        _pos++; // 'db'
        _pos++; // '('
        string? name = null;
        if (Current.Kind == TokenKind.String)
        {
            name = Current.Text;
            _pos++;
        }
        else
        {
            Error("expected a quoted database name inside db(\"…\")");
        }

        Expect(TokenKind.RightParen, "')' to close the db(\"…\") override");
        return name;
    }

    // member  := name ':' typeExpr modifier* ';'
    // typeExpr := ('Set'|'List'|'Bag') '<' Target '>' | 'Map' '<' Key ',' Target '>' | Type ['?']
    private void ParseMember(List<PropertyModel> properties, List<ReferenceModel> references)
    {
        var name = Current.Text;
        _pos++;

        if (!Expect(TokenKind.Colon, "':' between the member name and its type"))
        {
            RecoverToMemberEnd();
            return;
        }

        // Collection reference: Set/List/Bag/Map '<' ... '>'
        if (
            Current.Kind == TokenKind.Identifier
            && TryCollectionKind(Current.Text, out var collectionKind)
            && Peek().Kind == TokenKind.LeftAngle
        )
        {
            ParseCollectionReference(name, collectionKind, references);
            return;
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

        // Terminator: a `{ constraint…; annotation…; }` block, a bare ';', or a removed legacy modifier.
        var constraints = new List<ConstraintModel>();
        var annotations = new List<AnnotationModel>();
        if (Current.Kind == TokenKind.LeftBrace)
        {
            ParseMemberBlock(constraints, annotations);
        }
        else if (IsLegacyModifier(out var legacy))
        {
            var suggestion =
                legacy == "db" ? "{ annotation column(\"…\"); }" : $"{{ constraint {legacy}; }}";
            _diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.RemovedModifierSyntax,
                    LocationOf(Current),
                    new EquatableArray<string>([legacy, suggestion])
                )
            );
            RecoverToMemberEnd();
            return;
        }
        else
        {
            Expect(TokenKind.Semicolon, "';' or a '{ … }' constraint block after the member");
        }

        // Derive identity/concurrency flags + resolved column name from the block.
        var isPrimary = false;
        var isConcurrency = false;
        foreach (var c in constraints)
        {
            if (c.Kind == ConstraintKind.Primary)
            {
                isPrimary = true;
            }
            else if (c.Kind == ConstraintKind.Concurrency)
            {
                isConcurrency = true;
            }
        }

        var nameOverride = ColumnNameFrom(annotations);

        // Known value type → property; lowercase unknown → likely a mistyped value type (ORM003);
        // PascalCase → single reference (validated against the entity set → ORM002 if undefined).
        if (TypeMap.TryMap(typeName, out var clrType))
        {
            properties.Add(
                new PropertyModel(
                    name,
                    typeName,
                    clrType,
                    isNullable,
                    isPrimary,
                    isConcurrency,
                    nameOverride,
                    new EquatableArray<ConstraintModel>([.. constraints]),
                    new EquatableArray<AnnotationModel>([.. annotations])
                )
            );
            return;
        }

        if (typeName.Length > 0 && char.IsLower(typeName[0]))
        {
            _diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.UnknownPropertyType,
                    LocationOf(typeToken),
                    new EquatableArray<string>([name, typeName])
                )
            );
            return;
        }

        // Reference: constraints/annotations on references are out of scope in v1 (FR-016 → ORM036).
        if (constraints.Count > 0 || annotations.Count > 0)
        {
            _diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidAnnotationOrTarget,
                    LocationOf(typeToken),
                    new EquatableArray<string>([
                        $"constraints and annotations are not supported on reference member '{name}' (v1)",
                    ])
                )
            );
        }

        references.Add(
            new ReferenceModel(
                name,
                typeName,
                ReferenceKind.Ref,
                KeyType: null,
                IsRequired: !isNullable,
                LocationOf(typeToken)
            )
        );
    }

    private void ParseCollectionReference(
        string name,
        ReferenceKind kind,
        List<ReferenceModel> references
    )
    {
        _pos++; // collection-kind identifier
        Expect(TokenKind.LeftAngle, "'<' after the collection kind");

        string? keyType = null;
        if (kind == ReferenceKind.Map)
        {
            if (Current.Kind != TokenKind.Identifier)
            {
                Error("expected a map key type");
                RecoverToMemberEnd();
                return;
            }

            keyType = Current.Text;
            _pos++;
            Expect(TokenKind.Comma, "',' between the map key and value types");
        }

        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a target entity");
            RecoverToMemberEnd();
            return;
        }

        var targetToken = Current;
        _pos++;

        Expect(TokenKind.RightAngle, "'>' to close the collection type");
        Expect(TokenKind.Semicolon, "';' after the member declaration");

        references.Add(
            new ReferenceModel(
                name,
                targetToken.Text,
                kind,
                keyType,
                IsRequired: false,
                LocationOf(targetToken)
            )
        );
    }

    // A removed legacy member modifier (Feature 012 clean break, FR-012): `primary`, `concurrency`,
    // or trailing `db("…")`. Detected so we can emit a migration diagnostic (ORM035).
    private bool IsLegacyModifier(out string modifier)
    {
        if (
            Current.Kind == TokenKind.Identifier
            && Current.Text is "primary" or "concurrency" or "db"
        )
        {
            modifier = Current.Text;
            return true;
        }

        modifier = string.Empty;
        return false;
    }

    // The DB column name carried by a `column("…")` annotation, if present.
    private static string? ColumnNameFrom(List<AnnotationModel> annotations)
    {
        foreach (var a in annotations)
        {
            if (a.Name == "column" && a.Args.Count > 0)
            {
                return a.Args[0].Value;
            }
        }

        return null;
    }

    // member block := '{' (constraint_stmt | annotation_stmt)* '}'
    private void ParseMemberBlock(
        List<ConstraintModel> constraints,
        List<AnnotationModel> annotations
    )
    {
        Expect(TokenKind.LeftBrace, "'{' to open the member block");
        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (IsKeyword("constraint"))
            {
                ParseConstraintStatement(constraints, entityLevel: false);
            }
            else if (IsKeyword("annotation"))
            {
                ParseAnnotationStatement(annotations);
            }
            else
            {
                Error($"unexpected '{Describe(Current)}'; expected 'constraint' or 'annotation'");
                RecoverToStatementEnd();
            }
        }

        Expect(TokenKind.RightBrace, "'}' to close the member block");
    }

    // constraint_stmt := 'constraint' name [ '(' args ')' ] [ 'on' '(' members ')' ] [ 'as' name ] ';'
    private void ParseConstraintStatement(List<ConstraintModel> constraints, bool entityLevel)
    {
        _pos++; // 'constraint'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected a constraint name after 'constraint'");
            RecoverToStatementEnd();
            return;
        }

        var nameToken = Current;
        var cname = Current.Text;
        _pos++;

        var args = new List<ConstraintArg>();
        string? checkExpr = null;
        if (Current.Kind == TokenKind.LeftParen)
        {
            if (cname == "check")
            {
                checkExpr = ParseParenthesizedExpressionText();
            }
            else
            {
                ParseArgList(args);
            }
        }

        var targets = new List<string>();
        if (IsKeyword("on"))
        {
            _pos++; // 'on'
            ParseOnTargets(targets);
        }

        string? sqlName = null;
        if (IsKeyword("as"))
        {
            _pos++; // 'as'
            if (Current.Kind is TokenKind.Identifier or TokenKind.String)
            {
                sqlName = Current.Text;
                _pos++;
            }
            else
            {
                Error("expected a constraint name after 'as'");
            }
        }

        Expect(TokenKind.Semicolon, "';' after the constraint");

        if (!TryMapConstraintKind(cname, out var kind))
        {
            _diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.UnknownConstraint,
                    LocationOf(nameToken),
                    new EquatableArray<string>([cname])
                )
            );
            return;
        }

        constraints.Add(
            new ConstraintModel(
                kind,
                new EquatableArray<ConstraintArg>([.. args]),
                new EquatableArray<string>([.. targets]),
                checkExpr,
                sqlName,
                LocationOf(nameToken)
            )
        );
    }

    // annotation_stmt := 'annotation' name [ '(' args ')' ] ';'
    private void ParseAnnotationStatement(List<AnnotationModel> annotations)
    {
        _pos++; // 'annotation'
        if (Current.Kind != TokenKind.Identifier)
        {
            Error("expected an annotation name after 'annotation'");
            RecoverToStatementEnd();
            return;
        }

        var nameToken = Current;
        var aname = Current.Text;
        _pos++;

        var args = new List<ConstraintArg>();
        if (Current.Kind == TokenKind.LeftParen)
        {
            ParseArgList(args);
        }

        Expect(TokenKind.Semicolon, "';' after the annotation");

        annotations.Add(
            new AnnotationModel(
                aname,
                new EquatableArray<ConstraintArg>([.. args]),
                LocationOf(nameToken)
            )
        );
    }

    // arg_list := '(' arg (',' arg)* ')' ;  arg := literal | name '=' literal
    private void ParseArgList(List<ConstraintArg> args)
    {
        Expect(TokenKind.LeftParen, "'(' to open the argument list");
        while (Current.Kind != TokenKind.RightParen && Current.Kind != TokenKind.EndOfFile)
        {
            string? argName = null;
            if (Current.Kind == TokenKind.Identifier && Peek().Kind == TokenKind.Equals)
            {
                argName = Current.Text;
                _pos += 2; // name '='
            }

            if (Current.Kind is TokenKind.String or TokenKind.Number or TokenKind.Identifier)
            {
                args.Add(new ConstraintArg(argName, Current.Text));
                _pos++;
            }
            else
            {
                Error("expected an argument value");
                break;
            }

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
            else
            {
                break;
            }
        }

        Expect(TokenKind.RightParen, "')' to close the argument list");
    }

    // on_targets := '(' member (',' member)* ')'
    private void ParseOnTargets(List<string> targets)
    {
        Expect(TokenKind.LeftParen, "'(' after 'on'");
        while (Current.Kind != TokenKind.RightParen && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.Identifier)
            {
                targets.Add(Current.Text);
                _pos++;
            }
            else
            {
                Error("expected a member name in 'on (…)'");
                break;
            }

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
            }
            else
            {
                break;
            }
        }

        Expect(TokenKind.RightParen, "')' to close 'on (…)'");
    }

    // Captures the raw text of a parenthesized boolean expression for `constraint check (…)`,
    // by joining the inner token texts. Full expression lowering to DDL happens in a later phase.
    private string ParseParenthesizedExpressionText()
    {
        Expect(TokenKind.LeftParen, "'(' after 'check'");
        var parts = new List<string>();
        var depth = 1;
        while (depth > 0 && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.LeftParen)
            {
                depth++;
            }
            else if (Current.Kind == TokenKind.RightParen)
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }

            parts.Add(Current.Text);
            _pos++;
        }

        Expect(TokenKind.RightParen, "')' to close the check expression");
        return string.Join(" ", parts);
    }

    private static bool TryMapConstraintKind(string name, out ConstraintKind kind)
    {
        switch (name)
        {
            case "primary":
                kind = ConstraintKind.Primary;
                return true;
            case "concurrency":
                kind = ConstraintKind.Concurrency;
                return true;
            case "unique":
                kind = ConstraintKind.Unique;
                return true;
            case "check":
                kind = ConstraintKind.Check;
                return true;
            case "one_of":
                kind = ConstraintKind.OneOf;
                return true;
            case "max":
                kind = ConstraintKind.Max;
                return true;
            case "min":
                kind = ConstraintKind.Min;
                return true;
            case "max_exclusive":
                kind = ConstraintKind.MaxExclusive;
                return true;
            case "min_exclusive":
                kind = ConstraintKind.MinExclusive;
                return true;
            case "max_length":
                kind = ConstraintKind.MaxLength;
                return true;
            case "min_length":
                kind = ConstraintKind.MinLength;
                return true;
            case "length":
                kind = ConstraintKind.Length;
                return true;
            case "range":
                kind = ConstraintKind.Range;
                return true;
            case "regex":
                kind = ConstraintKind.Regex;
                return true;
            default:
                kind = ConstraintKind.Unique;
                return false;
        }
    }

    private void RecoverToStatementEnd()
    {
        while (
            Current.Kind is not (TokenKind.Semicolon or TokenKind.RightBrace or TokenKind.EndOfFile)
        )
        {
            _pos++;
        }

        if (Current.Kind == TokenKind.Semicolon)
        {
            _pos++;
        }
    }

    private static bool TryCollectionKind(string text, out ReferenceKind kind)
    {
        switch (text)
        {
            case "Set":
                kind = ReferenceKind.Set;
                return true;
            case "List":
                kind = ReferenceKind.List;
                return true;
            case "Bag":
                kind = ReferenceKind.Bag;
                return true;
            case "Map":
                kind = ReferenceKind.Map;
                return true;
            default:
                kind = ReferenceKind.Ref;
                return false;
        }
    }

    private Token Peek() => _tokens[System.Math.Min(_pos + 1, _tokens.Count - 1)];

    private void RecoverToMemberEnd()
    {
        while (
            Current.Kind is not (TokenKind.Semicolon or TokenKind.RightBrace or TokenKind.EndOfFile)
        )
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
        _diagnostics.Add(
            new DiagnosticInfo(
                DiagnosticDescriptors.SyntaxError,
                LocationOf(Current),
                new EquatableArray<string>([message])
            )
        );

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
