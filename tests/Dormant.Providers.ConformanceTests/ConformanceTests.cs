using System.Linq;
using Dormant.Providers.ConformanceTests.Schema.Catalog;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Providers.ConformanceTests;

/// <summary>
/// Cross-provider parity (FR-007/SC-001): the same authored units run against PostgreSQL (Testcontainers)
/// and SQLite (in-memory) from one source of truth. Every behavior is asserted identically per provider.
/// </summary>
public sealed class ConformanceTests
{
    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Insert_returning_then_get_by_key(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        var id = Guid.NewGuid();

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var inserted = await session.CreateWidget(id, "gizmo", 7);
            await Assert.That(inserted.Name).IsEqualTo("gizmo");
            await Assert.That(inserted.Quantity).IsEqualTo(7);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget).IsNotNull();
            await Assert.That(widget!.Name).IsEqualTo("gizmo");
            await Assert.That(widget.Quantity).IsEqualTo(7);
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Static_query_returns_matching_rows(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateWidget(Guid.NewGuid(), "alpha", 1);
            await session.CreateWidget(Guid.NewGuid(), "beta", 2);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var matches = await Drain(session.WidgetsByName("alpha"));
            await Assert.That(matches.Count).IsEqualTo(1);
            await Assert.That(matches[0].Name).IsEqualTo("alpha");
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Optional_filter_query_selects_supplied_fragments(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateWidget(Guid.NewGuid(), "alpha", 1);
            await session.CreateWidget(Guid.NewGuid(), "beta", 5);
            await session.CreateWidget(Guid.NewGuid(), "gamma", 5);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            // No filters supplied → all rows.
            await Assert.That((await Drain(session.SearchWidgets())).Count).IsEqualTo(3);
            // Only the quantity filter → quantity >= 5.
            await Assert
                .That((await Drain(session.SearchWidgets(minQuantity: 5))).Count)
                .IsEqualTo(2);
            // Both filters → quantity >= 5 AND name = "beta".
            var both = await Drain(session.SearchWidgets(minQuantity: 5, name: "beta"));
            await Assert.That(both.Count).IsEqualTo(1);
            await Assert.That(both[0].Name).IsEqualTo("beta");
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task With_block_persists_child_foreign_key(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        var authorId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var returnedId = await session.CreateAuthorWithArticle(
                authorId,
                "ada",
                articleId,
                "notes"
            );
            await Assert.That(returnedId).IsEqualTo(articleId);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var author = await session.GetAsync<Author>(authorId);
            var article = await session.GetAsync<Article>(articleId);
            await Assert.That(author).IsNotNull();
            await Assert.That(article).IsNotNull();
            await Assert.That(article!.Title).IsEqualTo("notes");
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Navigation_in_predicate_joins_related_entity(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        var authorId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateAuthorWithArticle(authorId, "grace", articleId, "compilers");
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            // `where a.writer.name == authorName` joins Article → Author and filters on the joined column.
            var matches = await Drain(session.ArticlesByAuthorName("grace"));
            await Assert.That(matches.Count).IsEqualTo(1);
            await Assert.That(matches[0].Title).IsEqualTo("compilers");
            await Assert.That(matches[0].WriterId).IsEqualTo(authorId);

            var none = await Drain(session.ArticlesByAuthorName("nobody"));
            await Assert.That(none.Count).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Root_object_shape_returns_nested_record(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        var authorId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateAuthorWithArticle(authorId, "hopper", articleId, "nanosecond");
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            // `select a { title, writer: { name } }` → one query joining Article→Author; nested record.
            var card = (await Drain(session.ArticleCard(articleId)))[0];
            await Assert.That(card.Title).IsEqualTo("nanosecond");
            await Assert.That(card.Writer.Name).IsEqualTo("hopper");
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task To_many_shape_returns_materialized_collection(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        var authorId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateAuthorWithArticle(
                authorId,
                "turing",
                articleId,
                "on computable numbers"
            );
            await session.CreateTag(Guid.NewGuid(), "math", articleId);
            await session.CreateTag(Guid.NewGuid(), "logic", articleId);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            // `select a { title, tags: { label } }` → one query; Tags is a fully materialized list.
            var article = (await Drain(session.ArticleWithTags(articleId)))[0];
            await Assert.That(article.Title).IsEqualTo("on computable numbers");
            await Assert.That(article.Tags.Count).IsEqualTo(2);
            var labels = article.Tags.Select(t => t.Label).OrderBy(l => l).ToList();
            await Assert.That(labels[0]).IsEqualTo("logic");
            await Assert.That(labels[1]).IsEqualTo("math");
        }

        // An article with no tags yields an empty (non-null) collection.
        var emptyId = Guid.NewGuid();
        var emptyAuthor = Guid.NewGuid();
        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateAuthorWithArticle(emptyAuthor, "lovelace", emptyId, "notes");
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var article = (await Drain(session.ArticleWithTags(emptyId)))[0];
            await Assert.That(article.Tags.Count).IsEqualTo(0);
        }
    }

    private static async Task<List<T>> Drain<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
