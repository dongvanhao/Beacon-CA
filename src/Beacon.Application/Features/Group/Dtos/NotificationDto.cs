using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Features.Group.Dtos;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    string? Data,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc);

public record NotificationListResponse(
    List<NotificationDto> Items,
    DateTime? NextCursor,
    bool HasNextPage,
    int UnreadCount);

public record MarkReadResponse(int UnreadCount);
