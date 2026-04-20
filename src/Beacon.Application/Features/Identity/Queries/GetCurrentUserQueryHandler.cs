using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public class GetCurrentUserQueryHandler(
    IUserRepository userRepository,
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    UserProfileMapper profileMapper)
    : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(query.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "User not found."));

        string? avatarUrl = null;
        if (user.AvatarMediaObjectId is { } avatarId)
        {
            var media = await mediaRepository.GetByIdAsync(avatarId, ct);
            if (media is not null)
                avatarUrl = (await storage.GetMediaUrlsAsync(media, ct)).Url;
        }

        return Result<UserProfileDto>.Success(profileMapper.ToProfileDto(user, avatarUrl));
    }
}
