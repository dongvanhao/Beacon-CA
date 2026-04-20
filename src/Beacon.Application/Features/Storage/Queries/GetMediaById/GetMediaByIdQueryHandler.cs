using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Storage.Dtos;
using Beacon.Application.Mappings.Storage;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Queries.GetMediaById;

public class GetMediaByIdQueryHandler(
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    MediaDtoMapper mapper)
    : IRequestHandler<GetMediaByIdQuery, Result<MediaDto>>
{
    public async Task<Result<MediaDto>> Handle(GetMediaByIdQuery query, CancellationToken ct)
    {
        var media = await mediaRepository.GetByIdAsync(query.Id, ct);
        if (media is null)
            return Result<MediaDto>.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Không tìm thấy media."));

        if (media.UploadProviderByUserId != query.CurrentUserId)
            return Result<MediaDto>.Failure(
                Error.Forbidden(ErrorCodes.Storage.MEDIA_FORBIDDEN, "Bạn không có quyền xem media này."));

        var url = await storage.GeneratePresignedGetUrlAsync(media.ObjectKey, ct);
        var thumbUrl = string.IsNullOrWhiteSpace(media.ThumbnailObjectKey)
            ? null
            : await storage.GeneratePresignedGetUrlAsync(media.ThumbnailObjectKey, ct);

        return Result<MediaDto>.Success(mapper.ToDto(media, url, thumbUrl));
    }
}
