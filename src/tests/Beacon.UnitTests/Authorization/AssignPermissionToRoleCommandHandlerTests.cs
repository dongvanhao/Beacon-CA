using Beacon.Application.Features.Authorization.Roles.Commands.AssignPermissionToRole;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Authorization;

public class AssignPermissionToRoleCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IPermissionRepository> _permissionRepository = new();
    private readonly AssignPermissionToRoleCommandHandler _sut;

    public AssignPermissionToRoleCommandHandlerTests()
    {
        var mapper = new RoleMapper(new PermissionMapper());
        _sut = new AssignPermissionToRoleCommandHandler(
            _roleRepository.Object,
            _permissionRepository.Object,
            mapper);
    }

    [Fact]
    public async Task Handle_WhenRoleAlreadyHasPermission_ReturnsConflictWithoutRemovingPermission()
    {
        var role = Role.Create("Admin");
        var permission = Permission.Create("roles:read");
        var existingRolePermission = RolePermission.Create(role.Id, permission.Id);

        _roleRepository
            .Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _permissionRepository
            .Setup(r => r.GetByIdAsync(permission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);
        _roleRepository
            .Setup(r => r.GetRolePermissionAsync(role.Id, permission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRolePermission);

        var result = await _sut.Handle(
            new AssignPermissionToRoleCommand(role.Id, permission.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Authorization.ROLE_PERMISSION_ALREADY_EXISTS);
        _roleRepository.Verify(r => r.RemoveRolePermission(It.IsAny<RolePermission>()), Times.Never);
        _roleRepository.Verify(r => r.AddRolePermissionAsync(It.IsAny<RolePermission>(), It.IsAny<CancellationToken>()), Times.Never);
        _roleRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
