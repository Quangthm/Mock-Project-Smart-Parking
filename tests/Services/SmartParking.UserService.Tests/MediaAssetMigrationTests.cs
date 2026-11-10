using Microsoft.EntityFrameworkCore;

namespace SmartParking.UserService.Tests;

public sealed class MediaAssetMigrationTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task AlignedSchemaReplayPreservesLegacyMediaReferences()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(id,full_name,email,password_hash,status,avatar_url) VALUES ({id},'Synthetic legacy user','legacy-media@example.test','fixture-only','ACTIVE','https://legacy.example.test/avatar.png')");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(ServiceSchema.ScriptPath("test/verify-user-access-schema.sql")));
        var canonical = await File.ReadAllTextAsync(ServiceSchema.ScriptPath("microservices/01-user-access-service-db.sql"));
        await db.Database.ExecuteSqlRawAsync(canonical);
        await db.Database.ExecuteSqlRawAsync(canonical);
        var user = await db.Users.SingleAsync(u => u.Id == id);
        Assert.Null(user.AvatarAssetId);
        Assert.Equal("https://legacy.example.test/avatar.png", user.AvatarUrl);
        Assert.Empty(await db.MediaAssets.ToArrayAsync());
    }
}
