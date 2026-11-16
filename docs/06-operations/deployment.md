# Triển khai production (VPS)

> Trạng thái: **Đã chốt quy trình; chưa kiểm chứng trên VPS thật** (phiên 2026-11-16). Hướng dẫn này chưa được thử trên VinaHost: **lần triển khai đầu có người chạy từng bước và kiểm theo checklist mục 9**.

**Mục đích:** Cách đưa PancrasERP lên một VPS, cấu hình, sao lưu, cập nhật và kiểm tra. Thay `erp.example.com` bằng tên miền thật. Mọi mật khẩu trong tài liệu là **chỗ giữ chỗ**, không dùng nguyên văn.

## 1. Môi trường

- Hai môi trường: **local** ([local-setup.md](local-setup.md)) và **production**. **Chưa có staging.** Mọi thay đổi và migration được kiểm kỹ ở local trước khi lên production.
- Một VPS chạy cả ứng dụng, PostgreSQL và tệp. Ubuntu 22.04/24.04 LTS.
- Kiến trúc: Internet → **Caddy** (HTTPS tự động) → Kestrel `127.0.0.1:5000` → PostgreSQL `127.0.0.1:5432`.

## 2. Chuẩn bị máy chủ

```bash
adduser deploy
usermod -aG sudo deploy
# Chép khóa SSH vào /home/deploy/.ssh/authorized_keys; tắt đăng nhập bằng mật khẩu và root
#   /etc/ssh/sshd_config: PasswordAuthentication no, PermitRootLogin no
sudo systemctl restart ssh

sudo ufw default deny incoming
sudo ufw allow OpenSSH
sudo ufw allow 80,443/tcp
sudo ufw enable
sudo apt install -y fail2ban unattended-upgrades
```

PostgreSQL chỉ nghe ở `localhost`; **không mở cổng 5432** ra ngoài (kể cả firewall của nhà cung cấp VPS).

## 3. Runtime và cơ sở dữ liệu

```bash
sudo apt install -y aspnetcore-runtime-10.0 postgresql postgresql-contrib caddy

sudo -u postgres psql <<'SQL'
CREATE ROLE pancras LOGIN PASSWORD 'ĐẶT_MẬT_KHẨU_DÀI_NGẪU_NHIÊN';
CREATE DATABASE pancras OWNER pancras;
SQL
```

## 4. Đưa ứng dụng lên

Trên máy dev:

```bash
cd src/Pancras.Web
npm install && npm run build:css      # site.css đã commit; chạy lại khi đổi giao diện
dotnet publish -c Release -o publish  # chép thư mục publish lên VPS: /opt/pancras/app
```

Trên VPS:

```bash
sudo useradd --system --home /opt/pancras --shell /usr/sbin/nologin pancras
sudo mkdir -p /opt/pancras/app /opt/pancras/keys /opt/pancras/uploads /opt/pancras/attachments
sudo chown -R pancras:pancras /opt/pancras
sudo chmod 700 /opt/pancras/keys /opt/pancras/attachments
```

| Thư mục | Chứa | Ghi chú |
| --- | --- | --- |
| `/opt/pancras/app` | Bản publish | Ghi đè khi cập nhật |
| `/opt/pancras/keys` | Khóa Data Protection (cookie đăng nhập, bí mật 2FA) | **Bắt buộc sao lưu**; mất thì mọi phiên và 2FA mất hiệu lực |
| `/opt/pancras/attachments` | Chứng từ đính kèm (tờ khai, hóa đơn, vận đơn, phiếu kho) | **Bắt buộc sao lưu**; không nằm trong thư mục công khai, chỉ tải qua trang sau khi đăng nhập có quyền |
| `/opt/pancras/uploads` | Ảnh sản phẩm | Cấu hình `Storage__UploadsPath=/opt/pancras/uploads`; ứng dụng phục vụ lại ở `/uploads` |

## 5. Cấu hình (biến môi trường)

