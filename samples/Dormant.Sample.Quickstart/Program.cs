// Quickstart sample (mirrors specs/001-orm-aot-sourcegen/quickstart.md). US1: the schema in
// schema/app.dqls is compiled by the DormantQL generator into partial types in namespace
// Dormant.Sample.Quickstart.Schema.App (FR-046); the hand-written partial in UserExtensions.cs
// coexists. Non-nullable members are `required` (FR-048). Persistence/query land in US2/US3.
using Dormant.Sample.Quickstart.Schema.App;

var user = new User
{
    Id = Guid.NewGuid(),
    Email = "ada@example.com",
    CreatedAt = DateTime.UtcNow,
    Version = 1,
};

Console.WriteLine($"User {user.Email} created at {user.CreatedAt:o}; recent? {user.IsRecent()}");
Console.WriteLine($"bio set? {user.Bio is not null}; posts loaded? {user.Posts.IsLoaded}");
return 0;
