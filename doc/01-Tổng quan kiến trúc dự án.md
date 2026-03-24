# 01. Tổng quan kiến trúc dự án Beacon
## 1. Mục đích của kiến trúc:
kiến trúc hiện tại được thiết kế để giải quyết 4 bài toán chính
- Dự án dễ mở rộng theo feature
- Dễ bảo trì khi số lượng file tăng
- Dễ phân  trách nhiệm cho team
- Giảm coupling giữa business logic và framework
## 2. Cấu trúc tổng thể
src/  
├── Beacon.Api  
├── Beacon.Application  
├── Beacon.Domain  
├── Beacon.Infrastructure  
└── Beacon.Shared  

## 3. Vai trò từng project
### 3.1 Beacon.Api
Đây là lớp ngoài cùng, chịu trách nhiệm giao tiếp với client. 
Chứa  
•	Controller  
•	Middleware  
•	BackgroundJobs  
•	Cấu hình JWT, Email, Telegram, Storage  
•	Extension methods để đăng ký DI và pipeline  
•	`Program.cs`  
### 3.2 Beacon.Application
Đây là nơi chứa use case và application logic  
Chứa  
•	DTO  
•	Service / use case  
•	Validator  
•	Mapping  
•	Interfaces cho các dịch vụ bên ngoài  
•	Custom exceptions ở cấp application  
Trách nhiệm  
•	Điều phối luồng xử lý của từng feature  
•	Gọi repository  
•	Kiểm tra rule ở mức use case  
•	Trả `Result` hoặc `Result<T>`  
### 3.3 Beacon.Domain
Chứa  
•	Entity  
•	Enum  
•	Value Object  
•	Repository interface  
•	Rule / invariant nghiệp vụ  
•	Base entity, auditable entity  

Nguyên tắc  
•	Domain là phần ổn định nhất  
•	Domain không được phụ thuộc `Infrastructure`  
•	Domain không nên biết về `ApiResponse`, `Controller`, `DbContext`  
### 3.4 Beacon.Infrastructure  
Đây là nơi triển khai kỹ thuật cụ thể.  
Chứa  
•	`AppDbContext`  
•	Entity configuration  
•	Repository implementation  
•	UnitOfWork  
•	JWT service  
•	Password hasher  
•	Email, Telegram, Storage service  
•	DateTime provider  
Vai trò  
•	Triển khai các interface được yêu cầu từ Domain/Application  
•	Là cầu nối tới database, file storage, email, telegram...  
### 3.5 Beacon.Shared
Đây là thư viện dùng chung cho toàn solution.  
Chứa  
•	`Result`, `Result<T>`, `Error`  
•	`ApiResponse<T>`  
•	Pagination  
•	Guard  
•	Constants  
•	Helper  
•	Abstractions như `IDateTimeProvider`, `IUserContext`  
Nguyên tắc  
•	Chỉ đưa vào Shared những thứ thật sự generic  
•	Không để logic nghiệp vụ của riêng một feature vào Shared  

---

## 4. Cách chia feature trong dự án
Identity
•	User  
•	RefreshToken  
•	Đăng nhập / xác thực / phân quyền  
Safety  
•	SafetySetting  
•	EmergencyContact  
•	DailySafetyStatus  
•	AlertEvent  
Checkins  
•	Checkin  
•	CheckinMedia  
Social  
•	Friendship  
•	Circle  
•	CircleMember  
•	Reaction  
•	Poke  
Notifications  
•	NotificationLog  
•	gửi email / telegram / dispatch thông báo  
Admin  
•	AuditLog  
•	ModerationReport  

---
