namespace app;

// Hand-written partial extending the generated `User` entity — proves custom members coexist with
// generated ones and survive regeneration (spec FR-003, US1 acceptance #2).
public partial class User
{
    public bool IsRecent() => CreatedAt > System.DateTime.UtcNow.AddDays(-7);
}
