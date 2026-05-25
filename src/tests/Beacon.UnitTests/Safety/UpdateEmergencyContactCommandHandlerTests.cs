using Beacon.Application.Features.Safety.Commands.UpdateEmergencyContact;
using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Safety;

public class UpdateEmergencyContactCommandHandlerTests
{
    private readonly Mock<IEmergencyContactRepository> _repoMock = new();
    private readonly EmergencyContactMapper _mapper = new();
    private readonly UpdateEmergencyContactCommandHandler _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    public UpdateEmergencyContactCommandHandlerTests()
    {
        _sut = new UpdateEmergencyContactCommandHandler(_repoMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenValidOwner_ShouldUpdateContact()
    {
        // Arrange
        var contact = EmergencyContact.Create(OwnerId, "Old Name", "0900000000", ContactChannelType.Phone, "Cha");
        var contactId = contact.Id;

        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync(contact);

        var request = new UpdateEmergencyContactRequest("New Name", "newval@email.com", ContactChannelType.Email, "Me", 2);

        // Act
        var result = await _sut.Handle(
            new UpdateEmergencyContactCommand(OwnerId, contactId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FullName.Should().Be("New Name");
        result.Value.ChannelType.Should().Be(ContactChannelType.Email.ToString());
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenContactNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync((EmergencyContact?)null);

        var request = new UpdateEmergencyContactRequest("Name", "val", ContactChannelType.Phone, null, 1);

        // Act
        var result = await _sut.Handle(
            new UpdateEmergencyContactCommand(OwnerId, contactId, request), CancellationToken.None);

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

        var request = new UpdateEmergencyContactRequest("Name2", "val2", ContactChannelType.Phone, null, 1);

        // Act — OtherId không phải owner
        var result = await _sut.Handle(
            new UpdateEmergencyContactCommand(OtherId, contactId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN);
    }
}
