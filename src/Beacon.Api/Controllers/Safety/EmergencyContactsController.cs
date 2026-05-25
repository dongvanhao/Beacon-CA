using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Safety.Commands.CreateEmergencyContact;
using Beacon.Application.Features.Safety.Commands.DeleteEmergencyContact;
using Beacon.Application.Features.Safety.Commands.SetPrimaryEmergencyContact;
using Beacon.Application.Features.Safety.Commands.UpdateEmergencyContact;
using Beacon.Application.Features.Safety.Dtos;
using Beacon.Application.Features.Safety.Queries.GetEmergencyContacts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Safety;

[Route("api/v1/emergency-contacts")]
[Authorize]
public class EmergencyContactsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Lấy danh sách liên hệ khẩn cấp của user hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// [
    ///   {
    ///     "id": "guid",
    ///     "fullName": "string",
    ///     "contactValue": "string (số điện thoại / email / telegram handle)",
    ///     "relationship": "string | null",
    ///     "channelType": "string (Email | Telegram | Sms | Phone)",
    ///     "priorityOrder": "int",
    ///     "isPrimary": "bool",
    ///     "isActive": "bool"
    ///   }
    /// ]
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetEmergencyContactsQuery(currentUser.UserId), ct));

    #region
    /// <summary>Thêm liên hệ khẩn cấp mới.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Tối đa 5 liên hệ khẩn cấp trên mỗi user.
    ///
    /// Body (<c>application/json</c>):
    /// <code>
    /// {
    ///   "fullName": "string (bắt buộc)",
    ///   "contactValue": "string (bắt buộc — số điện thoại / email / telegram handle)",
    ///   "channelType": "int — 1=Email, 2=Telegram, 3=Sms, 4=Phone",
    ///   "relationship": "string | null",
    ///   "priorityOrder": "int (bắt buộc, >= 1)"
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu không hợp lệ.
    /// - <c>EMERGENCY_CONTACT_LIMIT_EXCEEDED</c>: Đã có 5 liên hệ khẩn cấp.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id": "guid",
    ///   "fullName": "string",
    ///   "contactValue": "string",
    ///   "relationship": "string | null",
    ///   "channelType": "string (Email | Telegram | Sms | Phone)",
    ///   "priorityOrder": "int",
    ///   "isPrimary": "bool",
    ///   "isActive": "bool"
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmergencyContactRequest req, CancellationToken ct)
        => CreatedResult("api/v1/emergency-contacts",
            await mediator.Send(new CreateEmergencyContactCommand(currentUser.UserId, req), ct));

    #region
    /// <summary>Cập nhật thông tin liên hệ khẩn cấp.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Body (<c>application/json</c>):
    /// <code>
    /// {
    ///   "fullName": "string (bắt buộc)",
    ///   "contactValue": "string (bắt buộc — số điện thoại / email / telegram handle)",
    ///   "channelType": "int — 1=Email, 2=Telegram, 3=Sms, 4=Phone",
    ///   "relationship": "string | null",
    ///   "priorityOrder": "int (bắt buộc, >= 1)"
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu không hợp lệ.
    /// - <c>EMERGENCY_CONTACT_NOT_FOUND</c>: Không tìm thấy liên hệ.
    /// - <c>EMERGENCY_CONTACT_FORBIDDEN</c>: Không có quyền chỉnh sửa.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id": "guid",
    ///   "fullName": "string",
    ///   "contactValue": "string",
    ///   "relationship": "string | null",
    ///   "channelType": "string (Email | Telegram | Sms | Phone)",
    ///   "priorityOrder": "int",
    ///   "isPrimary": "bool",
    ///   "isActive": "bool"
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateEmergencyContactRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new UpdateEmergencyContactCommand(currentUser.UserId, id, req), ct));

    #region
    /// <summary>Xóa liên hệ khẩn cấp (soft delete).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>EMERGENCY_CONTACT_NOT_FOUND</c>: Không tìm thấy liên hệ.
    /// - <c>EMERGENCY_CONTACT_FORBIDDEN</c>: Không có quyền xóa.
    ///
    /// Cấu trúc <c>data</c> khi thành công: <c>null</c>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new DeleteEmergencyContactCommand(currentUser.UserId, id), ct));

    #region
    /// <summary>Đặt liên hệ khẩn cấp làm liên hệ chính.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ có 1 liên hệ chính tại một thời điểm. Liên hệ chính cũ sẽ bị clear tự động.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>EMERGENCY_CONTACT_NOT_FOUND</c>: Không tìm thấy liên hệ.
    /// - <c>EMERGENCY_CONTACT_FORBIDDEN</c>: Không có quyền thao tác.
    ///
    /// Cấu trúc <c>data</c> khi thành công: <c>null</c>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("{id:guid}/set-primary")]
    public async Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new SetPrimaryEmergencyContactCommand(currentUser.UserId, id), ct));
}
