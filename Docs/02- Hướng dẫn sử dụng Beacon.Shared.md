# 02. Hướng dẫn sử dụng Beacon.Shared
## 1. Mục tiêu của Shared
'Beacon.Shared' là nơi chứa các thành phần dùng chung cho toàn bộ solution  
Shared nên chứa  
•	response wrapper  
•	result pattern  
•	error model  
•	pagination  
•	guard  
•	helper generic  
•	abstractions trung lập  
## 2. Cấu trúc Shared hiện tại.
Beacon.Shared/  
├── Constants/  
├── Results/  
├── Common/  
│   ├── Pagination/  
│   ├── Responses/  
│   └── Guards/  
├── Helpers/  
└── Abstractions/  

---

## 3. Hướng dẫn từng phần

### 3.1 'Results/Error.cs'
Mục đích  
Đại diện cho một lỗi có cấu trúc.  

Nên chứa  
•	`Code`  
•	`Message`  

Ý nghĩa  
•	`Code`: để máy/client xử lý  
•	`Message`: để hiển thị hoặc log ở mức phù hợp  

Ví dụ tốt
new Error(ErrorCodes.UserNotFound, "User not found.");  
new Error(ErrorCodes.EmailAlreadyExists, "Email already exists.");  

Best practice  
•	`Code` nên ổn định  
•	`Message` rõ nghĩa, ngắn gọn   
•	Nên gom mã lỗi vào `Constants/ErrorCodes.cs`  
•	Không hard-code `"USER_NOT_FOUND"` lặp đi lặp lại khắp project  

### 3.2 'Results/Result.cs'
Mục đích  
Biểu diễn kết quả thành công / thất bại không có dữ liệu trả về.  
 
Dùng khi nào  
•	delete thành công  
•	update status thành công  
•	confirm alert thành công  

Best practice  
•	Dùng cho command không cần trả object  
•	Không thêm `Data` vào `Result`  
•	Dùng `Result.Failure(error)` cho lỗi dự đoán trước  

### 3.3 'Results/Result<T>.cs' 
Mục đích  
Biểu diễn kết quả thành công / thất bại có dữ liệu trả về.  

Dùng khi nào  
•	login trả token  
•	get profile trả data  
•	create entity trả object vừa tạo  

Best practice  
•	Chỉ đọc `Value` khi `IsSuccess == true`  
•	Không bọc `ApiResponse<T>` trong `Result<T>`  
•	`Result<T>` là contract nội bộ giữa Application và API  
### 3.4. `Common/Responses/ApiResponse.cs'
Mục đích  
Chuẩn JSON response trả ra ngoài API.  

Ý nghĩa từng field  
•	`Success`: request thành công hay không  
•	`Message`: thông điệp chung  
•	`Code`: mã lỗi hoặc mã nghiệp vụ nếu cần  
•	`Data`: dữ liệu trả về  
•	`Errors`: danh sách lỗi chi tiết hoặc object lỗi mở rộng  

Best practice  
•	Chỉ dùng ở API boundary  
•	Application không nên phụ thuộc `ApiResponse`  
•	Mọi controller nên trả cùng format này  
•	Có static factory method như hiện tại là hợp lý  

Ví dụ  
{  
  "success": true,  
  "message": "Get profile successfully",  
  "code": null,  
  "data": {  
    "id": 1,  
    "name": "Hao"  
  },  
  "errors": null  
}

### 3.5 'Common/Pagination'
`PagedRequest`  
Dùng để chuẩn hóa input phân trang.  

Nên có:  
•	`PageNumber`  
•	`PageSize`  

Có thể mở rộng:  
•	`SortBy`  
•	`SortDirection`  
•	`Keyword`  

`PagedResult`  
Dùng để trả kết quả phân trang.  

Nên có:  
•	items  
•	total count  
•	page number  
•	page size  
•	total pages  

Best practice  
•	mọi API list nên có chiến lược phân trang ngay từ đầu  
•	không trả toàn bộ dữ liệu nếu danh sách có thể lớn  

### 3.6. 'Common/Guards
Mục đích  
Chứa các hàm bảo vệ input / invariant cơ bản.  

Ví dụ:  
•	null check  
•	empty string check  
•	invalid range check  

Best practice  
•	Guard dùng cho validate cơ bản  
•	Không thay thế validator nghiệp vụ  
•	Không nhét logic domain phức tạp vào Guard  

### 3.7 'Constants'
Mục tiêu  
Tập trung các hằng số dùng chung.  

Ví dụ nên có  
•	`ErrorCodes`  
•	`ClaimTypesConstants`  
•	`SystemDefaults`  

Best practice  
•	Chỉ đưa constant thật sự dùng chung  
•	Không biến constants thành nơi nhét string ngẫu nhiên  

### 3.8 ' Helpers'

Mục đích  
Chứa utility trung lập.  

Ví dụ phù hợp  
•	format ngày giờ  
•	normalize string  
•	remove special chars nếu generic  

Không phù hợp  
•	helper riêng cho feature Safety  
•	helper chứa business logic  

### 3.9 'Abtractions'
Mục đích  
Chứa interface trung lập dùng toàn solution.  

Ví dụ  
•	`IDateTimeProvider`  
•	`IUserContext`  

Best practice  
•	abstraction phải nhỏ gọn  
•	không phụ thuộc HTTP cụ thể nếu tránh được  
•	implementation đặt ở Infrastructure  




