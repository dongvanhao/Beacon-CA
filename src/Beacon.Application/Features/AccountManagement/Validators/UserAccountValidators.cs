using Beacon.Application.Features.AccountManagement.Users.Commands.CreateUser;
using Beacon.Application.Features.AccountManagement.Users.Commands.UpdateUser;
using Beacon.Application.Features.AccountManagement.Users.Queries.GetUserById;
using Beacon.Application.Features.AccountManagement.Users.Queries.ListUsers;
using FluentValidation;

namespace Beacon.Application.Features.AccountManagement.Validators;

public class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page phai lon hon hoac bang 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize phai nam trong khoang 1-100.");
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
    }
}

public class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId khong duoc de trong.");
    }
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Username khong duoc de trong.")
            .MinimumLength(3).WithMessage("Username phai co it nhat 3 ky tu.")
            .MaximumLength(50).WithMessage("Username khong duoc vuot qua 50 ky tu.")
            .Matches(@"^[a-zA-Z0-9_\.]+$").WithMessage("Username chi duoc chua chu cai, so, dau gach duoi va dau cham.");

        RuleFor(x => x.Request.Email)
            .NotEmpty().WithMessage("Email khong duoc de trong.")
            .EmailAddress().WithMessage("Email khong hop le.")
            .MaximumLength(254).WithMessage("Email khong duoc vuot qua 254 ky tu.");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Mat khau khong duoc de trong.")
            .MinimumLength(8).WithMessage("Mat khau phai co it nhat 8 ky tu.")
            .MaximumLength(100).WithMessage("Mat khau khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.FamilyName)
            .NotEmpty().WithMessage("Ho khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ho khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.GivenName)
            .NotEmpty().WithMessage("Ten khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ten khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.PhoneNumber)
            .Matches(@"^(0|\+?[1-9])\d{1,14}$").WithMessage("So dien thoai khong hop le.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.PhoneNumber));

        RuleFor(x => x.Request.TimeZone)
            .NotEmpty().WithMessage("TimeZone khong duoc de trong.")
            .MaximumLength(100).WithMessage("TimeZone khong duoc vuot qua 100 ky tu.");
    }
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId khong duoc de trong.");

        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Username khong duoc de trong.")
            .MinimumLength(3).WithMessage("Username phai co it nhat 3 ky tu.")
            .MaximumLength(50).WithMessage("Username khong duoc vuot qua 50 ky tu.")
            .Matches(@"^[a-zA-Z0-9_\.]+$").WithMessage("Username chi duoc chua chu cai, so, dau gach duoi va dau cham.");

        RuleFor(x => x.Request.Email)
            .NotEmpty().WithMessage("Email khong duoc de trong.")
            .EmailAddress().WithMessage("Email khong hop le.")
            .MaximumLength(254).WithMessage("Email khong duoc vuot qua 254 ky tu.");

        RuleFor(x => x.Request.FamilyName)
            .NotEmpty().WithMessage("Ho khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ho khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.GivenName)
            .NotEmpty().WithMessage("Ten khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ten khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.PhoneNumber)
            .Matches(@"^(0|\+?[1-9])\d{1,14}$").WithMessage("So dien thoai khong hop le.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.PhoneNumber));

        RuleFor(x => x.Request.TimeZone)
            .NotEmpty().WithMessage("TimeZone khong duoc de trong.")
            .MaximumLength(100).WithMessage("TimeZone khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.Request.Password)
            .MinimumLength(8).WithMessage("Mat khau phai co it nhat 8 ky tu.")
            .MaximumLength(100).WithMessage("Mat khau khong duoc vuot qua 100 ky tu.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.Password));
    }
}
