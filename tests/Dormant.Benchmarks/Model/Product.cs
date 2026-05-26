namespace Dormant.Benchmarks.Model;

/// <summary>
/// Plain POCO used by Dapper, EF Core, and Insight.Database — no Dormant types (the projection boundary).
/// Property names match the columns Dormant's generator creates so convention-based mapping works. Bound to
/// the same physical SQLite table Dormant owns (it emits the DDL); these libraries never define schema.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
