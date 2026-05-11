using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using FluentAssertions;

namespace Beacon.UnitTests.Messaging;

public class MessageGroupMapperTests
{
    [Fact]
    public void ToDto_ShouldMapPeerUserId()
    {
        var peerUserId = Guid.NewGuid();
        var summary = new MessageGroupSummary(
            GroupId: Guid.NewGuid(),
            Type: MessageGroupType.Direct,
            DirectKey: "direct-key",
            PeerUserId: peerUserId,
            CreatedAtUtc: DateTime.UtcNow,
            LastMessageId: null,
            LastMessageContent: null,
            LastMessageAtUtc: null,
            LastMessageSenderFamilyName: null,
            LastMessageSenderGivenName: null,
            LastSeenMessageId: null,
            IsSeenLatest: true,
            UnreadCount: 0,
            DisplayName: "Peer User",
            AvatarObjectKey: null);

        var dto = new MessageGroupMapper().ToDto(summary);

        dto.PeerUserId.Should().Be(peerUserId);
    }
}
