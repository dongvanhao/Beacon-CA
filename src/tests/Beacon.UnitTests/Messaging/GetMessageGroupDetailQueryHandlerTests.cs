using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Queries.GetMessageGroupDetail;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class GetMessageGroupDetailQueryHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IMessageGroupMemberSettingRepository> _settingRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly MessageGroupDetailMapper _mapper = new();
    private readonly GetMessageGroupDetailQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetMessageGroupDetailQueryHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);

        _sut = new GetMessageGroupDetailQueryHandler(
            _groupRepoMock.Object,
            _settingRepoMock.Object,
            _currentUserMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private static MessageGroup BuildGroup(Guid groupId, params (Guid UserId, User User)[] members)
    {
        var group = new MessageGroup
        {
            Type = MessageGroupType.Direct,
            CreatedAtUtc = DateTime.UtcNow
        };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);

        foreach (var (userId, user) in members)
        {
            group.Members.Add(new MessageGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                User = user
            });
        }

        return group;
    }

    private static MessageGroupMember BuildMember(
        Guid groupId,
        Guid userId,
        User user,
        GroupMemberRole role = GroupMemberRole.Member,
        MessageGroupMemberStatus status = MessageGroupMemberStatus.Joined)
        => new()
        {
            GroupId = groupId,
            UserId = userId,
            User = user,
            Role = role,
            Status = status,
            JoinedAtUtc = DateTime.UtcNow
        };

    [Fact]
    public async Task Handle_WithValidGroupAndMember_ReturnsDetailWithMembers()
    {
        var groupId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Van B");
        var otherUser = User.Create("friend", "friend@example.com", "hash", "Nguyen", "Van A");

        var group = BuildGroup(groupId,
            (_currentUserId, currentUser),
            (otherUserId, otherUser));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GroupId.Should().Be(groupId);
        result.Value.Type.Should().Be(MessageGroupType.Direct);
        result.Value.Members.Should().HaveCount(2);
        result.Value.Members.Should().Contain(m => m.FamilyName == "Tran");
        result.Value.Members.Should().Contain(m => m.FamilyName == "Nguyen");
    }

    [Fact]
    public async Task Handle_WhenGroupNotFound_ReturnsNotFoundError()
    {
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenUserNotMember_ReturnsForbiddenError()
    {
        var groupId = Guid.NewGuid();
        var otherUser = User.Create("other", "other@example.com", "hash", "Nguyen", "Van A");
        var group = BuildGroup(groupId, (Guid.NewGuid(), otherUser));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoAvatar_ReturnsNullAvatarUrl()
    {
        var groupId = Guid.NewGuid();
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Van B");
        var group = BuildGroup(groupId, (_currentUserId, currentUser));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Members[0].AvatarUrl.Should().BeNull();
        _storageMock.Verify(
            s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMemberHasAvatar_PopulatesAvatarUrl()
    {
        var groupId = Guid.NewGuid();
        var avatar = MediaObject.Create("bucket", "avatar.jpg", "avatar.jpg", "image/jpeg", 1024, MediaType.Image);
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Van B");
        currentUser.UpdateAvatar(avatar.Id);
        currentUser.GetType().GetProperty("AvatarMediaObject")!.SetValue(currentUser, avatar);

        var group = BuildGroup(groupId, (_currentUserId, currentUser));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://presigned-url/avatar.jpg");

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Members[0].AvatarUrl.Should().Be("https://presigned-url/avatar.jpg");
    }

    [Fact]
    public async Task Handle_MultipleAvatars_UsesOneBatchCall()
    {
        var groupId = Guid.NewGuid();
        var avatar1 = MediaObject.Create("bucket", "a1.jpg", "a1.jpg", "image/jpeg", 1024, MediaType.Image);
        var avatar2 = MediaObject.Create("bucket", "a2.jpg", "a2.jpg", "image/jpeg", 1024, MediaType.Image);

        var user1 = User.Create("user1", "u1@example.com", "hash", "A", "B");
        user1.UpdateAvatar(avatar1.Id);
        user1.GetType().GetProperty("AvatarMediaObject")!.SetValue(user1, avatar1);

        var user2 = User.Create("user2", "u2@example.com", "hash", "C", "D");
        user2.UpdateAvatar(avatar2.Id);
        user2.GetType().GetProperty("AvatarMediaObject")!.SetValue(user2, avatar2);

        var group = BuildGroup(groupId, (_currentUserId, user1), (Guid.NewGuid(), user2));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://presigned-url");

        await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        // GetMediaUrlsBatchAsync calls GeneratePresignedGetUrlAsync once per media (no ThenInclude thumbnail here)
        _storageMock.Verify(
            s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenSettingExists_ReturnsCurrentUserSetting()
    {
        var groupId = Guid.NewGuid();
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Member");
        var group = BuildGroup(groupId, (_currentUserId, currentUser));
        var setting = MessageGroupMemberSetting.Create(groupId, _currentUserId);
        setting.UpdateCustomName("My chat");
        setting.SetMuted(true);

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _settingRepoMock
            .Setup(r => r.GetByGroupAndUserAsync(groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Setting.CustomName.Should().Be("My chat");
        result.Value.Setting.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsMember_ReturnsOnlyJoinedMembers()
    {
        var groupId = Guid.NewGuid();
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Member");
        var joinedUser = User.Create("joined", "joined@example.com", "hash", "Nguyen", "Joined");
        var pendingUser = User.Create("pending", "pending@example.com", "hash", "Le", "Pending");
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);
        group.Members.Add(BuildMember(groupId, _currentUserId, currentUser, GroupMemberRole.Member));
        group.Members.Add(BuildMember(groupId, Guid.NewGuid(), joinedUser));
        group.Members.Add(BuildMember(groupId, Guid.NewGuid(), pendingUser, status: MessageGroupMemberStatus.PendingApproval));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Members.Should().HaveCount(2);
        result.Value.Members.Should().OnlyContain(m => m.Status == MessageGroupMemberStatus.Joined);
    }

    [Fact]
    public async Task Handle_WhenCallerIsManager_ReturnsAllMemberStatuses()
    {
        var groupId = Guid.NewGuid();
        var currentUser = User.Create("me", "me@example.com", "hash", "Tran", "Manager");
        var joinedUser = User.Create("joined", "joined@example.com", "hash", "Nguyen", "Joined");
        var pendingUser = User.Create("pending", "pending@example.com", "hash", "Le", "Pending");
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);
        group.Members.Add(BuildMember(groupId, _currentUserId, currentUser, GroupMemberRole.Manager));
        group.Members.Add(BuildMember(groupId, Guid.NewGuid(), joinedUser));
        group.Members.Add(BuildMember(groupId, Guid.NewGuid(), pendingUser, status: MessageGroupMemberStatus.PendingApproval));

        _groupRepoMock
            .Setup(r => r.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new GetMessageGroupDetailQuery(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Members.Should().HaveCount(3);
        result.Value.Members.Should().Contain(m => m.Status == MessageGroupMemberStatus.PendingApproval);
    }
}
