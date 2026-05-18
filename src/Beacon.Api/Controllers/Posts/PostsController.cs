using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Commands.CreatePost;
using Beacon.Application.Features.Posts.Commands.DeletePost;
using Beacon.Application.Features.Posts.Commands.DeleteReaction;
using Beacon.Application.Features.Posts.Commands.UpdatePost;
using Beacon.Application.Features.Posts.Commands.UpsertReaction;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Queries.GetFeed;
using Beacon.Application.Features.Posts.Queries.GetFriendsFeed;
using Beacon.Application.Features.Posts.Queries.GetMyPosts;
using Beacon.Application.Features.Posts.Queries.GetPostDetail;
using Beacon.Application.Features.Posts.Queries.GetPostReactions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Beacon.Api.Controllers.Posts;

/// <summary>Quản lý bài đăng (tạo, đọc, chỉnh sửa, xóa) và feed trang chủ.</summary>
[Route("api/v1/posts")]
[Authorize]
public class PostsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>
    /// Tạo bài đăng mới kèm media.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Media phải do chính người dùng upload và có trạng thái <c>Ready</c>.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Tạo bài đăng thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ.
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại.
    /// - <c>MEDIA_ACCESS_DENIED</c>: Bạn không sở hữu media này.
    /// - <c>MEDIA_NOT_READY</c>: Media chưa được xử lý xong.
    /// - <c>UNSUPPORTED_MEDIA_TYPE</c>: Loại media không được hỗ trợ.
    /// - <c>INVALID_VIDEO_DURATION</c>: Video phải dài từ 5–10 giây.
    /// - <c>INVALID_VISIBILITY</c>: Visibility không hợp lệ (chỉ nhận "friends" | "private").
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":           "guid",
    ///   "ownerUserId":  "guid",
    ///   "media": {
    ///     "id":               "guid",
    ///     "url":              "string  (presigned URL, hết hạn 15 phút)",
    ///     "type":             "string  (image | video)",
    ///     "thumbnailUrl":     "string? (null nếu là ảnh)",
    ///     "durationSeconds":  "int?    (null nếu là ảnh)",
    ///     "width":            "int?",
    ///     "height":           "int?"
    ///   },
    ///   "caption":      "string?",
    ///   "visibility":   "string  (friends | private)",
    ///   "status":       "string  (active)",
    ///   "createdAtUtc": "datetime (UTC)",
    ///   "updatedAtUtc": "datetime? (UTC)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON:
    /// <code>
    /// {
    ///   "mediaId":    "guid     (bắt buộc)",
    ///   "caption":    "string?  (tuỳ chọn, tối đa 2 000 ký tự)",
    ///   "visibility": "string?  (tuỳ chọn — friends | private; mặc định friends)"
    /// }
    /// </code>
    /// </param>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request, CancellationToken ct)
    {
        var command = new CreatePostCommand(request, currentUser.UserId);
        return CreatedResult("api/v1/posts", await mediator.Send(command, ct));
    }

    #region
    /// <summary>
    /// Lấy feed trang chủ (bài đăng của bản thân + bạn bè, cursor pagination).
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Query:
    /// - <c>cursor</c> (tuỳ chọn, ISO datetime UTC): trả các bài có <c>createdAtUtc</c> nhỏ hơn giá trị này.
    /// - <c>limit</c> (mặc định 20, tối đa 100).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "items": [
    ///     {
    ///       "id":          "guid",
    ///       "ownerUserId": "guid",
    ///       "owner": {
    ///         "id":          "guid",
    ///         "displayName": "string",
    ///         "avatarUrl":   "string?"
    ///       },
    ///       "media": {
    ///         "id":               "guid",
    ///         "url":              "string",
    ///         "type":             "string  (image | video)",
    ///         "thumbnailUrl":     "string?",
    ///         "durationSeconds":  "int?",
    ///         "width":            "int?",
    ///         "height":           "int?"
    ///       },
    ///       "caption":         "string?",
    ///       "visibility":      "string",
    ///       "createdAtUtc":    "datetime (UTC)",
    ///       "reactionSummary": { "totalCount": "int", "icons": { "heart": "int", ... } },
    ///       "myReaction":      { "icon": "string" }
    ///     }
    ///   ],
    ///   "nextCursor": "string? (null khi hết trang)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new GetFeedQuery(currentUser.UserId, cursor, limit), ct));

    #region
    /// <summary>
    /// Lấy bài đăng (visibility=friends) từ một bạn bè cụ thể (cursor pagination).
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Query:
    /// - <c>cursor</c> (tuỳ chọn, ISO datetime UTC): trả các bài có <c>createdAtUtc</c> nhỏ hơn giá trị này.
    /// - <c>limit</c> (mặc định 20, tối đa 100).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    /// - <c>POST_ACCESS_DENIED</c>: Người dùng chỉ định không phải bạn bè.
    ///
    /// Cấu trúc <c>data</c> khi thành công: giống <c>GET /api/v1/posts/feed</c>
    /// (<c>items</c> + <c>nextCursor</c>), chỉ bao gồm bài của <c>friendId</c>.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("friends/{friendId:guid}")]
    public async Task<IActionResult> GetFriendsFeed(
        Guid friendId,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new GetFriendsFeedQuery(currentUser.UserId, friendId, cursor, limit), ct));

    #region
    /// <summary>
    /// Lấy danh sách bài đăng của bản thân (cursor pagination).
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Query:
    /// - <c>cursor</c> (tuỳ chọn, ISO datetime UTC): trả các bài có <c>createdAtUtc</c> nhỏ hơn giá trị này.
    /// - <c>limit</c> (mặc định 20, tối đa 100).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    ///
    /// Cấu trúc <c>data</c> khi thành công: giống <c>GET /api/v1/posts/feed</c>
    /// (<c>items</c> + <c>nextCursor</c>), chỉ bao gồm bài đăng của chính người dùng.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("me")]
    public async Task<IActionResult> GetMyPosts(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new GetMyPostsQuery(currentUser.UserId, cursor, limit), ct));

    #region
    /// <summary>
    /// Lấy chi tiết một bài đăng theo ID.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Chỉ chủ sở hữu hoặc bạn bè (khi visibility=friends) mới được xem.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại hoặc đã bị xóa.
    /// - <c>POST_ACCESS_DENIED</c>: Bạn không có quyền xem bài đăng này.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":          "guid",
    ///   "ownerUserId": "guid",
    ///   "owner": {
    ///     "id":          "guid",
    ///     "displayName": "string",
    ///     "avatarUrl":   "string?"
    ///   },
    ///   "media": {
    ///     "id":               "guid",
    ///     "url":              "string  (presigned URL, hết hạn 15 phút)",
    ///     "type":             "string  (image | video)",
    ///     "thumbnailUrl":     "string?",
    ///     "durationSeconds":  "int?",
    ///     "width":            "int?",
    ///     "height":           "int?"
    ///   },
    ///   "caption":         "string?",
    ///   "visibility":      "string  (friends | private)",
    ///   "status":          "string  (active)",
    ///   "createdAtUtc":    "datetime (UTC)",
    ///   "updatedAtUtc":    "datetime? (UTC)",
    ///   "reactionSummary": { "totalCount": "int", "icons": { "heart": "int", ... } },
    ///   "myReaction":      { "icon": "string" }
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("{postId:guid}")]
    public async Task<IActionResult> GetById(Guid postId, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetPostDetailQuery(postId, currentUser.UserId), ct));

    #region
    /// <summary>
    /// Cập nhật caption hoặc visibility của bài đăng.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Chỉ chủ sở hữu bài đăng mới được chỉnh sửa.
    /// Bỏ qua field hoặc truyền <c>null</c> = giữ nguyên giá trị hiện tại.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Cập nhật thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ.
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại.
    /// - <c>POST_UPDATE_DENIED</c>: Bạn không có quyền chỉnh sửa bài đăng này.
    /// - <c>INVALID_VISIBILITY</c>: Visibility không hợp lệ (chỉ nhận "friends" | "private").
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":           "guid",
    ///   "ownerUserId":  "guid",
    ///   "media": {
    ///     "id":               "guid",
    ///     "url":              "string",
    ///     "type":             "string  (image | video)",
    ///     "thumbnailUrl":     "string?",
    ///     "durationSeconds":  "int?",
    ///     "width":            "int?",
    ///     "height":           "int?"
    ///   },
    ///   "caption":      "string?",
    ///   "visibility":   "string  (friends | private)",
    ///   "status":       "string  (active)",
    ///   "createdAtUtc": "datetime (UTC)",
    ///   "updatedAtUtc": "datetime? (UTC)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON (tất cả tuỳ chọn):
    /// <code>
    /// {
    ///   "caption":    "string?  (null = giữ nguyên)",
    ///   "visibility": "string?  (friends | private; null = giữ nguyên)"
    /// }
    /// </code>
    /// </param>
    #endregion
    [HttpPatch("{postId:guid}")]
    public async Task<IActionResult> Update(Guid postId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdatePostRequest? request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdatePostCommand(postId, request ?? new UpdatePostRequest(), currentUser.UserId), ct));

    #region
    /// <summary>
    /// Soft-delete bài đăng của người dùng hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Chỉ chủ sở hữu bài đăng mới được xóa.
    /// Media đính kèm không bị xóa theo.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Xóa thành công.
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại.
    /// - <c>POST_DELETE_DENIED</c>: Bạn không có quyền xóa bài đăng này.
    ///
    /// Cấu trúc <c>data</c> khi thành công: <c>null</c>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete("{postId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeletePostCommand(postId, currentUser.UserId), ct));

    #region
    /// <summary>
    /// Tạo hoặc cập nhật reaction trên một bài đăng.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Nếu người dùng đã có reaction trên bài đăng này, icon sẽ được cập nhật (upsert).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Icon rỗng hoặc không hợp lệ.
    /// - <c>INVALID_REACTION_ICON</c>: Icon không nằm trong danh sách hỗ trợ (heart, haha, like, sad, wow).
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại hoặc đã bị xóa.
    /// - <c>POST_ACCESS_DENIED</c>: Bạn không có quyền xem bài đăng này.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "postId":          "guid",
    ///   "myReaction":      { "icon": "string" },
    ///   "reactionSummary": { "totalCount": "int", "icons": { "heart": "int", ... } }
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON:
    /// <code>
    /// {
    ///   "icon": "string  (bắt buộc — heart | haha | like | sad | wow)"
    /// }
    /// </code>
    /// </param>
    #endregion
    [HttpPut("{postId:guid}/reaction")]
    public async Task<IActionResult> UpsertReaction(
        Guid postId,
        [FromBody] UpsertPostReactionRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(
            new UpsertPostReactionCommand(postId, request.Icon, currentUser.UserId), ct));

    #region
    /// <summary>Lấy danh sách người dùng đã react trên một bài đăng.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Người dùng phải có quyền xem bài (chủ bài hoặc bạn bè khi visibility=friends).
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: icon không hợp lệ, limit ngoài [1,100], cursor sai format.
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại hoặc đã bị xóa.
    /// - <c>POST_ACCESS_DENIED</c>: Bạn không có quyền xem bài đăng này.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "items": [
    ///     {
    ///       "reactionId": "guid",
    ///       "icon":        "string  (heart | haha | like | sad | wow)",
    ///       "reactedAtUtc":"datetime (UTC)",
    ///       "user": { "id": "guid", "displayName": "string", "avatarUrl": "string?" }
    ///     }
    ///   ],
    ///   "summary":    { "totalCount": "int", "icons": { "heart": "int", "like": "int", ... } },
    ///   "nextCursor": "string? (null khi hết trang)",
    ///   "hasMore":    "bool"
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("{postId:guid}/reactions")]
    public async Task<IActionResult> GetReactions(
        Guid postId,
        [FromQuery] string? icon,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(
            new GetPostReactionsQuery(postId, currentUser.UserId, icon, cursor, limit), ct));

    #region
    /// <summary>
    /// Xóa reaction của người dùng trên một bài đăng (idempotent).
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Trả thành công kể cả khi reaction chưa tồn tại.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công.
    /// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại hoặc đã bị xóa.
    /// - <c>POST_ACCESS_DENIED</c>: Bạn không có quyền xem bài đăng này.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "postId":          "guid",
    ///   "myReaction":      null,
    ///   "reactionSummary": { "totalCount": "int", "icons": { "heart": "int", ... } }
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete("{postId:guid}/reaction")]
    public async Task<IActionResult> DeleteReaction(Guid postId, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new DeletePostReactionCommand(postId, currentUser.UserId), ct));
}
