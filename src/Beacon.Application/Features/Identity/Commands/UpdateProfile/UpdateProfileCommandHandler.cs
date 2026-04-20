using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.UpdateProfile;

public class UpdateProfileCommandHandler(
    IUserRepository userRepository,
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    UserProfileMapper mapper) : IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        var req = command.Request;

        // ── Resolve giá trị cuối cùng: dùng giá trị mới nếu được gửi, giữ nguyên nếu null ──
        var newFamilyName = req.FamilyName?.Trim() ?? user.FamilyName;
        var newGivenName  = req.GivenName?.Trim()  ?? user.GivenName;
        var newEmail      = req.Email?.Trim().ToLowerInvariant() ?? user.Email;
        var newPhone      = req.PhoneNumber is not null ? req.PhoneNumber : user.PhoneNumber;

        // ── Kiểm tra email unique (chỉ khi email thay đổi) ───────────────
        if (newEmail != user.Email)
        {
            if (await userRepository.ExistsByEmailExcludingUserAsync(newEmail, command.UserId, ct))
                return Result<UserProfileDto>.Failure(
                    Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_IN_USE, "Email đã được sử dụng bởi tài khoản khác."));
        }

        // ── Kiểm tra phone unique (chỉ khi phone thay đổi và không null) ─
        if (!string.IsNullOrWhiteSpace(newPhone) && newPhone != user.PhoneNumber)
        {
            if (await userRepository.ExistsByPhoneExcludingUserAsync(newPhone, command.UserId, ct))
                return Result<UserProfileDto>.Failure(
                    Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_IN_USE, "Số điện thoại đã được sử dụng."));
        }

        user.UpdateProfile(newFamilyName, newGivenName, newPhone, newEmail);
        await userRepository.SaveChangesAsync(ct);

        string? avatarUrl = null;
        if (user.AvatarMediaObjectId is { } avatarId)
        {
            var media = await mediaRepository.GetByIdAsync(avatarId, ct);
            if (media is not null)
                avatarUrl = (await storage.GetMediaUrlsAsync(media, ct)).Url;
        }

        return Result<UserProfileDto>.Success(mapper.ToProfileDto(user, avatarUrl));
    }
}
