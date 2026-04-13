# API: Devices — Quản lý thiết bị & Push Notification

> Cập nhật: 2026-04-13  
> Liên quan: `POST /api/v1/auth/login`, `POST /api/v1/devices/register`

---

## 1. Tại sao có module Devices?

Beacon cần gửi **push notification** đến điện thoại người dùng (cảnh báo an toàn, SOS, check-in reminder…) kể cả khi **app đang tắt**.

Để làm được điều đó, server cần lưu **FCM token** (Firebase) hoặc **APNs token** (Apple) của từng thiết bị.

```
Không có device token:          Có device token:
App mở → nhận tin ✅            App tắt → vẫn nhận thông báo ✅
App tắt → không nhận ❌
```

---

## 2. Luồng hoạt động tổng quan

```
[Người dùng mở app]
        │
        ▼
[1] POST /auth/login
    → Gửi: username + password
    → Server tự đọc User-Agent để nhận diện thiết bị (iPhone/Android/Web)
    → Nhận về: accessToken + refreshToken

        │  (Firebase SDK khởi động song song)
        ▼

[2] Firebase SDK trả về FCM token cho app
    (tự động, không cần làm gì)

        │
        ▼

[3] POST /devices/register      ← chỉ cần làm 1 lần / mỗi lần token thay đổi
    → Gửi: { deviceToken: "FCM_TOKEN" }
    → Header: Authorization: Bearer <accessToken>
    → Server lưu token vào đúng phiên đăng nhập

        │
        ▼

[Khi có sự kiện - ví dụ SOS]
    Server → Firebase → Điện thoại nhận notification dù app tắt ✅
```

---

## 3. API Login — những gì thay đổi

### Trước đây (❌ sai — bắt client khai báo device)

```json
POST /api/v1/auth/login
{
  "username": "dongvanhao",
  "password": "hao123",
  "deviceName": "Samsung S24",
  "platform": 2,
  "deviceToken": "fcm-token-xyz"
}
```

### Bây giờ (✅ đúng — server tự nhận diện)

```json
POST /api/v1/auth/login
{
  "username": "dongvanhao",
  "password": "hao123"
}
```

Server tự đọc header `User-Agent` mà trình duyệt/app gửi kèm tự động:

```
iPhone Safari gửi:
  User-Agent: Mozilla/5.0 (iPhone; CPU iPhone OS 17_0...)
  → Server hiểu: iOS Device

Chrome Windows gửi:
  User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64)
  → Server hiểu: Web Browser

Android app gửi:
  User-Agent: Mozilla/5.0 (Linux; Android 14; Pixel 8)
  → Server hiểu: Android Device
```

### Response login (không đổi)

```json
{
  "success": true,
  "message": "Success",
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "dongvanhao",
    "fullName": "Dong Van Hao",
    "accessToken": "eyJhbGci...",
    "refreshToken": "abc123...",
    "accessTokenExpiresAt": "2026-04-13T10:15:00Z"
  }
}
```

> **Lưu ý:** `accessToken` bây giờ chứa thêm `device_id` bên trong — client không cần quan tâm, server tự xử lý.

---

## 4. API Devices/Register — đăng ký push notification

### Endpoint

```
POST /api/v1/devices/register
Authorization: Bearer <accessToken>
Content-Type: application/json
```

### Request body

```json
{
  "deviceToken": "fcm-token-từ-firebase"
}
```

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| `deviceToken` | ✅ | FCM token (Android) hoặc APNs token (iOS). Lấy từ Firebase SDK. |

### Response thành công

```json
{
  "success": true,
  "message": "Success",
  "data": null
}
```

### Response lỗi — token hết hạn

```json
{
  "success": false,
  "message": "Device session not found in token.",
  "code": "TOKEN_INVALID"
}
```

---

## 5. Hướng dẫn tích hợp theo từng platform

### Flutter (Android/iOS)

