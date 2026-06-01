using Beacon.Application.Features.AccountManagement.Admins.Commands.CreateAdmin;
using Beacon.Application.Features.AccountManagement.Admins.Commands.UpdateAdmin;
using Beacon.Application.Features.AccountManagement.Admins.Queries.GetAdminById;
using Beacon.Application.Features.AccountManagement.Admins.Queries.ListAdmins;
using FluentValidation;

namespace Beacon.Application.Features.AccountManagement.Validators;

public class ListAdminsQueryValidator : AbstractValidator<ListAdminsQuery>
{
    public ListAdminsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page phai lon hon hoac bang 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize phai nam trong khoang 1-100.");
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
    }
}

public class GetAdminByIdQueryValidator : AbstractValidator<GetAdminByIdQuery>
{
    public GetAdminByIdQueryValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty().WithMessage("AdminId khong duoc de trong.");
    }
}

public class CreateAdminCommandValidator : AbstractValidator<CreateAdminCommand>
{
    public CreateAdminCommandValidator()
    {
        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Username khong duoc de trong.")
            .MinimumLength(3).WithMessage("Username phai co it nhat 3 ky tu.")
            .MaximumLength(50).WithMessage("Username khong duoc vuot qua 50 ky tu.")
            .Matches(@"^[a-zA-Z0-9_\.]+$").WithMessage("Username chi duoc chua chu cai, so, dau gach duoi va dau cham.");

        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Ho ten khong duoc de trong.")
            .MaximumLength(200).WithMessage("Ho ten khong duoc vuot qua 200 ky tu.");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Mat khau khong duoc de trong.")
            .MinimumLength(8).WithMessage("Mat khau phai co it nhat 8 ky tu.")
            .MaximumLength(100).WithMessage("Mat khau khong duoc vuot qua 100 ky tu.");

        RuleForEach(x => x.Request.RoleIds)
            .NotEmpty().WithMessage("RoleId khong duoc de trong.")
            .When(x => x.Request.RoleIds is not null);
    }
}

public class UpdateAdminCommandValidator : AbstractValidator<UpdateAdminCommand>
{
    public UpdateAdminCommandValidator()
    {
        RuleFor(x => x.AdminId).NotEmpty().WithMessage("AdminId khong duoc de trong.");

        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Username khong duoc de trong.")
            .MinimumLength(3).WithMessage("Username phai co it nhat 3 ky tu.")
            .MaximumLength(50).WithMessage("Username khong duoc vuot qua 50 ky tu.")
            .Matches(@"^[a-zA-Z0-9_\.]+$").WithMessage("Username chi duoc chua chu cai, so, dau gach duoi va dau cham.");

        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Ho ten khong duoc de trong.")
            .MaximumLength(200).WithMessage("Ho ten khong duoc vuot qua 200 ky tu.");

        RuleFor(x => x.Request.Password)
            .MinimumLength(8).WithMessage("Mat khau phai co it nhat 8 ky tu.")
            .MaximumLength(100).WithMessage("Mat khau khong duoc vuot qua 100 ky tu.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.Password));

        RuleForEach(x => x.Request.RoleIds)
            .NotEmpty().WithMessage("RoleId khong duoc de trong.")
            .When(x => x.Request.RoleIds is not null);
    }
}
