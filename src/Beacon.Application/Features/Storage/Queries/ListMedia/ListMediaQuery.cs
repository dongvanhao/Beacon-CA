using Beacon.Application.Features.Storage.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Queries.ListMedia;

public record ListMediaQuery(
    Guid CurrentUserId,
    DateTime? Cursor,
    int Limit) : IRequest<Result<CursorPagedResult<MediaDto>>>;
