# 4. Hướng dẫn chung cho BaseController
1. Mục tiêu  
`BaseController` giúp tránh lặp logic convert:  
•	`Result` -> `IActionResult`  
•	`Result<T>` -> `IActionResult`  
Thay vì mỗi controller tự dựng response, team dùng chung một chuẩn.  
---

2. Vai trò của BaseController hiện tại  
Hiện tại `BaseController` có 3 method chính:  
•	`HandleResult(Result result, string successMessage = "Success")`  
•	`HandleResult<T>(Result<T> result, string successMessage = "Success")`  
•	`CreatedResult<T>(Result<T> result, string actionName, object routeValues, string successMessage = "Created successfully")`  
 Đây là cách tổ chức đúng hướng.  

---

3. Khi nào dùng từng method
`HandleResult(Result result)`  
Dùng cho use case không trả data.  
Ví dụ:  
•	delete  
•	update status  
•	approve report  
`HandleResult<T>(Result<T> result)`  
Dùng khi use case trả data.  
Ví dụ:  
•	get by id  
•	login  
•	get profile  
•	get list  
`CreatedResult<T>(...)`  
Dùng cho `POST` tạo mới tài nguyên.  
Ví dụ:  
•	tạo user  
•	tạo check-in  
•	tạo circle  
---

4. Cách dùng trong controller  
 ```csharp
Ví dụ GET  
[HttpGet("{id}")]
public async Task<IActionResult> GetById(Guid id)
{
    var result = await _userService.GetByIdAsync(id);
    return HandleResult(result, "Get user successfully");
}

Ví dụ POST
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateCheckinRequestDto request)
{
    var result = await _checkinService.CreateAsync(request);
    return CreatedResult(result, nameof(GetById), new { id = result.Value.Id }, "Create checkin successfully");
}

Ví dụ DELETE
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    var result = await _userService.DeleteAsync(id);
    return HandleResult(result, "Delete user successfully");
}
```
---

5. Lợi ích của BaseController
- Giảm lặp code
- Mọi controller trả response giống nhau
- Dễ sửa format response ở một chỗ
- Dễ review code hơn

---

6. Hạn chế của bản hiện tại  
Hiện tại failure đang luôn trả:  
•	`BadRequest(...)`  
Điều này chưa tốt vì:  
•	not found nên là `404`  
•	conflict nên là `409`  
•	forbidden nên là `403`  
•	unauthorized nên là `401`  


