using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Commands.CreatePost;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class CreatePostHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IDailySafetyRecordRepository> _dailySafetyRecordRepoMock = new();
    private readonly Mock<ISafetySettingRepository> _safetySettingRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly CreatePostCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _mediaId = Guid.NewGuid();

    public CreatePostHandlerTests()
    {
        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/media");

        _postRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _postRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dailySafetyRecordRepoMock
            .Setup(r => r.GetByUserIdAndDateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailySafetyRecord?)null);

        _dailySafetyRecordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DailySafetyRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _safetySettingRepoMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Beacon.Domain.Entities.Setting.SafetySetting?)null);

        _sut = new CreatePostCommandHandler(
            _postRepoMock.Object,
            _mediaRepoMock.Object,
            _dailySafetyRecordRepoMock.Object,
            _safetySettingRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private MediaObject BuildMedia(
        MediaType type = MediaType.Image,
        MediaStatus status = MediaStatus.Ready,
        Guid? ownerId = null,
        int? durationSeconds = null)
    {
        var media = MediaObject.Create(
            bucketName: "beacon-media",
            objectKey: "test/object.jpg",
            originalFileName: "test.jpg",
            contentType: "image/jpeg",
            fileSizeBytes: 1024,
            mediaType: type,
            uploadedByUserId: ownerId ?? _userId);

        media.SetStatus(status);
        if (durationSeconds.HasValue) media.SetDuration(durationSeconds);

        return media;
    }

    [Fact]
    public async Task Handle_WithValidImageMedia_ReturnsSuccess()
    {
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId, Caption = "Hello", Visibility = "friends" },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Visibility.Should().Be("friends");
        result.Value.Caption.Should().Be("Hello");
    }

    [Fact]
    public async Task Handle_WithLocation_ReturnsLocation()
    {
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest
            {
                MediaId = _mediaId,
                Latitude = 10.762622m,
                Longitude = 106.660172m
            },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Latitude.Should().Be(10.762622m);
        result.Value.Longitude.Should().Be(106.660172m);
    }

    [Fact]
    public async Task Handle_WhenNoVisibilityProvided_DefaultsToFriends()
    {
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Visibility.Should().Be("friends");
    }

    [Fact]
    public async Task Handle_WhenTodayHealthRecordDoesNotExist_CreatesCheckedInRecordAndLinksPost()
    {
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        DailySafetyRecord? addedRecord = null;
        Post? addedPost = null;

        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);
        _dailySafetyRecordRepoMock
            .Setup(r => r.AddAsync(It.IsAny<DailySafetyRecord>(), It.IsAny<CancellationToken>()))
            .Callback<DailySafetyRecord, CancellationToken>((record, _) => addedRecord = record)
            .Returns(Task.CompletedTask);
        _postRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()))
            .Callback<Post, CancellationToken>((post, _) => addedPost = post)
            .Returns(Task.CompletedTask);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId, Caption = "Health post", Visibility = "friends" },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedRecord.Should().NotBeNull();
        addedRecord!.Status.Should().Be(Beacon.Domain.Enums.Safety.SafetyStatus.CheckedIn);
        addedRecord.CheckedInAtUtc.Should().NotBeNull();
        addedPost.Should().NotBeNull();
        addedPost!.DailySafetyRecordId.Should().Be(addedRecord.Id);
        result.Value!.DailySafetyRecordId.Should().Be(addedRecord.Id);
    }

    [Fact]
    public async Task Handle_WhenMediaNotFound_ReturnsNotFound()
    {
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaObject?)null);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Storage.MEDIA_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenMediaOwnedByOtherUser_ReturnsForbidden()
    {
        var otherUserId = Guid.NewGuid();
        var media = BuildMedia(ownerId: otherUserId);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.MEDIA_ACCESS_DENIED);
    }

    [Fact]
    public async Task Handle_WhenMediaStatusUploading_ReturnsFailure()
    {
        var media = BuildMedia(status: MediaStatus.Uploading);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Post.MEDIA_NOT_READY);
    }

    [Fact]
    public async Task Handle_WhenVideoTooShort_ReturnsFailure()
    {
        var media = BuildMedia(MediaType.Video, MediaStatus.Ready, durationSeconds: 3);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Post.INVALID_VIDEO_DURATION);
    }

    [Fact]
    public async Task Handle_WhenVideoTooLong_ReturnsFailure()
    {
        var media = BuildMedia(MediaType.Video, MediaStatus.Ready, durationSeconds: 15);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Post.INVALID_VIDEO_DURATION);
    }

    [Fact]
    public async Task Handle_WhenVideoDurationNull_ReturnsFailure()
    {
        var media = BuildMedia(MediaType.Video, MediaStatus.Ready, durationSeconds: null);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Post.INVALID_VIDEO_DURATION);
    }

    [Fact]
    public async Task Handle_WhenInvalidVisibility_ReturnsValidationFailure()
    {
        var media = BuildMedia(MediaType.Image, MediaStatus.Ready);
        _mediaRepoMock.Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        var command = new CreatePostCommand(
            new CreatePostRequest { MediaId = _mediaId, Visibility = "everyone" },
            _userId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be(ErrorCodes.Post.INVALID_VISIBILITY);
    }
}
