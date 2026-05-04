using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.FindUserByPhone;

public record FindUserByPhoneQuery(string Search) : IRequest<Result<UserSearchDto>>;
