using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RegisterDeviceCommandHandler(
    IUserDeviceRepository deviceRepository,
    ICurrentUserService currentUser) : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand command, CancellationToken ct)
    {
        // Lấy DeviceId từ JWT claim — được ghi lúc login
        var deviceId = currentUser.DeviceId;
        if (deviceId == Guid.Empty)
            return Result.Failure(
                Error.Unauthorized(ErrorCodes.Identity.TOKEN_INVALID, "Device session not found in token."));

        var device = await deviceRepository.GetByIdAsync(deviceId, ct);
        if (device is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.TOKEN_INVALID, "Device session not found."));

        // Cập nhật FCM/APNs token để server có thể gửi push notification sau này
        device.UpdateToken(command.Request.DeviceToken);
        device.RecordActivity();

        await deviceRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
