using Beacon.Application.Features.Identity.Commands.ChangePassword;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Features.Identity.Validators;
using FluentAssertions;

namespace Beacon.UnitTests.Identity;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    private static ChangePasswordCommand ValidCommand(string? currentPassword = "OldPass123!", string? newPassword = "NewPass456@")
        => new(new ChangePasswordRequest
        {
            CurrentPassword = currentPassword!,
            NewPassword = newPassword!
        });

    [Fact]
    public async Task Validate_WithValidRequest_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenCurrentPasswordEmpty_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(currentPassword: ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CurrentPassword"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordEmpty_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("để trống"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordTooShort_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: "Ab1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("8 ký tự"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordTooLong_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: new string('A', 97) + "1!aB"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("100 ký tự"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordNoUppercase_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: "newpass123!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("chữ hoa"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordNoLowercase_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: "NEWPASS123!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("chữ thường"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordNoDigit_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: "NewPassword!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("chữ số"));
    }

    [Fact]
    public async Task Validate_WhenNewPasswordNoSpecialChar_FailsValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand(newPassword: "NewPassword123"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ký tự đặc biệt"));
    }
}
