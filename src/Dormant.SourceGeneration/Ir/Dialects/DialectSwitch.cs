using System;
using Dormant.SourceGeneration.Emit;

namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// Emits the per-dialect SQL variant <c>switch</c> into generated code (005 D3): one arm per registered
/// <see cref="ISqlDialectRenderer"/>, each a non-interpolated raw string literal, selected at runtime by the
/// session's (or a passed) <c>DialectId</c>. Selection is a branch over compile-time-constant strings — never
/// runtime SQL compilation.
/// </summary>
internal static class DialectSwitch
{
    private const string DialectIdType = "global::Dormant.Abstractions.Providers.DialectId";

    /// <summary>Emits a switch whose value is the rendered <paramref name="statement"/> per dialect.</summary>
    public static void WriteStatementArg(
        SourceWriter writer,
        string indent,
        string subject,
        SqlStatement statement,
        string trailing
    ) => WriteSwitchArg(writer, indent, subject, r => r.Render(statement), trailing);

    /// <summary>
    /// Emits a <c>{subject} switch { DialectId.X => "...", … _ => throw }{trailing}</c> expression, each arm's
    /// SQL produced by <paramref name="render"/> for that dialect. Lines are written through the
    /// <see cref="SourceWriter"/> so its block indentation composes with the supplied <paramref name="indent"/>.
    /// </summary>
    public static void WriteSwitchArg(
        SourceWriter writer,
        string indent,
        string subject,
        Func<ISqlDialectRenderer, string> render,
        string trailing
    )
    {
        writer.Line(indent + subject + " switch");
        writer.Line(indent + "{");

        var armIndent = indent + "    ";
        var sqlIndent = armIndent + "    ";
        foreach (var renderer in DialectRenderers.All)
        {
            var sql = render(renderer);
            var fence = new string('"', RawFence(sql));
            writer.Line(armIndent + DialectIdType + "." + renderer.EnumMember + " =>");
            writer.Line(sqlIndent + fence);
            writer.Line(sqlIndent + sql);
            writer.Line(sqlIndent + fence + ",");
        }

        writer.Line(
            armIndent
                + "_ => throw new global::System.NotSupportedException(\"Dormant: no SQL variant for dialect \" + "
                + subject
                + " + \".\"),"
        );
        writer.Line(indent + "}" + trailing);
    }

    // The raw-string fence must be longer than the longest run of '"' in the content; never shorter than 3.
    private static int RawFence(string content)
    {
        var max = 0;
        var current = 0;
        foreach (var c in content)
        {
            if (c == '"')
            {
                current++;
                if (current > max)
                {
                    max = current;
                }
            }
            else
            {
                current = 0;
            }
        }

        return Math.Max(3, max + 1);
    }
}
