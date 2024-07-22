using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.Services.Features.Analytics.UseCases
{
    public class GetExpensesByCategoryUseCase(ITransactionRepository transactionRepository, ICategoryRepository categoryRepository) : IQueryHandler<TodayExpensesByCategory>
    {
        public async Task<TodayExpensesByCategory> Execute()
        {
            var transactions = await transactionRepository.GetAllTransactions(DateTimeOffset.Now.ToUnixTimeSeconds());

            if (!transactions.Any())
                return new TodayExpensesByCategory(null, Money.Zero);

            var total = Money.OfCents(transactions
                .Sum(t => t.Amount));
            var groups = transactions
                .GroupBy(
                    t => t.Category,
                    (
                            category, trs) =>
                        new ExpensesByCategory(category.Id, category,
                            Money.OfCents(trs.Sum(t => t.Amount), total.Currency))
                );

            return new TodayExpensesByCategory(groups, total);
        }
    }
}
