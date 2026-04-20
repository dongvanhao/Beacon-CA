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

        // ── Kiểm tra email unique (nếu khác email hiện tại) ──────────────
        var trimmedEmail = req.Email.Trim().ToLowerInvariant();
        if (trimmedEmail != user.Email)
        {
            if (await userRepository.ExistsByEmailExcludingUserAsync(trimmedEmail, command.UserId, ct))
                return Result<UserProfileDto>.Failure(
                    Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_IN_USE, "Email đã được sử dụng bởi tài khoản khác."));
        }

        // ── Kiểm tra phone unique (nếu có thay đổi) ───────────────────────
        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            var trimmedPhone = req.PhoneNumber.Trim();
            if (trimmedPhone != user.PhoneNumber)
            {
                if (await userRepository.ExistsByPhoneExcludingUserAsync(trimmedPhone, command.UserId, ct))
                    return Result<UserProfileDto>.Failure(
                        Error.Conflict(ErrorCodes.Identity.PHONE_ALREADY_IN_USE, "Số điện thoại đã được sử dụng."));
            }
        }

        user.UpdateProfile(req.FamilyName, req.GivenName, req.PhoneNumber, req.Email);
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
