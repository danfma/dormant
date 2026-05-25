using System.Collections.Generic;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Token kinds produced by the DormantQL <see cref="Lexer"/>.</summary>
internal enum TokenKind
{
    Identifier,
    Number,
    Colon,
    Semicolon,
    LeftBrace,
    RightBrace,
    LeftAngle,
    RightAngle,
    LessEqual,
    GreaterEqual,
    Equals,
    LeftParen,
    RightParen,
    Dot,
    Comma,
    Arrow,
    Question,
    EndOfFile,
    Unknown,
}

/// <summary>A lexical token with its source position (zero-based line/column).</summary>
internal readonly record struct Token(
    TokenKind Kind,
    string Text,
    int Start,
    int Length,
    int Line,
    int Column);

/// <summary>
/// Hand-written DormantQL lexer. Recognizes identifiers, punctuation (<c>: ; { } -> ?</c>), skips
/// whitespace and <c>#</c> line comments, and tracks positions for located diagnostics (research §5).
/// </summary>
internal static class Lexer
{
    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        var line = 0;
        var column = 0;

        void Advance(int n = 1)
        {
            for (var k = 0; k < n; k++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 0;
                }
                else
                {
                    column++;
                }

                i++;
            }
        }

        while (i < text.Length)
        {
            var c = text[i];

            if (c is ' ' or '\t' or '\r' or '\n')
            {
                Advance();
                continue;
            }

            if (c == '#')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    Advance();
                }

                continue;
            }

            var startLine = line;
            var startColumn = column;
            var start = i;

            switch (c)
            {
                case ':':
                    Advance();
                    tokens.Add(new Token(TokenKind.Colon, ":", start, 1, startLine, startColumn));
                    continue;
                case ';':
                    Advance();
                    tokens.Add(new Token(TokenKind.Semicolon, ";", start, 1, startLine, startColumn));
                    continue;
                case '{':
                    Advance();
                    tokens.Add(new Token(TokenKind.LeftBrace, "{", start, 1, startLine, startColumn));
                    continue;
                case '}':
                    Advance();
                    tokens.Add(new Token(TokenKind.RightBrace, "}", start, 1, startLine, startColumn));
                    continue;
                case '?':
                    Advance();
                    tokens.Add(new Token(TokenKind.Question, "?", start, 1, startLine, startColumn));
                    continue;
                case '<' when i + 1 < text.Length && text[i + 1] == '=':
                    Advance(2);
                    tokens.Add(new Token(TokenKind.LessEqual, "<=", start, 2, startLine, startColumn));
                    continue;
                case '<':
                    Advance();
                    tokens.Add(new Token(TokenKind.LeftAngle, "<", start, 1, startLine, startColumn));
                    continue;
                case '>' when i + 1 < text.Length && text[i + 1] == '=':
                    Advance(2);
                    tokens.Add(new Token(TokenKind.GreaterEqual, ">=", start, 2, startLine, startColumn));
                    continue;
                case '>':
                    Advance();
                    tokens.Add(new Token(TokenKind.RightAngle, ">", start, 1, startLine, startColumn));
                    continue;
                case '=':
                    Advance();
                    tokens.Add(new Token(TokenKind.Equals, "=", start, 1, startLine, startColumn));
                    continue;
                case '(':
                    Advance();
                    tokens.Add(new Token(TokenKind.LeftParen, "(", start, 1, startLine, startColumn));
                    continue;
                case ')':
                    Advance();
                    tokens.Add(new Token(TokenKind.RightParen, ")", start, 1, startLine, startColumn));
                    continue;
                case ',':
                    Advance();
                    tokens.Add(new Token(TokenKind.Comma, ",", start, 1, startLine, startColumn));
                    continue;
                case '-' when i + 1 < text.Length && text[i + 1] == '>':
                    Advance(2);
                    tokens.Add(new Token(TokenKind.Arrow, "->", start, 2, startLine, startColumn));
                    continue;
                case '.' when !(i + 1 < text.Length && char.IsDigit(text[i + 1])):
                    Advance();
                    tokens.Add(new Token(TokenKind.Dot, ".", start, 1, startLine, startColumn));
                    continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                var seenDot = false;
                while (i < text.Length && (char.IsDigit(text[i]) || (text[i] == '.' && !seenDot)))
                {
                    if (text[i] == '.')
                    {
                        seenDot = true;
                    }

                    Advance();
                }

                var numLen = i - start;
                tokens.Add(new Token(
                    TokenKind.Number, text.Substring(start, numLen), start, numLen, startLine, startColumn));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                while (i < text.Length && IsIdentifierPart(text[i]))
                {
                    Advance();
                }

                var len = i - start;
                tokens.Add(new Token(
                    TokenKind.Identifier, text.Substring(start, len), start, len, startLine, startColumn));
                continue;
            }

            Advance();
            tokens.Add(new Token(TokenKind.Unknown, c.ToString(), start, 1, startLine, startColumn));
        }

        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, i, 0, line, column));
        return tokens;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
