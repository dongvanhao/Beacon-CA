# Feature: Friend & Messaging

## Objective

Implement a social layer for Beacon: users can send/accept friend requests, manage a friend list with relationship types, and exchange private messages in a 1-1 group-chat model. Pagination uses cursor-based infinite scroll throughout.

## Target Users

- **Authenticated users** (`[Authorize]`) — send/accept/decline friend requests, list friends, send/read messages.
- No admin-only endpoints in MVP. Admin visibility of this data is out of scope.

---

## Core Features & Use Cases

### 1. Friend Requests

| # | Use Case | Acceptance Criteria |
|---|---|---|
| 1.1 | Send friend request | Creates a `FriendRequest` with status `Pending`. Fails if: self-request, duplicate pending request, or already friends. |
| 1.2 | Accept friend request | Receiver only. Creates `Friend` record + private `MessageGroup` + 2 `MessageGroupMember` rows, marks request `Accepted`. Atomic (single transaction). |
| 1.3 | Decline friend request | Receiver only. Marks request `Declined`. No friend or group created. |
| 1.4 | List received requests | Paginated cursor list of incoming `Pending` requests for current user. |
| 1.5 | List sent requests | Paginated cursor list of outgoing requests by current user. |

### 2. Friend Management

| # | Use Case | Acceptance Criteria |
|---|---|---|
| 2.1 | List friends | Cursor-paginated list of friends (either side of the symmetric pair). |
| 2.2 | Get friend detail | Return friendship type and shared `MessageGroup` id. |
| 2.3 | Update friend type | Change `type` (Family / CloseFriend / Normal / Custom) on an existing friendship. |
| 2.4 | Remove friend | Delete `Friend` record (hard delete — not sensitive lookup data). |

### 3. Private Messaging

| # | Use Case | Acceptance Criteria |
|---|---|---|
| 3.1 | Send message | Sender must be a `MessageGroupMember`. Creates a `Message`. |
| 3.2 | List messages in group | Cursor-based (by `createdAt DESC`), returns `items`, `nextCursor`, `hasMore`. Only group members can read. |
| 3.3 | List my message groups | Returns all groups the current user belongs to, with last message preview. Cursor-paginated. |

---

## Out of Scope (MVP)

- Group chats (more than 2 members)
- Message read receipts / delivery status
- Message editing or deletion
- Push notifications on new messages
- Block / mute users
- Friend suggestions / recommendations
- Admin moderation tools

---

## Technical Approach

### Domain Layer (`Beacon.Domain`)

**New Entities** — placed in `Domain/Entities/Group/` and `Domain/Entities/Messaging/`:

```
Friend              → BaseEntity (no audit needed; createdAt via BaseEntity or manual prop)
FriendRequest       → BaseEntity
MessageGroup        → BaseEntity
MessageGroupMember  → (composite key: GroupId + UserId — no surrogate PK needed)
Message             → BaseEntity
```

**Entity Sketches:**

```csharp
// Domain/Entities/Group/Friend.cs
// FIX #1: MessageGroupId stored directly on Friend — avoids JOIN when resolving groupId from friendship
public class Friend : BaseEntity
{
    public Guid UserId1 { get; set; }         // always Min(senderId, receiverId)
    public Guid UserId2 { get; set; }         // always Max(senderId, receiverId)
    public FriendType Type { get; set; }
    public Guid MessageGroupId { get; set; }  // FK to the private MessageGroup created at accept time
    public DateTime CreatedAtUtc { get; set; }
    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;
    public MessageGroup MessageGroup { get; set; } = null!;
}

// Domain/Entities/Group/FriendRequest.cs
public class FriendRequest : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public FriendRequestStatus Status { get; set; }  // enum
    public DateTime CreatedAtUtc { get; set; }
}

// Domain/Entities/Messaging/MessageGroup.cs
public class MessageGroup : BaseEntity
{
    public bool IsPrivate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<MessageGroupMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}

// Domain/Entities/Messaging/MessageGroupMember.cs
public class MessageGroupMember          // no BaseEntity — composite PK
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public MessageGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}

// Domain/Entities/Messaging/Message.cs
public class Message : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public MessageGroup Group { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
```

