using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Commands.CreatePost;

public class CreatePostCommandHandler(
    IPostRepository postRepo,
    IMediaObjectRepository mediaRepo,
    IDailySafetyRecordRepository dailySafetyRecordRepo,
    ISafetySettingRepository safetySettingRepo,
    IStorageService storage,
    IRealtimeNotifier notifier,
    IFriendRepository friendRepo,
    PostDtoMapper mapper)
    : IRequestHandler<CreatePostCommand, Result<PostResponse>>
{
    private static readonly TimeZoneInfo VietnamTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    public async Task<Result<PostResponse>> Handle(CreatePostCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var currentUserId = command.CurrentUserId;

        // 1. Parse visibility
        PostVisibility visibility;
        if (string.IsNullOrEmpty(request.Visibility) || request.Visibility.Equals("friends", StringComparison.OrdinalIgnoreCase))
        {
            visibility = PostVisibility.Friends;
        }
        else if (request.Visibility.Equals("private", StringComparison.OrdinalIgnoreCase))
        {
            visibility = PostVisibility.Private;
        }
        else
        {
            return Result<PostResponse>.Failure(
                Error.Validation(ErrorCodes.Post.INVALID_VISIBILITY,
                    "Visibility không hợp lệ. Chỉ hỗ trợ: 'friends' hoặc 'private'."));
        }

        // 2. Fetch media
        var media = await mediaRepo.GetByIdAsync(request.MediaId, ct);
        if (media is null)
            return Result<PostResponse>.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Media không tồn tại."));

        // 3. Check media ownership
        if (media.UploadProviderByUserId != currentUserId)
            return Result<PostResponse>.Failure(
                Error.Forbidden(ErrorCodes.Post.MEDIA_ACCESS_DENIED, "Bạn không có quyền sử dụng media này."));

        // 4. Check media readiness
        if (!media.IsReadyForPost())
            return Result<PostResponse>.Failure(
                Error.Failure(ErrorCodes.Post.MEDIA_NOT_READY, "Media chưa sẵn sàng để đăng."));

        // 5. Check media type
        if (media.MediaType != MediaType.Image && media.MediaType != MediaType.Video)
            return Result<PostResponse>.Failure(
                Error.Failure(ErrorCodes.Post.UNSUPPORTED_MEDIA_TYPE, "Loại media không được hỗ trợ. Chỉ hỗ trợ ảnh và video."));

        // 6. Video duration check
        if (media.MediaType == MediaType.Video)
        {
            if (media.DurationSeconds == null || media.DurationSeconds < 5 || media.DurationSeconds > 10)
                return Result<PostResponse>.Failure(
                    Error.Failure(ErrorCodes.Post.INVALID_VIDEO_DURATION, "Video phải có độ dài từ 5 đến 10 giây."));
        }

        // 7. Ensure today's safety record is checked in by this post.
        var healthRecord = await GetOrCreateTodayHealthRecordAsync(currentUserId, ct);
        if (healthRecord.Status != Beacon.Domain.Enums.Safety.SafetyStatus.CheckedIn)
            healthRecord.MarkCheckedIn(DateTime.UtcNow);

        // 8. Create post entity
        var post = Post.Create(
            currentUserId,
            request.MediaId,
            request.Caption,
            visibility,
            healthRecord.Id,
            request.Latitude,
            request.Longitude);

        // 9. Persist
        await postRepo.AddAsync(post, ct);
        await postRepo.SaveChangesAsync(ct);

        // 10. Get media URL
        var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);
        var mediaResponse = mapper.ToMediaResponse(media, url, thumbUrl);
        var response = mapper.ToPostResponse(post, mediaResponse);

        if (visibility == PostVisibility.Friends)
        {
            var friendIds = await friendRepo.ListFriendIdsAsync(currentUserId, ct);
            if (friendIds.Count > 0)
                await notifier.NotifyNewPostAsync(response, friendIds, ct);
        }

        // 11. Return response
        return Result<PostResponse>.Success(response);
    }

    private async Task<DailySafetyRecord> GetOrCreateTodayHealthRecordAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz));
        var record = await dailySafetyRecordRepo.GetByUserIdAndDateAsync(userId, today, ct);
        if (record is not null)
            return record;

        var deadline = await ComputeDeadlineAsync(userId, today, ct);
        record = DailySafetyRecord.Create(userId, today, deadline);
        await dailySafetyRecordRepo.AddAsync(record, ct);
        return record;
    }

    private async Task<DateTime> ComputeDeadlineAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var setting = await safetySettingRepo.GetByUserIdAsync(userId, ct);
        var deadlineTime = setting?.DailyDeadlineLocalTime ?? new TimeOnly(23, 59);
        var deadlineVn = today.ToDateTime(deadlineTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(deadlineVn, VietnamTz);
    }
}
