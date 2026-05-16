using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using FluentAssertions;

namespace Beacon.UnitTests.Storage;

public class MediaObjectDomainTests
{
    private static MediaObject BuildMedia(
        MediaStatus status = MediaStatus.Uploading,
        int? duration = null)
    {
        var media = MediaObject.Create(
            bucketName: "test-bucket",
            objectKey: "test/file.mp4",
            originalFileName: "file.mp4",
            contentType: "video/mp4",
            fileSizeBytes: 1024 * 1024,
            mediaType: MediaType.Video);

        if (status != MediaStatus.Uploading)
            media.SetStatus(status);

        if (duration.HasValue)
            media.SetDuration(duration.Value);

        return media;
    }

    [Fact]
    public void SetStatus_ShouldUpdateStatus()
    {
        var media = BuildMedia();

        media.SetStatus(MediaStatus.Ready);

        media.Status.Should().Be(MediaStatus.Ready);
    }

    [Fact]
    public void SetDuration_ShouldUpdateDurationSeconds()
    {
        var media = BuildMedia();

        media.SetDuration(8);

        media.DurationSeconds.Should().Be(8);
    }

    [Fact]
    public void SetDuration_WithNull_ShouldClearDuration()
    {
        var media = BuildMedia(duration: 5);

        media.SetDuration(null);

        media.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void DefaultStatus_ShouldBeUploading()
    {
        var media = MediaObject.Create(
            bucketName: "bucket",
            objectKey: "key.jpg",
            originalFileName: "file.jpg",
            contentType: "image/jpeg",
            fileSizeBytes: 1024,
            mediaType: MediaType.Image);

        media.Status.Should().Be(MediaStatus.Uploading);
    }

    [Fact]
    public void IsReadyForPost_WhenStatusReady_ReturnsTrue()
    {
        var media = BuildMedia(MediaStatus.Ready);

        media.IsReadyForPost().Should().BeTrue();
    }

    [Fact]
    public void IsReadyForPost_WhenStatusActive_ReturnsTrue()
    {
        var media = BuildMedia(MediaStatus.Active);

        media.IsReadyForPost().Should().BeTrue();
    }

    [Fact]
    public void IsReadyForPost_WhenStatusUploading_ReturnsFalse()
    {
        var media = BuildMedia(MediaStatus.Uploading);

        media.IsReadyForPost().Should().BeFalse();
    }

    [Fact]
    public void IsReadyForPost_WhenStatusProcessing_ReturnsFalse()
    {
        var media = BuildMedia(MediaStatus.Processing);

        media.IsReadyForPost().Should().BeFalse();
    }

    [Fact]
    public void IsReadyForPost_WhenStatusFailed_ReturnsFalse()
    {
        var media = BuildMedia(MediaStatus.Failed);

        media.IsReadyForPost().Should().BeFalse();
    }
}
