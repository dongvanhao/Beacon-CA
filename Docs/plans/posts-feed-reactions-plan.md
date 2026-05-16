# Plan: Posts, Feed và Post Reactions

**Spec:** `docs/specs/posts-feed-reactions-spec.md`  
**Module:** Posts (mới — greenfield)  
**Phạm vi:** 4 phases, 11 slices  
**Ngày:** 2026-05-16

---

## Tổng quan thứ tự

```
Phase 0 ─── Prerequisite: Mở rộng MediaObject (DurationSeconds + Status)
Phase 1 ─── Foundation: Post + PostReaction Domain + DB
Phase 2 ─── Write Use Cases: CreatePost · UpdatePost · DeletePost
Phase 3 ─── Read Use Cases: GetPostDetail · GetFeed
Phase 4 ─── Reactions: UpsertReaction · DeleteReaction
```

Mỗi slice theo TDD: **Test (RED) → Domain → Handler → Validator → Mapper → Repo → EF → Migration → Controller → Test (GREEN)**

---

## Dependency Map

```
Slice 0.1 ──→ required by all Post slices (MediaObject.Status + DurationSeconds)
Slice 1.1 ──→ required by all slices (entities + enums + error codes)
Slice 1.2 ──→ required by all slices (repo interfaces)
Slice 1.3 ──→ required by all slices (EF config + migration)
              ↓
Slice 2.1 (CreatePost) ── can run after 1.1 + 1.2 + 1.3 + 0.1
Slice 2.2 (UpdatePost) ── requires 2.1 (controller exists)
Slice 2.3 (DeletePost) ── requires 2.1 (controller exists)
Slice 3.1 (GetPostDetail) ── requires 1.1 + 1.2 + 1.3
Slice 3.2 (GetFeed) ── requires 3.1 (reactionSummary helper shared)
Slice 4.1 (UpsertReaction) ── requires 1.1 + 1.2 + 1.3
Slice 4.2 (DeleteReaction) ── requires 4.1
```

---

## Phase 0: Prerequisite — Mở rộng MediaObject

> **Lý do:** `MediaObject` hiện tại thiếu `DurationSeconds` (int?) và `Status` (MediaStatus enum).  
> Posts module cần 2 fields này để validate video 5–10s và kiểm tra media đã ready chưa.  
> Phase này phải hoàn thành **trước tất cả** các phase còn lại.

---

### Slice 0.1: [Extend MediaObject] — Bổ sung DurationSeconds + Status + MediaStatus enum

**Module:** Storage (hiện có)  
**Type:** Infrastructure change + Migration  
**Files ảnh hưởng:**
- `src/Beacon.Domain/Enums/MediaStatus.cs` ← Tạo mới
- `src/Beacon.Domain/Entities/Storage/MediaObject.cs` ← Sửa
- `src/Beacon.Infrashtructure/Presistence/Configuration/Storage/MediaObjectConfiguration.cs` ← Sửa
- `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs` ← Không cần sửa (DbSet đã có)
- Migration mới

---

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Storage/MediaObjectDomainTests.cs`

```csharp
// Test domain methods mới của MediaObject
public class MediaObjectDomainTests
{
    [Fact]
    public void SetDurationAndStatus_ShouldUpdateFields()
    {
        var media = MediaObject.Create(...);
        media.SetStatus(MediaStatus.Ready);
        media.SetDuration(8);
        Assert.Equal(MediaStatus.Ready, media.Status);
        Assert.Equal(8, media.DurationSeconds);
    }

    [Fact]
    public void IsReadyForPost_WhenStatusReady_ReturnsTrue()
    {
        var media = ... // Status = Ready
        Assert.True(media.IsReadyForPost());
    }

    [Fact]
    public void IsReadyForPost_WhenStatusUploading_ReturnsFalse()
    {
        var media = ... // Status = Uploading
        Assert.False(media.IsReadyForPost());
    }
}
```

→ Test FAIL vì `MediaStatus`, `Status`, `DurationSeconds`, `IsReadyForPost()` chưa tồn tại.

---

#### Bước 2 — Domain: Tạo MediaStatus enum

File: `src/Beacon.Domain/Enums/MediaStatus.cs`

```csharp
namespace Beacon.Domain.Enums;

public enum MediaStatus
{
    Uploading  = 1,
    Processing = 2,
    Ready      = 3,
    Active     = 4,
    Failed     = 5
}
```

---

#### Bước 3 — Domain: Cập nhật MediaObject entity

File: `src/Beacon.Domain/Entities/Storage/MediaObject.cs`

Thêm vào entity:

```csharp
public MediaStatus Status { get; private set; } = MediaStatus.Uploading;
public int? DurationSeconds { get; private set; }

// Domain methods
public void SetStatus(MediaStatus status) => Status = status;
public void SetDuration(int? durationSeconds) => DurationSeconds = durationSeconds;

// Business rule — dùng trong Posts module
public bool IsReadyForPost()
    => Status is MediaStatus.Ready or MediaStatus.Active;
```

Cập nhật `Create()` factory: thêm `MediaStatus status = MediaStatus.Uploading` param, set `Status = status`.

---

#### Bước 4 — EF Config: Cập nhật MediaObjectConfiguration

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Storage/MediaObjectConfiguration.cs`

```csharp
builder.Property(m => m.Status)
    .IsRequired()
    .HasConversion<int>();

builder.Property(m => m.DurationSeconds)
    .IsRequired(false);
```

---

#### Bước 5 — Migration

```bash
dotnet ef migrations add AddMediaStatusAndDuration \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

Review migration: confirm thêm cột `Status int NOT NULL DEFAULT 1` và `DurationSeconds int NULL`.

---

#### Bước 6 — Test (GREEN)

Chạy `dotnet test` — `MediaObjectDomainTests` phải GREEN.

**Acceptance Criteria:**
- [ ] `dotnet build` — 0 error
- [ ] `MediaObjectDomainTests` pass
- [ ] Migration tạo đúng 2 columns
- [ ] Media module vẫn build và test pass (không có regression)

**Dependencies:** Không có

---

## ✅ Checkpoint Phase 0

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet ef database update` — migration apply thành công
- [ ] Review migration file: không có DROP COLUMN ngoài ý muốn

---

## Phase 1: Foundation — Domain + Database

> Tạo toàn bộ building blocks: entities, enums, error codes, repository interfaces, EF config và migration.  
> Không có API endpoint ở phase này — chỉ là foundation cho các slices sau.

---

### Slice 1.1: [Domain] — Post + PostReaction entities + Enums + Error codes

**Module:** Posts (mới)  
**Type:** Domain-only  
**Không có API endpoint**

