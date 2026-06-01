using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.Enums.Messaging;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.DevSeed;

public sealed class DevTestDataSeeder(AppDbContext db, ILogger<DevTestDataSeeder> logger)
{
    public const string SeedLoginUsername = "beacon_n2n_seed";
    public const string SeedLoginPassword = "Beacon@123";
    public const string SeedLoginEmail = "beacon.n2n.seed@example.com";
    public const string SeedLoginPhone = "+84987654321";

    private const string SeedFriendUsername = "beacon_n2n_friend";
    private const string SeedFriendEmail = "beacon.n2n.friend@example.com";
    private const string SeedFriendPhone = "+84987654322";

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.Database.EnsureDeletedAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SeedAsync(ct);
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Username == SeedLoginUsername, ct))
        {
            logger.LogInformation("Dev test data already seeded.");
            return;
        }

        var now = DateTime.UtcNow;
        var loginUser = CreateUser(
            SeedLoginUsername,
            SeedLoginEmail,
            SeedLoginPhone,
            familyName: "Beacon",
            givenName: "Seed");
        var friendUser = CreateUser(
            SeedFriendUsername,
            SeedFriendEmail,
            SeedFriendPhone,
            familyName: "Beacon",
            givenName: "Friend");

        await db.Users.AddRangeAsync([loginUser, friendUser], ct);
        await db.SafetySettings.AddRangeAsync(
            [
                SafetySetting.CreateDefault(loginUser.Id, new TimeOnly(23, 59)),
                SafetySetting.CreateDefault(friendUser.Id, new TimeOnly(23, 59))
            ],
            ct);

        var friendship = Friend.Create(loginUser.Id, friendUser.Id);
        await db.Friends.AddAsync(friendship, ct);

        var directGroup = new MessageGroup
        {
            Type = MessageGroupType.Direct,
            DirectKey = MessageGroup.BuildDirectKey(loginUser.Id, friendUser.Id),
            CreatedAtUtc = now,
            RequireApprovalToAddMembers = false
        };

        directGroup.Members.Add(new MessageGroupMember
        {
            GroupId = directGroup.Id,
            UserId = loginUser.Id,
            Role = GroupMemberRole.Owner,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = now
        });
        directGroup.Members.Add(new MessageGroupMember
        {
            GroupId = directGroup.Id,
            UserId = friendUser.Id,
            Role = GroupMemberRole.Member,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = now
        });

        await db.MessageGroups.AddAsync(directGroup, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Dev test data seeded with login user {Username}.", SeedLoginUsername);
    }

    private static User CreateUser(
        string username,
        string email,
        string phone,
        string familyName,
        string givenName)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedLoginPassword);
        return User.Create(
            username: username,
            email: email,
            passwordHash: passwordHash,
            familyName: familyName,
            givenName: givenName,
            phoneNumber: phone,
            isEmailVerified: true);
    }
}
