# Feature: HTTPS + Nginx Reverse Proxy cho Beacon API

> Spec hạ tầng (infrastructure). **Không** thêm/sửa Domain, Application, hay EF Core — chỉ chạm `Program.cs` (cấu hình proxy), Docker Compose, file Nginx, và biến môi trường.

## Objective
Đặt một Nginx reverse proxy phía trước Beacon.Api để thực hiện **TLS termination** (HTTPS), cho phép client gọi `https://api.<domain>` thay vì `http://host:5000` thuần. Cấu hình hỗ trợ **cả hai môi trường** qua biến môi trường: dev dùng cert **self-signed**, production dùng **Let's Encrypt (Certbot)** tự gia hạn.

## Target Users
- **DevOps / người triển khai** — vận hành stack Docker, cấp/gia hạn cert.
- **Mobile/Web client** (gián tiếp) — được phục vụ qua kết nối HTTPS đáng tin cậy.
- Không thay đổi role/permission nào trong ứng dụng (RBAC giữ nguyên).

## Core Features & Use Cases

1. **TLS termination tại Nginx** — Nginx lắng nghe `443` (TLS) + `80` (redirect→443), proxy HTTP thuần `8080` tới container `api` qua mạng nội bộ Docker.
   - *Acceptance:* `curl https://<domain>/health` trả `200` qua TLS; `curl http://<domain>/...` được `301` về `https://`.

2. **Self-signed cert cho dev** — script sinh cert OpenSSL nội bộ; chọn qua `TLS_MODE=selfsigned`.
   - *Acceptance:* `docker compose --profile dev up` khởi động được, `openssl s_client` thấy cert self-signed; client bỏ qua cảnh báo vẫn gọi được API.

3. **Let's Encrypt cho production** — Certbot companion cấp + gia hạn cert tự động qua HTTP-01 challenge; chọn qua `TLS_MODE=letsencrypt`.
   - *Acceptance:* Sau `docker compose --profile prod up`, Certbot lấy cert thật cho `${API_DOMAIN}`; trình duyệt tin cậy (khóa xanh); cron/timer gia hạn chạy.

4. **Forwarded headers đúng** — Kestrel nhận `X-Forwarded-Proto=https` và `X-Forwarded-For` từ Nginx; IP container Nginx được thêm vào `ForwardedHeaders:KnownProxies`.
   - *Acceptance:* Response của một endpoint sinh URL tuyệt đối trả về `https://...`; log ghi đúng client IP (không phải IP của Nginx).

5. **SignalR qua proxy** — Nginx chuyển tiếp WebSocket (`Upgrade`/`Connection`) cho hub realtime.
   - *Acceptance:* Client SignalR kết nối được tới `wss://<domain>/hubs/...` qua Nginx, nhận message realtime.

6. **HTTP/2 + nén + giới hạn upload** — Nginx bật HTTP/2, gzip, `client_max_body_size` ≥ 110MB (khớp `RequestSizeLimit` upload media).
   - *Acceptance:* Upload file 100MB qua HTTPS thành công, không bị Nginx cắt (`413`).

## Out of Scope
- **Gộp MinIO sau cùng Nginx** — giữ nguyên `minio-nginx` + `minio.conf` hiện tại (theo lựa chọn người dùng: *Chỉ Beacon API*).
- HTTP/3 (QUIC), mTLS, WAF/ModSecurity.
- Thay đổi cơ chế Auth/JWT/RBAC.
- CDN, load balancing nhiều instance API.
- Đổi MinIO public host/chữ ký S3 (không động vào).

## Technical Approach

> Đây là spec hạ tầng — các layer Domain/Application/Infrastructure **không đổi**. Mục dưới liệt kê đúng những gì chạm tới.

