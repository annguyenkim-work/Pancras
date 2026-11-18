#!/usr/bin/env bash
# Triển khai PancrasERP trên VPS. Chạy bằng root (sudo) từ workflow GitHub Actions:
#   sudo /usr/local/bin/pancras-deploy.sh /opt/pancras/incoming/<tên-bản>
# Thư mục incoming chứa: app/ (bản publish) và efbundle (migrations bundle), cùng build.
# Thứ tự: sao lưu DB -> dừng app -> áp migration -> đổi thư mục app -> khởi động -> kiểm tra.
# Nếu kiểm tra thất bại: khôi phục thư mục app cũ. Migration KHÔNG tự đảo ngược (xem docs/06-operations/deployment.md).
set -euo pipefail

INCOMING="${1:?Thiếu thư mục bản build}"
# Chỉ nhận thư mục con của incoming; tránh dùng quyền root để đọc/chạy đường dẫn tùy ý.
case "$INCOMING" in
    /opt/pancras/incoming/*) ;;
    *) echo "Thư mục bản build phải nằm trong /opt/pancras/incoming/" >&2; exit 1 ;;
esac
[[ "$INCOMING" != *..* ]] || { echo "Đường dẫn không hợp lệ" >&2; exit 1; }
APP_DIR=/opt/pancras/app
PREV_DIR=/opt/pancras/app-prev
ENV_FILE=/etc/pancras.env
BACKUP_DIR=/var/backups/pancras
SERVICE=pancras

[[ -d "$INCOMING/app" && -x "$INCOMING/efbundle" ]] || { echo "Bản build thiếu app/ hoặc efbundle" >&2; exit 1; }
[[ -f "$ENV_FILE" ]] || { echo "Thiếu $ENV_FILE" >&2; exit 1; }

# Đọc chuỗi kết nối từ file môi trường (không in ra màn hình).
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
: "${ConnectionStrings__DefaultConnection:?Thiếu ConnectionStrings__DefaultConnection trong $ENV_FILE}"
# Địa chỉ nội bộ của Kestrel lấy từ cùng file môi trường (ASPNETCORE_URLS).
HEALTH_URL="${ASPNETCORE_URLS:-http://127.0.0.1:5000}/health/live"
HEALTH_URL="${HEALTH_URL//+/127.0.0.1}"
HEALTH_URL="${HEALTH_URL//\*/127.0.0.1}"

# 1. Sao lưu cơ sở dữ liệu trước mọi thay đổi.
mkdir -p "$BACKUP_DIR"
STAMP=$(date +%F_%H%M%S)
sudo -u postgres pg_dump -Fc pancras > "$BACKUP_DIR/db_$STAMP.dump"
echo "Đã sao lưu: $BACKUP_DIR/db_$STAMP.dump"

# 2. Dừng ứng dụng để không ghi dữ liệu trong lúc migrate.
systemctl stop "$SERVICE"

rollback() {
    echo "Triển khai lỗi, khôi phục bản cũ." >&2
    if [[ -d "$PREV_DIR" ]]; then
        rm -rf "$APP_DIR.failed"
        mv "$APP_DIR" "$APP_DIR.failed"
        mv "$PREV_DIR" "$APP_DIR"
        systemctl start "$SERVICE" || true
    fi
    echo "Cơ sở dữ liệu KHÔNG được đảo migration. Nếu migration đã chạy, khôi phục từ $BACKUP_DIR/db_$STAMP.dump nếu cần." >&2
}

# 3. Áp migration.
if ! "$INCOMING/efbundle" --connection "$ConnectionStrings__DefaultConnection"; then
    echo "Migration thất bại." >&2
    systemctl start "$SERVICE" || true
    exit 1
fi

# 4. Đổi thư mục ứng dụng, giữ bản cũ để khôi phục.
rm -rf "$PREV_DIR"
if [[ -d "$APP_DIR" ]]; then
    mv "$APP_DIR" "$PREV_DIR"
fi
cp -r "$INCOMING/app" "$APP_DIR"
# Không đưa file cấu hình phát triển vào production.
rm -f "$APP_DIR/appsettings.Development.json"
chown -R pancras:pancras "$APP_DIR"

# 5. Khởi động và kiểm tra (tối đa 60 giây).
systemctl start "$SERVICE"
for _ in $(seq 1 30); do
    if curl -fsS --max-time 3 "$HEALTH_URL" >/dev/null 2>&1; then
        echo "Triển khai thành công: $(basename "$INCOMING")"
        rm -rf "$INCOMING"
        exit 0
    fi
    sleep 2
done

rollback
exit 1
