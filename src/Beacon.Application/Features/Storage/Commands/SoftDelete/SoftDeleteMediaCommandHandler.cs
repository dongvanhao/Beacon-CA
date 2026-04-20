using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Commands.SoftDelete;

public class SoftDeleteMediaCommandHandler(
    IMediaObjectRepository mediaRepository)
    : IRequestHandler<SoftDeleteMediaCommand, Result>
{
    public async Task<Result> Handle(SoftDeleteMediaCommand command, CancellationToken ct)
    {
        var media = await mediaRepository.GetByIdAsync(command.Id, ct);
        if (media is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Không tìm thấy media."));

        if (media.UploadProviderByUserId != command.CurrentUserId)
            return Result.Failure(
                Error.Forbidden(ErrorCodes.Storage.MEDIA_FORBIDDEN, "Bạn không có quyền xóa media này."));

        media.Delete();
        await mediaRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
