# 07. Hướng dẫn tạo Repository

## 1. Mục tiêu
Tài liệu này hướng dẫn cách tạo Repository theo kiến trúc hiện tại của Beacon:
- Định nghĩa interface ở Application
- Cài đặt repository ở Infrastructure
- Đăng ký Dependency Injection trong API
- Áp dụng best practice khi làm việc với EF Core

---

## 2. Quy ước kiến trúc
1. Interface Repository đặt ở Application.
2. Implementation Repository đặt ở Infrastructure.
3. Repository chỉ xử lý truy vấn và thao tác dữ liệu.
4. Business logic đặt ở UseCase/Service (Application), không đặt trong Repository.

---

## 3. Cấu trúc thư mục
- Interface:
  - `src/Beacon.Application/Common/Interfaces/IRepository/`
- Implementation:
  - `src/Beacon.Infrashtructure/Repository/Identity/`
  - `src/Beacon.Infrashtructure/Repository/User/`

Ví dụ hiện tại:
- `IAdminRepository`
- `IUserRepository`
- `AdminRepository`
- `UserRepository`

---

## 4. Các bước tạo Repository mới
### Bước 1: Tạo interface ở Application
Tạo file interface trong:
`src/Beacon.Application/Common/Interfaces/IRepository/`

Ví dụ:
```csharp
public interface IExampleRepository
{
    Task<Entity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Entity entity, CancellationToken cancellationToken = default);
    void Update(Entity entity);
}
```

### Bước 2: Tạo implementation ở Infrastructure
Tạo class trong:
`src/Beacon.Infrashtructure/Repository/...`

Inject `AppDbContext` và implement interface.

Ví dụ:
```csharp
public class ExampleRepository : IExampleRepository
{
    private readonly AppDbContext _dbContext;

    public ExampleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Entity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Entity>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<Entity>().AddAsync(entity, cancellationToken);
    }

    public void Update(Entity entity)
    {
        _dbContext.Set<Entity>().Update(entity);
    }
}
```

### Bước 3: Đăng ký DI trong API
Mở `src/Beacon.Api/Program.cs` và add:

```csharp
builder.Services.AddScoped<IExampleRepository, ExampleRepository>();
```

### Bước 4: Build kiểm tra compile
```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src
dotnet build Beacon.Api\Beacon.Api.csproj
```

---

## 5. Best practice
1. Tất cả method async nên nhận `CancellationToken`.
2. Method chỉ đọc dữ liệu dùng `AsNoTracking()` khi không cần update.
3. Repository không gọi `SaveChanges()` trực tiếp nếu hệ thống dùng UnitOfWork.
4. Đặt tên method rõ mục đích:
- `GetBy...Async`
- `Exists...Async`
- `AddAsync`
- `Update`
5. Chỉ `Include(...)` khi thật sự cần dữ liệu liên quan.
6. Tránh viết query phức tạp lặp lại nhiều nơi, tách private query helpers khi cần.

---

## 6. Mẫu áp dụng trong project hiện tại
### UserRepository
- File: `src/Beacon.Infrashtructure/Repository/User/UserRepository.cs`
- Chức năng chính:
  - Get theo `Id/UserName/Email/Phone`
  - Check tồn tại theo `UserName/Email/Phone`
  - Add/Update user

### AdminRepository
- File: `src/Beacon.Infrashtructure/Repository/Identity/AdminRepository.cs`
- Chức năng chính:
  - Get theo `Id/UserName`
  - Check tồn tại `UserName`
  - Add/Update admin

---

## 7. Checklist review trước khi commit
1. Interface nằm đúng project Application.
2. Implementation nằm đúng project Infrastructure.
3. Đã đăng ký DI trong `Program.cs`.
4. Build thành công.
5. Không có business logic nghiệp vụ trong Repository.
6. Query có index phù hợp với cột tìm kiếm chính.

---

## 8. Lỗi thường gặp
### 8.1 Không resolve được repository khi chạy
Nguyên nhân: quên đăng ký DI.

Cách xử lý: thêm `AddScoped<,>` trong `Program.cs`.

### 8.2 Lỗi circular dependency
Nguyên nhân: Repository phụ thuộc Service hoặc ngược chiều kiến trúc.

Cách xử lý: giữ đúng chiều phụ thuộc:
- Api -> Application -> Infrastructure

### 8.3 Query chậm
Nguyên nhân: thiếu index hoặc Include quá nhiều.

Cách xử lý:
- thêm index qua EF configuration + migration
- tối giản projection/query