---

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Posts/PostDomainTests.cs`

```csharp
public class PostDomainTests
{
    // Post.Create
    [Fact]
    public void Create_ShouldSetDefaultsCorrectly()
    {
        var post = Post.Create(ownerId, mediaId, "Hello", PostVisibility.Friends);
        Assert.Equal(PostStatus.Active, post.Status);
        Assert.Null(post.DeletedAtUtc);
        Assert.Equal("Hello", post.Caption);
    }

    // Post.UpdateContent
    [Fact]
    public void UpdateContent_ShouldChangeCaptionAndVisibility()
    {
        var post = Post.Create(...);
        post.UpdateContent("New caption", PostVisibility.Private);
        Assert.Equal("New caption", post.Caption);
        Assert.Equal(PostVisibility.Private, post.Visibility);
    }

    // Post.SoftDelete
    [Fact]
    public void SoftDelete_ShouldSetDeletedAtUtc()
    {
        var post = Post.Create(...);
        post.SoftDelete();
        Assert.NotNull(post.DeletedAtUtc);
    }

    [Fact]
    public void IsDeleted_WhenDeletedAtUtcSet_ReturnsTrue()
    {
        var post = Post.Create(...);
        post.SoftDelete();
        Assert.True(post.IsDeleted);
    }

    // PostReaction.Create
    [Fact]
    public void PostReaction_Create_ShouldSetIconAndTimestamps()
    {
        var reaction = PostReaction.Create(postId, userId, "❤️");
        Assert.Equal("❤️", reaction.Icon);
    }

    // PostReaction.UpdateIcon
    [Fact]
    public void PostReaction_UpdateIcon_ShouldChangeIcon()
    {
        var reaction = PostReaction.Create(postId, userId, "❤️");
        reaction.UpdateIcon("😂");
        Assert.Equal("😂", reaction.Icon);
    }
}
```

→ Test FAIL vì `Post`, `PostReaction`, `PostVisibility`, `PostStatus` chưa tồn tại.

---

#### Bước 2 — Domain: Enums

Files mới:

`src/Beacon.Domain/Enums/PostVisibility.cs`
```csharp
public enum PostVisibility { Friends = 1, Private = 2 }
```

`src/Beacon.Domain/Enums/PostStatus.cs`
```csharp
public enum PostStatus { Active = 1, Hidden = 2 }
```

`src/Beacon.Domain/Enums/ReactionIcon.cs` — **Constants, không phải enum** (vì icon là emoji string):
```csharp
public static class ReactionIcons
{
    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string> { "❤️", "😂", "👍", "😢", "😮" };

    public static bool IsValid(string icon) => Supported.Contains(icon);
}
```

---

#### Bước 3 — Domain: Post entity

File: `src/Beacon.Domain/Entities/Posts/Post.cs`

```csharp
// Kế thừa AuditableEntity (CreatedAtUtc + UpdatedAtUtc)
// Soft delete tự manage bằng DeletedAtUtc nullable (không kế thừa SoftDeletableEntity)
public class Post : AuditableEntity
{
    public Guid OwnerUserId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public PostStatus Status { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    // Computed
    public bool IsDeleted => DeletedAtUtc.HasValue;

    protected Post() { }

    public static Post Create(Guid ownerUserId, Guid mediaId,
        string? caption, PostVisibility visibility) => new()
    {
        OwnerUserId = ownerUserId,
        MediaId = mediaId,
        Caption = caption?.Trim(),
        Visibility = visibility,
        Status = PostStatus.Active,
        DeletedAtUtc = null
    };

    // Business method — không chứa business logic trong handler
    public void UpdateContent(string? caption, PostVisibility visibility)
    {
        Caption = caption?.Trim();
        Visibility = visibility;
    }

    public void SoftDelete()
        => DeletedAtUtc = DateTime.UtcNow;
}
```

---

#### Bước 4 — Domain: PostReaction entity

File: `src/Beacon.Domain/Entities/Posts/PostReaction.cs`

```csharp
// Kế thừa AuditableEntity (CreatedAtUtc + UpdatedAtUtc)
public class PostReaction : AuditableEntity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public string Icon { get; private set; } = default!;

    protected PostReaction() { }

    public static PostReaction Create(Guid postId, Guid userId, string icon) => new()
    {
        PostId = postId,
        UserId = userId,
        Icon = icon
    };

    public void UpdateIcon(string newIcon) => Icon = newIcon;
}
```

---

#### Bước 5 — Shared: Error codes

File: `src/Beacon.Shared/Constants/ErrorCodes.cs` — Thêm 2 nested class:

```csharp
public static class Post
{
    public const string POST_NOT_FOUND         = "POST_NOT_FOUND";
    public const string POST_ACCESS_DENIED     = "POST_ACCESS_DENIED";
    public const string POST_UPDATE_DENIED     = "POST_UPDATE_DENIED";
    public const string POST_DELETE_DENIED     = "POST_DELETE_DENIED";
    public const string INVALID_VISIBILITY     = "INVALID_VISIBILITY";
    public const string MEDIA_NOT_READY        = "MEDIA_NOT_READY";
    public const string MEDIA_ACCESS_DENIED    = "MEDIA_ACCESS_DENIED";
    public const string UNSUPPORTED_MEDIA_TYPE = "UNSUPPORTED_MEDIA_TYPE";
    public const string INVALID_VIDEO_DURATION = "INVALID_VIDEO_DURATION";
}

public static class Reaction
{
    public const string INVALID_REACTION_ICON = "INVALID_REACTION_ICON";
    public const string REACTION_CONFLICT     = "REACTION_CONFLICT";
}
```

---

#### Bước 6 — Test (GREEN)

`dotnet test` — `PostDomainTests` phải GREEN.

**Acceptance Criteria:**
- [ ] `dotnet build` — 0 error
- [ ] `PostDomainTests` pass
- [ ] Error codes được định nghĩa đủ

**Dependencies:** Slice 0.1 (MediaStatus)

---

### Slice 1.2: [Domain] — Repository Interfaces

**Module:** Posts  
**Type:** Interface-only (Domain layer)  
**Không có test riêng — các unit test slice sau sẽ mock interfaces này**

---

#### Files cần tạo

`src/Beacon.Domain/IRepository/Posts/IPostRepository.cs`

```csharp
public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Post post, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Feed query — trả về posts của currentUser + bạn bè,
    /// với visibility check và cursor pagination.
    /// </summary>
    Task<List<Post>> GetFeedAsync(
        Guid currentUserId,
        List<Guid> friendIds,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default);
}
```

`src/Beacon.Domain/IRepository/Posts/IPostReactionRepository.cs`

```csharp
public interface IPostReactionRepository
{
    Task<PostReaction?> GetByPostAndUserAsync(
        Guid postId, Guid userId, CancellationToken ct = default);

    Task AddAsync(PostReaction reaction, CancellationToken ct = default);

    void Remove(PostReaction reaction);