| Khóa | Ý nghĩa |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Bật HSTS, cookie `Secure`, đọc IP thật qua proxy |
| `ASPNETCORE_URLS=http://127.0.0.1:5000` | Chỉ nghe nội bộ |
| `AllowedHosts=erp.example.com` | Chỉ nhận tên miền thật |
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối PostgreSQL |
| `DataProtection__KeysPath=/opt/pancras/keys` | Thư mục khóa mã hóa |
| `Storage__AttachmentsPath=/opt/pancras/attachments` | Thư mục chứng từ |
| `Storage__UploadsPath=/opt/pancras/uploads` | Thư mục ảnh sản phẩm |
| `Deployment__KnownProxies__0=<IP>` | Chỉ cần khi Caddy không chạy cùng máy |
| `RateLimiting__LoginPerMinute` | Giới hạn đăng nhập theo IP (mặc định 120) |

- **Chuỗi kết nối và mật khẩu không ghi vào mã nguồn hay unit systemd.** Đặt trong `EnvironmentFile=/etc/pancras.env` (quyền `600`, chủ sở hữu root).
- Không có tài khoản mặc định (xem mục 7).

## 6. Chạy bằng systemd và HTTPS

`/etc/systemd/system/pancras.service`:

```ini
[Unit]
Description=PancrasERP
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/pancras/app
ExecStart=/usr/bin/dotnet /opt/pancras/app/Pancras.Web.dll
User=pancras
Restart=always
RestartSec=5
EnvironmentFile=/etc/pancras.env
NoNewPrivileges=true
ProtectSystem=full
ReadWritePaths=/opt/pancras

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now pancras
journalctl -u pancras -f
```

`/etc/caddy/Caddyfile`:

```
erp.example.com {
    encode zstd gzip
    reverse_proxy 127.0.0.1:5000
}
```

Cần trỏ bản ghi DNS `A` về IP VPS trước; Caddy tự xin và gia hạn chứng chỉ Let's Encrypt. Ứng dụng tự thêm HSTS và header bảo mật ở production, và đọc IP thật qua `X-Forwarded-For` (dùng cho giới hạn đăng nhập và nhật ký). Tùy chọn: đặt sau Cloudflare (gói miễn phí) và chỉ nhận kết nối từ dải IP Cloudflare.

## 7. Migration và Admin đầu tiên

### Migration

- **Không tự migrate khi ứng dụng khởi động.** Áp chủ động khi deploy, **sau khi `pg_dump`**.
- Dùng **migrations bundle** (một tệp thực thi) build cùng bản publish, chép lên VPS và chạy tại đó; **không dùng SSH tunnel từ máy dev**:

```bash
# trên máy dev, cùng phiên bản với bản publish
dotnet ef migrations bundle --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure -o efbundle
# trên VPS
./efbundle --connection "Host=127.0.0.1;Port=5432;Database=pancras;Username=pancras;Password=..."
```

- Migration có xóa cột/bảng phải được nêu rõ trong ghi chú cập nhật và đã sao lưu.

### Admin đầu tiên

Chạy trên VPS tại `/opt/pancras/app` với cùng biến môi trường:

```bash
sudo -u pancras env \
  ASPNETCORE_ENVIRONMENT=Production \
  "ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=pancras;Username=pancras;Password=..." \
  PANCRAS_ADMIN_NAME="Nguyễn Văn A" \
  PANCRAS_ADMIN_EMAIL="admin@congty.com" \
  PANCRAS_ADMIN_PHONE="0900000000" \
  PANCRAS_ADMIN_PASSWORD="mật khẩu từ 8 đến 128 ký tự" \
  dotnet Pancras.Web.dll create-admin
```

Lệnh in mã đăng nhập (sinh từ họ tên), chỉ chạy khi chưa có Admin hoạt động. Sau khi đăng nhập: đổi mật khẩu ở `/profile` và **bật 2FA**.

## 8. Sao lưu và khôi phục

Một VPS là một điểm lỗi duy nhất; **bản sao lưu phải nằm ngoài máy này** (VPS thứ hai hoặc kho lưu trữ đối tượng).

