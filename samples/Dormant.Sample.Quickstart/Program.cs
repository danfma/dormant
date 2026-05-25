// Quickstart sample (mirrors specs/001-orm-aot-sourcegen/quickstart.md). The schema in schema/app.dqls
// and queries in schema/app.dql are compiled by the DormantQL generator into partial types + ISession
// query extension methods in namespace Dormant.Sample.Quickstart.Schema.App (FR-046); the hand-written
// partial in UserExtensions.cs coexists (FR-003). Non-nullable members are `required` (FR-048).
//
// Set DORMANT_SAMPLE_DB to a PostgreSQL connection string to run the full apply + CRUD + query
// round-trip; without it the sample just shows the generated surface (so `dotnet run` needs no database).
using Dormant.Provider.PostgreSql;
using Dormant.Sample.Quickstart.Schema.App;

var connectionString = Environment.GetEnvironmentVariable("DORMANT_SAMPLE_DB");

if (connectionString is null)
{
    var preview = new User
    {
        Id = Guid.NewGuid(),
        Email = "ada@example.com",
        CreatedAt = DateTime.UtcNow,
        Version = 1,
    };

    Console.WriteLine($"User {preview.Email} created at {preview.CreatedAt:o}; recent? {preview.IsRecent()}");
    Console.WriteLine($"bio set? {preview.Bio is not null}; posts loaded? {preview.Posts.IsLoaded}");
    Console.WriteLine("Set DORMANT_SAMPLE_DB to a PostgreSQL connection string to run the round-trip.");
    return;
}

// Apply the generated schema (CREATE SCHEMA "app" + CREATE TABLE ...), then CRUD + query.
await DormantPostgres.EnsureCreatedAsync(connectionString);
await using var factory = DormantPostgres.CreateSessionFactory(connectionString);

var id = Guid.NewGuid();
await using (var session = await factory.OpenSessionAsync())
{
    await session.AddAsync(new User { Id = id, Email = "ada@example.com", CreatedAt = DateTime.UtcNow, Version = 1 });
    await session.CommitAsync();
}

await using (var session = await factory.OpenSessionAsync())
{
    var ada = await session.GetAsync<User>(id);
    Console.WriteLine($"loaded by key: {ada?.Email}");

    // Generated DSL query — an ISession extension method carrying build-time SQL.
    await foreach (var user in session.UsersByEmail("ada@example.com"))
    {
        Console.WriteLine($"query result: {user.Email}, recent? {user.IsRecent()}");
    }
}
