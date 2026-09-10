using PartnerTransactionBff.Contracts;

namespace PartnerTransactionBff.Infrastructure;

public interface ITransactionQueue
{
    Task PublishAsync(PartnerTransactionMessage message, CancellationToken cancellationToken);
}
