using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.SearchMessages;

public class SearchMessagesQueryHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    IMessageGroupMemberSettingRepository settingRepo,
    ICurrentUserService currentUser,
    MessageMapper mapper,
    MessagePostMapper postMapper)
    : IRequestHandler<SearchMessagesQuery, Result<CursorPagedResult<MessageDto, long>>>
{
    public async Task<Result<CursorPagedResult<MessageDto, long>>> Handle(
        SearchMessagesQuery query, CancellationToken ct)
    {
        if (!await groupRepo.IsMemberAsync(query.GroupId, currentUser.UserId, ct))
            return Result<CursorPagedResult<MessageDto, long>>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var limit = Math.Clamp(query.Limit, 1, 100);
        var searchTerm = query.Search.Trim();
        var paged = await messageRepo.SearchByGroupAsync(query.GroupId, searchTerm, query.Cursor, limit, ct);
        var customNamesByUserId = (await settingRepo.ListByGroupAsync(query.GroupId, ct))
            .Where(s => !string.IsNullOrWhiteSpace(s.CustomName))
            .ToDictionary(s => s.UserId, s => s.CustomName!);

        var dtos = new List<MessageDto>(paged.Data.Count);
        foreach (var message in paged.Data)
        {
            var senderDisplayName = ResolveSenderDisplayName(
                message.SenderId,
                message.Sender.FamilyName,
                message.Sender.GivenName,
                customNamesByUserId);

            dtos.Add(mapper.ToDto(
                message,
                senderDisplayName,
                await postMapper.ToDtoAsync(message.Post, ct)));
        }

        return Result<CursorPagedResult<MessageDto, long>>.Success(new CursorPagedResult<MessageDto, long>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }

    private static string ResolveSenderDisplayName(
        Guid senderId,
        string familyName,
        string givenName,
        IReadOnlyDictionary<Guid, string> customNamesByUserId)
    {
        if (customNamesByUserId.TryGetValue(senderId, out var customName)
            && !string.IsNullOrWhiteSpace(customName))
            return customName;

        var fullName = $"{familyName} {givenName}".Trim();
        return fullName != string.Empty ? fullName : "Người dùng";
    }
}