```dart
// Bước 1: Login
final loginRes = await http.post('/api/v1/auth/login', body: {
  'username': 'dongvanhao',
  'password': 'hao123',
});
final accessToken = loginRes.data['accessToken'];

// Bước 2: Lấy FCM token từ Firebase (tự động)
final fcmToken = await FirebaseMessaging.instance.getToken();

// Bước 3: Gửi lên server (làm sau khi login xong)
await http.post(
  '/api/v1/devices/register',
  headers: {'Authorization': 'Bearer $accessToken'},
  body: {'deviceToken': fcmToken},
);
```

### React Native

```js
// Bước 1: Login
const { accessToken } = await api.post('/auth/login', { username, password });

// Bước 2: Lấy FCM token
const fcmToken = await messaging().getToken();

// Bước 3: Đăng ký
await api.post('/devices/register',
  { deviceToken: fcmToken },
  { headers: { Authorization: `Bearer ${accessToken}` } }
);
```

### Web (không cần làm bước 3)

Web không có push notification qua FCM theo cách này → chỉ cần Login bình thường, bỏ qua bước `/devices/register`.

---

## 6. Khi nào cần gọi `/devices/register`?

| Tình huống | Có cần gọi không? |
|---|---|
| Lần đầu mở app | ✅ Có — sau khi login |
| Firebase cấp token mới | ✅ Có — token thay đổi định kỳ |
| Đăng nhập lại | ✅ Có — session mới, cần cập nhật |
| Mở app khi đã login sẵn | ❌ Không cần — token cũ vẫn dùng được |
| Web app | ❌ Không cần |

### Cách bắt sự kiện token thay đổi (Flutter)

```dart
FirebaseMessaging.instance.onTokenRefresh.listen((newToken) {
  // Token tự động thay đổi → gửi lại lên server
  api.post('/devices/register', body: {'deviceToken': newToken});
});
```

---

## 7. Cơ chế Single Device Login

Khi user đăng nhập từ thiết bị mới, **tất cả phiên cũ bị thu hồi tự động**:

```
Tình huống:
  Đang dùng điện thoại (phiên A) ──── refreshToken A còn hạn

  [Đăng nhập từ máy tính]
        ↓
  Server: revoke refreshToken A
  Server: tạo phiên mới B cho máy tính

  Điện thoại cũ dùng refreshToken A → ❌ Unauthorized
  Máy tính dùng refreshToken B → ✅ Hợp lệ
```

Hành vi này **tự động**, client không cần xử lý gì thêm. Nếu user bị đăng xuất đột ngột → cho user biết "Tài khoản vừa đăng nhập từ thiết bị khác".

---

## 8. Sơ đồ database

```
Users ──────────────── UserDevices
  │ 1                       │ 1
  │                         │
  └── ∞ RefreshTokens ──────┘
            (FK: UserDeviceId)

UserDevices
├── Id (Guid)            ← session ID, ghi trong JWT
├── UserId
├── Platform             ← iOS / Android / Web / Unknown
├── DeviceName           ← "iOS Device", "Android Device"...
├── DeviceToken          ← FCM/APNs token (null cho đến khi gọi /devices/register)
├── IsActive
└── LastSeenAtUtc
```

---

## 9. FAQ

**Q: Client có cần gửi User-Agent thủ công không?**  
A: Không. Trình duyệt và HTTP client (Dio, Axios, fetch…) tự gửi kèm trong mọi request.

**Q: DeviceToken trong JWT là gì?**  
A: Không phải FCM token. Đây là `device_id` (UUID) — ID nội bộ của phiên đăng nhập, server dùng để biết cần cập nhật `UserDevice` nào khi gọi `/devices/register`.

**Q: Không gọi `/devices/register` thì sao?**  
A: Login vẫn hoạt động bình thường. Chỉ là server không thể gửi push notification khi app tắt.

**Q: Token FCM bao lâu thay đổi một lần?**  
A: Firebase có thể cấp token mới bất kỳ lúc nào (thường sau vài tuần hoặc khi reinstall app). Nên lắng nghe sự kiện `onTokenRefresh` và gọi lại `/devices/register` mỗi khi token đổi.
