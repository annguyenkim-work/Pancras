# Triển khai PancrasERP lên một VPS (VinaHost)

Một VPS chạy cả ứng dụng, PostgreSQL và file ảnh. Hướng dẫn cho Ubuntu 22.04/24.04 LTS. Thay `erp.example.com` bằng tên miền thật.

Kiến trúc: Internet → Caddy (HTTPS tự động) → Kestrel `127.0.0.1:5000` → PostgreSQL `127.0.0.1:5432`.

## 1. Chuẩn bị máy chủ

```bash
adduser deploy                       # tài khoản chạy việc triển khai, không dùng root
usermod -aG sudo deploy
# Chép khóa SSH của bạn vào /home/deploy/.ssh/authorized_keys, rồi tắt đăng nhập bằng mật khẩu:
#   /etc/ssh/sshd_config: PasswordAuthentication no, PermitRootLogin no
sudo systemctl restart ssh

sudo ufw default deny incoming
sudo ufw allow OpenSSH
sudo ufw allow 80,443/tcp
sudo ufw enable
sudo apt install -y fail2ban unattended-upgrades
```

PostgreSQL chỉ nghe ở `localhost`. Không mở cổng 5432 ra ngoài, kể cả trong firewall của VinaHost.

## 2. Cài runtime và cơ sở dữ liệu

```bash
# .NET 10 ASP.NET runtime: theo hướng dẫn chính thức của Microsoft cho Ubuntu
sudo apt install -y aspnetcore-runtime-10.0 postgresql postgresql-contrib caddy

sudo -u postgres psql <<'SQL'
CREATE ROLE pancras LOGIN PASSWORD 'ĐẶT_MẬT_KHẨU_DÀI_NGẪU_NHIÊN';
CREATE DATABASE pancras OWNER pancras;
SQL
```

## 3. Đưa ứng dụng lên

Trên máy phát triển:

```bash
cd src/Pancras.Web
npm install && npm run build:css      # sinh wwwroot/css/site.css (file đã được commit, chạy lại khi sửa giao diện)
dotnet publish -c Release -o publish
# chép thư mục publish lên VPS: /opt/pancras/app
```

Trên VPS:

```bash
sudo useradd --system --home /opt/pancras --shell /usr/sbin/nologin pancras
sudo mkdir -p /opt/pancras/app /opt/pancras/keys /opt/pancras/uploads /opt/pancras/attachments
sudo chown -R pancras:pancras /opt/pancras
sudo chmod 700 /opt/pancras/keys /opt/pancras/attachments
```

`/opt/pancras/attachments` chứa chứng từ đính kèm của đơn mua (tờ khai, hóa đơn, vận đơn). Thư mục này **không** nằm trong thư mục công khai của web, chỉ tải được qua trang đơn mua sau khi đăng nhập bằng tài khoản Admin, Giám đốc, Kế toán. Đây là chứng từ kế toán nên phải nằm trong bản sao lưu.

Ảnh sản phẩm được lưu trong `wwwroot/uploads` của thư mục ứng dụng. Để không mất ảnh khi cập nhật bản mới, tạo liên kết:
`ln -s /opt/pancras/uploads /opt/pancras/app/wwwroot/uploads` (làm một lần sau lần publish đầu).

### Chạy migration

Chạy từ máy phát triển trỏ vào DB trên VPS (qua SSH tunnel `ssh -L 5433:127.0.0.1:5432 deploy@erp.example.com`), hoặc dùng `dotnet ef migrations bundle`:

```bash
dotnet ef database update --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure \
  --connection "Host=127.0.0.1;Port=5433;Database=pancras;Username=pancras;Password=..."
```

### Tạo tài khoản Admin đầu tiên

Mã nguồn không có tài khoản mặc định. Sau khi áp migration, tạo Admin đầu tiên bằng lệnh sau (chạy trên VPS, tại `/opt/pancras/app`, cùng biến môi trường chuỗi kết nối như ở mục 4):

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

Lệnh in ra mã đăng nhập (sinh từ họ tên). Nó chỉ chạy khi chưa có Admin nào đang hoạt động; nếu đã có, lệnh từ chối. Sau khi đăng nhập, đổi mật khẩu trong trang hồ sơ và bật 2FA.

## 4. Chạy bằng systemd

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
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=AllowedHosts=erp.example.com
Environment=DataProtection__KeysPath=/opt/pancras/keys
Environment=Storage__AttachmentsPath=/opt/pancras/attachments
Environment="ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=5432;Database=pancras;Username=pancras;Password=ĐẶT_MẬT_KHẨU"
# Nếu Caddy không chạy trên cùng máy, khai báo IP của nó:
# Environment=Deployment__KnownProxies__0=10.0.0.5
NoNewPrivileges=true
ProtectSystem=full
ReadWritePaths=/opt/pancras

