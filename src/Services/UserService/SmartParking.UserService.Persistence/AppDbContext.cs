using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<AccountRole> AccountRoles { get; set; } = null!;
        public DbSet<AuthSession> AuthSessions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.Status).HasColumnName("status").HasConversion(
                    value => value == SmartParking.UserService.Domain.Enum.UserStatus.PendingVerification
                        ? "PENDING_VERIFICATION" : value.ToString().ToUpperInvariant(),
                    value => Enum.Parse<SmartParking.UserService.Domain.Enum.UserStatus>(value.Replace("_", ""), true));
                entity.Property(e => e.FailedLoginAttempts).HasColumnName("failed_login_attempts");
                entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
                
                entity.Property(e => e.CreatedOn).HasColumnName("created_at");
                entity.Property(e => e.ModifiedOn).HasColumnName("updated_at");
                entity.Property(e => e.DeletedOn).HasColumnName("deleted_at");

                entity.Ignore(e => e.IsDeleted);
                entity.Ignore(e => e.CreatedBy);
                entity.Ignore(e => e.ModifiedBy);
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.AccountType).HasColumnName("account_type");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.SiteId).HasColumnName("site_id");
                entity.Property(e => e.MembershipTier).HasColumnName("membership_tier");
                entity.Property(e => e.LoyaltyPoints).HasColumnName("loyalty_points");
                entity.Property(e => e.Status).HasColumnName("status");
                
                entity.Property(e => e.CreatedOn).HasColumnName("created_at");
                entity.Property(e => e.DeletedOn).HasColumnName("deleted_at");
                
                entity.Ignore(e => e.ModifiedOn);
                entity.Ignore(e => e.IsDeleted);
                entity.Ignore(e => e.CreatedBy);
                entity.Ignore(e => e.ModifiedBy);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Accounts)
                    .HasForeignKey(e => e.UserId);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.Code);
                entity.Property(e => e.Code).HasColumnName("code");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
            });

            modelBuilder.Entity<AccountRole>(entity =>
            {
                entity.ToTable("account_roles");
                entity.HasKey(e => new { e.AccountId, e.RoleCode });
                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.RoleCode).HasColumnName("role_code");

                entity.HasOne(e => e.Account)
                    .WithMany(a => a.AccountRoles)
                    .HasForeignKey(e => e.AccountId);

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.AccountRoles)
                    .HasForeignKey(e => e.RoleCode);
            });

            modelBuilder.Entity<AuthSession>(entity =>
            {
                entity.ToTable("user_refresh_tokens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.AccessTokenId).HasColumnName("access_token_id");
                entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(255);
                entity.Property(e => e.AccessExpiresAt).HasColumnName("access_expires_at");
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
                entity.Property(e => e.IsRevoked).HasColumnName("is_revoked");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasIndex(e => e.AccessTokenId).IsUnique();
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            });
        }
    }
}
