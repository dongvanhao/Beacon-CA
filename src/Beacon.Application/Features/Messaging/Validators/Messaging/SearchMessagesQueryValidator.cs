using Beacon.Application.Features.Messaging.Queries.SearchMessages;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class SearchMessagesQueryValidator : AbstractValidator<SearchMessagesQuery>
{
    public SearchMessagesQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Search)
            .NotEmpty()
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MaximumLength(200)
            .WithMessage("Từ khóa tìm kiếm không được vượt quá 200 ký tự.");
    }
}
