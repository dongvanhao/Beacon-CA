# Technical Specification: Posts, Feed và Post Reactions

**Version:** 1.0  
**Ngày:** 2026-05-16  
**Tác giả:** Senior Backend Engineer  
**Module:** Posts (Beacon)

---

## Mục lục

1. [Overview](#1-overview)
2. [Media Integration](#2-media-integration)
3. [Database Design](#3-database-design)
4. [Business Rules](#4-business-rules)
5. [API Specification](#5-api-specification)
6. [Query Logic / Pseudo SQL](#6-query-logic--pseudo-sql)
7. [Edge Cases](#7-edge-cases)
8. [Acceptance Criteria Summary](#8-acceptance-criteria-summary)
9. [Suggested Implementation Notes for .NET](#9-suggested-implementation-notes-for-net)
10. [Suggested Ticket Breakdown](#10-suggested-ticket-breakdown)

---

## 1. Overview

### 1.1 Mục tiêu

Feature Posts, Feed và Post Reactions cho phép user đăng bài theo phong cách Locket — mỗi bài gắn với 1 ảnh hoặc video ngắn — và tương tác với bài đăng của bạn bè thông qua reaction icon.

### 1.2 Phạm vi (In Scope)

- Tạo post gắn với 1 media (ảnh hoặc video ngắn 5–10 giây).
- Lấy feed theo dạng cursor pagination: post của bản thân và bạn bè.
- Xem chi tiết một post.
- Cập nhật caption và visibility của post.
- Xóa post (soft delete).
- Reaction post: tạo, cập nhật, xóa reaction theo icon.
- Mỗi user chỉ có 1 reaction (icon) trên mỗi post.

### 1.3 Ngoài phạm vi (Out of Scope)

- Upload file trực tiếp trong Posts module — đã xử lý bởi Media module (MinIO).
- Post với nhiều media (multi-media post) — để lại cho giai đoạn sau.
- Comment / reply trên post.
- Share / repost.
- Push notification khi có reaction.
- Quản lý reaction icons động (dynamic icon management).
- Admin moderation cho post.

### 1.4 Các API chính

| Method | Endpoint | Mô tả |
|---|---|---|
| `POST` | `/api/v1/posts` | Tạo post mới |
| `GET` | `/api/v1/posts/feed` | Lấy feed (cursor pagination) |
| `GET` | `/api/v1/posts/{postId:guid}` | Xem chi tiết post |
| `PATCH` | `/api/v1/posts/{postId:guid}` | Cập nhật post |
| `DELETE` | `/api/v1/posts/{postId:guid}` | Xóa post (soft delete) |
| `PUT` | `/api/v1/posts/{postId:guid}/reaction` | Tạo hoặc cập nhật reaction |
| `DELETE` | `/api/v1/posts/{postId:guid}/reaction` | Xóa reaction |

### 1.5 Flow tạo post (Locket-style)

```
[Frontend]
    │
    ├── 1. User chụp ảnh (camera) hoặc chọn ảnh/video từ thư viện
    │
    ├── 2. Upload file → POST /api/v1/media (Media module hiện có)
    │        │
    │        └── Media module lưu file vào MinIO, tạo MediaObject record, trả về mediaId
    │
    └── 3. Tạo post → POST /api/v1/posts { mediaId, caption, visibility }
             │
             └── Posts module: validate mediaId → tạo Post record → trả về PostResponse
```

Posts module **không** xử lý upload file, **không** tương tác trực tiếp với MinIO. Chỉ nhận `mediaId` và validate thông qua bảng `MediaObjects` hiện có.

### 1.6 Assumptions

| # | Giả định |
|---|---|
| A1 | Bảng `MediaObjects` hiện tại **chưa có** field `DurationSeconds` và `Status` — cần bổ sung migration riêng trước khi implement Posts API. |
| A2 | Bảng friendship/friend hiện có, có thể query danh sách `friendUserIds` của một user. Spec này không định nghĩa friendship module. |
| A3 | `ICurrentUserService` đã tồn tại và cung cấp `UserId` của user đang đăng nhập. |
| A4 | Giai đoạn đầu cho phép user reaction post của chính mình (self-reaction). Nếu business muốn cấm, cần bổ sung rule sau. |
| A5 | URL trả về cho media (url, thumbnailUrl) được tạo bởi Media module/Storage service sử dụng MinIO public endpoint — Posts module không tự tạo URL. |
| A6 | Cursor được encode/decode bằng Base64 chứa JSON `{ "createdAt": "ISO8601", "id": "uuid" }`. |
| A7 | Khi `limit` client gửi vượt max (50), server **clamp** về 50 thay vì trả lỗi — UX tốt hơn và không gây lỗi không cần thiết. |
| A8 | `MediaObject.UploadProviderByUserId` là field owner của media. Spec này tham chiếu field này khi kiểm tra ownership. |

---

## 2. Media Integration

### 2.1 Media module hiện có

Media module đã được implement với:
- Entity `MediaObject` tại `Domain/Entities/Storage/MediaObject.cs`
- Lưu trữ trên MinIO qua `StorageProvider.MinIO`
- Soft delete qua `IsDeleted` / `DeletedAtUtc`
- Thumbnail qua `ThumbnailObjectKey`
- Enum `MediaType` : `Image = 1`, `Video = 2`

### 2.2 Posts module không upload file

Posts module **chỉ đọc** record `MediaObject` đã tồn tại để validate. Mọi thao tác upload/delete file trên MinIO vẫn thuộc phạm vi Media module.

### 2.3 Luồng upload media rồi tạo post

```
1. Client upload file → POST /api/v1/media
2. Media module tạo MediaObject, lưu MinIO
3. Media module trả về: { "data": { "id": "<mediaId>", ... } }
4. Client gọi: POST /api/v1/posts { "mediaId": "<mediaId>", ... }
5. Posts handler query MediaObject by mediaId
6. Validate media (xem 2.4)
7. Tạo Post record nếu hợp lệ
```

### 2.4 Media validation rules

Posts module phải validate các điều kiện sau khi nhận `mediaId`:

| # | Điều kiện | Lỗi trả về |
|---|---|---|
| V1 | `mediaId` tồn tại trong bảng `MediaObjects` (chưa bị xóa mềm) | `MEDIA_NOT_FOUND` → 404 |
| V2 | `MediaObject.UploadProviderByUserId == currentUserId` | `MEDIA_ACCESS_DENIED` → 403 |
| V3 | `MediaObject.Status` phải là `Ready` hoặc `Active` | `MEDIA_NOT_READY` → 400 |
| V4 | `MediaObject.MediaType` phải là `Image` hoặc `Video` | `UNSUPPORTED_MEDIA_TYPE` → 400 |
| V5 | Nếu `MediaType == Video`: `DurationSeconds >= 5` và `DurationSeconds <= 10` | `INVALID_VIDEO_DURATION` → 400 |
| V6 | Nếu `MediaType == Video`: `DurationSeconds != null` (không cho video chưa có metadata) | `INVALID_VIDEO_DURATION` → 400 |

Backend **không** tin vào duration hoặc source do client gửi lên. Luôn đọc từ `MediaObject.DurationSeconds` đã được Media module xử lý và lưu vào database.

### 2.5 Supported media types

- `Image` (ảnh tĩnh — mọi định dạng MinIO hỗ trợ)
- `Video` (video ngắn 5–10 giây)

### 2.6 Video duration rules

- `DurationSeconds` tối thiểu: **5 giây**
- `DurationSeconds` tối đa: **10 giây**
- Nếu `DurationSeconds` là `null`: từ chối, trả `INVALID_VIDEO_DURATION`
- Nếu frontend đã trim video trước khi upload, backend vẫn validate lại metadata đã lưu trong `MediaObject`

### 2.7 Media status rules

Chỉ cho phép tạo post với media ở trạng thái:
- `Ready`
- `Active`

Từ chối với trạng thái:
- `Uploading` → `MEDIA_NOT_READY`
- `Processing` → `MEDIA_NOT_READY`
- `Failed` → `MEDIA_NOT_READY`

### 2.8 Media ownership rules

`MediaObject.UploadProviderByUserId` phải bằng `currentUserId`. Không cho dùng media của user khác (trừ khi có cơ chế shared media sau này).

### 2.9 Media fields cần có — trạng thái hiện tại

| Field | Hiện có | Ghi chú |
|---|---|---|
| `Id` | ✅ | `Guid` |
| `UploadProviderByUserId` | ✅ | Owner — `Guid?` |
| `MediaType` | ✅ | Enum `Image/Video` |
| `ContentType` | ✅ | MIME type |
| `ObjectKey` | ✅ | Dùng để build URL |
| `ThumbnailObjectKey` | ✅ | Thumbnail cho video |
| `FileSizeBytes` | ✅ | `long` |
| `Width` | ✅ | `int?` |
| `Height` | ✅ | `int?` |
| `IsDeleted` | ✅ | Soft delete |
| `CreatedAtUtc` | ✅ | |
| **`DurationSeconds`** | ❌ **THIẾU** | Cần bổ sung — `int?` — cho validation video 5–10s |
| **`Status`** | ❌ **THIẾU** | Cần bổ sung — enum `MediaStatus` — để validate uploading/processing/ready/failed |
| `Source` | ❌ Không có | Không bắt buộc — chỉ dùng analytics |

> **Action required trước khi implement Posts API:**
> 1. Thêm `DurationSeconds int?` vào `MediaObject` entity.
> 2. Thêm enum `MediaStatus { Uploading=1, Processing=2, Ready=3, Active=4, Failed=5 }` và field `Status MediaStatus` vào `MediaObject`.
> 3. Cập nhật Media module để set `Status` và `DurationSeconds` khi upload/processing xong.
> 4. Tạo EF Core migration cho `MediaObjects` table.

---

## 3. Database Design

### 3.1 Bảng `Posts`

#### Mục đích

Lưu thông tin bài đăng của user. Mỗi post gắn với 1 media (ảnh hoặc video ngắn).

#### Fields

| Column | Data Type (SQL Server) | C# Type | Nullable | Default | Ghi chú |
|---|---|---|---|---|---|
| `Id` | `uniqueidentifier` | `Guid` | No | `newid()` / `Guid.NewGuid()` | PK |
| `OwnerUserId` | `uniqueidentifier` | `Guid` | No | — | FK → Users(Id) |
| `MediaId` | `uniqueidentifier` | `Guid` | No | — | FK → MediaObjects(Id) |
| `Caption` | `nvarchar(500)` | `string?` | Yes | NULL | Max 500 ký tự, trimmed |
| `Visibility` | `int` | `PostVisibility` (enum) | No | `1` (Friends) | Stored as int |
| `Status` | `int` | `PostStatus` (enum) | No | `1` (Active) | Stored as int |
| `CreatedAtUtc` | `datetime2` | `DateTime` | No | `GETUTCDATE()` | Set khi tạo |
| `UpdatedAtUtc` | `datetime2` | `DateTime` | No | `GETUTCDATE()` | Cập nhật mỗi lần thay đổi |
| `DeletedAtUtc` | `datetime2` | `DateTime?` | Yes | NULL | Soft delete — null = chưa xóa |

#### Enums (C#)

```csharp
public enum PostVisibility
{
    Friends = 1,   // Owner + bạn bè thấy
    Private = 2    // Chỉ owner thấy
}

public enum PostStatus
{
    Active = 1,    // Hiển thị bình thường
    Hidden = 2     // Bị ẩn (moderation hoặc user tự ẩn)
}
```

> **Lưu ý:** Không dùng `Status = Deleted`. Trạng thái xóa xác định bởi `DeletedAtUtc IS NOT NULL`. Dùng 2 field riêng biệt để tránh duplicate state và giữ audit trail rõ ràng.

#### Constraints

```sql
ALTER TABLE Posts ADD CONSTRAINT PK_Posts PRIMARY KEY (Id);
ALTER TABLE Posts ADD CONSTRAINT FK_Posts_Users_OwnerUserId
    FOREIGN KEY (OwnerUserId) REFERENCES Users(Id);
ALTER TABLE Posts ADD CONSTRAINT FK_Posts_MediaObjects_MediaId
    FOREIGN KEY (MediaId) REFERENCES MediaObjects(Id);
ALTER TABLE Posts ADD CONSTRAINT CK_Posts_Caption_Length
    CHECK (LEN(Caption) <= 500);
```

#### Indexes

```sql
-- Lấy post theo owner (profile page / my posts)
CREATE INDEX IX_Posts_OwnerUserId_CreatedAtUtc
    ON Posts (OwnerUserId, CreatedAtUtc DESC, Id DESC);

-- Feed query: filter + sort
CREATE INDEX IX_Posts_Feed_Filter
    ON Posts (Status, DeletedAtUtc, CreatedAtUtc DESC, Id DESC);

-- Nếu dùng SQL Server 2016+ có thể dùng filtered index:
-- CREATE INDEX IX_Posts_Active_Feed
--     ON Posts (CreatedAtUtc DESC, Id DESC)
--     WHERE Status = 1 AND DeletedAtUtc IS NULL;
```

#### Relationships

- `Post` nhiều-một `User` (OwnerUserId)
- `Post` nhiều-một `MediaObject` (MediaId)
- `Post` một-nhiều `PostReaction`

#### Soft Delete

- Field `DeletedAtUtc` là trạng thái xóa chính.
- EF Core query filter: `builder.HasQueryFilter(p => p.DeletedAtUtc == null)` trong `AppDbContext.OnModelCreating`.
- Khi soft delete: set `DeletedAtUtc = DateTime.UtcNow` và `UpdatedAtUtc = DateTime.UtcNow`.
- Post đã xóa: ẩn khỏi feed, ẩn khỏi API detail (trả 404), không cho reaction mới.
- `PostReactions` không bị xóa khi post bị soft delete (bảo toàn lịch sử).

#### Entity base class

`Post` kế thừa `AuditableEntity` (có `CreatedAtUtc`, `UpdatedAtUtc`). Soft delete được implement thủ công bằng `DeletedAtUtc` thay vì kế thừa `SoftDeletableEntity` vì `SoftDeletableEntity` dùng `IsDeleted` + `DeletedAtUtc` — ở đây chỉ cần `DeletedAtUtc` để tránh duplicate boolean.

> **Alternatively:** Có thể kế thừa `SoftDeletableEntity` và dùng `IsDeleted` nếu muốn thống nhất với pattern hiện tại của project. Cần quyết định trước khi implement.

---

### 3.2 Bảng `PostReactions`

#### Mục đích

Lưu reaction của từng user trên từng post. Mỗi user chỉ có tối đa 1 reaction trên 1 post. Reaction summary (tổng số, số theo icon) được tính bằng aggregate query — **không** lưu denormalized counter.

#### Fields

| Column | Data Type (SQL Server) | C# Type | Nullable | Default | Ghi chú |
|---|---|---|---|---|---|
| `Id` | `uniqueidentifier` | `Guid` | No | `newid()` | PK |
| `PostId` | `uniqueidentifier` | `Guid` | No | — | FK → Posts(Id) |
| `UserId` | `uniqueidentifier` | `Guid` | No | — | FK → Users(Id) |
| `Icon` | `nvarchar(10)` | `string` | No | — | Emoji string, vd "❤️" |
| `CreatedAtUtc` | `datetime2` | `DateTime` | No | `GETUTCDATE()` | Lần react đầu tiên |
| `UpdatedAtUtc` | `datetime2` | `DateTime` | No | `GETUTCDATE()` | Lần đổi icon gần nhất |

#### Supported Icons (Phase 1)

```
❤️  😂  👍  😢  😮
```

Validate ở application layer. Nếu cần quản lý động sau này, tách bảng `ReactionIcons`.

#### Constraints

```sql
ALTER TABLE PostReactions ADD CONSTRAINT PK_PostReactions PRIMARY KEY (Id);

-- Đảm bảo mỗi user chỉ có 1 reaction trên 1 post
ALTER TABLE PostReactions ADD CONSTRAINT UX_PostReactions_PostId_UserId
    UNIQUE (PostId, UserId);

ALTER TABLE PostReactions ADD CONSTRAINT FK_PostReactions_Posts_PostId
    FOREIGN KEY (PostId) REFERENCES Posts(Id);

ALTER TABLE PostReactions ADD CONSTRAINT FK_PostReactions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES Users(Id);
```

#### Indexes

```sql
-- Đảm bảo unique (đã là unique constraint — tự tạo index)
-- UX_PostReactions_PostId_UserId phục vụ: upsert, lấy myReaction

-- Phục vụ aggregate số lượng từng icon trên 1 post
CREATE INDEX IX_PostReactions_PostId_Icon
    ON PostReactions (PostId, Icon);

-- Phục vụ query reaction theo user
CREATE INDEX IX_PostReactions_UserId
    ON PostReactions (UserId);
```

#### Lý do không dùng `icons_json` và `total_count`

| Thiết kế cũ (không dùng) | Thiết kế này |
|---|---|
| Lưu `icons_json: {"❤️":2,"😂":1}` trong bảng | Aggregate query `GROUP BY PostId, Icon` |
| Lưu `total_count` trong bảng | `COUNT(*)` từ bảng |
| Khó đảm bảo 1 user = 1 reaction | `UNIQUE (PostId, UserId)` enforce ở DB |
| Race condition khi update counter | Không có counter → không có race condition |
| Khó lấy `myReaction` | Query đơn giản: `WHERE PostId=X AND UserId=Y` |

#### Aggregate Reaction Summary

```sql
-- Đếm theo từng icon cho 1 post
SELECT Icon, COUNT(*) AS Count
FROM PostReactions
WHERE PostId = @PostId
GROUP BY Icon;

-- Đếm tổng reaction cho 1 post
SELECT COUNT(*) AS TotalCount
FROM PostReactions
WHERE PostId = @PostId;

-- Lấy myReaction của user hiện tại
SELECT Icon
FROM PostReactions
WHERE PostId = @PostId AND UserId = @CurrentUserId;
```

---

## 4. Business Rules

### 4.1 Rules cho Post

- Mỗi post gắn với đúng 1 media (giai đoạn đầu).
- Khi tạo: `Status = Active`, `DeletedAtUtc = null`.
- Caption tối đa 500 ký tự, trim khoảng trắng.
- Visibility mặc định: `Friends`.
- Không được thay đổi `OwnerUserId`, `MediaId`, `CreatedAtUtc`, `DeletedAtUtc` qua Update API.

### 4.2 Rules cho Media khi tạo Post

- `mediaId` phải tồn tại và chưa bị soft delete.
- Media phải thuộc `currentUser` (field `UploadProviderByUserId`).
- Media `Status` phải là `Ready` hoặc `Active`.
- Media `MediaType` phải là `Image` hoặc `Video`.

### 4.3 Rules cho Video ngắn

- `DurationSeconds` phải `>= 5` và `<= 10`.
- Nếu `DurationSeconds == null`: từ chối (video chưa được xử lý metadata).
- Backend validate từ `MediaObject.DurationSeconds` — không tin client.

### 4.4 Rules cho Feed

- Feed gồm post của `currentUser` (mọi visibility) và bạn bè (chỉ `visibility = Friends`).
- Chỉ lấy post `Status = Active` và `DeletedAtUtc IS NULL`.
- Sắp xếp: `CreatedAtUtc DESC`, `Id DESC` (tiebreaker).
- Cursor pagination với cursor là `(CreatedAtUtc, Id)`.
- Mỗi item feed trả về: post info + owner info + media info + reactionSummary + myReaction.

### 4.5 Rules cho Visibility

| Visibility | Ai được xem |
|---|---|
| `Friends` | Owner + bạn bè của owner |
| `Private` | Chỉ owner |

Rule này áp dụng cho: feed, detail, reaction.

### 4.6 Rules cho Reaction

- Mỗi user chỉ có 1 reaction trên 1 post (unique constraint DB).
- Icon phải thuộc danh sách: `❤️ 😂 👍 😢 😮`.
- Nếu chưa có: tạo mới.
- Nếu đã có và icon khác: update icon.
- Nếu đã có và icon giống: no-op, trả về current state.
- Giai đoạn đầu: cho phép self-reaction (react post của chính mình).
- Không được react post đã bị soft delete.
- User phải có quyền xem post mới được react.

### 4.7 Rules cho Soft Delete

- Post bị xóa bằng soft delete: `DeletedAtUtc = UtcNow`.
- Post đã xóa không xuất hiện trong feed.
- API detail trả 404 với post đã xóa.
- Không cho reaction mới trên post đã xóa.
- `PostReactions` giữ nguyên sau khi post bị soft delete (bảo toàn lịch sử).
- Chỉ owner mới được xóa post của mình.

### 4.8 Rules cho Quyền truy cập

| Action | Điều kiện |
|---|---|
| Xem post | Owner, hoặc bạn bè nếu `visibility = Friends` |
| React post | Có quyền xem post |
| Update post | Chỉ owner |
| Xóa post | Chỉ owner |

---

## 5. API Specification

### 5.1 Create Post

**Endpoint:** `POST /api/v1/posts`  
**Auth:** `[Authorize]` — Access Token bắt buộc

#### Request

```json
{
  "mediaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "caption": "Hello world!",
  "visibility": "friends"
}
```

| Field | Type | Required | Validation |
|---|---|---|---|
| `mediaId` | `Guid` | Yes | Phải là valid Guid |
| `caption` | `string?` | No | Max 500 ký tự sau trim; null/empty được chấp nhận |
| `visibility` | `string` | No | `"friends"` hoặc `"private"`; default `"friends"` |

#### Media Validation (thứ tự ưu tiên)

1. `mediaId` tồn tại → 404 nếu không
2. Media chưa bị soft delete → 404 nếu đã xóa
3. `UploadProviderByUserId == currentUserId` → 403 nếu không
4. `Status in (Ready, Active)` → 400 `MEDIA_NOT_READY` nếu không
5. `MediaType in (Image, Video)` → 400 `UNSUPPORTED_MEDIA_TYPE` nếu không
6. Nếu `MediaType == Video`: `DurationSeconds != null && >= 5 && <= 10` → 400 `INVALID_VIDEO_DURATION`

#### Response (201 Created)

```json
{
  "success": true,
  "message": "Post created successfully.",
  "code": null,
  "data": {
    "id": "post-id-guid",
    "ownerUserId": "user-id-guid",
    "media": {
      "id": "media-id-guid",
      "url": "https://minio.example.com/bucket/object-key",
      "type": "image",
      "thumbnailUrl": null,
      "durationSeconds": null,
      "width": 1080,
      "height": 1920
    },
    "caption": "Hello world!",
    "visibility": "friends",
    "status": "active",
    "createdAtUtc": "2026-05-15T10:00:00Z"
  },
  "errors": null
}
```

Response với video:

```json
{
  "data": {
    "media": {
      "id": "media-id-guid",
      "url": "https://...",
      "type": "video",
      "thumbnailUrl": "https://...",
      "durationSeconds": 8,
      "width": 720,
      "height": 1280
    }
  }
}
```

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| Request không hợp lệ (FluentValidation) | 400 | `VALIDATION_ERROR` |
| `caption` vượt 500 ký tự | 400 | `VALIDATION_ERROR` |
| `visibility` không hợp lệ | 400 | `INVALID_VISIBILITY` |
| `mediaId` không tồn tại | 404 | `MEDIA_NOT_FOUND` |
| Media không thuộc user | 403 | `MEDIA_ACCESS_DENIED` |
| Media chưa sẵn sàng | 400 | `MEDIA_NOT_READY` |
| Media type không hỗ trợ | 400 | `UNSUPPORTED_MEDIA_TYPE` |
| Video duration không hợp lệ | 400 | `INVALID_VIDEO_DURATION` |

#### Acceptance Criteria

- [ ] Tạo post thành công với ảnh hợp lệ → trả 201 với PostResponse đầy đủ.
- [ ] Tạo post thành công với video 5–10s → trả 201 với `thumbnailUrl` và `durationSeconds`.
- [ ] Tạo post với video < 5s hoặc > 10s → trả 400 `INVALID_VIDEO_DURATION`.
- [ ] Tạo post với media của user khác → trả 403.
- [ ] Tạo post với media đang uploading → trả 400.
- [ ] Tạo post không có caption → thành công (caption null).
- [ ] Caption vượt 500 ký tự → trả 400.
- [ ] Không truyền visibility → mặc định `friends`.
- [ ] Posts module không gọi MinIO trực tiếp.

---

### 5.2 Get Feed

**Endpoint:** `GET /api/v1/posts/feed?cursor=&limit=20`  
**Auth:** `[Authorize]`

#### Query Parameters

| Param | Type | Required | Default | Validation |
|---|---|---|---|---|
| `cursor` | `string` | No | null (first page) | Base64 encoded `{createdAt, id}` |
| `limit` | `int` | No | 20 | Min 1, Max 50; nếu > 50 thì clamp về 50 |

> **Lý do clamp thay vì 400:** Feed là thao tác đọc, clamp giúp client không bị lỗi khi thay đổi giá trị. Dễ dàng document hơn và UX tốt hơn.

#### Cursor Pagination

Cursor encode `(createdAtUtc, id)` dưới dạng Base64 JSON:

```json
// Decoded cursor
{ "createdAt": "2026-05-15T10:00:00.000Z", "id": "post-id-guid" }
```

Dùng keyset pagination thay vì offset để tránh:
- Post bị skip khi có post mới được tạo trong khi user đang scroll.
- Performance giảm khi offset lớn (không cần `SKIP N` rows).

Điều kiện cursor (lấy page tiếp theo):

```sql
(CreatedAtUtc < @cursorCreatedAt)
OR (CreatedAtUtc = @cursorCreatedAt AND Id < @cursorId)
```

`nextCursor` là null nếu không còn item tiếp theo (số item trả về < limit).

#### Visibility Filter

```sql
-- Post của chính mình (mọi visibility) HOẶC post của bạn bè (chỉ Friends)
(
    OwnerUserId = @currentUserId
    OR (
        OwnerUserId IN @friendUserIds
        AND Visibility = 1  -- Friends
    )
)
AND Status = 1          -- Active
AND DeletedAtUtc IS NULL
```

#### Response (200 OK)

```json
{
  "success": true,
  "message": "Feed retrieved successfully.",
  "code": null,
  "data": {
    "items": [
      {
        "id": "post-id-guid",
        "ownerUserId": "user-id-guid",
        "owner": {
          "id": "user-id-guid",
          "displayName": "Nguyen Van A",
          "avatarUrl": "https://..."
        },
        "media": {
          "id": "media-id-guid",
          "url": "https://...",
          "type": "image",
          "thumbnailUrl": null,
          "durationSeconds": null,
          "width": 1080,
          "height": 1920
        },
        "caption": "Hello",
        "visibility": "friends",
        "createdAtUtc": "2026-05-15T10:00:00Z",
        "reactionSummary": {
          "totalCount": 3,
          "icons": {
            "❤️": 2,
            "😂": 1
          }
        },
        "myReaction": {
          "icon": "❤️"
        }
      }
    ],
    "nextCursor": "eyJjcmVhdGVkQXQiOiIyMDI2LTA1LTE1VDA5OjAwOjAwWiIsImlkIjoiYWJjZCJ9"
  },
  "errors": null
}
```

`myReaction` là `null` nếu user chưa react post đó.

#### Lấy Reaction Summary tránh N+1

Không lấy reaction theo từng post (N+1). Thay vào đó:

1. Query danh sách `postIds` từ feed query.
2. Một query aggregate tất cả reactions cho danh sách `postIds`:
   ```sql
   SELECT PostId, Icon, COUNT(*) AS Count
   FROM PostReactions
   WHERE PostId IN @postIds
   GROUP BY PostId, Icon
   ```
3. Một query lấy `myReactions` cho tất cả `postIds`:
   ```sql
   SELECT PostId, Icon
   FROM PostReactions
   WHERE PostId IN @postIds AND UserId = @currentUserId
   ```
4. Map kết quả vào từng post item trong memory.

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |

#### Acceptance Criteria

- [ ] Feed trả post của bản thân (cả `friends` và `private`).
- [ ] Feed trả post của bạn bè chỉ với `visibility = friends`.
- [ ] Post `visibility = private` của bạn bè không xuất hiện trong feed.
- [ ] Post đã bị soft delete không xuất hiện.
- [ ] Post `status = hidden` không xuất hiện.
- [ ] Cursor pagination hoạt động đúng khi có nhiều post cùng `createdAtUtc`.
- [ ] `reactionSummary` và `myReaction` được trả về cho mọi item.
- [ ] `myReaction = null` nếu user chưa react.
- [ ] Không có N+1 query khi lấy reaction summary.
- [ ] `limit` vượt 50 bị clamp về 50.

---

### 5.3 Get Post Detail

**Endpoint:** `GET /api/v1/posts/{postId:guid}`  
**Auth:** `[Authorize]`

#### Access Control

| Điều kiện | Kết quả |
|---|---|
| Post không tồn tại hoặc đã bị soft delete | 404 `POST_NOT_FOUND` |
| Post `status != active` | 404 `POST_NOT_FOUND` (không lộ sự tồn tại) |
| User là owner | Cho xem |
| `visibility = friends` và user là bạn bè của owner | Cho xem |
| `visibility = private` và user không phải owner | 403 `POST_ACCESS_DENIED` |

#### Response (200 OK)

Cấu trúc giống item trong feed, bổ sung full `updatedAtUtc`:

```json
{
  "success": true,
  "data": {
    "id": "post-id-guid",
    "ownerUserId": "user-id-guid",
    "owner": {
      "id": "user-id-guid",
      "displayName": "Nguyen Van A",
      "avatarUrl": "https://..."
    },
    "media": {
      "id": "media-id-guid",
      "url": "https://...",
      "type": "video",
      "thumbnailUrl": "https://...",
      "durationSeconds": 8,
      "width": 720,
      "height": 1280
    },
    "caption": "Hello",
    "visibility": "friends",
    "status": "active",
    "createdAtUtc": "2026-05-15T10:00:00Z",
    "updatedAtUtc": "2026-05-15T10:00:00Z",
    "reactionSummary": {
      "totalCount": 3,
      "icons": {
        "❤️": 2,
        "😂": 1
      }
    },
    "myReaction": {
      "icon": "❤️"
    }
  }
}
```

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| Post không tồn tại / đã xóa | 404 | `POST_NOT_FOUND` |
| Không có quyền xem | 403 | `POST_ACCESS_DENIED` |

#### Acceptance Criteria

- [ ] Owner lấy được post của mình (cả `private` và `friends`).
- [ ] Bạn bè lấy được post `visibility = friends`.
- [ ] Bạn bè không lấy được post `visibility = private` → 403.
- [ ] Post bị soft delete → 404.
- [ ] `reactionSummary` và `myReaction` được trả về.

---

### 5.4 Update Post

**Endpoint:** `PATCH /api/v1/posts/{postId:guid}`  
**Auth:** `[Authorize]`

#### Request

```json
{
  "caption": "Updated caption",
  "visibility": "private"
}
```

| Field | Type | Required | Validation |
|---|---|---|---|
| `caption` | `string?` | No | Max 500 ký tự sau trim; null để clear caption |
| `visibility` | `string?` | No | `"friends"` hoặc `"private"` nếu có |

#### Allowed Fields

- `caption` — có thể update (kể cả set null)
- `visibility` — có thể update

#### Fields không được phép update

- `ownerUserId`, `mediaId`, `createdAtUtc`, `deletedAtUtc`, `status` (dùng API riêng nếu cần)

#### Business Rules

1. User phải là owner của post → 403 nếu không.
2. Post phải tồn tại và chưa bị soft delete → 404.
3. Khi update thành công: `updatedAtUtc = UtcNow`.
4. Caption null/empty: lưu null vào DB (clear caption).

#### Response (200 OK)

Trả về PostResponse đầy đủ sau khi update (giống Get Detail, không bao gồm reactionSummary để tối giản — hoặc bao gồm nếu muốn consistent).

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| Post không tồn tại / đã xóa | 404 | `POST_NOT_FOUND` |
| User không phải owner | 403 | `POST_UPDATE_DENIED` |
| Visibility không hợp lệ | 400 | `INVALID_VISIBILITY` |
| Caption vượt 500 ký tự | 400 | `VALIDATION_ERROR` |

#### Acceptance Criteria

- [ ] Owner update caption thành công.
- [ ] Owner update visibility thành công.
- [ ] Non-owner update → 403.
- [ ] Update post đã xóa → 404.
- [ ] Caption null → clear caption trong DB.
- [ ] `updatedAtUtc` được cập nhật.

---

### 5.5 Delete Post

**Endpoint:** `DELETE /api/v1/posts/{postId:guid}`  
**Auth:** `[Authorize]`

#### Soft Delete Behavior

Khi xóa:
- `DeletedAtUtc = DateTime.UtcNow`
- `UpdatedAtUtc = DateTime.UtcNow`
- `Status` giữ nguyên (không thay đổi) — `DeletedAtUtc` là trạng thái xóa chính
- `PostReactions` giữ nguyên (bảo toàn lịch sử)

Sau khi xóa:
- Post không xuất hiện trong feed (query filter)
- API detail trả 404
- Không cho reaction mới

#### Response

Trả về `ApiResponse<T>` với `data = null` theo convention Beacon (không dùng 204):

```json
{
  "success": true,
  "message": "Post deleted successfully.",
  "code": null,
  "data": null,
  "errors": null
}
```

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| Post không tồn tại / đã xóa | 404 | `POST_NOT_FOUND` |
| User không phải owner | 403 | `POST_DELETE_DENIED` |

#### Acceptance Criteria

- [ ] Owner xóa post thành công → `DeletedAtUtc` được set.
- [ ] Post sau khi xóa không xuất hiện trong feed.
- [ ] API detail sau khi xóa trả 404.
- [ ] Non-owner xóa → 403.
- [ ] Xóa post đã xóa trước đó → 404 (idempotent về mặt response).
- [ ] `PostReactions` không bị xóa.

---

### 5.6 Create/Update Reaction

**Endpoint:** `PUT /api/v1/posts/{postId:guid}/reaction`  
**Auth:** `[Authorize]`

#### Request

```json
{
  "icon": "❤️"
}
```

| Field | Type | Required | Validation |
|---|---|---|---|
| `icon` | `string` | Yes | Phải thuộc `{ ❤️, 😂, 👍, 😢, 😮 }` |

#### Upsert Behavior

1. Query `PostReaction` bằng `(PostId, UserId)`.
2. Nếu không có: tạo mới với `Icon = request.icon`.
3. Nếu có và icon khác: update `Icon = request.icon`, `UpdatedAtUtc = UtcNow`.
4. Nếu có và icon giống: no-op, trả về current state ngay (không ghi DB).

EF Core không có built-in upsert. Sử dụng pattern:

```
var existing = await repo.GetReactionAsync(postId, currentUserId, ct);
if (existing is null)
    await repo.AddAsync(new PostReaction(...), ct);
else if (existing.Icon != request.Icon)
    existing.UpdateIcon(request.Icon);
// else: no-op
await repo.SaveChangesAsync(ct);
```

> Transaction không bắt buộc vì chỉ có 1 `SaveChangesAsync`. Unique constraint DB đảm bảo không duplicate.

#### Idempotency

Gửi cùng icon nhiều lần → kết quả giống nhau, không tạo duplicate. API luôn trả về trạng thái hiện tại.

#### Race Condition

Unique constraint `(PostId, UserId)` ở DB đảm bảo không bao giờ có 2 reaction cho cùng (user, post). Nếu 2 request đến cùng lúc, 1 request sẽ nhận `DbUpdateException` do unique violation → có thể retry hoặc trả 409.

> Với SQL Server, có thể dùng `MERGE` statement để atomic upsert — xem xét nếu cần performance cao.

#### Response (200 OK)

```json
{
  "success": true,
  "message": "Reaction updated.",
  "code": null,
  "data": {
    "postId": "post-id-guid",
    "myReaction": {
      "icon": "😂"
    },
    "reactionSummary": {
      "totalCount": 3,
      "icons": {
        "❤️": 2,
        "😂": 1
      }
    }
  },
  "errors": null
}
```

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| `icon` không hợp lệ | 400 | `INVALID_REACTION_ICON` |
| Post không tồn tại / đã xóa | 404 | `POST_NOT_FOUND` |
| Không có quyền react (visibility + friendship check) | 403 | `POST_ACCESS_DENIED` |
| Race condition (hiếm) | 409 | `REACTION_CONFLICT` |

#### Acceptance Criteria

- [ ] User chưa react → tạo mới reaction.
- [ ] User đổi icon → update icon.
- [ ] User gửi lại cùng icon → no-op, trả current state.
- [ ] Icon không thuộc danh sách → 400.
- [ ] React post đã xóa → 404.
- [ ] React post `private` của người lạ → 403.
- [ ] `reactionSummary` đúng sau khi upsert.

---

### 5.7 Delete Reaction

**Endpoint:** `DELETE /api/v1/posts/{postId:guid}/reaction`  
**Auth:** `[Authorize]`

#### Behavior

1. Validate post tồn tại, chưa xóa, user có quyền xem.
2. Nếu user đã có reaction → xóa.
3. Nếu user chưa có reaction → no-op (idempotent), trả 200.

#### Soft Delete Policy cho Post

Post đã bị soft delete: trả **404** `POST_NOT_FOUND` — không lộ sự tồn tại của post đã xóa với user không phải owner.

#### Idempotency

Gọi DELETE reaction nhiều lần → luôn trả 200, `myReaction = null`.

#### Response (200 OK)

```json
{
  "success": true,
  "message": "Reaction removed.",
  "code": null,
  "data": {
    "postId": "post-id-guid",
    "myReaction": null,
    "reactionSummary": {
      "totalCount": 2,
      "icons": {
        "❤️": 2
      }
    }
  },
  "errors": null
}
```

#### Error Cases

| Điều kiện | HTTP | Code |
|---|---|---|
| Chưa đăng nhập | 401 | `UNAUTHORIZED` |
| Post không tồn tại / đã xóa | 404 | `POST_NOT_FOUND` |
| Không có quyền xem post | 403 | `POST_ACCESS_DENIED` |

#### Acceptance Criteria

- [ ] User xóa reaction thành công → `myReaction = null`, summary cập nhật.
- [ ] User chưa có reaction → 200 idempotent.
- [ ] Post đã xóa → 404.
- [ ] Không có quyền xem → 403.

---

## 6. Query Logic / Pseudo SQL

### 6.1 Validate Media trước khi tạo post

```sql
SELECT
    mo.Id,
    mo.UploadProviderByUserId,
    mo.MediaType,
    mo.Status,
    mo.DurationSeconds,
    mo.ObjectKey,
    mo.ThumbnailObjectKey,
    mo.Width,
    mo.Height,
    mo.ContentType
FROM MediaObjects mo
WHERE mo.Id = @MediaId
  AND mo.IsDeleted = 0;
-- Nếu null → MEDIA_NOT_FOUND
-- Nếu UploadProviderByUserId != @currentUserId → MEDIA_ACCESS_DENIED
-- Nếu Status NOT IN (Ready, Active) → MEDIA_NOT_READY
-- Nếu MediaType NOT IN (Image, Video) → UNSUPPORTED_MEDIA_TYPE
-- Nếu MediaType = Video AND (DurationSeconds IS NULL OR DurationSeconds < 5 OR DurationSeconds > 10) → INVALID_VIDEO_DURATION
```

### 6.2 Tạo Post

```sql
INSERT INTO Posts (Id, OwnerUserId, MediaId, Caption, Visibility, Status, CreatedAtUtc, UpdatedAtUtc, DeletedAtUtc)
VALUES (@NewGuid, @CurrentUserId, @MediaId, @Caption, @Visibility, 1 /*Active*/, @UtcNow, @UtcNow, NULL);
```

### 6.3 Lấy Feed

```sql
-- Bước 1: Lấy friendUserIds của currentUser
SELECT FriendUserId
FROM Friendships
WHERE UserId = @CurrentUserId AND Status = 'Accepted'; -- tùy schema friendship

-- Bước 2: Lấy posts
SELECT
    p.Id, p.OwnerUserId, p.MediaId, p.Caption, p.Visibility, p.Status,
    p.CreatedAtUtc, p.UpdatedAtUtc,
    u.DisplayName AS OwnerDisplayName, u.AvatarUrl AS OwnerAvatarUrl,
    mo.ObjectKey, mo.ThumbnailObjectKey, mo.MediaType,
    mo.Width, mo.Height, mo.DurationSeconds, mo.ContentType
FROM Posts p
INNER JOIN Users u ON u.Id = p.OwnerUserId
INNER JOIN MediaObjects mo ON mo.Id = p.MediaId
WHERE
    p.Status = 1               -- Active
    AND p.DeletedAtUtc IS NULL
    AND (
        p.OwnerUserId = @CurrentUserId
        OR (
            p.OwnerUserId IN @FriendUserIds
            AND p.Visibility = 1  -- Friends
        )
    )
    -- Cursor condition (nếu có cursor)
    AND (
        @CursorCreatedAt IS NULL
        OR p.CreatedAtUtc < @CursorCreatedAt
        OR (p.CreatedAtUtc = @CursorCreatedAt AND p.Id < @CursorId)
    )
ORDER BY p.CreatedAtUtc DESC, p.Id DESC
FETCH NEXT @Limit ROWS ONLY;
```

### 6.4 Aggregate Reaction Summary (batch — tránh N+1)

```sql
-- Lấy tất cả reactions cho danh sách postIds trong feed
SELECT PostId, Icon, COUNT(*) AS Count
FROM PostReactions
WHERE PostId IN @PostIds
GROUP BY PostId, Icon;

-- Lấy myReaction cho danh sách postIds
SELECT PostId, Icon
FROM PostReactions
WHERE PostId IN @PostIds AND UserId = @CurrentUserId;
```

Map trong memory: group theo `PostId`, tính `totalCount = SUM(Count)`, build `icons dictionary`.

### 6.5 Lấy Post Detail

```sql
SELECT p.*, u.DisplayName, u.AvatarUrl, mo.*
FROM Posts p
INNER JOIN Users u ON u.Id = p.OwnerUserId
INNER JOIN MediaObjects mo ON mo.Id = p.MediaId
WHERE p.Id = @PostId
  AND p.DeletedAtUtc IS NULL
  AND p.Status = 1;
-- Nếu null → POST_NOT_FOUND
-- Kiểm tra quyền xem sau khi lấy được post
```

### 6.6 Update Post

```sql
UPDATE Posts
SET Caption = @Caption,
    Visibility = @Visibility,
    UpdatedAtUtc = @UtcNow
WHERE Id = @PostId
  AND OwnerUserId = @CurrentUserId
  AND DeletedAtUtc IS NULL;
```

### 6.7 Soft Delete Post

```sql
UPDATE Posts
SET DeletedAtUtc = @UtcNow,
    UpdatedAtUtc = @UtcNow
WHERE Id = @PostId
  AND OwnerUserId = @CurrentUserId
  AND DeletedAtUtc IS NULL;
```

### 6.8 Upsert Reaction

```
1. SELECT * FROM PostReactions WHERE PostId = @PostId AND UserId = @CurrentUserId
2. Nếu không có → INSERT INTO PostReactions (Id, PostId, UserId, Icon, CreatedAtUtc, UpdatedAtUtc)
   Nếu có và icon khác → UPDATE PostReactions SET Icon = @Icon, UpdatedAtUtc = @UtcNow WHERE Id = @Id
   Nếu có và icon giống → no-op
3. Tính lại ReactionSummary (xem 6.4)
```

### 6.9 Delete Reaction

```sql
DELETE FROM PostReactions
WHERE PostId = @PostId AND UserId = @CurrentUserId;
-- Nếu 0 rows affected → idempotent, vẫn trả 200
-- Tính lại ReactionSummary
```

---

## 7. Edge Cases

| # | Edge Case | Xử lý |
|---|---|---|
| E1 | Tạo post với `mediaId` không tồn tại | 404 `MEDIA_NOT_FOUND` |
| E2 | Tạo post với media của user khác | 403 `MEDIA_ACCESS_DENIED` |
| E3 | Media `status = uploading` | 400 `MEDIA_NOT_READY` |
| E4 | Media `status = processing` | 400 `MEDIA_NOT_READY` |
| E5 | Media `status = failed` | 400 `MEDIA_NOT_READY` |
| E6 | Video nhưng `DurationSeconds = null` | 400 `INVALID_VIDEO_DURATION` (metadata chưa có) |
| E7 | Video `DurationSeconds < 5` | 400 `INVALID_VIDEO_DURATION` |
| E8 | Video `DurationSeconds > 10` | 400 `INVALID_VIDEO_DURATION` |
| E9 | Media type không phải Image/Video | 400 `UNSUPPORTED_MEDIA_TYPE` |
| E10 | User xem post `private` của người lạ | 403 `POST_ACCESS_DENIED` |
| E11 | React post không tồn tại | 404 `POST_NOT_FOUND` |
| E12 | React post đã bị soft delete | 404 `POST_NOT_FOUND` |
| E13 | React post `private` của người lạ | 403 `POST_ACCESS_DENIED` |
| E14 | User đổi icon nhiều lần | Chỉ giữ icon mới nhất (update in-place) |
| E15 | User gửi lại cùng icon đang có | No-op, trả 200 với current state |
| E16 | User gửi icon không nằm trong danh sách | 400 `INVALID_REACTION_ICON` |
| E17 | Race condition: 2 request tạo reaction cùng lúc | Unique constraint bắt → 409 `REACTION_CONFLICT` cho request thứ 2 |
| E18 | Feed có nhiều post cùng `CreatedAtUtc` | Tiebreaker `Id DESC` đảm bảo thứ tự ổn định |
| E19 | Feed không còn item tiếp theo | `nextCursor = null` |
| E20 | Friend relationship bị xóa sau khi post được tạo | Feed query live theo friendship hiện tại — post của ex-friend không còn xuất hiện |
| E21 | User xóa post đã bị xóa trước đó | 404 `POST_NOT_FOUND` |
| E22 | User update post không phải của mình | 403 `POST_UPDATE_DENIED` |
| E23 | Caption chứa toàn khoảng trắng | Sau trim → lưu null hoặc empty tùy logic, đề xuất lưu null |
| E24 | Delete reaction khi chưa có reaction | 200 idempotent, `myReaction = null` |
| E25 | `limit = 0` trong feed | Trả 400 hoặc clamp về 1 — đề xuất min = 1 |

---

## 8. Acceptance Criteria Summary

### Media Integration

- [ ] Posts module không thực hiện upload file trực tiếp.
- [ ] Media validation kiểm tra đủ 6 điều kiện (tồn tại, ownership, status, type, video duration, video metadata).
- [ ] Backend validate `DurationSeconds` từ `MediaObject` — không từ client request.
- [ ] `DurationSeconds` và `Status` đã được bổ sung vào `MediaObject` entity và migration.

### Database

- [ ] Bảng `Posts` có đúng schema với indexes đề xuất.
- [ ] Bảng `PostReactions` có `UNIQUE (PostId, UserId)`.
- [ ] Soft delete filter trên `Posts` được config trong `OnModelCreating`.
- [ ] `PostStatus` và `PostVisibility` enums lưu dạng `int`.

### Create Post

- [ ] Tạo thành công với ảnh → 201.
- [ ] Tạo thành công với video 5–10s → 201 với `thumbnailUrl`.
- [ ] Video duration ngoài 5–10s → 400.
- [ ] Media của user khác → 403.
- [ ] Media chưa sẵn sàng → 400.

### Feed

- [ ] Trả post của bản thân (mọi visibility).
- [ ] Trả post bạn bè chỉ `Friends`.
- [ ] Không trả post `Private` của bạn bè.
- [ ] Cursor pagination đúng, không skip/duplicate.
- [ ] `reactionSummary` và `myReaction` được trả về không N+1.
- [ ] `nextCursor = null` khi hết data.

### Post Detail

- [ ] Owner xem được cả `Private` và `Friends`.
- [ ] Bạn bè xem được `Friends`, không xem được `Private`.
- [ ] Post đã xóa → 404.

### Update Post

- [ ] Owner update `caption` / `visibility` thành công.
- [ ] Non-owner → 403.
- [ ] Không cho update `mediaId`, `ownerId`, `createdAtUtc`.

### Delete Post

- [ ] Soft delete: `DeletedAtUtc` được set.
- [ ] Post ẩn khỏi feed sau khi xóa.
- [ ] `PostReactions` không bị xóa.

### Reaction

- [ ] Upsert đúng: tạo mới / update / no-op.
- [ ] `UNIQUE (PostId, UserId)` enforce.
- [ ] Icon không hợp lệ → 400.
- [ ] Delete reaction idempotent.
- [ ] `reactionSummary` chính xác sau mỗi thao tác.

### Security / Access Control

- [ ] Mọi endpoint yêu cầu Access Token hợp lệ.
- [ ] Visibility check đúng trên mọi endpoint (feed, detail, reaction).
- [ ] Owner check đúng trên update/delete.
- [ ] Post đã xóa không lộ thông tin qua bất kỳ endpoint nào.

---

## 9. Suggested Implementation Notes for .NET

### Entities (Domain Layer)

```csharp
// Domain/Entities/Posts/Post.cs
public class Post : AuditableEntity
{
    public Guid OwnerUserId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public PostStatus Status { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    // Factory method + Update methods
}

// Domain/Entities/Posts/PostReaction.cs
public class PostReaction : AuditableEntity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public string Icon { get; private set; }

    // UpdateIcon method
}
```

### Enums (Domain Layer)

```csharp
// Domain/Enums/PostVisibility.cs
public enum PostVisibility { Friends = 1, Private = 2 }

// Domain/Enums/PostStatus.cs
public enum PostStatus { Active = 1, Hidden = 2 }

// Domain/Enums/ReactionIcon.cs (hoặc dùng constant class)
public static class ReactionIcons
{
    public static readonly HashSet<string> Supported = new() { "❤️", "😂", "👍", "😢", "😮" };
}

// Domain/Enums/MediaStatus.cs (bổ sung vào Media module)
public enum MediaStatus { Uploading = 1, Processing = 2, Ready = 3, Active = 4, Failed = 5 }
```

### DTOs (Application Layer)

```csharp
// Request DTOs
CreatePostRequest        { MediaId, Caption, Visibility }
UpdatePostRequest        { Caption, Visibility }
UpsertPostReactionRequest { Icon }

// Response DTOs
PostResponse             { Id, OwnerUserId, Media, Caption, Visibility, Status, CreatedAtUtc, UpdatedAtUtc }
FeedPostResponse         { Id, OwnerUserId, Owner, Media, Caption, Visibility, CreatedAtUtc, ReactionSummary, MyReaction }
FeedResponse             { Items: List<FeedPostResponse>, NextCursor }
MediaInPostResponse      { Id, Url, Type, ThumbnailUrl, DurationSeconds, Width, Height }
OwnerInFeedResponse      { Id, DisplayName, AvatarUrl }
PostReactionResponse     { PostId, MyReaction, ReactionSummary }
PostReactionSummaryResponse { TotalCount, Icons: Dictionary<string, int> }
MyReactionResponse       { Icon }
```

### MediatR Commands & Queries

```csharp
// Commands
CreatePostCommand(CreatePostRequest Req, Guid CurrentUserId)
UpdatePostCommand(Guid PostId, UpdatePostRequest Req, Guid CurrentUserId)
DeletePostCommand(Guid PostId, Guid CurrentUserId)
UpsertPostReactionCommand(Guid PostId, string Icon, Guid CurrentUserId)
DeletePostReactionCommand(Guid PostId, Guid CurrentUserId)

// Queries
GetFeedQuery(Guid CurrentUserId, string? Cursor, int Limit)
GetPostDetailQuery(Guid PostId, Guid CurrentUserId)
```

### Repository Interfaces (Domain Layer)

```csharp
// Domain/IRepository/Posts/IPostRepository.cs
public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Post post, CancellationToken ct = default);
    Task<List<Post>> GetFeedAsync(Guid currentUserId, IEnumerable<Guid> friendIds,
        DateTime? cursorCreatedAt, Guid? cursorId, int limit, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// Domain/IRepository/Posts/IPostReactionRepository.cs
public interface IPostReactionRepository
{
    Task<PostReaction?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default);
    Task AddAsync(PostReaction reaction, CancellationToken ct = default);
    void Remove(PostReaction reaction);
    Task<List<PostReaction>> GetByPostIdsAsync(IEnumerable<Guid> postIds, CancellationToken ct = default);
    Task<List<PostReaction>> GetByPostIdsForUserAsync(IEnumerable<Guid> postIds, Guid userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### Tránh N+1 — Reaction Summary Strategy

```
Handler xử lý GetFeed:
1. postIds = feed query → List<PostId>
2. allReactions = repo.GetByPostIdsAsync(postIds)     // 1 query
3. myReactions = repo.GetByPostIdsForUserAsync(postIds, userId) // 1 query
4. Map trong memory:
   - Group allReactions by PostId → build reactionSummary per post
   - Group myReactions by PostId → build myReaction per post
5. Merge vào FeedPostResponse items
```

### Upsert Strategy (EF Core)

Không dùng raw SQL MERGE. Dùng read-then-write pattern:

```
var existing = await _reactionRepo.GetByPostAndUserAsync(postId, userId, ct);
if (existing is null)
    await _reactionRepo.AddAsync(new PostReaction(...), ct);
else if (existing.Icon != command.Icon)
    existing.UpdateIcon(command.Icon); // domain method
// else: no-op, không gọi SaveChanges
if (hasChanges)
    await _reactionRepo.SaveChangesAsync(ct);
```

Unique constraint ở DB đảm bảo safety khi có race condition.

### Mappers (Application Layer)

```csharp
// Application/Mappings/Posts/PostDtoMapper.cs
public sealed class PostDtoMapper
{
    public PostResponse ToPostResponse(Post post, MediaInPostResponse media) => ...;
    public FeedPostResponse ToFeedPostResponse(Post post, OwnerInFeedResponse owner,
        MediaInPostResponse media, PostReactionSummaryResponse summary, MyReactionResponse? myReaction) => ...;
    public MediaInPostResponse ToMediaResponse(MediaObject media, string url, string? thumbnailUrl) => ...;
}
```

---

## 10. Suggested Ticket Breakdown

### Ticket 1: [Prerequisite] Bổ sung `DurationSeconds` và `Status` vào `MediaObject`

**Scope:** Media module  
**Tasks:**
- Thêm enum `MediaStatus { Uploading, Processing, Ready, Active, Failed }` vào `Domain/Enums/`
- Thêm `DurationSeconds int?` và `Status MediaStatus` vào `MediaObject` entity
- Cập nhật `MediaObjectConfiguration` (EF config)
- Tạo EF Core migration
- Cập nhật Media upload handler để set `Status = Uploading` → `Ready/Failed`

---

### Ticket 2: [Database] Tạo bảng `Posts`

**Tasks:**
- Tạo `Post` entity (`Domain/Entities/Posts/`)
- Tạo `PostVisibility` và `PostStatus` enums
- Tạo `IPostRepository` interface
- Tạo `PostConfiguration` (EF config)
- Thêm `DbSet<Post>` vào `AppDbContext`
- Thêm soft delete query filter trong `OnModelCreating`
- Tạo EF Core migration

---

### Ticket 3: [Database] Tạo bảng `PostReactions`

**Tasks:**
- Tạo `PostReaction` entity
- Tạo `ReactionIcons` constants
- Tạo `IPostReactionRepository` interface
- Tạo `PostReactionConfiguration` (unique constraint, indexes)
- Thêm `DbSet<PostReaction>` vào `AppDbContext`
- Tạo EF Core migration

---

### Ticket 4: [API] Create Post

**Tasks:**
- `CreatePostCommand` + `CreatePostCommandHandler`
- `CreatePostCommandValidator` (FluentValidation)
- `ValidateMediaForPostAsync` logic trong handler
- `PostDtoMapper` (khởi tạo)
- `PostsController` với `POST /api/v1/posts`
- Đăng ký repository + mapper trong DI
- Unit test handler
- Integration test endpoint

---

### Ticket 5: [API] Get Feed

**Tasks:**
- `GetFeedQuery` + `GetFeedQueryHandler`
- Cursor encode/decode helper
- Batch reaction summary query (tránh N+1)
- `FeedResponse` DTO
- `GET /api/v1/posts/feed` endpoint
- Unit test handler (visibility, cursor, reaction summary)
- Integration test

---

### Ticket 6: [API] Get Post Detail

**Tasks:**
- `GetPostDetailQuery` + `GetPostDetailQueryHandler`
- Access control (visibility + friendship check)
- `GET /api/v1/posts/{postId:guid}` endpoint
- Unit test + Integration test

---

### Ticket 7: [API] Update Post

**Tasks:**
- `UpdatePostCommand` + `UpdatePostCommandHandler`
- `PATCH /api/v1/posts/{postId:guid}` endpoint
- Unit test + Integration test

---

### Ticket 8: [API] Soft Delete Post

**Tasks:**
- `DeletePostCommand` + `DeletePostCommandHandler`
- `DELETE /api/v1/posts/{postId:guid}` endpoint
- Unit test + Integration test

---

### Ticket 9: [API] Create/Update Post Reaction

**Tasks:**
- `UpsertPostReactionCommand` + `UpsertPostReactionCommandHandler`
- `UpsertPostReactionCommandValidator`
- `PostReactionRepository` implementation (upsert logic)
- `PUT /api/v1/posts/{postId:guid}/reaction` endpoint
- Unit test (tạo mới, update, no-op, race condition)
- Integration test

---

### Ticket 10: [API] Delete Post Reaction

**Tasks:**
- `DeletePostReactionCommand` + `DeletePostReactionCommandHandler`
- `DELETE /api/v1/posts/{postId:guid}/reaction` endpoint
- Unit test (idempotent, reaction không tồn tại)
- Integration test

---

### Ticket 11: [Tests] Full Test Suite

**Tasks:**
- Unit tests: mọi handler (CreatePost, GetFeed, GetDetail, Update, Delete, Upsert/DeleteReaction)
- Integration tests: mọi endpoint với test database
- Edge case tests: media validation, video duration, visibility, cursor pagination, reaction upsert idempotency
- Error case tests: 401, 403, 404, 400 cho mọi endpoint

---

*Spec này đã được review dựa trên codebase Beacon hiện tại (2026-05-16). Mọi thay đổi về domain hoặc business rule cần được cập nhật vào spec trước khi implement.*
