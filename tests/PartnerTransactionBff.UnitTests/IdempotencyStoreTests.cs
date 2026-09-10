using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PartnerTransactionBff.Application;
using PartnerTransactionBff.Configuration;
using Polly;
using Xunit;

namespace PartnerTransactionBff.UnitTests;

public sealed class IdempotencyStoreTests
{
    [Fact]
    public async Task Same_key_can_only_begin_once()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyStore(
            cache,
            Options.Create(new IdempotencyOptions { EntryLifetime = TimeSpan.FromMinutes(5) }));

        Assert.True(await store.TryBeginAsync("P-1001:TXN-1", CancellationToken.None));
        Assert.False(await store.TryBeginAsync("P-1001:TXN-1", CancellationToken.None));

        await store.RemoveAsync("P-1001:TXN-1", CancellationToken.None);
        Assert.True(await store.TryBeginAsync("P-1001:TXN-1", CancellationToken.None));
    }
}
