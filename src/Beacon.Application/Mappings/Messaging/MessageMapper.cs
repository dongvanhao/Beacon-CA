using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;
using System.Text.Json;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessageMapper
{
    public MessageDto ToDto(
        Message m,
        string senderDisplayName,
        MessagePostDto? post = null)
        => new(
            m.Id,
            m.GroupId,
            m.SenderId,
            senderDisplayName,
            m.Content,
            m.Type,
            ParseMetadata(m.MetadataJson),
            m.CreatedAtUtc,
            m.PostId,
            post);

    private static JsonElement? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
