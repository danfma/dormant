namespace Dormant.Sample.Quickstart.Schema.App;

// Hand-written partial extending the generated `User` entity — proves custom members coexist with
// generated ones and survive regeneration (spec FR-003, US1 acceptance #2). The namespace follows the
// FR-046 formula: project root namespace + folder (schema) + module (app), PascalCased.
public partial class User
{
    public bool IsRecent() => CreatedAt > System.DateTime.UtcNow.AddDays(-7);
}
