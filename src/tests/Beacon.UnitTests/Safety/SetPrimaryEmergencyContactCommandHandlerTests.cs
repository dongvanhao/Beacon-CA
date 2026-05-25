using Beacon.Application.Features.Safety.Commands.SetPrimaryEmergencyContact;
using Beacon.Application.Mappings.Safety;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Safety;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Safety;

public class SetPrimaryEmergencyContactCommandHandlerTests
{
    private readonly Mock<IEmergencyContactRepository> _repoMock = new();
    private readonly EmergencyContactMapper _mapper = new();
    private readonly SetPrimaryEmergencyContactCommandHandler _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    public SetPrimaryEmergencyContactCommandHandlerTests()
    {
        _sut = new SetPrimaryEmergencyContactCommandHandler(_repoMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenNoPreviousPrimary_ShouldSetContactAsPrimary()
    {
        // Arrange
        var contact = EmergencyContact.Create(OwnerId, "Name", "val", ContactChannelType.Phone);
        _repoMock.Setup(r => r.GetByIdAsync(contact.Id, default)).ReturnsAsync(contact);
        _repoMock.Setup(r => r.GetPrimaryByUserIdAsync(OwnerId, default)).ReturnsAsync((EmergencyContact?)null);

        // Act
        var result = await _sut.Handle(
            new SetPrimaryEmergencyContactCommand(OwnerId, contact.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        contact.IsPrimary.Should().BeTrue();
        result.Value!.IsPrimary.Should().BeTrue();
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPreviousPrimaryExists_ShouldClearOldAndSetNew()
    {
        // Arrange
        var oldPrimary = EmergencyContact.Create(OwnerId, "Old", "old", ContactChannelType.Phone);
        oldPrimary.SetAsPrimary();

        var newContact = EmergencyContact.Create(OwnerId, "New", "new", ContactChannelType.Phone);

        _repoMock.Setup(r => r.GetByIdAsync(newContact.Id, default)).ReturnsAsync(newContact);
        _repoMock.Setup(r => r.GetPrimaryByUserIdAsync(OwnerId, default)).ReturnsAsync(oldPrimary);

        // Act
        var result = await _sut.Handle(
            new SetPrimaryEmergencyContactCommand(OwnerId, newContact.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        oldPrimary.IsPrimary.Should().BeFalse();
        newContact.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenContactNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(contactId, default)).ReturnsAsync((EmergencyContact?)null);

        // Act
        var result = await _sut.Handle(
            new SetPrimaryEmergencyContactCommand(OwnerId, contactId), CancellationToken.None);

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
        _repoMock.Setup(r => r.GetByIdAsync(contact.Id, default)).ReturnsAsync(contact);

        // Act — OtherId không phải owner
        var result = await _sut.Handle(
            new SetPrimaryEmergencyContactCommand(OtherId, contact.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Safety.EMERGENCY_CONTACT_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_WhenContactAlreadyPrimary_ShouldReturnSuccess_NoSideEffect()
    {
        // Arrange
        var contact = EmergencyContact.Create(OwnerId, "Name", "val", ContactChannelType.Phone);
        contact.SetAsPrimary();

        _repoMock.Setup(r => r.GetByIdAsync(contact.Id, default)).ReturnsAsync(contact);
        // GetPrimary trả chính contact đó (same Id) → không clear
        _repoMock.Setup(r => r.GetPrimaryByUserIdAsync(OwnerId, default)).ReturnsAsync(contact);

        // Act
        var result = await _sut.Handle(
            new SetPrimaryEmergencyContactCommand(OwnerId, contact.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        contact.IsPrimary.Should().BeTrue();
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
