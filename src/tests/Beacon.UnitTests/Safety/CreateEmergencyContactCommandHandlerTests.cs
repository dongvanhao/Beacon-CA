using Beacon.Application.Features.Safety.Commands.CreateEmergencyContact;
using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Safety;

public class CreateEmergencyContactCommandHandlerTests
{
    private readonly Mock<IEmergencyContactRepository> _repoMock = new();
    private readonly EmergencyContactMapper _mapper = new();
    private readonly CreateEmergencyContactCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public CreateEmergencyContactCommandHandlerTests()
    {
        _sut = new CreateEmergencyContactCommandHandler(_repoMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreateContact()
    {
        // Arrange
        _repoMock.Setup(r => r.CountActiveByUserIdAsync(UserId, default)).ReturnsAsync(2);

        var request = new CreateEmergencyContactRequest(
            "Nguyen Van A", "0912345678", ContactChannelType.Phone, "Cha", 1);

        // Act
        var result = await _sut.Handle(
            new CreateEmergencyContactCommand(UserId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FullName.Should().Be("Nguyen Van A");
        result.Value.ChannelType.Should().Be(ContactChannelType.Phone.ToString());
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Safety.EmergencyContact>(), default), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLimitExceeded_ShouldReturnLimitExceededError()
    {
        // Arrange
        _repoMock.Setup(r => r.CountActiveByUserIdAsync(UserId, default)).ReturnsAsync(5);

        var request = new CreateEmergencyContactRequest(
            "Nguyen Van X", "0900000000", ContactChannelType.Phone, null, 1);

        // Act
        var result = await _sut.Handle(
            new CreateEmergencyContactCommand(UserId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Safety.EMERGENCY_CONTACT_LIMIT_EXCEEDED);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Safety.EmergencyContact>(), default), Times.Never);
    }
}
