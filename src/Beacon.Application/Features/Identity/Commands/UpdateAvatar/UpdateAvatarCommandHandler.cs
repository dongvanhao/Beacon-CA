using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands.UpdateAvatar;

public class UpdateAvatarCommandHandler(
    IUserRepository userRepository,
    IMediaObjectRepository mediaRepository,
    UserProfileMapper mapper) : IRequestHandler<UpdateAvatarCommand, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(UpdateAvatarCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        var newMedia = await mediaRepository.GetByIdAsync(command.MediaObjectId, ct);
        if (newMedia is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Media không tồn tại."));

        if (newMedia.IsDeleted)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Media không tồn tại."));

        if (newMedia.UploadProviderByUserId != command.UserId)
            return Result<UserProfileDto>.Failure(
                Error.Forbidden(ErrorCodes.Storage.MEDIA_FORBIDDEN, "Bạn không có quyền dùng media này."));

        if (newMedia.MediaType != MediaType.Image)
            return Result<UserProfileDto>.Failure(
                Error.Validation(ErrorCodes.Storage.INVALID_FILE_TYPE, "Avatar phải là file ảnh."));

        if (user.AvatarMediaObjectId != null)
        {
            var oldMedia = await mediaRepository.GetByIdAsync(user.AvatarMediaObjectId.Value, ct);
            oldMedia?.Delete();
        }

        user.UpdateAvatar(command.MediaObjectId);
        await userRepository.SaveChangesAsync(ct);

        return Result<UserProfileDto>.Success(mapper.ToProfileDto(user));
    }
}
