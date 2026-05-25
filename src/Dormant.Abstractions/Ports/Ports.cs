using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Ports;

/// <summary>Driver port: opens provider sessions (connection + transaction scope).</summary>
public interface IDataSource : IAsyncDisposable
{
    /// <summary>Opens a new provider session.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open <see cref="IDbSession"/>.</returns>
    ValueTask<IDbSession> OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Driver port: a connection bound to a transaction, executing prebuilt statements.</summary>
public interface IDbSession : IAsyncDisposable
{
    /// <summary>Begins the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the transaction has begun.</returns>
    ValueTask BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when committed.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when rolled back.</returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes a query and streams materialized rows without boxing.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="statement">The prebuilt, parameterized statement.</param>
    /// <param name="materialize">The generated row materializer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of materialized rows.</returns>
    IAsyncEnumerable<TRow> QueryAsync<TRow>(
        PreparedStatement statement,
        RowMaterializer<TRow> materialize,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a non-query statement (insert/update/delete) and returns affected rows.</summary>
    /// <param name="statement">The prebuilt, parameterized statement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    ValueTask<int> ExecuteAsync(PreparedStatement statement, CancellationToken cancellationToken = default);
}

/// <summary>Provider SQL dialect: identifier quoting, placeholders, and capability checks.</summary>
public interface ISqlDialect
{
    /// <summary>Quotes an identifier (table/column) for this provider.</summary>
    /// <param name="name">The unquoted identifier.</param>
    /// <returns>The quoted identifier.</returns>
    string QuoteIdentifier(string name);

    /// <summary>Renders the positional placeholder for the given one-based index (e.g. <c>$1</c>).</summary>
    /// <param name="index">One-based parameter index.</param>
    /// <returns>The placeholder text.</returns>
    string Placeholder(int index);

    /// <summary>Returns whether this provider supports the given native provider scope (spec FR-042).</summary>
    /// <param name="providerScope">The provider scope name (e.g. <c>postgres</c>).</param>
    /// <returns><see langword="true"/> if supported.</returns>
    bool Supports(string providerScope);
}

/// <summary>Reads/writes a column value of type <typeparamref name="T"/> without boxing (spec FR-019/FR-025).</summary>
/// <typeparam name="T">The CLR type bound to a column.</typeparam>
public interface ITypeBinding<T>
{
    /// <summary>Reads <typeparamref name="T"/> from the column at <paramref name="ordinal"/>.</summary>
    /// <param name="reader">The field reader.</param>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns>The value.</returns>
    T Read(IFieldReader reader, int ordinal);

    /// <summary>Writes <paramref name="value"/> as the positional parameter <paramref name="index"/>.</summary>
    /// <param name="writer">The parameter writer.</param>
    /// <param name="index">One-based positional parameter index.</param>
    /// <param name="value">The value to bind.</param>
    void Write(IParameterWriter writer, int index, T value);
}

/// <summary>Resolves type bindings (scalar, collection, or native) for the active provider.</summary>
public interface ITypeBindingRegistry
{
    /// <summary>Resolves the binding for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The CLR type.</typeparam>
    /// <returns>The resolved binding.</returns>
    ITypeBinding<T> Resolve<T>();
}

/// <summary>The declared signature of a provider-native function/operator (spec FR-039).</summary>
/// <param name="ProviderScope">The provider scope the function is declared under (e.g. <c>postgres</c>).</param>
/// <param name="Name">The function/operator name.</param>
/// <param name="ParameterTypes">The declared parameter CLR type names, in order.</param>
/// <param name="ReturnType">The single, statically-known return CLR type name.</param>
public sealed record NativeFunctionSignature(
    string ProviderScope,
    string Name,
    IReadOnlyList<string> ParameterTypes,
    string ReturnType);

/// <summary>Catalog of provider-native functions/operators available for type-checked invocation.</summary>
public interface INativeFunctionCatalog
{
    /// <summary>Attempts to resolve a native function signature.</summary>
    /// <param name="providerScope">The provider scope.</param>
    /// <param name="name">The function/operator name.</param>
    /// <param name="signature">The signature when found.</param>
    /// <returns><see langword="true"/> if a matching signature exists.</returns>
    bool TryGet(string providerScope, string name, out NativeFunctionSignature? signature);
}

/// <summary>A migration's identity and applied/pending bookkeeping record (spec FR-020/FR-021).</summary>
/// <param name="Id">The ordered migration id (e.g. a timestamp).</param>
/// <param name="Name">The human-readable migration name.</param>
public sealed record MigrationRecord(string Id, string Name);

/// <summary>Persistence port for migration state: applied set, apply, and revert.</summary>
public interface IMigrationStore
{
    /// <summary>Returns the migrations already applied to the database, in order.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The applied migrations.</returns>
    ValueTask<IReadOnlyList<MigrationRecord>> GetAppliedAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies a migration and records it.</summary>
    /// <param name="migration">The migration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when applied.</returns>
    ValueTask ApplyAsync(MigrationRecord migration, CancellationToken cancellationToken = default);

    /// <summary>Reverts a previously applied migration.</summary>
    /// <param name="migration">The migration to revert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when reverted.</returns>
    ValueTask RevertAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
}