    /// <summary>Batch load reactions cho danh sách postIds — tránh N+1.</summary>
    Task<List<PostReaction>> GetByPostIdsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default);

    /// <summary>Batch load myReaction của currentUser cho danh sách postIds.</summary>
    Task<List<PostReaction>> GetByPostIdsForUserAsync(
        IEnumerable<Guid> postIds, Guid userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**Dependencies:** Slice 1.1

---

### Slice 1.3: [Infrastructure] — EF Config + AppDbContext + Migration

**Module:** Posts  
**Type:** Infrastructure + DB  
**Không có API endpoint**

---

#### Bước 1 — EF Config: PostConfiguration

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Posts/PostConfiguration.cs`

```csharp
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OwnerUserId).IsRequired();
        builder.Property(p => p.MediaId).IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.Visibility)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.DeletedAtUtc).IsRequired(false);

        // FK — không cascade delete để giữ lịch sử
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MediaObject>()
            .WithMany()
            .HasForeignKey(p => p.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => new { p.OwnerUserId, p.CreatedAtUtc, p.Id })
            .HasDatabaseName("IX_Posts_OwnerUserId_CreatedAtUtc");

        builder.HasIndex(p => new { p.Status, p.DeletedAtUtc, p.CreatedAtUtc, p.Id })
            .HasDatabaseName("IX_Posts_Feed_Filter");
    }
}
```

---

#### Bước 2 — EF Config: PostReactionConfiguration

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Posts/PostReactionConfiguration.cs`

```csharp
public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.ToTable("PostReactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PostId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Icon).IsRequired().HasMaxLength(10);

        // UNIQUE (PostId, UserId) — đảm bảo 1 user 1 reaction / post
        builder.HasIndex(r => new { r.PostId, r.UserId })
            .IsUnique()
            .HasDatabaseName("UX_PostReactions_PostId_UserId");

        builder.HasIndex(r => new { r.PostId, r.Icon })
            .HasDatabaseName("IX_PostReactions_PostId_Icon");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_PostReactions_UserId");

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

#### Bước 3 — AppDbContext: Thêm DbSets + Soft Delete Filter

File: `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`

```csharp
// Thêm DbSets
public DbSet<Post> Posts => Set<Post>();
public DbSet<PostReaction> PostReactions => Set<PostReaction>();

// Thêm vào OnModelCreating (soft delete filter)
modelBuilder.Entity<Post>()
    .HasQueryFilter(p => p.DeletedAtUtc == null);
```

> ⚠️ Query filter chỉ lọc `DeletedAtUtc == null`. `Status` KHÔNG filter ở EF level — handler tự filter theo context.

---

#### Bước 4 — Migration

```bash
dotnet ef migrations add AddPostsAndPostReactions \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

Review migration: xác nhận tạo `Posts`, `PostReactions` tables với đúng columns, FKs, indexes.

---

#### Bước 5 — Repository Implementations (Stub)

Tạo stub implementations để DI hoạt động:

`src/Beacon.Infrashtructure/Repository/Posts/PostRepository.cs`
`src/Beacon.Infrashtructure/Repository/Posts/PostReactionRepository.cs`

Đăng ký trong `InfrastructureServiceExtensions.cs`:

```csharp
services.AddScoped<IPostRepository, PostRepository>();
services.AddScoped<IPostReactionRepository, PostReactionRepository>();
```

---

**Acceptance Criteria:**
- [ ] `dotnet build` — 0 error
- [ ] `dotnet ef database update` thành công
- [ ] Migration tạo đúng 2 tables với constraints
- [ ] Unique index `UX_PostReactions_PostId_UserId` tồn tại
- [ ] Query filter `DeletedAtUtc == null` được apply trên Posts

**Dependencies:** Slice 1.1, 1.2

---

## ✅ Checkpoint Phase 1

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet ef database update` — thành công
- [ ] `Post` + `PostReaction` entities có đủ domain methods
- [ ] Error codes được định nghĩa
- [ ] Repository interfaces đủ methods

---

## Phase 2: Write Use Cases

> Mỗi slice = 1 Command use case, đi full stack từ test đến controller.

---

### Slice 2.1: [CreatePost] — Tạo post mới

**Module:** Posts  
**Type:** Command  
**Endpoint:** `POST /api/v1/posts`

---

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Posts/CreatePostHandlerTests.cs`

```csharp
public class CreatePostHandlerTests
{
    private readonly Mock<IPostRepository> _postRepo = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepo = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly CreatePostCommandHandler _handler;

    // Setup constructor

    [Fact]
    public async Task Handle_WithValidImageMedia_ReturnsSuccess()
    {
        // Arrange — media Image, Status = Ready
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        _mediaRepo.Setup(r => r.GetByIdAsync(media.Id, default)).ReturnsAsync(media);
        _postRepo.Setup(r => r.AddAsync(It.IsAny<Post>(), default)).Returns(Task.CompletedTask);
        _postRepo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = media.Id, Caption = "Hello", Visibility = "friends" },
            CurrentUserId: media.UploadProviderByUserId!.Value);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(media.Id, result.Value.Media.Id);
    }

    [Fact]
    public async Task Handle_WhenMediaNotFound_ReturnsNotFound()
    {
        _mediaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((MediaObject?)null);
        // Assert result.Error.Code == "MEDIA_NOT_FOUND" && ErrorType.NotFound
    }

    [Fact]
    public async Task Handle_WhenMediaOwnedByOtherUser_ReturnsForbidden()
    { /* ... */ }

    [Fact]
    public async Task Handle_WhenMediaStatusUploading_ReturnsFailure()
    { /* ... ErrorCode = MEDIA_NOT_READY */ }

    [Fact]
    public async Task Handle_WhenVideoTooShort_ReturnsFailure()
    {
        // DurationSeconds = 3 → INVALID_VIDEO_DURATION
    }

    [Fact]
    public async Task Handle_WhenVideoTooLong_ReturnsFailure()
    {
        // DurationSeconds = 15 → INVALID_VIDEO_DURATION
    }

    [Fact]
    public async Task Handle_WhenVideoDurationNull_ReturnsFailure()
    {
        // DurationSeconds = null → INVALID_VIDEO_DURATION
    }
}
```

---

#### Bước 2 — DTOs

`src/Beacon.Application/Features/Posts/Dtos/CreatePostRequest.cs`

```csharp
public record CreatePostRequest
{
    public Guid MediaId { get; init; }
    public string? Caption { get; init; }
    public string? Visibility { get; init; }  // "friends" | "private"
}
```

`src/Beacon.Application/Features/Posts/Dtos/PostResponse.cs`

```csharp
public record PostResponse
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public MediaInPostResponse Media { get; init; } = default!;
    public string? Caption { get; init; }
    public string Visibility { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public record MediaInPostResponse
{
    public Guid Id { get; init; }
    public string Url { get; init; } = default!;
    public string Type { get; init; } = default!;   // "image" | "video"
    public string? ThumbnailUrl { get; init; }
    public int? DurationSeconds { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}
```