**New Enums** — `Domain/Enums/Group/`:

```csharp
public enum FriendType    { Family, CloseFriend, Normal, Custom }
public enum FriendRequestStatus { Pending, Accepted, Declined }
```

**Repository Interfaces** — `Domain/IRepository/Group/` & `Domain/IRepository/Messaging/`:

```csharp
IFriendRepository
{
    Task<Friend?> GetByUsersAsync(Guid userId1, Guid userId2, CancellationToken ct);  // canonical Min/Max
    Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct);
    Task AddAsync(Friend friend, CancellationToken ct);
    Task DeleteAsync(Friend friend, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

IFriendRequestRepository
{
    // FIX #2: checks BOTH directions (A→B and B→A) to catch bidirectional duplicates
    Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct);
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(FriendRequest req, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

IMessageGroupRepository
{
    Task<MessageGroup?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(MessageGroup group, CancellationToken ct);
    // FIX #3: required by RemoveFriendCommand to evict both members before deleting Friend
    Task RemoveMembersAsync(Guid groupId, Guid userId1, Guid userId2, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

IMessageRepository
{
    Task AddAsync(Message message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

---

### Application Layer (`Beacon.Application`)

**Commands:**

| Command | Handler responsibility |
|---|---|
| `SendFriendRequestCommand` | Guard: self-send → `SELF_FRIEND_REQUEST`; duplicate bidirectional (A→B OR B→A with Pending) → `FRIEND_REQUEST_DUPLICATE`; already friends → `ALREADY_FRIENDS`. Create `FriendRequest`. |
| `AcceptFriendRequestCommand` | Guard: ownership, already-accepted → `Result.Failure`. Transactional: create Friend (with `MessageGroupId`) + MessageGroup + 2 Members, mark Accepted. |
| `DeclineFriendRequestCommand` | Guard: ownership, wrong status → `Result.Failure`. Mark `Declined`. |
| `UpdateFriendTypeCommand` | Guard: must be friend. Update `FriendType`. |
| `RemoveFriendCommand` | Guard: must be friend. Delete `Friend` record **and** both `MessageGroupMember` rows. `MessageGroup` stays (orphaned, harmless). Future `SendMessage` blocked by membership check. |
| `SendMessageCommand` | Guard: sender must be `MessageGroupMember`. Create Message. |

**Queries:**

| Query | Returns |
|---|---|
| `ListReceivedFriendRequestsQuery` | `CursorPagedResult<FriendRequestDto>` |
| `ListSentFriendRequestsQuery` | `CursorPagedResult<FriendRequestDto>` |
| `ListFriendsQuery` | `CursorPagedResult<FriendDto>` |
| `GetFriendDetailQuery` | `FriendDetailDto` |
| `ListMessagesQuery` | `CursorPagedResult<MessageDto>` (by groupId, cursor = createdAt DESC) |
| `ListMyMessageGroupsQuery` | `CursorPagedResult<MessageGroupSummaryDto>` |

**DTOs** (in `Application/Features/{Group|Messaging}/Dtos/`):

```
FriendRequestDto    { Id, SenderId, SenderUsername, SenderAvatarUrl, CreatedAtUtc }
FriendDto           { UserId, Username, AvatarUrl, Type, CreatedAtUtc, MessageGroupId }
FriendDetailDto     extends FriendDto
MessageDto          { Id, SenderId, SenderUsername, Content, CreatedAtUtc }
MessageGroupSummaryDto { GroupId, OtherUserId, OtherUsername, OtherAvatarUrl, LastMessage, LastMessageAtUtc }
```

**Validators** — `Application/Features/{Module}/Validators/`:

`ValidationBehavior` (MediatR pipeline) auto-runs all validators before the handler — no manual invocation needed.

- `SendFriendRequestCommandValidator`
  - `ReceiverId` not empty / not `Guid.Empty`
  - **DO NOT** check self-send here — validator has no access to `ICurrentUserService`; self-check is handler-only business guard
- `SendMessageCommandValidator`
  - `Content` not empty, max length 4 000 chars
  - `GroupId` not empty / not `Guid.Empty`

Validation failure → `ValidationBehavior` throws `ValidationException(errors)` → `ExceptionHandlingMiddleware` catches → **400** with `{ success: false, code: "VALIDATION_ERROR", errors: [...] }`. Controller has no try/catch.

---

### Infrastructure Layer (`Beacon.Infrashtructure`)

**EF Configurations** — `Infrashtructure/Presistence/Configuration/Group/` & `.../Messaging/`:

```
FriendConfiguration
  → Table "Friends"
  → UNIQUE INDEX (UserId1, UserId2)            — canonical pair enforces no duplicates
  → FK MessageGroupId → MessageGroups.Id       — FIX #1: direct mapping, no JOIN needed

