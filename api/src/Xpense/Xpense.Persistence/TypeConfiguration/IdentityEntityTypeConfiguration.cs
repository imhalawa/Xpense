using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class IdentityEntityTypeConfiguration :
    IEntityTypeConfiguration<XpenseUser>,
    IEntityTypeConfiguration<IdentityRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserPasskey<Guid>>
{
    private const string XpenseSchema = "Xpense";
    private const string RolesTable = "Roles";
    private const string RoleClaimsTable = "RoleClaims";
    private const string UsersTable = "Users";
    private const string UserClaimsTable = "UserClaims";
    private const string UserLoginsTable = "UserLogins";
    private const string UserPasskeysTable = "UserPasskeys";
    private const string UserRolesTable = "UserRoles";
    private const string UserTokensTable = "UserTokens";

    public void Configure(EntityTypeBuilder<XpenseUser> builder) =>
        builder.ToTable(UsersTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) =>
        builder.ToTable(RolesTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) =>
        builder.ToTable(UserRolesTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) =>
        builder.ToTable(UserClaimsTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) =>
        builder.ToTable(UserLoginsTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) =>
        builder.ToTable(UserTokensTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) =>
        builder.ToTable(RoleClaimsTable, XpenseSchema);

    public void Configure(EntityTypeBuilder<IdentityUserPasskey<Guid>> builder)
    {
        builder.ToTable(UserPasskeysTable, XpenseSchema);
        builder.HasKey(passkey => passkey.CredentialId);
        builder.Property(passkey => passkey.CredentialId).HasMaxLength(1024);
        builder.OwnsOne(passkey => passkey.Data).ToJson();
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(passkey => passkey.UserId)
            .IsRequired();
    }
}
