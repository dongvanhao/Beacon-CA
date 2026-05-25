using Beacon.Application.Features.Safety.Queries.GetEmergencyContacts;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Safety;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Safety;

public class GetEmergencyContactsQueryHandlerTests
{
    private readonly Mock<IEmergencyContactRepository> _repoMock = new();
    private readonly EmergencyContactMapper _mapper = new();
    private readonly GetEmergencyContactsQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public GetEmergencyContactsQueryHandlerTests()
    {
        _sut = new GetEmergencyContactsQueryHandler(_repoMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyUserContacts()
    {
        // Arrange
        var contacts = new List<EmergencyContact>
        {
            EmergencyContact.Create(UserId, "Nguyen Van A", "0912345678", ContactChannelType.Phone, "Cha"),
            EmergencyContact.Create(UserId, "Tran Thi B", "b@gmail.com", ContactChannelType.Email, "Me")
        };

        _repoMock.Setup(r => r.GetByUserIdAsync(UserId, default))
            .ReturnsAsync(contacts);

        // Act
        var result = await _sut.Handle(new GetEmergencyContactsQuery(UserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].FullName.Should().Be("Nguyen Van A");
    }

    [Fact]
    public async Task Handle_WhenNoContacts_ShouldReturnEmptyList()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(UserId, default))
            .ReturnsAsync(new List<EmergencyContact>());

        // Act
        var result = await _sut.Handle(new GetEmergencyContactsQuery(UserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