FriendRequestConfiguration
  → Table "FriendRequests"
  → No DB unique constraint on (SenderId, ReceiverId)
    Reason: bidirectional duplicate (B→A when A→B Pending) must be caught at application
    layer by querying both directions — a partial unique index can't express this cross-row
    bidirectionality cleanly. Handler is authoritative.

MessageGroupConfiguration   → Table "MessageGroups"

MessageGroupMemberConfiguration
  → Table "MessageGroupMembers"
  → Composite PK (GroupId, UserId)
  → INDEX IX_MessageGroupMembers_UserId        — FIX #4: required for "list my groups" query

MessageConfiguration
  → Table "Messages"
  → INDEX IX_Messages_GroupId_CreatedAtUtc DESC
```

**Soft-delete**: None of these entities need `SoftDeletableEntity` in MVP (Friend removal = hard delete; messages are immutable in MVP).

**Repository implementations** — `Infrashtructure/Repository/Group/` & `.../Messaging/`

**DbContext additions**: 5 new `DbSet<T>` properties.

**Migration**: single migration `Add_Friend_And_Messaging_Tables`.

---

### API Layer (`Beacon.Api`)

**Controllers:**

| Controller | Route prefix |
|---|---|
| `FriendRequestsController` | `api/v1/friend-requests` |
| `FriendsController` | `api/v1/friends` |
| `MessageGroupsController` | `api/v1/message-groups` |

**Endpoints:**

| Method | Route | Controller method | HTTP |
|---|---|---|---|
| POST | `api/v1/friend-requests` | `CreatedResult("api/v1/friend-requests", result)` | **201** |
| GET | `api/v1/friend-requests/received` | `HandleResult(result)` | 200 |
| GET | `api/v1/friend-requests/sent` | `HandleResult(result)` | 200 |
| PATCH | `api/v1/friend-requests/{id:guid}/accept` | `HandleResult(result)` | 200 |
| PATCH | `api/v1/friend-requests/{id:guid}/decline` | `HandleResult(result)` | 200 |
| GET | `api/v1/friends` | `HandleResult(result)` | 200 |
| GET | `api/v1/friends/{userId:guid}` | `HandleResult(result)` | 200 |
| PATCH | `api/v1/friends/{userId:guid}/type` | `HandleResult(result)` | 200 |
| DELETE | `api/v1/friends/{userId:guid}` | `HandleResult(result)` — `data: null` (NO 204) | 200 |
| GET | `api/v1/message-groups` | `HandleResult(result)` | 200 |
| GET | `api/v1/message-groups/{groupId:guid}/messages` | `HandleResult(result)` | 200 |
| POST | `api/v1/message-groups/{groupId:guid}/messages` | `CreatedResult("api/v1/message-groups/{groupId}/messages", result)` | **201** |

All endpoints: `[Authorize]`. No `[AdminOnly]` or `[HasPermission]` needed for MVP.

Query params for list endpoints: `?cursor=<ISO-UTC>&limit=20`.

---

## Error Codes (add to `ErrorCodes.cs`)

Must be nested inside the existing `public static class ErrorCodes { }` — same pattern as `ErrorCodes.Identity`, `ErrorCodes.Storage`, etc.

```csharp
// Beacon.Shared/Constants/ErrorCodes.cs
public static class ErrorCodes
{
    // ... existing classes unchanged ...

