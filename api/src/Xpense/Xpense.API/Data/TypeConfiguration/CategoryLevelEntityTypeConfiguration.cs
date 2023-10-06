using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration;

public class CategoryLevelEntityTypeConfiguration: BaseEntityTypeConfiguration<CategoryLevel>
{
    public override void Configure(EntityTypeBuilder<CategoryLevel> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

        //Category Name Index 
        builder.HasIndex(e => e.Name).IsUnique();
    }
}