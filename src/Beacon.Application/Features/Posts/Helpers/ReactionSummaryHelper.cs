using Beacon.Application.Features.Posts.Dtos;
using Beacon.Domain.Entities.Posts;

namespace Beacon.Application.Features.Posts.Helpers;

public static class ReactionSummaryHelper
{
    public static PostReactionSummaryResponse BuildSummary(IEnumerable<PostReaction> reactions)
    {
        var list = reactions.ToList();
        var icons = list
            .SelectMany(r => Domain.Enums.ReactionIcons.Split(r.Icon))
            .GroupBy(icon => icon)
            .ToDictionary(g => g.Key, g => g.Count());
        return new PostReactionSummaryResponse { TotalCount = list.Count, Icons = icons };
    }
}
