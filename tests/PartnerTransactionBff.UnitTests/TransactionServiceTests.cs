using PartnerTransactionBff.Application;
using PartnerTransactionBff.Contracts;
using PartnerTransactionBff.Domain;
using PartnerTransactionBff.Infrastructure;
using PartnerTransactionBff.Services;
using Polly;
using Xunit;

namespace PartnerTransactionBff.UnitTests;

public sealed class TransactionServiceTests
{
    private static PartnerTransactionRequest Valid() =>
        new("P-1001", "TXN-99823", 250m, "USD", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Invalid_payload_does_not_call_dependencies()
    {
        var verification = new FakeVerification(new(true, "P-1001", "Partner P-1001", "STANDARD"));
        var queue = new FakeQueue();
        var store = new FakeIdempotencyStore();
        var sut = CreateSut(verification, queue, store);

        var result = await sut.ProcessAsync(
            Valid() with { Amount = 0 },
            null,
            CancellationToken.None);

        Assert.Equal(TransactionProcessStatus.Rejected, result.Status);
        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, queue.Calls);
    }

    [Fact]
    public async Task Verified_partner_is_enriched_and_queued()
    {
        var partner = new PartnerVerificationResponse(true, "P-1001", "Contoso Partner", "GOLD");
        var verification = new FakeVerification(partner);
        var queue = new FakeQueue();
        var store = new FakeIdempotencyStore();
        var sut = CreateSut(verification, queue, store);

        var result = await sut.ProcessAsync(Valid(), null, CancellationToken.None);

        Assert.Equal(TransactionProcessStatus.Accepted, result.Status);
        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, queue.Calls);
        Assert.NotNull(queue.LastMessage);
        Assert.Equal("Contoso Partner", queue.LastMessage!.PartnerName);
        Assert.Equal("GOLD", queue.LastMessage.PartnerTier);
        Assert.Equal(1, queue.LastMessage.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(queue.LastMessage.MessageId));
    }

    [Fact]
    public async Task Unverified_partner_is_not_queued_and_idempotency_is_released()
    {
        var verification = new FakeVerification(null);
        var queue = new FakeQueue();
        var store = new FakeIdempotencyStore();
        var sut = CreateSut(verification, queue, store);

        var result = await sut.ProcessAsync(Valid(), null, CancellationToken.None);

        Assert.Equal(TransactionProcessStatus.Rejected, result.Status);
        Assert.Equal(1, verification.Calls);
        Assert.Equal(0, queue.Calls);
        Assert.Single(store.RemovedKeys);
    }

    [Fact]
    public async Task Duplicate_transaction_is_not_verified_or_published()
    {
        var verification = new FakeVerification(new(true, "P-1001", "Partner P-1001", "STANDARD"));
        var queue = new FakeQueue();
        var store = new FakeIdempotencyStore { AllowBegin = false };
        var sut = CreateSut(verification, queue, store);

        var result = await sut.ProcessAsync(Valid(), null, CancellationToken.None);

        Assert.Equal(TransactionProcessStatus.Duplicate, result.Status);
        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, queue.Calls);
    }

    [Fact]
    public async Task Queue_failure_releases_idempotency_key()
    {
        var verification = new FakeVerification(new(true, "P-1001", "Partner P-1001", "STANDARD"));
        var queue = new FakeQueue { Exception = new InvalidOperationException("queue failed") };
        var store = new FakeIdempotencyStore();
        var sut = CreateSut(verification, queue, store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessAsync(Valid(), null, CancellationToken.None));

        Assert.Single(store.RemovedKeys);
    }

    private static TransactionService CreateSut(
        IPartnerVerificationClient verification,
        ITransactionQueue queue,
        IIdempotencyStore store) =>
        new(
            new TransactionValidator(),
            verification,
            queue,
            store,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionService>.Instance);

    private sealed class FakeVerification(PartnerVerificationResponse? result) : IPartnerVerificationClient
    {
        public int Calls { get; private set; }

        public Task<PartnerVerificationResponse?> VerifyAsync(string partnerId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeQueue : ITransactionQueue
    {
        public int Calls { get; private set; }
        public PartnerTransactionMessage? LastMessage { get; private set; }
        public Exception? Exception { get; init; }

        public Task PublishAsync(PartnerTransactionMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        public bool AllowBegin { get; init; } = true;
        public List<string> RemovedKeys { get; } = [];

        public Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(AllowBegin);

        public Task CompleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

}
