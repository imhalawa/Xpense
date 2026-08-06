using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class BudgetEntityTypeConfiguration : BaseEntityTypeConfiguration<Budget>
{
    public override void Configure(EntityTypeBuilder<Budget> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        // Amount is projected from AmountMinorUnits and Currency, not stored.
        builder.Ignore(budget => budget.Amount);

        // Budget (M) - Category (1). No collection on Category: nothing needs to walk that way, and
        // adding one would put a navigation on Category that only this feature reads. Restrict
        // because a category is soft-deleted rather than removed -- DeleteCategory soft-deletes the
        // budgets pointing at it in the same operation.
        builder.HasOne(budget => budget.Category)
            .WithMany()
            .HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Every read starts from a category and a window.
        builder.HasIndex(budget => new { budget.CategoryId, budget.StartsOn });

        // The factory enforces these too. That is not redundancy to remove: a migration, a script or
        // a future caller can write a row without going through it.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Budget_Amount_Positive", """ "AmountMinorUnits" > 0 """);

            table.HasCheckConstraint(
                "CK_Budget_Ends_On_Or_After_Start",
                """ "EndsOn" IS NULL OR "EndsOn" >= "StartsOn" """);
        });

        // "A budget that does not repeat must have an end" is deliberately *not* a check constraint.
        // Saying it in SQL means writing the Recurrence enum's integer value into the schema, and an
        // enum whose numbers ended up in two places is exactly how Credit and Debit came to disagree
        // with TransferLegDirection. It lives in Budget.For and Budget.Restate instead.
    }
}
