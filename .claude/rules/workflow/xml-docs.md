# XML Documentation cho Controller endpoints

Sau khi tạo hoặc sửa bất kỳ endpoint nào trong `src/Beacon.Api/Controllers/`, **bắt buộc phải thêm XML documentation** theo format sau:

```csharp
/// <summary>
/// Mô tả ngắn gọn chức năng của endpoint.
/// </summary>
/// <remarks>
/// Các giá trị <c>code</c> có thể xuất hiện trong response:
///
/// - <c>null</c>: Thành công (success = true).
/// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ.
/// - <c>ERROR_CODE</c>: Mô tả ngắn.
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// {
///   "field": "type  (ghi chú thêm nếu cần)"
/// }
/// </code>
///
/// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
/// </remarks>
```

## Quy tắc áp dụng

- Áp dụng cho **mọi** public action method trong controllers, không bỏ sót
- Endpoint không trả về data (data = null) vẫn phải có phần "Format response chuẩn"
- Các error code phải khớp với hằng số trong `Beacon.Shared/Constants/ErrorCodes.cs`
- Nếu endpoint dùng `[Authorize]`, ghi rõ yêu cầu `Authorization: Bearer <token>` trong remarks
- Nếu endpoint dùng `[AdminOnly]`, ghi rõ chỉ admin token mới được phép gọi
