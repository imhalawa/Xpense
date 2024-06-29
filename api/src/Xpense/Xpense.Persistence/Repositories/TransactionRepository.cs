using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public class TransactionRepository(XpenseDbContext dbContext)
    : Repository<Transaction>(dbContext), ITransactionRepository;