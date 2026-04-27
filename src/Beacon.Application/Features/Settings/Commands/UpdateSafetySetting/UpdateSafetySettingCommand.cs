using Beacon.Application.Features.Settings.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Settings.Commands.UpdateSafetySetting;

public record UpdateSafetySettingCommand(Guid UserId, UpdateSafetySettingRequest Request)
    : IRequest<Result<SafetySettingDto>>;
