using Beacon.Application.Features.Safety.Commands.DeleteEmergencyContact;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Safety;

public class DeleteEmergencyContactCommandHandlerTests
{
    private readonly Mock<IEmergencyContactRepository> _repoMock = new();
    private readonly DeleteEmergencyContactCommandHandler _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    public DeleteEmergencyContactCommandHandlerTests()
    {
        _sut = new DeleteEmergencyContactCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenValidOwner_ShouldSoftDeleteContact()
    {
        // Arrange
        var contact = EmergencyContact.Create(OwnerId, "Name", "val", ContactChannelType.Phone);
        var contactId = contact.Id;
        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync(contact);

        // Act
        var result = await _sut.Handle(
            new DeleteEmergencyContactCommand(OwnerId, contactId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        contact.IsActive.Should().BeFalse();
        contact.IsDeleted.Should().BeTrue();
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenContactNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync((EmergencyContact?)null);

        // Act
        var result = await _sut.Handle(
            new DeleteEmergencyContactCommand(OwnerId, contactId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Safety.EMERGENCY_CONTACT_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ShouldReturnForbiddenError()
    {
        // Arrange
        var contact = EmergencyContact.Create(OwnerId, "Name", "val", ContactChannelType.Phone);
        var contactId = contact.Id;
        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync(contact);

        // Act — OtherId không phải owner
        var result = await _sut.Handle(
            new DeleteEmergencyContactCommand(OtherId, contactId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN);
    }
}
