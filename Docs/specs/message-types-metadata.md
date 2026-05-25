# Message Type Metadata

Messages have a `type` and an optional `metadata` object.

- Database column: `Messages.MetadataJson`
- API/realtime field: `metadata`
- Normal user messages usually have `metadata: null`
- System messages use `metadata` to carry structured data for the client

## MessageType Values

| Value | Name | Metadata |
| --- | --- | --- |
| 0 | `Normal` | `null` |
| 1 | `NicknameChanged` | currently unused |
| 2 | `RoleChanged` | object |
| 3 | `MemberAdded` | object |
| 4 | `MemberLeft` | object |
| 5 | `MemberNicknameChanged` | object |
| 6 | `GroupAvatarChanged` | object |
| 7 | `GroupDeleted` | object |
| 8 | `GroupApprovalSettingChanged` | `null` |
| 9 | `MemberApproved` | object |
| 10 | `MemberDenied` | object |
| 11 | `GroupNameChanged` | object |

## Common Fields

Most system metadata includes:

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001"
}
```

`actorUserId` is the user who performed the action.

## Member Object Shape

Used by `MemberAdded` and `MemberApproved`.

```json
{
  "userId": "bbbbbbbb-0000-0000-0000-000000000002",
  "familyName": "Tran",
  "givenName": "Bob",
  "avatarUrl": null,
  "role": 0,
  "status": 0,
  "lastSeenMessageId": null
}
```

`role`: `0 = Member`, `1 = Owner`, `2 = Manager`.

`status`: `0 = Joined`, `1 = PendingApproval`.

## Normal

Type: `0`

```json
null
```

## RoleChanged

Type: `2`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "userId": "bbbbbbbb-0000-0000-0000-000000000002",
  "role": 2
}
```

`userId` is the member whose role was changed.

## MemberAdded

Type: `3`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "members": [
    {
      "userId": "bbbbbbbb-0000-0000-0000-000000000002",
      "familyName": "Tran",
      "givenName": "Bob",
      "avatarUrl": null,
      "role": 0,
      "status": 0,
      "lastSeenMessageId": null
    }
  ]
}
```

## MemberLeft

Type: `4`

When the user leaves by themselves:

```json
{
  "actorUserId": "bbbbbbbb-0000-0000-0000-000000000002",
  "userId": "bbbbbbbb-0000-0000-0000-000000000002"
}
```

When a member is removed by another user:

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "userId": "bbbbbbbb-0000-0000-0000-000000000002",
  "removedByUserId": "aaaaaaaa-0000-0000-0000-000000000001"
}
```

`userId` is the member who left or was removed.

## MemberNicknameChanged

Type: `5`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "userId": "bbbbbbbb-0000-0000-0000-000000000002",
  "customName": "Bob New Name"
}
```

`customName: null` means the nickname was cleared.

## GroupAvatarChanged

Type: `6`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "avatarUrl": "https://example.com/group-avatar.jpg"
}
```

## GroupDeleted

Type: `7`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "groupId": "cccccccc-0000-0000-0000-000000000003"
}
```

## GroupApprovalSettingChanged

Type: `8`

```json
null
```

The message content says whether approval was enabled or disabled.

## MemberApproved

Type: `9`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "member": {
    "userId": "bbbbbbbb-0000-0000-0000-000000000002",
    "familyName": "Tran",
    "givenName": "Bob",
    "avatarUrl": null,
    "role": 0,
    "status": 0,
    "lastSeenMessageId": null
  }
}
```

## MemberDenied

Type: `10`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "userId": "bbbbbbbb-0000-0000-0000-000000000002"
}
```

## GroupNameChanged

Type: `11`

```json
{
  "actorUserId": "aaaaaaaa-0000-0000-0000-000000000001",
  "name": "New group name"
}
```