- **Hằng ngày 02:00:** `pg_dump -Fc` + nén `uploads`, `keys`, `attachments`; **giữ 14 ngày** trên máy; **đẩy ra ngoài máy và mã hóa** trước khi đẩy (`rclone crypt` hoặc `gpg -c`). Bản sao lưu chứa dữ liệu nhân sự và khóa bí mật.
- **`keys` và `attachments` bắt buộc nằm trong bản sao lưu.**
- **Thử khôi phục** ít nhất một lần trên máy khác **trước khi tin** bản sao lưu, và **lặp lại định kỳ** (ví dụ mỗi quý):

```bash
createdb pancras_restore
pg_restore -d pancras_restore db_YYYY-MM-DD_HHMM.dump
```

Mẫu script `/usr/local/bin/pancras-backup.sh` (cron `0 2 * * *`, quyền `700`):

```bash
#!/usr/bin/env bash
set -euo pipefail
STAMP=$(date +%F_%H%M)
DEST=/var/backups/pancras
mkdir -p "$DEST"
sudo -u postgres pg_dump -Fc pancras > "$DEST/db_$STAMP.dump"
tar -czf "$DEST/files_$STAMP.tar.gz" /opt/pancras/uploads /opt/pancras/keys /opt/pancras/attachments
find "$DEST" -type f -mtime +14 -delete
rclone copy "$DEST" offsite:pancras-backup --max-age 2d   # remote đã cấu hình, có crypt
```

## 9. Cập nhật phiên bản và kiểm tra

Cập nhật:
1. **`pg_dump` trước.**
2. Publish bản mới (đã `npm run build:css`), chép đè `/opt/pancras/app`.
3. Áp **migrations bundle** nếu có migration mới.
4. `sudo systemctl restart pancras`, xem `journalctl -u pancras`.

Checklist sau khi triển khai (**bắt buộc ở lần đầu**):
- [ ] `https://erp.example.com` mở được; `http://` tự chuyển sang `https://`.
- [ ] Header có `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options`; cookie đăng nhập có `Secure`.
- [ ] Đăng nhập sai 5 lần bị khóa; `/audit` ghi nhận.
- [ ] Cổng 5432 và 5000 không truy cập được từ ngoài (`nmap` từ máy khác).
- [ ] Khởi động lại VPS: ứng dụng tự chạy lại và phiên đăng nhập còn hiệu lực (khóa nằm ở `keys`).
- [ ] Đã chạy thử `create-admin`, đổi mật khẩu và bật 2FA.
- [ ] Đã chạy script sao lưu một lần và **thử khôi phục** trên máy khác.

## 10. Giám sát tối thiểu

- Dịch vụ kiểm tra bên ngoài (ví dụ UptimeRobot, miễn phí) gọi `/login` và báo khi ứng dụng sập.
- Theo dõi dung lượng đĩa (bản sao lưu và tệp tải lên tăng dần).
- Log đọc bằng `journalctl -u pancras` (xem [async-logging-errors.md](../02-coding-rules/async-logging-errors.md)).

## Nợ lệch so với quy trình

| Vấn đề | Hiện trạng |
| --- | --- |
| Chưa kiểm chứng | Tài liệu chưa chạy thử trên VinaHost thật; nhiều bước chưa thực hiện |
| `docs/deployment-vps.md` cũ | Ghi cách dùng `dotnet ef database update` qua SSH tunnel và chuỗi kết nối ghi thẳng trong unit; docs mới thay thế |

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16 (chưa làm):** thêm health check (endpoint kiểm tra app và DB) khi đưa lên VinaHost.
- **Đã chốt 2026-11-16:** chưa cần staging và quét virus tệp tải lên. Giám sát lỗi và cảnh báo tập trung vẫn chưa chốt.
- **Đã chốt 2026-11-16:** log trên máy chủ giữ 30 ngày, xoay vòng; bản sao lưu giữ 14 ngày, không lưu dài hạn thêm. (Cấu hình xoay vòng log trên VinaHost chưa làm.)
- **Đã chốt 2026-11-16:** chấp nhận mất tối đa 1 ngày dữ liệu (RPO) và khôi phục trong 1 ngày (RTO), khớp sao lưu hằng ngày. Người chịu trách nhiệm trực chưa chốt.
