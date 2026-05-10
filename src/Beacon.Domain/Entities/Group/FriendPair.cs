namespace Beacon.Domain.Entities.Group;

public static class FriendPair
{
    /// <summary>Normalize pair so UserId1 = Min, UserId2 = Max — matches DB unique index.</summary>
    public static (Guid UserId1, Guid UserId2) Normalize(Guid a, Guid b)
        => a < b ? (a, b) : (b, a);
}
