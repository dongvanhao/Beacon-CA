# Cấu hình Endpoint URL Media

## Đã thay đổi gì

- MinIO:Endpoint chỉ còn là endpoint nội bộ để backend upload/delete/khởi tạo bucket với MinIO/S3.
- MinIO:PublicEndpoint là endpoint duy nhất được dùng khi backend tạo presigned media URL trả về client.
- Presigned URL được ký trực tiếp bằng MinIO:PublicEndpoint. Backend không rewrite host sau khi ký, vì việc đó sẽ làm sai chữ ký và gây lỗi SignatureDoesNotMatch.
- appsettings.json không còn hardcode http://10.0.2.2:9000.
- docker-compose.yml map MinIO__PublicEndpoint từ biến HOST_IP, nên có thể đổi giữa emulator, browser và điện thoại thật bằng cách đổi môi trường chạy.

## Các field bị ảnh hưởng

Tất cả API field được resolve qua IStorageService sẽ dùng MinIO:PublicEndpoint, gồm:

- avatarUrl
- senderAvatarUrl
- displayAvatarUrl
- media url
- media thumbnailUrl

Các luồng hiện tại có trả URL media/avatar:

- GET /api/v1/auth/me
- Các endpoint profile/update avatar của user
- Các endpoint upload/list/detail media
- Các endpoint friends/search/presence/friend requests có avatar
- Các endpoint message group list/detail/create có avatar

Trong code backend hiện tại chưa có API feed/post image riêng. Check-in response hiện chỉ trả mediaObjectId, không trả media URL trực tiếp.

## Mô hình cấu hình

Luôn tách 2 endpoint:

```env
# Endpoint nội bộ backend dùng để nói chuyện với MinIO/S3.
MinIO__Endpoint=http://minio:9000

# Endpoint client dùng để tải ảnh/media.
# Địa chỉ này phải truy cập được từ thiết bị gọi API.
MinIO__PublicEndpoint=http://192.168.1.139:9000

``` 

Khi chạy bằng Docker Compose, chỉ cần đổi HOST_IP trong file .env:

```env
HOST_IP=192.168.1.139
```

Docker Compose sẽ expand thành:

```env
MinIO__PublicEndpoint=http://192.168.1.139:9000
```

## Đổi linh hoạt giữa setting cũ và setting mới

### Test trên điện thoại thật cùng Wi-Fi

Dùng LAN IP của máy đang chạy Docker/API:

```env
HOST_IP=192.168.1.139
```

URL media kỳ vọng trong API response:

```text
http://192.168.1.139:9000/beacon-media/...
``` 

Đây là setting nên dùng khi mobile app chạy trên điện thoại thật và đang gọi backend qua:

```text
http://192.168.1.139:5000/api/v1
```

### Test trên Android Emulator

Dùng host alias của Android Emulator:

```env
HOST_IP=10.0.2.2
```

URL media kỳ vọng trong API response:

```text
http://10.0.2.2:9000/beacon-media/...
```

Setting này chỉ dùng cho emulator. Điện thoại thật không truy cập được 10.0.2.2.

## Test browser trên chính máy backend

Có thể dùng:

```env
HOST_IP=localhost
```

Setting này chỉ dùng khi client cũng chạy trên máy backend. Điện thoại thật không truy cập được MinIO của máy tính thông qua localhost.

## Cách áp dụng sau khi đổi .env

Sau khi đổi HOST_IP, cần recreate API container để ASP.NET Core đọc lại biến môi trường:

```powershell
docker compose up -d --build api
```

Không cần xóa data MinIO. Presigned URL được tạo mới theo từng request, nên API response mới sẽ dùng HOST_IP mới.

Có thể kiểm tra config Docker Compose đã expand đúng chưa bằng:

```powershell
docker compose config
```

Tìm giá trị:

```text
MinIO__PublicEndpoint: http://192.168.1.139:9000
```

## Checklist kiểm tra API

Gọi API từ đúng client mục tiêu và xác nhận host của mọi URL media trùng với HOST_IP đã chọn:

- GET /api/v1/auth/me
- Endpoint user profile/avatar
- POST /api/v1/media
- GET /api/v1/media/{id}
- GET /api/v1/media
- Friend/friend request endpoints có avatar
- Message group list/detail endpoints có avatar

Nếu URL trả về vẫn còn 10.0.2.2, localhost hoặc minio, hãy kiểm tra:

- .env đã có HOST_IP đúng chưa.
- API container đã được recreate sau khi đổi .env chưa.
- docker compose config có hiện MinIO__PublicEndpoint đúng giá trị mong muốn chưa.
- Mobile app có tự rewrite URL ở client không. Nếu có, nên tắt logic rewrite đó và dùng URL backend trả về.