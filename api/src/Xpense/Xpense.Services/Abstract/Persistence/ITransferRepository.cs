using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Persistence;

public interface ITransferRepository
{
    Task<Transfer> ExecuteAtomic(Func<Task<Transfer>> createTransfer);
}