---

#### Bước 3 — Command + Handler

`src/Beacon.Application/Features/Posts/Commands/CreatePost/CreatePostCommand.cs`

```csharp
public record CreatePostCommand(
    CreatePostRequest Request,
    Guid CurrentUserId
) : IRequest<Result<PostResponse>>;
```

`src/Beacon.Application/Features/Posts/Commands/CreatePost/CreatePostCommandHandler.cs`

Handler orchestrate theo thứ tự:

1. Parse visibility → nếu không hợp lệ: `Result.Failure(Error.Validation(INVALID_VISIBILITY, ...))`
2. `mediaRepo.GetByIdAsync(mediaId)` → null: `NotFound(MEDIA_NOT_FOUND)`
3. `media.UploadProviderByUserId != currentUserId` → `Forbidden(MEDIA_ACCESS_DENIED)`
4. `!media.IsReadyForPost()` → `Failure(MEDIA_NOT_READY)`
5. `media.MediaType not in (Image, Video)` → `Failure(UNSUPPORTED_MEDIA_TYPE)`
6. Video check: `DurationSeconds == null || < 5 || > 10` → `Failure(INVALID_VIDEO_DURATION)`
7. `Post.Create(...)` — domain factory
8. `postRepo.AddAsync(post)` + `postRepo.SaveChangesAsync()`
9. Build `MediaInPostResponse` — URL từ `IStorageService` hoặc `IMediaUrlService`
10. Return `Result.Success(_mapper.ToPostResponse(post, mediaResponse))`

> **IStorageService:** Posts module cần tạo URL từ `ObjectKey`. Dùng `IStorageService` (đã có) để lấy URL — inject vào handler.

---

#### Bước 4 — Validator

`src/Beacon.Application/Features/Posts/Validators/CreatePostCommandValidator.cs`

```csharp
public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Request.MediaId)
            .NotEmpty().WithMessage("MediaId không được để trống.");

        RuleFor(x => x.Request.Caption)
            .MaximumLength(500)
            .WithMessage("Caption không được vượt quá 500 ký tự.")
            .When(x => x.Request.Caption is not null);

        RuleFor(x => x.Request.Visibility)
            .Must(v => v is null or "friends" or "private")
            .WithMessage("Visibility phải là 'friends' hoặc 'private'.");
    }
}
```

---

#### Bước 5 — Mapper

`src/Beacon.Application/Mappings/Posts/PostDtoMapper.cs`

```csharp
public sealed class PostDtoMapper
{
    public PostResponse ToPostResponse(Post post, MediaInPostResponse media) => new()
    {
        Id         = post.Id,
        OwnerUserId = post.OwnerUserId,
        Media       = media,
        Caption     = post.Caption,
        Visibility  = post.Visibility.ToString().ToLowerInvariant(),
        Status      = post.Status.ToString().ToLowerInvariant(),
        CreatedAtUtc = post.CreatedAtUtc,
        UpdatedAtUtc = post.UpdatedAtUtc
    };

    public MediaInPostResponse ToMediaResponse(MediaObject media, string url, string? thumbnailUrl) => new()
    {
        Id              = media.Id,
        Url             = url,
        Type            = media.MediaType.ToString().ToLowerInvariant(),
        ThumbnailUrl    = thumbnailUrl,
        DurationSeconds = media.DurationSeconds,
        Width           = media.Width,
        Height          = media.Height
    };
}
```

Đăng ký Singleton trong `ApplicationServiceExtensions.cs`:

```csharp
services.AddSingleton<PostDtoMapper>();
```

---

#### Bước 6 — Repository Implementation (CreatePost)

`src/Beacon.Infrashtructure/Repository/Posts/PostRepository.cs`

```csharp
public class PostRepository(AppDbContext db) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Post post, CancellationToken ct)
        => await db.Posts.AddAsync(post, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);

    // GetFeedAsync — implement ở Slice 3.2
    public Task<List<Post>> GetFeedAsync(...) => throw new NotImplementedException();
}
```

---

#### Bước 7 — Controller (PostsController)

File mới: `src/Beacon.Api/Controllers/Posts/PostsController.cs`

```csharp
[Route("api/v1/posts")]
[Authorize]
public class PostsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Tạo một post mới với media đã upload.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Tạo post thành công.
    /// - <c>VALIDATION_ERROR</c>: Request không hợp lệ.
    /// - <c>INVALID_VISIBILITY</c>: Visibility không hợp lệ.
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại.
    /// - <c>MEDIA_ACCESS_DENIED</c>: Media không thuộc về user.
    /// - <c>MEDIA_NOT_READY</c>: Media chưa sẵn sàng.
    /// - <c>UNSUPPORTED_MEDIA_TYPE</c>: Media type không hỗ trợ.
    /// - <c>INVALID_VIDEO_DURATION</c>: Video không đúng 5–10 giây.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePostRequest request, CancellationToken ct)
    {
        var command = new CreatePostCommand(request, currentUser.UserId);
        return CreatedResult("api/v1/posts", await mediator.Send(command, ct));
    }
}
```

---

#### Bước 8 — Test (GREEN)

`CreatePostHandlerTests` phải GREEN. Thêm integration test:

`tests/Beacon.IntergrationTests/Posts/PostsControllerTests.cs`

```csharp
public class PostsControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<...>
{
    [Fact]
    public async Task CreatePost_WithValidRequest_Returns201()

    [Fact]
    public async Task CreatePost_WithoutToken_Returns401()

    [Fact]
    public async Task CreatePost_WithNonExistentMedia_Returns404()

    [Fact]
    public async Task CreatePost_WithOtherUserMedia_Returns403()

    [Fact]
    public async Task CreatePost_WithVideoTooShort_Returns400()
}
```

**Acceptance Criteria:**
- [ ] Unit tests GREEN (tất cả success + failure cases)
- [ ] Integration test: 201 / 400 / 401 / 403 / 404 pass
- [ ] Response shape đúng `ApiResponse<PostResponse>`
- [ ] Posts module không gọi MinIO trực tiếp

**Dependencies:** Slice 0.1, 1.1, 1.2, 1.3

---

### Slice 2.2: [UpdatePost] — Cập nhật caption và visibility

**Module:** Posts  
**Type:** Command  
**Endpoint:** `PATCH /api/v1/posts/{postId:guid}`

---

#### Bước 1 — Test (RED)

File: `tests/Beacon.UnitTests/Posts/UpdatePostHandlerTests.cs`

