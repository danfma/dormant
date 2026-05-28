namespace Dormant.Providers.ConformanceTests;

/// <summary>A user-owned plain record (the Clean-Arch boundary) targeted by a query's <c>into</c> (009 US3).</summary>
public sealed record ArticleFeedDto(string Headline, string AuthorName);
