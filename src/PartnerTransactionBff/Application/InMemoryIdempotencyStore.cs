using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PartnerTransactionBff.Configuration;

namespace PartnerTransactionBff.Application;

/// <summary>
/// Process-local idempotency guard for the assessment/demo.
/// Production deployments should replace this with a durable/distributed store
/// such as PostgreSQL or Redis with an atomic unique constraint/operation.
/// </summary>
public sealed class InMemoryIdempotencyStore(
    IMemoryCache cache,
    IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out _))
            {
                return false;
            }

            cache.Set(key, true, options.Value.EntryLifetime);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task CompleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}