    public static class Friend
    {
        public const string SELF_FRIEND_REQUEST        = "SELF_FRIEND_REQUEST";
        public const string FRIEND_REQUEST_DUPLICATE   = "FRIEND_REQUEST_DUPLICATE";
        public const string ALREADY_FRIENDS            = "ALREADY_FRIENDS";
        public const string FRIEND_REQUEST_NOT_FOUND   = "FRIEND_REQUEST_NOT_FOUND";
        public const string FRIEND_REQUEST_NOT_PENDING = "FRIEND_REQUEST_NOT_PENDING";
        public const string FRIEND_REQUEST_FORBIDDEN   = "FRIEND_REQUEST_FORBIDDEN";
        public const string FRIEND_NOT_FOUND           = "FRIEND_NOT_FOUND";
    }

    public static class Messaging
    {
        public const string MESSAGE_GROUP_NOT_FOUND  = "MESSAGE_GROUP_NOT_FOUND";
        public const string MESSAGE_GROUP_FORBIDDEN  = "MESSAGE_GROUP_FORBIDDEN";
    }
}
```

Usage in handlers: `ErrorCodes.Friend.FRIEND_NOT_FOUND`, `ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN`.

---

## Pagination Contract

All list endpoints return `ApiResponse<CursorPagedResult<T>>`. Shape matches the actual `CursorPagedResult<T>` class in `Beacon.Shared`:

```json
{
  "success": true,
  "message": "Success",
  "code": null,
  "data": {
    "data": [ { "..." } ],
    "meta": {
      "nextCursor": "2025-05-01T10:00:00.000Z",
      "limit": 20,
      "hasMore": true
    }
  },
  "errors": null
}
```

- `data.data` — the item array (`List<T>`)
- `data.meta.nextCursor` — ISO-8601 UTC datetime, `null` when no more pages
- `data.meta.hasMore` — `false` on last page
- `data.meta.limit` — echoes the requested limit

Query params: `?cursor=<ISO-UTC>&limit=20` (max 50 for messages, max 100 for friends).

**Failure response** (same envelope, `data: null`):
```json
{ "success": false, "message": "Not found", "code": "FRIEND_NOT_FOUND", "data": null, "errors": null }
```

---

## Edge Case Handling

| Edge Case | Handler Guard | Error |
|---|---|---|
| Self friend request | `senderId == receiverId` | `SELF_FRIEND_REQUEST` (Validation) |
| Duplicate pending request | Query both directions: `(s=A,r=B OR s=B,r=A) AND Pending` | `FRIEND_REQUEST_DUPLICATE` (Conflict) |
| Already friends | Query Friend table | `ALREADY_FRIENDS` (Conflict) |
| Accept twice | Status != Pending | `FRIEND_REQUEST_NOT_PENDING` (Conflict) |
| Accept someone else's request | receiverId != currentUser | `FRIEND_REQUEST_FORBIDDEN` (Forbidden) |
| Decline someone else's request | same as above | `FRIEND_REQUEST_FORBIDDEN` (Forbidden) |
| Send message to group not a member of | Check membership | `MESSAGE_GROUP_FORBIDDEN` (Forbidden) |

---

## Error Track — Result vs Exception

ALL business guards in handlers use `Result.Failure(Error.X(...))`. Exceptions are reserved for unrecoverable infrastructure failures only (DB connection, MinIO). Controllers have zero try/catch.

| Guard | Track | Factory |
|---|---|---|
| Resource not found | `Result.Failure` | `Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "...")` |
| Self-send, invalid input shape | `Result.Failure` | `Error.Validation(ErrorCodes.Friend.SELF_FRIEND_REQUEST, "...")` |
| Duplicate request / already friends | `Result.Failure` | `Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_DUPLICATE, "...")` |
| Wrong owner (accept/decline) | `Result.Failure` | `Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "...")` |
| Non-member sending message | `Result.Failure` | `Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "...")` |
| DB/MinIO down | `throw` | `throw new SomeInfraException(...)` → caught by `ExceptionHandlingMiddleware` → 500 |

## Multi-Repository Transaction Note

`AcceptFriendRequestCommandHandler` and `RemoveFriendCommandHandler` each touch **3 repositories** in one transaction. This works because all repositories receive the same `AppDbContext` instance (scoped DI per request). A single `SaveChangesAsync()` call at the end flushes all tracked changes atomically — no explicit `BeginTransactionAsync` needed.

```csharp
// All repos share the same DbContext scope — one SaveChanges = one atomic commit
await _friendRepo.AddAsync(friend, ct);
await _messageGroupRepo.AddAsync(group, ct);
// members tracked via EF navigation or direct add
await _friendRequestRepo.SaveChangesAsync(ct);  // any repo's SaveChanges flushes all
```

---

## Transactional Flow — AcceptFriendRequest

```
BEGIN TRANSACTION
  1. Load FriendRequest by id → 404 if missing
  2. currentUser == receiverId → 403 if not
  3. status == Pending → 409 if already Accepted/Declined
  4. INSERT MessageGroup (isPrivate=true)                          ← create first to get groupId
  5. INSERT Friend (userId1=min(s,r), userId2=max(s,r),           ← FIX #1: store groupId here
                   type=Normal, messageGroupId=group.Id)
  6. INSERT MessageGroupMember (groupId, senderId)
  7. INSERT MessageGroupMember (groupId, receiverId)
  8. UPDATE FriendRequest status = Accepted
  9. SaveChangesAsync (single call covers all above — EF atomic)
