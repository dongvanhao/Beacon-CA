using Beacon.Application.Features.Messaging.Queries.GetMessageGroupDetail;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class GetMessageGroupDetailQueryValidator : AbstractValidator<GetMessageGroupDetailQuery>
{
    public GetMessageGroupDetailQueryValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("GroupId không hợp lệ.");
    }
}