### Application/API Layer (`Program.cs`) — chỉ cấu hình proxy
- `ForwardedHeaders` đã có sẵn ([Program.cs:24-40](src/Beacon.Api/Program.cs#L24-L40)) — **không viết lại**, chỉ nạp `KnownProxies` của Nginx qua config `ForwardedHeaders:KnownProxies`.
- Cân nhắc `ForwardLimit` phù hợp số hop proxy (Nginx = 1).
- `UseHttpsRedirection()` ([Program.cs:134](src/Beacon.Api/Program.cs#L134)): xem xét **tắt ở trong container** (Nginx đã lo redirect 80→443) để tránh redirect loop khi Nginx terminate TLS — quyết định trong phần plan, mặc định để Nginx redirect, Kestrel chỉ phục vụ HTTP nội bộ.

### Infrastructure / Deployment (phần chính)
- **`nginx/api.conf.template`** — server block cho API: `listen 443 ssl http2`, `listen 80` (redirect/ACME), upstream `proxy_pass http://api:8080`, header `X-Forwarded-*`, block WebSocket cho SignalR, `client_max_body_size 110m`, gzip.
- **`nginx/Dockerfile`** hoặc dùng `nginx:1.27-alpine` + `envsubst` template (theo pattern `minio-nginx` đã có).
- **`docker-compose.yml`** — thêm service `api-nginx` (cổng `80:80`, `443:443`), Docker **profiles** `dev` / `prod` để tách chế độ cert; service `certbot` chỉ ở profile `prod`.
- **Sinh cert self-signed** — script `nginx/certs/gen-selfsigned.sh` (OpenSSL) cho dev.
- **Certbot** — volume chia sẻ `certbot/conf` + `certbot/www`; container companion `renew` (entrypoint loop `certbot renew`).
- **Volume** — `letsencrypt_certs`, `certbot_www` cho ACME webroot.

### Presentation / Routing
- Không thêm endpoint mới. `/.well-known/acme-challenge/` được Nginx phục vụ từ webroot (chỉ profile prod).
- Giữ nguyên envelope `ApiResponse<T>` — proxy trong suốt với response body.

## Configuration & Secrets
Thêm vào `.env` / `.env.example` (giá trị thật **không commit**):

| Biến | Ý nghĩa | Ví dụ |
|---|---|---|
| `TLS_MODE` | `selfsigned` \| `letsencrypt` | `selfsigned` |
| `API_DOMAIN` | Domain công khai của API | `api.beacon.example.com` |
| `LETSENCRYPT_EMAIL` | Email đăng ký ACME | `ops@beacon.example.com` |
| `API_NGINX_HTTP_PORT` / `API_NGINX_HTTPS_PORT` | Map cổng host | `80` / `443` |

- Cert/khóa private **không** vào image, mount qua volume.
- Cập nhật `ForwardedHeaders:KnownProxies` (appsettings.Production.json) bằng subnet/IP của `api-nginx`.

## Code Style & Architecture
- Tuân thủ pattern Nginx đã có trong repo (`minio.conf` + `envsubst` template + log_format) cho nhất quán.
- Không đưa secret vào file Nginx commit; dùng biến môi trường + `envsubst`.
- Mọi cấu hình chọn-theo-môi-trường đi qua `TLS_MODE` + Docker `profiles`, **không** fork compose file.

## Testing Strategy
> Spec hạ tầng — không có unit/integration test C# mới. Kiểm thử ở mức vận hành.

- **Smoke (dev, self-signed):**
  - `curl -k https://localhost/health` → `200`.
  - `curl -I http://localhost/health` → `301` về https.
  - Upload media 100MB qua HTTPS → không `413`.
  - SignalR client kết nối `wss://localhost/hubs/...` thành công.
- **Smoke (prod, Let's Encrypt):**
  - Cert được trình duyệt tin cậy (không `-k`).
  - `certbot renew --dry-run` thành công.
- **Forwarded headers:** gọi endpoint sinh absolute URL → trả `https://`; xác nhận log ghi client IP thật.
- **Regression:** chạy lại `dotnet test` để chắc thay đổi `Program.cs` (nếu có) không phá integration test hiện có.

## Security Considerations
- **TLS config**: chỉ TLS 1.2/1.3, cipher mạnh, `ssl_session_cache`; bật HSTS (`Strict-Transport-Security`) ở profile prod.
- **Hangfire Dashboard** ([Program.cs:144](src/Beacon.Api/Program.cs#L144)): hiện đang mở — **khuyến nghị** chặn hoặc giới hạn IP tại Nginx (`location /hangfire { allow <ip>; deny all; }`). *Ghi nhận ở đây; bật/tắt quyết định lúc plan.*
- Ẩn header `Server`/`X-Powered-By`; thêm `X-Content-Type-Options`, `X-Frame-Options`.
- Không log `Authorization`/`Cookie` trong Nginx access log (tuân `error-handling/RULE.md` — không log PII/secret).
- Private key cert quyền `600`, không vào git (cập nhật `.gitignore`).

## Boundaries

### Always Do
- Tận dụng `ForwardedHeaders` + `UseHttpsRedirection` đã có; chỉ cấu hình, không viết lại middleware.
- Tách dev/prod bằng `TLS_MODE` + Docker `profiles`, một compose file duy nhất.
- Giữ MinIO proxy hiện tại nguyên vẹn.
- `client_max_body_size` đồng bộ với `RequestSizeLimit` (110MB).

### Ask First
- Thêm NuGet package (dự kiến **không cần** — thuần hạ tầng).
- Bật HSTS preload (ảnh hưởng dài hạn tới domain).
- Tắt `UseHttpsRedirection` trong app.
- Mở/chặn Hangfire Dashboard hoặc Swagger ra public.

### Never Do
- Commit private key / cert thật / `.env` có secret.
- Đặt secret trong file Nginx hoặc appsettings.
- Hardcode domain vào image (luôn qua env).
- Phá chữ ký presigned URL của MinIO.

## Open Questions (chốt ở `/plan`)
1. Có tắt `UseHttpsRedirection` trong container không (Nginx đã redirect 80→443)?
2. Chặn Hangfire Dashboard + Swagger ở production qua Nginx hay giữ mở?
3. Bật HSTS ngay ở prod, hay chờ verify domain ổn định?
4. Cập nhật `KnownProxies` bằng IP tĩnh container hay subnet Docker network?

## Next Step
Sau khi spec được duyệt → chạy `/plan` để chia thành các bước triển khai có thứ tự (Nginx template → compose profiles → cert scripts → Program.cs config → smoke test).