[Install]
WantedBy=multi-user.target
```

Chuỗi kết nối nên đặt trong file riêng `EnvironmentFile=/etc/pancras.env` (quyền `600`, chủ sở hữu root) thay vì ghi thẳng vào unit.

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now pancras
journalctl -u pancras -f
```

**Thư mục `DataProtection__KeysPath` rất quan trọng.** Nó giữ khóa mã hóa cookie đăng nhập và khóa bí mật 2FA. Nếu mất, mọi phiên bị đăng xuất và mọi 2FA đã bật không dùng được nữa (Admin phải tắt rồi bật lại). Phải nằm trong bản sao lưu.

## 5. HTTPS bằng Caddy

`/etc/caddy/Caddyfile`:

```
erp.example.com {
    encode zstd gzip
    reverse_proxy 127.0.0.1:5000
}
```

```bash
sudo systemctl reload caddy
```

Caddy tự xin và gia hạn chứng chỉ Let's Encrypt. Cần trỏ bản ghi DNS `A` của tên miền về IP VPS trước. Ứng dụng tự thêm HSTS, header bảo mật và đặt cookie `Secure` khi `ASPNETCORE_ENVIRONMENT=Production`, và đọc IP thật của khách qua `X-Forwarded-For` (dùng cho giới hạn đăng nhập và nhật ký).

Tùy chọn: đặt tên miền sau Cloudflare (gói miễn phí) để chống tấn công từ chối dịch vụ và che IP VPS. Khi đó cấu hình Caddy chỉ nhận kết nối từ dải IP Cloudflare.

## 6. Sao lưu

Một VPS là một điểm lỗi duy nhất, nên bản sao lưu phải nằm **ngoài máy này** (VPS thứ hai, hoặc kho lưu trữ đối tượng).

`/usr/local/bin/pancras-backup.sh` (chạy bằng cron lúc 02:00, quyền `700`):

```bash
#!/usr/bin/env bash
set -euo pipefail
STAMP=$(date +%F_%H%M)
DEST=/var/backups/pancras
mkdir -p "$DEST"
sudo -u postgres pg_dump -Fc pancras > "$DEST/db_$STAMP.dump"
tar -czf "$DEST/files_$STAMP.tar.gz" /opt/pancras/uploads /opt/pancras/keys /opt/pancras/attachments
find "$DEST" -type f -mtime +14 -delete
# Đẩy ra ngoài máy. Ví dụ với rclone đã cấu hình remote tên "offsite":
rclone copy "$DEST" offsite:pancras-backup --max-age 2d
```

```
0 2 * * * root /usr/local/bin/pancras-backup.sh
```

**Thử khôi phục ít nhất một lần** trên máy khác trước khi tin vào bản sao lưu:

```bash
createdb pancras_restore
pg_restore -d pancras_restore db_YYYY-MM-DD_HHMM.dump
```

Bản sao lưu chứa dữ liệu nhân sự và khóa bí mật, cần mã hóa trước khi đẩy lên nơi lưu trữ bên ngoài (`rclone` có `crypt`, hoặc `gpg -c`).

## 7. Cập nhật phiên bản mới

1. Sao lưu DB (`pg_dump`) trước.
2. Publish bản mới, chép đè vào `/opt/pancras/app` (giữ nguyên liên kết `uploads`).
3. Áp migration mới nếu có.
4. `sudo systemctl restart pancras`, xem `journalctl -u pancras`.

## 8. Kiểm tra sau khi triển khai

- `https://erp.example.com` mở được, `http://` tự chuyển sang `https://`.
- Trình duyệt hiện ổ khóa; header phản hồi có `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options`.
- Đăng nhập sai 5 lần bị khóa; trang `/audit` ghi nhận.
- Cổng 5432 và 5000 không truy cập được từ bên ngoài (`nmap` từ máy khác).
- Khởi động lại VPS: ứng dụng tự chạy lại, phiên đăng nhập còn hiệu lực (khóa nằm trong `keys`).

## Còn thiếu (chưa làm)

- **CSP còn `'unsafe-inline'` cho script:** nhiều trang dùng `onclick` và `<script>` nội dòng. Muốn siết chặt cần chuyển sang file JS riêng hoặc dùng nonce.
- **Phông chữ tải từ Google Fonts:** cần cho phép trong CSP. Có thể tự lưu phông trên máy chủ nếu muốn không phụ thuộc bên ngoài.
- **Quên mật khẩu qua email:** chưa có. Hiện Admin đặt lại mật khẩu.
- **Giám sát:** chưa có cảnh báo khi ứng dụng sập hoặc ổ đĩa đầy (có thể dùng UptimeRobot miễn phí cho `/login`).
