using Beacon.Application.Features.Storage.Commands.Upload;
using FluentValidation;

namespace Beacon.Application.Features.Storage.Validators.Storage;

public class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    private const long MaxImageBytes = 10L * 1024 * 1024;   // 10 MB
    private const long MaxVideoBytes = 100L * 1024 * 1024;  // 100 MB

    private static readonly string[] AllowedImageMimes =
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static readonly string[] AllowedVideoMimes =
    {
        "video/mp4",
        "video/quicktime",
        "video/webm"
    };

    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("File không được để trống.")
            .Must(f => f is not null && f.Length > 0).WithMessage("File không được rỗng.");

        RuleFor(x => x.File.ContentType)
            .NotEmpty()
            .Must(BeAllowedMimeType)
            .WithMessage("Loại file không được hỗ trợ. Chỉ chấp nhận image/* hoặc video/*.")
            .When(x => x.File is not null);

        RuleFor(x => x.File)
            .Must(BeWithinSizeLimit)
            .WithMessage("File vượt quá dung lượng cho phép (ảnh ≤ 10MB, video ≤ 100MB).")
            .When(x => x.File is not null && BeAllowedMimeType(x.File!.ContentType));

        RuleFor(x => x.CurrentUserId)
            .NotEqual(Guid.Empty).WithMessage("Người dùng chưa xác thực.");
    }

    private static bool BeAllowedMimeType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var lower = contentType.ToLowerInvariant();
        return AllowedImageMimes.Contains(lower) || AllowedVideoMimes.Contains(lower);
    }

    private static bool BeWithinSizeLimit(Microsoft.AspNetCore.Http.IFormFile? file)
    {
        if (file is null) return false;
        var ct = file.ContentType?.ToLowerInvariant();
        if (ct is null) return false;

        if (AllowedImageMimes.Contains(ct)) return file.Length <= MaxImageBytes;
        if (AllowedVideoMimes.Contains(ct)) return file.Length <= MaxVideoBytes;
        return false;
    }
}
