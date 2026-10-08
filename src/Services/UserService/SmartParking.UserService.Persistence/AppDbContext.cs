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
        public DbSet<DriverRegistration> DriverRegistrations { get; set; } = null!;
        public DbSet<OwnerApplication> OwnerApplications { get; set; } = null!;
        public DbSet<OperatorGrant> OperatorGrants { get; set; } = null!;
        public DbSet<AuthChallenge> AuthChallenges { get; set; } = null!;
        public DbSet<WorkflowDelivery> WorkflowDeliveries { get; set; } = null!;
        public DbSet<MfaCredential> MfaCredentials { get; set; } = null!;
        public DbSet<RegisteredVehicle> RegisteredVehicles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            MapWorkflow<AuthChallenge>(modelBuilder, "auth_challenges", "Id");
            MapWorkflow<WorkflowDelivery>(modelBuilder, "workflow_deliveries", "Id");
            MapWorkflow<MfaCredential>(modelBuilder, "mfa_credentials", "UserId");
            modelBuilder.Entity<RegisteredVehicle>(entity=>
            {
                entity.ToTable("vehicles");entity.HasKey(v=>v.Id);
                entity.Property(v=>v.Id).HasColumnName("id");
                entity.Property(v=>v.AccountId).HasColumnName("account_id");
                entity.Property(v=>v.RawPlate).HasColumnName("original_plate").HasMaxLength(30);
                entity.Property(v=>v.CanonicalPlate).HasColumnName("normalized_plate").HasMaxLength(20);
                entity.Property(v=>v.VehicleType).HasColumnName("vehicle_type");
                entity.Property(v=>v.ImageReference).HasColumnName("image_url");
                entity.Property(v=>v.CreatedAt).HasColumnName("created_at");
                entity.Property(v=>v.UpdatedAt).HasColumnName("updated_at");
                entity.Property(v=>v.DeletedAt).HasColumnName("deleted_at");
                entity.HasOne<Account>().WithMany().HasForeignKey(v=>v.AccountId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<OperatorGrant>(entity =>
            {
                entity.ToTable("operator_grants");
                entity.HasKey(e => e.AccountId);
                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.Permissions).HasColumnName("permissions").HasColumnType("text[]");
                entity.HasOne(e => e.Account).WithOne().HasForeignKey<OperatorGrant>(e => e.AccountId);
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OwnerApplication>(entity =>
            {
                entity.ToTable("owner_applications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.BusinessName).HasColumnName("business_name").HasMaxLength(255);
                entity.Property(e => e.LotType).HasColumnName("lot_type").HasMaxLength(30);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
                entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
                entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(e => e.ReviewNote).HasColumnName("review_note").HasMaxLength(2000);
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedBy);
            });

            modelBuilder.Entity<DriverRegistration>(entity =>
            {
                entity.ToTable("driver_registrations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Channel).HasColumnName("channel");
                entity.Property(e => e.CodeHash).HasColumnName("code_hash");
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
                entity.Property(e => e.ResendAvailableAt).HasColumnName("resend_available_at");
                entity.Property(e => e.FailedAttempts).HasColumnName("failed_attempts");
                entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
                entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.CompanyName).HasColumnName("company_name");
                entity.Property(e=>e.EmailVerifiedAt).HasColumnName("email_verified_at");
                entity.Property(e=>e.PhoneVerifiedAt).HasColumnName("phone_verified_at");
                entity.Property(e => e.Status).HasColumnName("status").HasConversion(
                    value => value == SmartParking.UserService.Domain.Enum.UserStatus.PendingVerification
                        ? "PENDING_VERIFICATION" : value == SmartParking.UserService.Domain.Enum.UserStatus.PendingApproval
                        ? "PENDING_APPROVAL" : value.ToString().ToUpperInvariant(),
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
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.SiteId).HasColumnName("site_id");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.Permissions).HasColumnName("permissions").HasColumnType("text[]");
                
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
        private static void MapWorkflow<T>(ModelBuilder builder, string table, string key) where T : class
        {
            var entity = builder.Entity<T>();
            entity.ToTable(table); entity.HasKey(key);
            foreach (var property in typeof(T).GetProperties())
                entity.Property(property.Name).HasColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            entity.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict);
        }
    }
}
