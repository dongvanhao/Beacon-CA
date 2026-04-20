using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Storage.Commands.HardDelete;

public class HardDeleteMediaCommandHandler(
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    ILogger<HardDeleteMediaCommandHandler> logger)
    : IRequestHandler<HardDeleteMediaCommand, Result>
{
    public async Task<Result> Handle(HardDeleteMediaCommand command, CancellationToken ct)
    {
        var media = await mediaRepository.GetByIdIncludeDeletedAsync(command.Id, ct);
        if (media is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Không tìm thấy media."));

        try
        {
            await storage.RemoveAsync(media.ObjectKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove object {ObjectKey} from storage.", media.ObjectKey);
            return Result.Failure(
                Error.Failure(ErrorCodes.Storage.STORAGE_UNAVAILABLE, "Không thể xóa file trên storage."));
        }

        if (!string.IsNullOrWhiteSpace(media.ThumbnailObjectKey))
        {
            try { await storage.RemoveAsync(media.ThumbnailObjectKey, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Thumbnail remove failed for {Key}; continuing.", media.ThumbnailObjectKey); }
        }

        mediaRepository.Remove(media);
        await mediaRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
