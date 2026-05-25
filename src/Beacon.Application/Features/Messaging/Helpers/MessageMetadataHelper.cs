using System.Text.Json;
using Beacon.Domain.Entities.Messaging;

namespace Beacon.Application.Features.Messaging.Helpers;

public static class MessageMetadataHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(object metadata)
        => JsonSerializer.Serialize(metadata, JsonOptions);

    public static object Member(MessageGroupMember member, string? avatarUrl = null)
        => new
        {
            userId = member.UserId,
            familyName = member.User.FamilyName,
            givenName = member.User.GivenName,
            avatarUrl,
            role = (int)member.Role,
            status = (int)member.Status,
            lastSeenMessageId = member.LastSeenMessageId
        };
}
