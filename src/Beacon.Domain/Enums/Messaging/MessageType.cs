namespace Beacon.Domain.Enums.Messaging;

public enum MessageType
{
    Normal = 0,
    NicknameChanged = 1,
    RoleChanged = 2,
    MemberAdded = 3,
    MemberLeft = 4,
    MemberNicknameChanged = 5,
    GroupAvatarChanged = 6,
    GroupDeleted = 7,
    GroupApprovalSettingChanged = 8,
    MemberApproved = 9,
    MemberDenied = 10,
    GroupNameChanged = 11
}
