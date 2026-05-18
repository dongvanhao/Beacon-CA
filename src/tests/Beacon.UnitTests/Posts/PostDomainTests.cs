using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using FluentAssertions;

namespace Beacon.UnitTests.Posts;

public class PostDomainTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid MediaId = Guid.NewGuid();

    // ─── Post.Create ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetDefaultsCorrectly()
    {
        var post = Post.Create(OwnerId, MediaId, "Hello", PostVisibility.Friends);

        post.Status.Should().Be(PostStatus.Active);
        post.DeletedAtUtc.Should().BeNull();
        post.Caption.Should().Be("Hello");
        post.Visibility.Should().Be(PostVisibility.Friends);
        post.OwnerUserId.Should().Be(OwnerId);
        post.MediaId.Should().Be(MediaId);
    }

    [Fact]
    public void Create_ShouldTrimCaption()
    {
        var post = Post.Create(OwnerId, MediaId, "  Hello World  ", PostVisibility.Friends);

        post.Caption.Should().Be("Hello World");
    }

    [Fact]
    public void Create_WithNullCaption_ShouldSetNullCaption()
    {
        var post = Post.Create(OwnerId, MediaId, null, PostVisibility.Private);

        post.Caption.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldInitializeIsDeletedFalse()
    {
        var post = Post.Create(OwnerId, MediaId, null, PostVisibility.Friends);

        post.IsDeleted.Should().BeFalse();
    }

    // ─── Post.UpdateContent ─────────────────────────────────────────────────

    [Fact]
    public void UpdateContent_ShouldChangeCaptionAndVisibility()
    {
        var post = Post.Create(OwnerId, MediaId, "Old caption", PostVisibility.Friends);

        post.UpdateContent("New caption", PostVisibility.Private, null, null);

        post.Caption.Should().Be("New caption");
        post.Visibility.Should().Be(PostVisibility.Private);
    }

    [Fact]
    public void UpdateContent_ShouldTrimCaption()
    {
        var post = Post.Create(OwnerId, MediaId, "Old", PostVisibility.Friends);

        post.UpdateContent("  Trimmed  ", PostVisibility.Friends, null, null);

        post.Caption.Should().Be("Trimmed");
    }

    // ─── Post.SoftDelete ────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_ShouldSetDeletedAtUtc()
    {
        var post = Post.Create(OwnerId, MediaId, null, PostVisibility.Friends);
        var before = DateTime.UtcNow;

        post.SoftDelete();

        post.DeletedAtUtc.Should().NotBeNull();
        post.DeletedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void IsDeleted_WhenDeletedAtUtcSet_ReturnsTrue()
    {
        var post = Post.Create(OwnerId, MediaId, null, PostVisibility.Friends);

        post.SoftDelete();

        post.IsDeleted.Should().BeTrue();
    }

    // ─── PostReaction.Create ────────────────────────────────────────────────

    [Fact]
    public void PostReaction_Create_ShouldSetIconPostAndUser()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var reaction = PostReaction.Create(postId, userId, "heart");

        reaction.Icon.Should().Be("heart");
        reaction.PostId.Should().Be(postId);
        reaction.UserId.Should().Be(userId);
    }

    // ─── PostReaction.UpdateIcon ─────────────────────────────────────────────

    [Fact]
    public void PostReaction_AppendIcon_ShouldAppendIcon()
    {
        var reaction = PostReaction.Create(Guid.NewGuid(), Guid.NewGuid(), "heart");

        reaction.AppendIcon("haha");

        reaction.Icon.Should().Be("heart - haha");
    }

    [Fact]
    public void PostReaction_AppendIcon_WhenMoreThanThreeIcons_ShouldDropOldest()
    {
        var reaction = PostReaction.Create(Guid.NewGuid(), Guid.NewGuid(), "heart");

        reaction.AppendIcon("haha");
        reaction.AppendIcon("like");
        reaction.AppendIcon("wow");

        reaction.Icon.Should().Be("haha - like - wow");
    }

    // ─── ReactionIcons ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("heart")]
    [InlineData("haha")]
    [InlineData("like")]
    [InlineData("sad")]
    [InlineData("wow")]
    [InlineData("rocket")]
    [InlineData("😊")]
    public void ReactionIcons_IsValid_ReturnsTrueForSupportedFormat(string icon)
    {
        ReactionIcons.IsValid(icon).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("heart - haha")]
    public void ReactionIcons_IsValid_ReturnsFalseForInvalidFormat(string icon)
    {
        ReactionIcons.IsValid(icon).Should().BeFalse();
    }
}
