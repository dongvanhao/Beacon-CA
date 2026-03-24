## 1. Mục tiêu  
Tài liệu này thống nhất cách dùng:  
•	`Error`  
•	`Result`  
•	`Result<T>`  
•	`ApiResponse<T>`  

Đây là phần rất quan trọng vì nếu team dùng lẫn lộn, API sẽ khó maintain.  

## 2. Phân biệt vai trò

2.1. `Result` và `Result<T>`  
Đây là contract nội bộ trong backend, thường dùng từ Application trả về cho API.  

Vai trò  
•	biểu diễn thành công / thất bại có kiểm soát  
•	tránh throw exception cho mọi case nghiệp vụ  
•	làm rõ “đây là lỗi expected”  

2.2. `ApiResponse<T>`  
Đây là contract trả ra ngoài client  
 
Vai trò  
•	chuẩn hóa JSON response  
•	giúp frontend/mobile parse nhất quán  
•	giúp mọi endpoint có format giống nhau  

---

3. Cách hiểu trong dự án này  

Nguyên tắc lõi  
•	**Application Service trả `Result`**  
•	**Controller convert `Result` thành `ApiResponse`**  
•	**Middleware bắt exception và cũng trả `ApiResponse`**  

Như vậy client luôn nhận cùng một schema.  

---

4. Khi nào dùng `Result`  
Dùng khi lỗi là dự đoán trước được trong nghiệp vụ.  

Ví dụ  
•	User không tồn tại  
•	Email đã được sử dụng  
•	Check-in hôm nay đã tồn tại  
•	Friendship đã tồn tại  
•	Không đủ quyền theo rule nghiệp vụ  

Ví dụ trong service  
```csharp
if (user is null)  
{  
    return Result<UserDto>.Failure(  
        new Error(ErrorCodes.UserNotFound, "User not found."));  
}  
```
---

5. Khi nào dùng exception  
Dùng khi có vấn đề bất thường hoặc lỗi cần đi theo luồng exception.  

Ví dụ  
•	DB connection lỗi  
•	file storage lỗi  
•	lỗi cấu hình JWT  
•	lỗi không mong muốn  
•	validation framework ném exception  
•	unauthorized ở middleware/auth pipeline  

Kết luận  
•	Lỗi expected -> `Result`  
•	Lỗi unexpected / exceptional -> `Exception`  

---
 
6. Chuẩn dùng `Error`  
`Error` nên là loại lỗi tối thiểu gồm:  
•	`Code`  
•	`Message`  

Khuyến nghị mạnh  
Tạo sẵn `ErrorCodes.cs`:  
```csharp
public static class ErrorCodes  
{  
    public const string UserNotFound = "USER_NOT_FOUND";  
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";  
    public const string ValidationError = "VALIDATION_ERROR";  
    public const string Conflict = "CONFLICT";  
}  
Lợi ích  
•	code ổn định  
•	client có thể switch theo `Code`  
•	tránh typo  
```
---

7. Ví dụ flow hoàn chỉnh  

Service  
```csharp
public async Task<Result<UserDto>> GetByIdAsync(Guid id)  
{  
    var user = await _userRepository.GetByIdAsync(id);  
    if (user is null)  
    {  
        return Result<UserDto>.Failure(  
            new Error(ErrorCodes.UserNotFound, "User not found."));  
    }  
    var dto = new UserDto { ... };  
    return Result<UserDto>.Success(dto);  
}  
```

Controller   
```csharp
[HttpGet("{id}")]  
public async Task<IActionResult> GetById(Guid id)  
{  
    var result = await _userService.GetByIdAsync(id);  
    return HandleResult(result, "Get user successfully");  
}  
API response thành công  
{  
  "success": true,  
  "message": "Get user successfully",  
  "code": null,  
  "data": { ... },  
  "errors": null  
}  

API response thất bại  
{  
  "success": false,  
  "message": "User not found.",  
  "code": "USER_NOT_FOUND",  
  "data": null,  
  "errors": null  
}  
```
---
