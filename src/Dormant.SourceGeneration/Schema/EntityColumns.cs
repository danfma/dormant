using System.Collections.Generic;
using System.Linq;
using Dormant.SourceGeneration.Emit;
using Dormant.SourceGeneration.Parsing;

namespace Dormant.SourceGeneration.Schema;

/// <summary>
/// Single source of truth for an entity's flat column layout (009 P-A): value columns in declaration
/// order, followed by a <c>&lt;ref&gt;_id</c> foreign-key column for each single (to-one) reference in
/// reference declaration order. Every place that SELECTs / RETURNs a full entity (the binding's
/// <c>SelectByKey</c>, a full-entity query <c>select a</c>, an insert/update <c>returning</c> entity)
/// and the entity's materializer ctor MUST agree on this ordering, or positional reads misalign.
/// To-many collections (Set/List/Bag/Map) are schema metadata only — they contribute no column.
/// </summary>
internal static class EntityColumns
{
    /// <summary>The single (to-one) references, in declaration order — each backs a <c>&lt;ref&gt;_id</c> column.</summary>
    internal static IEnumerable<ReferenceModel> ToOne(EntityModel entity) =>
        entity.References.Where(r => r.Kind == ReferenceKind.Ref);

    /// <summary>The resolved database column name for a to-one reference's foreign key (<c>&lt;ref&gt;_id</c>).</summary>
    internal static string ForeignKeyColumn(
        ReferenceModel reference,
        NamingConvention convention
    ) => NamingConventions.Resolve(reference.Name, null, convention) + "_id";

    /// <summary>The C# property name for a to-one reference's foreign-key scalar (e.g. <c>WriterId</c>).</summary>
    internal static string ForeignKeyProperty(ReferenceModel reference) =>
        Naming.ToPascalCase(reference.Name) + "Id";

    /// <summary>The CLR type of a to-one reference's foreign-key scalar (the target entity's PK CLR type).</summary>
    internal static string ForeignKeyClrType(
        ReferenceModel reference,
        IReadOnlyDictionary<string, string> refPkDslTypes
    )
    {
        var dsl = refPkDslTypes.TryGetValue(reference.TargetEntity, out var t) ? t : "Uuid";
        return TypeMap.TryMap(dsl, out var clr) ? clr : "global::System.Guid";
    }

    /// <summary>Value-column database names (declaration order) followed by the to-one FK columns.</summary>
    internal static List<string> SelectColumnNames(EntityModel entity, NamingConvention convention)
    {
        var names = entity
            .Properties.Select(p => NamingConventions.Resolve(p.Name, p.NameOverride, convention))
            .ToList();
        names.AddRange(ToOne(entity).Select(r => ForeignKeyColumn(r, convention)));
        return names;
    }
}
