using Beacon.Application.Features.Storage.Dtos;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Beacon.Application.Features.Storage.Commands.Upload;

public record UploadMediaCommand(IFormFile File, Guid CurrentUserId) : IRequest<Result<MediaDto>>;
