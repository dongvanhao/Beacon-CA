using Beacon.Application.Features.Settings.Dtos;
using Beacon.Application.Mappings.Settings;
using Beacon.Domain.IRepository.Settings;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Settings.Queries.GetSafetySetting;

public class GetSafetySettingQueryHandler(
    ISafetySettingRepository repo,
    SafetySettingMapper mapper)
    : IRequestHandler<GetSafetySettingQuery, Result<SafetySettingDto>>
{
    public async Task<Result<SafetySettingDto>> Handle(GetSafetySettingQuery q, CancellationToken ct)
    {
        var setting = await repo.GetByUserIdAsync(q.UserId, ct);
        var dto = setting is null ? mapper.ToDefaultDto() : mapper.ToDto(setting);
        return Result<SafetySettingDto>.Success(dto);
    }
}
