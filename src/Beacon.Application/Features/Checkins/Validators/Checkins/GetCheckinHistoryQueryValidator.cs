using Beacon.Application.Features.Checkins.Queries.GetCheckinHistory;
using FluentValidation;

namespace Beacon.Application.Features.Checkins.Validators.Checkins;

public class GetCheckinHistoryQueryValidator : AbstractValidator<GetCheckinHistoryQuery>
{
    public GetCheckinHistoryQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit phải trong khoảng 1–100.");
    }
}