```csharp
[Fact]
public async Task Handle_WhenOwnerUpdates_ReturnsSuccess()

[Fact]
public async Task Handle_WhenPostNotFound_ReturnsNotFound()

[Fact]
public async Task Handle_WhenNonOwnerUpdates_ReturnsForbidden()
// Error.Forbidden(POST_UPDATE_DENIED)

[Fact]
public async Task Handle_WhenPostIsDeleted_ReturnsNotFound()
// soft deleted post → không tìm thấy (EF filter loại ra)
```

---

#### Bước 2 — DTOs

`src/Beacon.Application/Features/Posts/Dtos/UpdatePostRequest.cs`

```csharp
public record UpdatePostRequest
{
    public string? Caption { get; init; }
    public string? Visibility { get; init; }
}
```

---

#### Bước 3 — Command + Handler

`UpdatePostCommand(Guid PostId, UpdatePostRequest Request, Guid CurrentUserId)`

Handler:
1. `postRepo.GetByIdAsync(postId)` → null: `NotFound(POST_NOT_FOUND)`
2. `post.OwnerUserId != currentUserId` → `Forbidden(POST_UPDATE_DENIED)`
3. Parse visibility nếu có → validate
4. `post.UpdateContent(caption, visibility)` — domain method
5. `postRepo.SaveChangesAsync()`
6. Build và return `PostResponse`

---

#### Bước 4 — Validator

`UpdatePostCommandValidator` — validate caption length, visibility value

---

#### Bước 5 — Mapper

Dùng `PostDtoMapper.ToPostResponse` đã có từ Slice 2.1.

---

#### Bước 6 — Repository: UpdateAsync

Thêm `UpdateAsync` vào `PostRepository` (chỉ cần `SaveChangesAsync` vì EF tracking):

```csharp
// Không cần method riêng — EF change tracking tự detect thay đổi trên entity
// SaveChangesAsync đã đủ
```

---

#### Bước 7 — Controller Action

Thêm vào `PostsController`:

```csharp
[HttpPatch("{postId:guid}")]
public async Task<IActionResult> Update(Guid postId,
    [FromBody] UpdatePostRequest request, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new UpdatePostCommand(postId, request, currentUser.UserId), ct));
```

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN
- [ ] Integration: 200 / 400 / 401 / 403 / 404
- [ ] `updatedAtUtc` được cập nhật
- [ ] Không cho update `mediaId`, `ownerId`, `createdAtUtc`

**Dependencies:** Slice 2.1

---

### Slice 2.3: [DeletePost] — Xóa post (soft delete)

**Module:** Posts  
**Type:** Command  
**Endpoint:** `DELETE /api/v1/posts/{postId:guid}`

---

#### Bước 1 — Test (RED)

```csharp
[Fact]
public async Task Handle_WhenOwnerDeletes_SetsDeletedAtUtc()

[Fact]
public async Task Handle_WhenPostNotFound_ReturnsNotFound()

[Fact]
public async Task Handle_WhenNonOwnerDeletes_ReturnsForbidden()
// POST_DELETE_DENIED
```

---

#### Bước 2 — Command + Handler

`DeletePostCommand(Guid PostId, Guid CurrentUserId)`

Handler:
1. `postRepo.GetByIdAsync(postId)` → null: `NotFound(POST_NOT_FOUND)`
2. `post.OwnerUserId != currentUserId` → `Forbidden(POST_DELETE_DENIED)`
3. `post.SoftDelete()` — domain method
4. `postRepo.SaveChangesAsync()`
5. `Result.Success<object?>(null)` — data null, message "Xóa post thành công"

---

#### Bước 3 — Controller Action

```csharp
[HttpDelete("{postId:guid}")]
public async Task<IActionResult> Delete(Guid postId, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new DeletePostCommand(postId, currentUser.UserId), ct));
```

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN
- [ ] `DeletedAtUtc` được set sau khi xóa
- [ ] Post đã xóa không xuất hiện ở các query sau
- [ ] `PostReactions` giữ nguyên sau khi soft delete
- [ ] Integration: 200 / 401 / 403 / 404

**Dependencies:** Slice 2.1

---

## ✅ Checkpoint Phase 2

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test` — tất cả GREEN
- [ ] Ba endpoints CRUD hoạt động đúng
- [ ] Soft delete filter hoạt động — post đã xóa ẩn hoàn toàn

---

## Phase 3: Read Use Cases

---

### Slice 3.1: [GetPostDetail] — Xem chi tiết một post

**Module:** Posts  
**Type:** Query  
**Endpoint:** `GET /api/v1/posts/{postId:guid}`

---

#### Bước 1 — Test (RED)

File: `tests/Beacon.UnitTests/Posts/GetPostDetailHandlerTests.cs`

```csharp
[Fact]
public async Task Handle_WhenOwnerFetches_ReturnsPost()

[Fact]
public async Task Handle_WhenFriendFetchesFriendsPost_ReturnsPost()

[Fact]
public async Task Handle_WhenStrangerFetchesPrivatePost_ReturnsForbidden()
// POST_ACCESS_DENIED

[Fact]
public async Task Handle_WhenPostNotFound_ReturnsNotFound()

[Fact]
public async Task Handle_WhenFriendFetchesFriendVisibilityPost_ReturnsPost()

[Fact]
public async Task Handle_WhenFriendFetchesPrivatePost_ReturnsForbidden()
```

---

#### Bước 2 — DTOs

`src/Beacon.Application/Features/Posts/Dtos/PostDetailResponse.cs` (extend `PostResponse`):

```csharp
public record PostDetailResponse : PostResponse
{
    public OwnerInPostResponse Owner { get; init; } = default!;
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
    public MyReactionResponse? MyReaction { get; init; }
}

public record OwnerInPostResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = default!;
    public string? AvatarUrl { get; init; }
}

public record PostReactionSummaryResponse
{
    public int TotalCount { get; init; }
    public Dictionary<string, int> Icons { get; init; } = new();
}

