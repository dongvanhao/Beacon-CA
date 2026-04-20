using Microsoft.AspNetCore.Http;

namespace Beacon.Application.Features.Storage.Dtos;

public class UploadMediaRequest
{
    public IFormFile File { get; set; } = default!;
}
