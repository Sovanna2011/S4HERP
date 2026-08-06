using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Security.Domain;

namespace S4HERP.Security.Infrastructure;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("User", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.UserName }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Email });

        b.HasIndex(x => new { x.TenantId, x.ExternalIdentityProvider, x.ExternalSubjectId })
            .IsUnique()
            .HasFilter("[ExternalSubjectId] IS NOT NULL")
            .HasDatabaseName("UX_User_ExternalIdentity");

        // An interactive user needs some way to authenticate — a local password
        // or a federated identity. Requiring a password hash specifically would
        // block every OIDC user, which is the intended direction of travel.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_User_Credential",
            "[UserType] NOT IN (1,6) OR [PasswordHash] IS NOT NULL OR [ExternalSubjectId] IS NOT NULL"));
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Role", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permission", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRole", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId })
            .IsUnique()
            .HasFilter($"[ValidTo] = '{DateRange.OpenEnded:yyyy-MM-dd}'")
            .HasDatabaseName("UX_UserRole_Open");

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermission", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionId }).IsUnique();
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserCompanyCodeConfiguration : IEntityTypeConfiguration<UserCompanyCode>
{
    public void Configure(EntityTypeBuilder<UserCompanyCode> b)
    {
        b.ToTable("UserCompanyCode", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.CompanyCodeId }).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuthorizationObjectConfiguration : IEntityTypeConfiguration<AuthorizationObject>
{
    public void Configure(EntityTypeBuilder<AuthorizationObject> b)
    {
        b.ToTable("AuthorizationObject", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class AuthorizationFieldConfiguration : IEntityTypeConfiguration<AuthorizationField>
{
    public void Configure(EntityTypeBuilder<AuthorizationField> b)
    {
        b.ToTable("AuthorizationField", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.AuthorizationObjectId, x.Code }).IsUnique();
        b.HasOne(x => x.AuthorizationObject).WithMany()
            .HasForeignKey(x => x.AuthorizationObjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleAuthorizationConfiguration : IEntityTypeConfiguration<RoleAuthorization>
{
    public void Configure(EntityTypeBuilder<RoleAuthorization> b)
    {
        b.ToTable("RoleAuthorization", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.AuthorizationObjectId });
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AuthorizationObject).WithMany()
            .HasForeignKey(x => x.AuthorizationObjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoleAuthorizationValueConfiguration : IEntityTypeConfiguration<RoleAuthorizationValue>
{
    public void Configure(EntityTypeBuilder<RoleAuthorizationValue> b)
    {
        b.ToTable("RoleAuthorizationValue", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.RoleAuthorizationId, x.AuthorizationFieldId });

        b.HasOne(x => x.RoleAuthorization).WithMany(x => x.Values)
            .HasForeignKey(x => x.RoleAuthorizationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AuthorizationField).WithMany()
            .HasForeignKey(x => x.AuthorizationFieldId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoleTransactionCodeConfiguration : IEntityTypeConfiguration<RoleTransactionCode>
{
    public void Configure(EntityTypeBuilder<RoleTransactionCode> b)
    {
        b.ToTable("RoleTransactionCode", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.RoleId, x.TransactionCodeId }).IsUnique();
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSubstitutionConfiguration : IEntityTypeConfiguration<UserSubstitution>
{
    public void Configure(EntityTypeBuilder<UserSubstitution> b)
    {
        b.ToTable("UserSubstitution", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.ValidFrom });

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubstituteUser).WithMany().HasForeignKey(x => x.SubstituteUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_UserSubstitution_NotSelf", "[UserId] <> [SubstituteUserId]"));
    }
}

public class SegregationOfDutiesRuleConfiguration : IEntityTypeConfiguration<SegregationOfDutiesRule>
{
    public void Configure(EntityTypeBuilder<SegregationOfDutiesRule> b)
    {
        b.ToTable("SegregationOfDutiesRule", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint(
            "CK_SodRule_DistinctObjects",
            "[ConflictingAuthorizationObjectA] <> [ConflictingAuthorizationObjectB]"));
    }
}

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> b)
    {
        b.ToTable("LoginHistory", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.AttemptedUserName, x.AttemptedAtUtc });
        b.HasIndex(x => new { x.TenantId, x.AttemptedAtUtc });
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("UserSession", Schemas.Sec);
        b.HasIndex(x => x.SessionId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UserId, x.EndedAtUtc });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> b)
    {
        b.ToTable("PasswordHistory", Schemas.Sec);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.ChangedAtUtc });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