public record MyReactionResponse { public string Icon { get; init; } = default!; }
```

---

#### Bước 3 — Query + Handler

`GetPostDetailQuery(Guid PostId, Guid CurrentUserId)`

Handler:
1. `postRepo.GetByIdAsync(postId)` — EF filter đã loại deleted posts → null: `NotFound(POST_NOT_FOUND)`
2. `post.Status != Active` → `NotFound(POST_NOT_FOUND)` (không lộ sự tồn tại)
3. Access control:
   - `post.OwnerUserId == currentUserId` → allow
   - `post.Visibility == Friends` → `friendRepo.AreFriendsAsync(currentUserId, post.OwnerUserId)` → true: allow
   - Else → `Forbidden(POST_ACCESS_DENIED)`
4. Fetch owner info từ `IUserRepository`
5. Build `MediaInPostResponse` với URL từ storage service
6. Fetch reaction summary (xem helper dưới)
7. Return `PostDetailResponse`

---

#### Bước 4 — Reaction Summary Helper

Tạo helper/service để tính reaction summary (dùng lại ở Slice 3.2 — GetFeed):

`src/Beacon.Application/Features/Posts/Helpers/ReactionSummaryHelper.cs`

```csharp
public static class ReactionSummaryHelper
{
    public static PostReactionSummaryResponse BuildSummary(
        IEnumerable<PostReaction> reactions)
    {
        var icons = reactions
            .GroupBy(r => r.Icon)
            .ToDictionary(g => g.Key, g => g.Count());
        return new PostReactionSummaryResponse
        {
            TotalCount = reactions.Count(),
            Icons = icons
        };
    }
}
```

---

#### Bước 5 — Repository: GetByIdAsync (đã có)

Không cần method mới. Cần thêm query cho owner info vào `IUserRepository` nếu chưa có method lấy display name.

---

#### Bước 6 — Controller Action

```csharp
[HttpGet("{postId:guid}")]
public async Task<IActionResult> GetById(Guid postId, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new GetPostDetailQuery(postId, currentUser.UserId), ct));
```

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN cho tất cả access control scenarios
- [ ] Integration: 200 / 401 / 403 / 404
- [ ] `reactionSummary` và `myReaction` được trả về
- [ ] Không lộ post đã xóa hoặc private post của người khác

**Dependencies:** Slice 1.1, 1.2, 1.3, 2.1 (PostsController file)

---

### Slice 3.2: [GetFeed] — Lấy feed cursor pagination

**Module:** Posts  
**Type:** Query  
**Endpoint:** `GET /api/v1/posts/feed?cursor=&limit=20`

> Slice phức tạp nhất — cursor pagination + visibility filter + batch reactions.

---

#### Bước 1 — Test (RED)

File: `tests/Beacon.UnitTests/Posts/GetFeedHandlerTests.cs`

```csharp
[Fact]
public async Task Handle_ReturnsOwnPosts_IncludingPrivate()
// Feed của user phải gồm cả private posts của chính mình

[Fact]
public async Task Handle_ReturnsFriendPosts_OnlyFriendsVisibility()
// Không trả private posts của bạn bè

[Fact]
public async Task Handle_ExcludesDeletedPosts()

[Fact]
public async Task Handle_ExcludesHiddenPosts()

[Fact]
public async Task Handle_WithCursor_ReturnsNextPage()
// nextCursor không null khi còn item

[Fact]
public async Task Handle_WhenNoMoreItems_ReturnsNullCursor()

[Fact]
public async Task Handle_WithLimitOverMax_ClampsTo50()
// limit = 100 → trả đúng 50 items hoặc ít hơn

[Fact]
public async Task Handle_ReactionSummary_NoBatchNPlusOne()
// Verify chỉ có 2 queries reaction (batch), không phải N queries
```

---

#### Bước 2 — DTOs

`src/Beacon.Application/Features/Posts/Dtos/FeedPostResponse.cs`

```csharp
public record FeedPostResponse
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public OwnerInPostResponse Owner { get; init; } = default!;
    public MediaInPostResponse Media { get; init; } = default!;
    public string? Caption { get; init; }
    public string Visibility { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
    public MyReactionResponse? MyReaction { get; init; }
}

public record FeedResponse
{
    public List<FeedPostResponse> Items { get; init; } = new();
    public string? NextCursor { get; init; }
}
```

---

#### Bước 3 — Cursor Helper

`src/Beacon.Application/Features/Posts/Helpers/FeedCursorHelper.cs`

```csharp
public static class FeedCursorHelper
{
    private record CursorData(DateTime CreatedAt, Guid Id);

