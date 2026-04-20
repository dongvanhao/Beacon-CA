using Beacon.Application.Features.Storage.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Queries.GetMediaById;

public record GetMediaByIdQuery(Guid Id, Guid CurrentUserId) : IRequest<Result<MediaDto>>;
