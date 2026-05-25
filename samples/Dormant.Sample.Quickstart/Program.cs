// Quickstart sample (mirrors specs/001-orm-aot-sourcegen/quickstart.md). US1: the schema in
// schema/app.dqls is compiled by the DormantQL generator into partial `app.User` / `app.Post` types;
// the hand-written partial in UserExtensions.cs coexists. Persistence/query land in US2/US3.
using app;

var user = new User
{
    Id = Guid.NewGuid(),
    Email = "ada@example.com",
    CreatedAt = DateTime.UtcNow,
    Version = 1,
};

Console.WriteLine($"User {user.Email} created at {user.CreatedAt:o}; recent? {user.IsRecent()}");
Console.WriteLine($"posts loaded? {user.Posts.IsLoaded}");
return 0;