COMMIT
```

> **Canonical normalization**: `userId1 = Min(senderId, receiverId)`, `userId2 = Max(...)`.
> The UNIQUE INDEX on `(UserId1, UserId2)` then guarantees no duplicate friendships at DB level,
> regardless of which user initiated the request.

---

## Transactional Flow — RemoveFriend

```
  1. Load Friend where (userId1=min(me,them), userId2=max(me,them)) → 404 if missing
  2. DELETE MessageGroupMember where GroupId = friend.MessageGroupId AND UserId = me
  3. DELETE MessageGroupMember where GroupId = friend.MessageGroupId AND UserId = them
  4. DELETE Friend
  5. SaveChangesAsync
     MessageGroup record is intentionally left as orphan — harmless, no cleanup needed in MVP.
     Future SendMessage by either user → membership check returns 403 (no member row exists).
```

---

## Testing Strategy

**Unit Tests** (`tests/Beacon.UnitTests/Group/` & `.../Messaging/`):

- `SendFriendRequestCommandHandlerTests` — self-request, duplicate, already-friends, success
- `AcceptFriendRequestCommandHandlerTests` — not found, forbidden, already-accepted, success (verify Friend + Group + Members created)
- `DeclineFriendRequestCommandHandlerTests` — forbidden, wrong status, success
- `SendMessageCommandHandlerTests` — not member, success

**Integration Tests** (`tests/Beacon.IntergrationTests/Group/` & `.../Messaging/`):

- `FriendRequestsControllerTests` — POST send, PATCH accept/decline, GET lists
- `FriendsControllerTests` — GET list/detail, PATCH type, DELETE
- `MessageGroupsControllerTests` — GET groups, GET messages, POST send message

---

## Resolved Design Decisions (from review)

| # | Decision | Rationale |
|---|---|---|
| FIX 1 | `Friend` stores `MessageGroupId` FK | O(1) lookup vs. JOIN through `MessageGroupMember` |
| FIX 2 | Duplicate request = bidirectional check in handler via `HasPendingBetweenAsync` | DB unique index cannot express `(A→B OR B→A)` condition in one constraint |
| FIX 3 | `UNIQUE (UserId1, UserId2)` on `Friend` enforced by canonical `Min/Max` normalization | Prevents duplicate friendship at DB level regardless of who initiated |
| FIX 4 | `INDEX IX_MessageGroupMembers_UserId` added | Required for `ListMyMessageGroupsQuery` (filter by user, not by group) |
| FIX 5 | `RemoveFriend` deletes both `MessageGroupMember` rows | `MessageGroup` left as orphan; future `SendMessage` blocked by membership check — no extra guard needed |

## Checklist Before `/plan`

- [ ] Confirm: message max-length constraint (proposed: 4000 chars)
- [ ] Confirm: no real-time (SignalR) in MVP — polling or future sprint
- [ ] Confirm: orphaned `MessageGroup` after unfriend is acceptable (no cleanup needed in MVP)

---

## Next Step

Run `/plan` to decompose into ordered vertical slices: Domain → Infra → Application → API → Tests.
