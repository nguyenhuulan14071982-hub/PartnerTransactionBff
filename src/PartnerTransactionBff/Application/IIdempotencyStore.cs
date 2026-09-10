namespace PartnerTransactionBff.Application;

public interface IIdempotencyStore
{
    Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken);
    Task CompleteAsync(string key, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
