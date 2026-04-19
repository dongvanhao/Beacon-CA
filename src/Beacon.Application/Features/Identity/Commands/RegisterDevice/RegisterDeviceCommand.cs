using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record RegisterDeviceCommand(RegisterDeviceRequest Request) : IRequest<Result>;
