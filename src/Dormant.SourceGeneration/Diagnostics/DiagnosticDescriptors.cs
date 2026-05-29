using Dormant.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dormant.SourceGeneration.Diagnostics;

/// <summary>Stable diagnostic descriptors for the DormantQL generator/analyzer (spec FR-004/FR-028).</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "DormantQL";

    /// <summary>A schema/query DormantQL file could not be parsed.</summary>
    public static readonly DiagnosticDescriptor SyntaxError = new(
        id: "ORM001",
        title: "DormantQL syntax error",
        messageFormat: "DormantQL syntax error: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A link targets an entity that is not defined in the schema.</summary>
    public static readonly DiagnosticDescriptor UndefinedLinkTarget = new(
        id: "ORM002",
        title: "Link targets an undefined entity",
        messageFormat: "Link '{0}' targets entity '{1}', which is not defined in the schema",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A property declares a value type that is not a known DormantQL v1 type.</summary>
    public static readonly DiagnosticDescriptor UnknownPropertyType = new(
        id: "ORM003",
        title: "Unknown property type",
        messageFormat: "Property '{0}' has unknown DormantQL type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Use a v1 DormantQL value type such as str, bool, int32, int64, float64, decimal, uuid, datetime, bytes, or json."
    );

    /// <summary>A query selects an entity that is not defined in any schema of the same module.</summary>
    public static readonly DiagnosticDescriptor UnknownQueryEntity = new(
        id: "ORM010",
        title: "Query targets an undefined entity",
        messageFormat: "Query '{0}' selects entity '{1}', which is not defined in the schema",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A query references a column that does not exist on the selected entity.</summary>
    public static readonly DiagnosticDescriptor UnknownQueryColumn = new(
        id: "ORM011",
        title: "Query references an unknown column",
        messageFormat: "Query '{0}' references column '{1}', which is not a value property of entity '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A query references a parameter that was not declared in its parameter list.</summary>
    public static readonly DiagnosticDescriptor UnknownQueryParameter = new(
        id: "ORM012",
        title: "Query references an undeclared parameter",
        messageFormat: "Query '{0}' references parameter '{1}', which is not declared",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>Two members of the same entity resolve to the same database column name.</summary>
    public static readonly DiagnosticDescriptor NameCollision = new(
        id: "ORM013",
        title: "Database name collision",
        messageFormat: "Entity '{0}' members '{1}' and '{2}' both map to database name '{3}' under the active naming convention",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Apply a db(\"…\") override to one of the members, or rename it, so each column has a distinct database name."
    );

    // --- 003 LINQ-style grammar diagnostics (FR-009/FR-015) ---

    /// <summary>A removed 002 construct was used; the message points to the replacement form.</summary>
    public static readonly DiagnosticDescriptor RemovedSyntax = new(
        id: "ORM020",
        title: "Removed DormantQL syntax",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The prior DormantQL grammar was replaced by the LINQ-style query/mutation grammar. Use brace blocks, alias-qualified members, '=' assignment, and C#/TypeScript operators."
    );

    /// <summary>A subject (from/insert/update/delete) was declared without an explicit alias.</summary>
    public static readonly DiagnosticDescriptor MissingAlias = new(
        id: "ORM021",
        title: "Subject is missing an explicit alias",
        messageFormat: "Subject '{0}' must declare an explicit alias such as '{0} u'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A member reference uses an alias that was not declared in the unit.</summary>
    public static readonly DiagnosticDescriptor UndeclaredAlias = new(
        id: "ORM022",
        title: "Reference to an undeclared alias",
        messageFormat: "Alias '{0}' is not declared in this unit",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>An alias name was declared more than once in the same unit.</summary>
    public static readonly DiagnosticDescriptor DuplicateAlias = new(
        id: "ORM023",
        title: "Duplicate alias",
        messageFormat: "Alias '{0}' is declared more than once in this unit",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A member reference was written without an alias qualifier.</summary>
    public static readonly DiagnosticDescriptor UnqualifiedMember = new(
        id: "ORM024",
        title: "Member reference is not alias-qualified",
        messageFormat: "Member '{0}' must be alias-qualified such as 'u.{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>Clauses were authored outside the canonical order.</summary>
    public static readonly DiagnosticDescriptor WrongClauseOrder = new(
        id: "ORM025",
        title: "Clauses are out of canonical order",
        messageFormat: "Clause '{0}' is out of order, expected canonical order {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>An insert omits a required (non-optional) member.</summary>
    public static readonly DiagnosticDescriptor MissingRequiredMember = new(
        id: "ORM026",
        title: "Insert is missing a required member",
        messageFormat: "Insert of '{0}' is missing required member '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A returning/select expression references a member it does not produce.</summary>
    public static readonly DiagnosticDescriptor ResultMemberNotInShape = new(
        id: "ORM027",
        title: "Result member is not in the projected shape",
        messageFormat: "Member '{0}' is not part of the result shape of unit '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A free-composition select declares the same member name twice (009 US2, FR-010).</summary>
    public static readonly DiagnosticDescriptor DuplicateCompositionMember = new(
        id: "ORM028",
        title: "Duplicate composition member",
        messageFormat: "Composition in query '{0}' declares member '{1}' more than once",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // --- Feature 012: EdgeQL-style constraints, scalar types, inheritance, annotations ---

    /// <summary>A constraint name is not part of the standard library (012 FR-003/FR-009).</summary>
    public static readonly DiagnosticDescriptor UnknownConstraint = new(
        id: "ORM029",
        title: "Unknown constraint",
        messageFormat: "Unknown constraint '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Use a standard constraint: unique, check, one_of, max, min, max_exclusive, min_exclusive, max_length, min_length, length, range, regex, primary, or concurrency."
    );

    /// <summary>A constraint is applied to a member whose type does not support it (012 FR-009).</summary>
    public static readonly DiagnosticDescriptor ConstraintTypeMismatch = new(
        id: "ORM030",
        title: "Constraint not applicable to member type",
        messageFormat: "Constraint '{0}' cannot be applied to member '{1}' of type '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A constraint references a member that does not exist on the entity (012 FR-009).</summary>
    public static readonly DiagnosticDescriptor ConstraintUnknownMember = new(
        id: "ORM031",
        title: "Constraint references an unknown member",
        messageFormat: "Constraint references member '{0}', which is not defined on entity '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>Two constraints in the same module request the same <c>as</c> name (012 FR-005/SC-003).</summary>
    public static readonly DiagnosticDescriptor DuplicateConstraintName = new(
        id: "ORM032",
        title: "Duplicate constraint name",
        messageFormat: "Constraint name '{0}' is declared more than once",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A scalar type extends an unknown or non-scalar base (012 FR-007).</summary>
    public static readonly DiagnosticDescriptor UnknownScalarBase = new(
        id: "ORM033",
        title: "Unknown scalar base type",
        messageFormat: "Scalar type '{0}' extends '{1}', which is not a known base scalar type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>Entity inheritance has a conflict or a cycle (012 FR-008).</summary>
    public static readonly DiagnosticDescriptor InheritanceConflict = new(
        id: "ORM034",
        title: "Inheritance conflict",
        messageFormat: "Entity '{0}' has an inheritance conflict or cycle ({1})",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>A removed legacy member modifier was used; migrate to the constraint syntax (012 FR-012).</summary>
    public static readonly DiagnosticDescriptor RemovedModifierSyntax = new(
        id: "ORM035",
        title: "Removed member modifier syntax",
        messageFormat: "The '{0}' member modifier was removed, use the constraint block form '{1}' instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>An unknown/misshaped annotation, or a constraint/annotation on a reference/collection (012 FR-015/FR-016).</summary>
    public static readonly DiagnosticDescriptor InvalidAnnotationOrTarget = new(
        id: "ORM036",
        title: "Invalid annotation or constraint target",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}

/// <summary>An equatable, pipeline-safe description of a diagnostic (research §5).</summary>
/// <param name="Descriptor">The descriptor to report.</param>
/// <param name="Location">The source location, or <see langword="null"/> for a file-less diagnostic.</param>
/// <param name="MessageArgs">Message format arguments.</param>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs
)
{
    /// <summary>Materializes a Roslyn <see cref="Diagnostic"/> (call only in the output stage).</summary>
    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation(), args);
    }
}

/// <summary>An equatable, pipeline-safe source location over a DormantQL file (research §5).</summary>
/// <param name="FilePath">The DormantQL file path.</param>
/// <param name="TextSpan">The character span.</param>
/// <param name="LineSpan">The line/column span (zero-based).</param>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>Materializes a Roslyn <see cref="Location"/>.</summary>
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}
