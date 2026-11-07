using Microsoft.EntityFrameworkCore;

namespace SmartParking.UserService.Tests;

public sealed class MediaAssetMigrationTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task PopulatedPreMediaDatabaseAcceptsPreflightAndRerunnableUpgradeWithoutLosingLegacyData()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync(preMedia: true);
        await using var db = fixture.Open();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(id,full_name,email,password_hash,status,avatar_url) VALUES ({id},'Synthetic legacy user','legacy-media@example.test','fixture-only','ACTIVE','https://legacy.example.test/avatar.png')");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(ServiceSchema.ScriptPath("test/verify-user-access-schema.sql")));
        var canonical = await File.ReadAllTextAsync(ServiceSchema.ScriptPath("microservices/01-user-access-service-db.sql"));
        var start = canonical.IndexOf("-- SPARK-373 additive", StringComparison.Ordinal);
        var end = canonical.IndexOf("-- END SPARK-373 media schema", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Canonical media section must be explicitly bounded.");
        var upgrade = canonical[start..end];
        await db.Database.ExecuteSqlRawAsync(upgrade);
        await db.Database.ExecuteSqlRawAsync(upgrade);
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(ServiceSchema.ScriptPath("test/verify-user-media-schema.sql")));
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(ServiceSchema.ScriptPath("test/inventory-user-media.sql")));
        var user = await db.Users.SingleAsync(u => u.Id == id);
        Assert.Null(user.AvatarAssetId);
        Assert.Equal("https://legacy.example.test/avatar.png", user.AvatarUrl);
        Assert.Empty(await db.MediaAssets.ToArrayAsync());
    }
}