    public static (DateTime? createdAt, Guid? id) Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return (null, null);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var data = JsonSerializer.Deserialize<CursorData>(json)!;
        return (data.CreatedAt, data.Id);
    }

    public static string Encode(DateTime createdAt, Guid id)
    {
        var json = JsonSerializer.Serialize(new CursorData(createdAt, id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
```

---

#### Bước 4 — Query + Handler

`GetFeedQuery(Guid CurrentUserId, string? Cursor, int Limit)`

Handler:
1. Clamp `limit`: `Math.Clamp(limit, 1, 50)`
2. Decode cursor → `(cursorCreatedAt, cursorId)`
3. `friendRepo.ListFriendIdsAsync(currentUserId)` — lấy tất cả friend IDs
4. `postRepo.GetFeedAsync(currentUserId, friendIds, cursorCreatedAt, cursorId, limit + 1)` — lấy limit+1 để detect hasMore
5. `hasMore = posts.Count > limit` → slice posts về `limit` items
6. Batch reaction query:
   - `postIds = posts.Select(p => p.Id)`
   - `allReactions = reactionRepo.GetByPostIdsAsync(postIds)`
   - `myReactions = reactionRepo.GetByPostIdsForUserAsync(postIds, currentUserId)`
7. Map từng post → `FeedPostResponse` với owner, media URL, reaction summary
8. Compute `nextCursor = hasMore ? FeedCursorHelper.Encode(lastPost.CreatedAtUtc, lastPost.Id) : null`
9. Return `FeedResponse`

---

#### Bước 5 — Repository: GetFeedAsync

Implement trong `PostRepository`:

```csharp
public async Task<List<Post>> GetFeedAsync(
    Guid currentUserId, List<Guid> friendIds,
    DateTime? cursorCreatedAt, Guid? cursorId,
    int limit, CancellationToken ct)
{
    var query = db.Posts
        .Where(p => p.Status == PostStatus.Active)
        .Where(p =>
            p.OwnerUserId == currentUserId
            || (friendIds.Contains(p.OwnerUserId) && p.Visibility == PostVisibility.Friends))
        .AsQueryable();

    if (cursorCreatedAt.HasValue && cursorId.HasValue)
        query = query.Where(p =>
            p.CreatedAtUtc < cursorCreatedAt.Value
            || (p.CreatedAtUtc == cursorCreatedAt.Value && p.Id.CompareTo(cursorId.Value) < 0));

    return await query
        .OrderByDescending(p => p.CreatedAtUtc)
        .ThenByDescending(p => p.Id)
        .Take(limit)
        .ToListAsync(ct);
}
```

> **Lưu ý:** EF global filter `DeletedAtUtc == null` đã được apply — không cần filter thủ công.

---

#### Bước 6 — PostReactionRepository: Batch methods

`src/Beacon.Infrashtructure/Repository/Posts/PostReactionRepository.cs`

```csharp
public Task<List<PostReaction>> GetByPostIdsAsync(IEnumerable<Guid> postIds, CancellationToken ct)
    => db.PostReactions
        .Where(r => postIds.Contains(r.PostId))
        .ToListAsync(ct);

public Task<List<PostReaction>> GetByPostIdsForUserAsync(
    IEnumerable<Guid> postIds, Guid userId, CancellationToken ct)
    => db.PostReactions
        .Where(r => postIds.Contains(r.PostId) && r.UserId == userId)
        .ToListAsync(ct);
```

---

#### Bước 7 — Controller Action

```csharp
[HttpGet("feed")]
public async Task<IActionResult> GetFeed(
    [FromQuery] string? cursor,
    [FromQuery] int limit = 20,
    CancellationToken ct = default)
    => HandleResult(await mediator.Send(
        new GetFeedQuery(currentUser.UserId, cursor, limit), ct));
```

> ⚠️ `[HttpGet("feed")]` phải đặt **trước** `[HttpGet("{postId:guid}")]` trong controller để không bị route conflict.

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN — bao gồm visibility, cursor, clamp, batch reactions
- [ ] Integration: feed trả đúng posts theo visibility rules
- [ ] Cursor pagination: không skip/duplicate khi nhiều post cùng `createdAtUtc`
- [ ] Reaction summary: chỉ 2 DB queries (batch), không N+1
- [ ] `nextCursor = null` khi hết data

**Dependencies:** Slice 1.1, 1.2, 1.3, 3.1 (helper classes)

---

## ✅ Checkpoint Phase 3

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test` — tất cả GREEN
- [ ] Feed trả đúng visibility rules
- [ ] Cursor pagination ổn định
- [ ] Batch reaction query hoạt động (verify bằng SQL log trong integration test)

---

## Phase 4: Reactions

---

### Slice 4.1: [UpsertReaction] — Tạo hoặc cập nhật reaction

**Module:** Posts  
**Type:** Command (Upsert)  
**Endpoint:** `PUT /api/v1/posts/{postId:guid}/reaction`

---

#### Bước 1 — Test (RED)

File: `tests/Beacon.UnitTests/Posts/UpsertPostReactionHandlerTests.cs`

```csharp
[Fact]
public async Task Handle_WhenNoExistingReaction_CreatesNew()
// existing = null → AddAsync được gọi

[Fact]
public async Task Handle_WhenExistingReactionDifferentIcon_UpdatesIcon()
// existing.Icon = "❤️", request.Icon = "😂" → UpdateIcon được gọi

[Fact]
public async Task Handle_WhenExistingReactionSameIcon_NoOp()
// existing.Icon = "❤️", request.Icon = "❤️" → SaveChanges không được gọi

[Fact]
public async Task Handle_WhenPostNotFound_ReturnsNotFound()

[Fact]
public async Task Handle_WhenPostDeleted_ReturnsNotFound()

[Fact]
public async Task Handle_WhenUserHasNoPermissionToViewPost_ReturnsForbidden()

[Fact]
public async Task Handle_WhenIconInvalid_ReturnsFailure()
// "🎉" → INVALID_REACTION_ICON
```

---

#### Bước 2 — DTOs

`src/Beacon.Application/Features/Posts/Dtos/UpsertPostReactionRequest.cs`

```csharp
public record UpsertPostReactionRequest { public string Icon { get; init; } = default!; }
```

`src/Beacon.Application/Features/Posts/Dtos/PostReactionResponse.cs`

```csharp
public record PostReactionResponse
{
    public Guid PostId { get; init; }
    public MyReactionResponse? MyReaction { get; init; }
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
}
```

---

#### Bước 3 — Command + Handler

`UpsertPostReactionCommand(Guid PostId, string Icon, Guid CurrentUserId)`

Handler:
1. Validate `ReactionIcons.IsValid(icon)` → false: `Failure(INVALID_REACTION_ICON)`
2. `postRepo.GetByIdAsync(postId)` → null: `NotFound(POST_NOT_FOUND)`
3. `post.Status != Active` → `NotFound(POST_NOT_FOUND)`
4. Access control (giống GetPostDetail):
   - owner: allow
   - `visibility == Friends && AreFriendsAsync`: allow
   - else: `Forbidden(POST_ACCESS_DENIED)`
5. `reactionRepo.GetByPostAndUserAsync(postId, currentUserId)` → upsert logic
6. `reactionRepo.SaveChangesAsync()` — **chỉ khi có thay đổi**
7. Tính reaction summary (batch query cho 1 post)
8. Return `PostReactionResponse`

---

#### Bước 4 — Validator

`UpsertPostReactionCommandValidator`:

```csharp
RuleFor(x => x.Icon)
    .NotEmpty().WithMessage("Icon không được để trống.")
    .Must(ReactionIcons.IsValid)
    .WithMessage("Icon không hợp lệ. Chỉ hỗ trợ: ❤️ 😂 👍 😢 😮");
```

---

#### Bước 5 — PostReactionRepository: GetByPostAndUserAsync + AddAsync + Remove

```csharp
public Task<PostReaction?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct)
    => db.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

public async Task AddAsync(PostReaction reaction, CancellationToken ct)
    => await db.PostReactions.AddAsync(reaction, ct);

public void Remove(PostReaction reaction) => db.PostReactions.Remove(reaction);

public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
```

---

#### Bước 6 — Controller Action

Thêm controller mới hoặc thêm action vào `PostsController`:

```csharp
[HttpPut("{postId:guid}/reaction")]
public async Task<IActionResult> UpsertReaction(
    Guid postId,
    [FromBody] UpsertPostReactionRequest request,
    CancellationToken ct)
    => HandleResult(await mediator.Send(
        new UpsertPostReactionCommand(postId, request.Icon, currentUser.UserId), ct));
```

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN — tạo mới / update / no-op / invalid icon / post not found / access denied
- [ ] Unique constraint DB đảm bảo không duplicate reaction
- [ ] No-op: không gọi `SaveChanges` khi icon giống nhau
- [ ] `reactionSummary` chính xác trong response
- [ ] Integration: 200 / 400 / 401 / 403 / 404

**Dependencies:** Slice 1.1, 1.2, 1.3, 3.1 (access control pattern)

---

### Slice 4.2: [DeleteReaction] — Xóa reaction (idempotent)

**Module:** Posts  
**Type:** Command  
**Endpoint:** `DELETE /api/v1/posts/{postId:guid}/reaction`

---

#### Bước 1 — Test (RED)

```csharp
[Fact]
public async Task Handle_WhenReactionExists_DeletesAndReturnsSummary()

[Fact]
public async Task Handle_WhenReactionNotExists_ReturnsSuccessIdempotent()
// Không có reaction → vẫn trả 200, myReaction = null

[Fact]
public async Task Handle_WhenPostNotFound_ReturnsNotFound()

[Fact]
public async Task Handle_WhenNoPermissionToViewPost_ReturnsForbidden()
```

---

#### Bước 2 — Command + Handler

`DeletePostReactionCommand(Guid PostId, Guid CurrentUserId)`

Handler:
1. `postRepo.GetByIdAsync(postId)` → null: `NotFound(POST_NOT_FOUND)`
2. `post.Status != Active` → `NotFound(POST_NOT_FOUND)`
3. Access control (như trên)
4. `reactionRepo.GetByPostAndUserAsync(postId, currentUserId)` → nếu có: `Remove` + `SaveChangesAsync`
5. Nếu không có reaction → no-op (idempotent)
6. Tính lại reaction summary
7. Return `PostReactionResponse { MyReaction = null, ... }`

---

#### Bước 3 — Controller Action

```csharp
[HttpDelete("{postId:guid}/reaction")]
public async Task<IActionResult> DeleteReaction(Guid postId, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new DeletePostReactionCommand(postId, currentUser.UserId), ct));
```

---

**Acceptance Criteria:**
- [ ] Unit tests GREEN — có reaction / không có reaction / not found / forbidden
- [ ] Idempotent: gọi DELETE nhiều lần → luôn 200
- [ ] `myReaction = null` trong response
- [ ] Integration: 200 / 401 / 403 / 404

**Dependencies:** Slice 4.1

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] 7 endpoints hoạt động đúng theo spec
- [ ] Swagger: tất cả endpoints hiển thị đúng route và auth requirement
- [ ] Không có raw DbContext inject trong Application layer
- [ ] Tất cả handler trả `Result<T>` — không throw cho business failure
- [ ] Mọi error message bằng tiếng Việt
- [ ] `code-reviewer`: review toàn bộ Posts module
- [ ] `security-auditor`: audit auth + visibility checks
- [ ] `test-engineer`: verify coverage ≥ 70% Application layer

---

## Tóm tắt Files cần tạo mới

### Domain (Beacon.Domain)
```
Enums/
  MediaStatus.cs                           ← Slice 0.1
  PostVisibility.cs                        ← Slice 1.1
  PostStatus.cs                            ← Slice 1.1
  ReactionIcons.cs                         ← Slice 1.1
Entities/Posts/
  Post.cs                                  ← Slice 1.1
  PostReaction.cs                          ← Slice 1.1
IRepository/Posts/
  IPostRepository.cs                       ← Slice 1.2
  IPostReactionRepository.cs               ← Slice 1.2
```

### Application (Beacon.Application)
```
Features/Posts/Commands/
  CreatePost/CreatePostCommand.cs          ← Slice 2.1
  CreatePost/CreatePostCommandHandler.cs   ← Slice 2.1
  UpdatePost/UpdatePostCommand.cs          ← Slice 2.2
  UpdatePost/UpdatePostCommandHandler.cs   ← Slice 2.2
  DeletePost/DeletePostCommand.cs          ← Slice 2.3
  DeletePost/DeletePostCommandHandler.cs   ← Slice 2.3
  UpsertReaction/...                       ← Slice 4.1
  DeleteReaction/...                       ← Slice 4.2
Features/Posts/Queries/
  GetPostDetail/GetPostDetailQuery.cs      ← Slice 3.1
  GetPostDetail/GetPostDetailQueryHandler.cs
  GetFeed/GetFeedQuery.cs                  ← Slice 3.2
  GetFeed/GetFeedQueryHandler.cs
Features/Posts/Validators/
  CreatePostCommandValidator.cs            ← Slice 2.1
  UpdatePostCommandValidator.cs            ← Slice 2.2
  UpsertPostReactionCommandValidator.cs    ← Slice 4.1
Features/Posts/Dtos/
  CreatePostRequest.cs                     ← Slice 2.1
  UpdatePostRequest.cs                     ← Slice 2.2
  UpsertPostReactionRequest.cs             ← Slice 4.1
  PostResponse.cs                          ← Slice 2.1
  PostDetailResponse.cs                    ← Slice 3.1
  FeedPostResponse.cs                      ← Slice 3.2
  FeedResponse.cs                          ← Slice 3.2
  PostReactionResponse.cs                  ← Slice 4.1
  MediaInPostResponse.cs                   ← Slice 2.1
  OwnerInPostResponse.cs                   ← Slice 3.1
  PostReactionSummaryResponse.cs           ← Slice 3.1
  MyReactionResponse.cs                    ← Slice 3.1
Features/Posts/Helpers/
  ReactionSummaryHelper.cs                 ← Slice 3.1
  FeedCursorHelper.cs                      ← Slice 3.2
Mappings/Posts/
  PostDtoMapper.cs                         ← Slice 2.1
```

### Infrastructure (Beacon.Infrashtructure)
```
Presistence/Configuration/Posts/
  PostConfiguration.cs                     ← Slice 1.3
  PostReactionConfiguration.cs             ← Slice 1.3
Repository/Posts/
  PostRepository.cs                        ← Slice 1.3 + 2.1 + 3.2
  PostReactionRepository.cs               ← Slice 1.3 + 4.1
Migrations/
  {timestamp}_AddMediaStatusAndDuration.cs ← Slice 0.1
  {timestamp}_AddPostsAndPostReactions.cs  ← Slice 1.3
```

### API (Beacon.Api)
```
Controllers/Posts/
  PostsController.cs                       ← Slice 2.1 (+ actions thêm dần)
```

### Tests
```
Beacon.UnitTests/Posts/
  PostDomainTests.cs                       ← Slice 1.1
  CreatePostHandlerTests.cs                ← Slice 2.1
  UpdatePostHandlerTests.cs                ← Slice 2.2
  DeletePostHandlerTests.cs                ← Slice 2.3
  GetPostDetailHandlerTests.cs             ← Slice 3.1
  GetFeedHandlerTests.cs                   ← Slice 3.2
  UpsertPostReactionHandlerTests.cs        ← Slice 4.1
  DeletePostReactionHandlerTests.cs        ← Slice 4.2
  MediaObjectDomainTests.cs               ← Slice 0.1
Beacon.IntergrationTests/Posts/
  PostsControllerTests.cs                  ← Slice 2.1 → 4.2
```

### Shared (Beacon.Shared)
```
Constants/ErrorCodes.cs  ← thêm class Post + Reaction  ← Slice 1.1
```

---

## Files cần sửa

| File | Slice | Thay đổi |
|---|---|---|
| `Domain/Entities/Storage/MediaObject.cs` | 0.1 | Thêm `Status`, `DurationSeconds`, `IsReadyForPost()` |
| `Infrashtructure/Presistence/Configuration/Storage/MediaObjectConfiguration.cs` | 0.1 | Thêm config 2 fields mới |
| `Infrashtructure/Presistence/AppDbContext.cs` | 1.3 | Thêm `DbSet<Post>`, `DbSet<PostReaction>`, soft delete filter |
| `Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs` | 1.3 | Đăng ký `IPostRepository`, `IPostReactionRepository` |
| `Application/DependencyInjection/ApplicationServiceExtensions.cs` | 2.1 | Đăng ký `PostDtoMapper` Singleton |
| `Shared/Constants/ErrorCodes.cs` | 1.1 | Thêm class `Post` + `Reaction` |
