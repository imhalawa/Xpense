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

        builder.Ignore(budget => budget.Amount);

        builder.HasOne(budget => budget.Category)
            .WithMany()
            .HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(budget => new { budget.CategoryId, budget.StartsOn });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Budget_Amount_Positive", """ "AmountMinorUnits" > 0 """);

            table.HasCheckConstraint(
                "CK_Budget_Ends_On_Or_After_Start",
                """ "EndsOn" IS NULL OR "EndsOn" >= "StartsOn" """);

            table.HasCheckConstraint(
                "CK_Budget_Alert_Threshold_Percent",
                """ "AlertThresholdPercent" IS NULL OR ("AlertThresholdPercent" BETWEEN 1 AND 100) """);
        });

    }
}
