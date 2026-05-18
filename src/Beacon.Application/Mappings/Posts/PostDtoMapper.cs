using Beacon.Application.Features.Posts.Dtos;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Safety;

namespace Beacon.Application.Mappings.Posts;

public sealed class PostDtoMapper
{
    public PostResponse ToPostResponse(Post post, MediaInPostResponse media) => new()
    {
        Id = post.Id,
        OwnerUserId = post.OwnerUserId,
        DailySafetyRecordId = post.DailySafetyRecordId,
        DailySafetyRecord = ToDailySafetyRecordResponse(post.DailySafetyRecord),
        Latitude = post.Latitude,
        Longitude = post.Longitude,
        Media = media,
        Caption = post.Caption,
        Visibility = post.Visibility.ToString().ToLowerInvariant(),
        Status = post.Status.ToString().ToLowerInvariant(),
        CreatedAtUtc = post.CreatedAtUtc,
        UpdatedAtUtc = post.UpdatedAtUtc
    };

    public MediaInPostResponse ToMediaResponse(MediaObject media, string url, string? thumbnailUrl) => new()
    {
        Id = media.Id,
        Url = url,
        Type = media.MediaType.ToString().ToLowerInvariant(),
        ThumbnailUrl = thumbnailUrl,
        DurationSeconds = media.DurationSeconds,
        Width = media.Width,
        Height = media.Height
    };

    public PostReactionItemResponse ToReactionItemResponse(
        PostReaction reaction, string displayName, string? avatarUrl) => new()
    {
        ReactionId = reaction.Id,
        Icon = reaction.Icon,
        ReactedAtUtc = reaction.CreatedAtUtc,
        User = new ReactorUserResponse
        {
            Id = reaction.UserId,
            DisplayName = displayName,
            AvatarUrl = avatarUrl
        }
    };

    public DailySafetyRecordInPostResponse? ToDailySafetyRecordResponse(DailySafetyRecord? record)
        => record is null
            ? null
            : new DailySafetyRecordInPostResponse
            {
                Id = record.Id,
                UserId = record.UserId,
                Date = record.Date,
                Status = record.Status.ToString(),
                DeadlineAtUtc = record.DeadlineAtUtc,
                CheckedInAtUtc = record.CheckedInAtUtc,
                MarkedMissedAtUtc = record.MarkedMissedAtUtc,
                ReminderSentAtUtc = record.ReminderSentAtUtc,
                ResolvedAtUtc = record.ResolvedAtUtc,
                LastEvaluatedAtUtc = record.LastEvaluatedAtUtc
            };
}
