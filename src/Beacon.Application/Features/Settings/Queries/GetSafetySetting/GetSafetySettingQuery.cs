using Beacon.Application.Features.Settings.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Settings.Queries.GetSafetySetting;

public record GetSafetySettingQuery(Guid UserId) : IRequest<Result<SafetySettingDto>>;
