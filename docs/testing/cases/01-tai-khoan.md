# 01 – Tài khoản: Đăng nhập, Xác thực hai bước, Hồ sơ, Đăng xuất, Từ chối truy cập

Màn hình: `/login`, màn "Xác thực hai bước", `/profile` ("Hồ sơ của tôi"), nút "Đăng xuất", trang từ chối truy cập.
Ký hiệu vai trò và tài khoản test: xem [README](README.md).

---

## 1. Màn hình Đăng nhập (`/login`)

Thành phần: ô **Mã nhân viên**, ô **Mật khẩu**, nút **Đăng nhập**.

### 1.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| LOGIN-01 | P1 | Nhập đúng mã NV + mật khẩu → Đăng nhập | Vào trang "Tổng quan điều hành", hiện "Xin chào, <tên>" | ☐ |
| LOGIN-02 | P1 | Mã đúng, mật khẩu sai | Báo lỗi chung, **không** tiết lộ mã có tồn tại hay không | ☐ |
| LOGIN-03 | P1 | Mã không tồn tại | Cùng câu báo lỗi như LOGIN-02; thời gian phản hồi tương đương (không nhanh hơn rõ rệt) | ☐ |
| LOGIN-04 | P1 | Bỏ trống mã / bỏ trống mật khẩu / cả hai | Báo thiếu thông tin, không gọi đăng nhập | ☐ |
| LOGIN-05 | P2 | Mã NV nhập khác hoa/thường, có khoảng trắng đầu/cuối | Ghi lại hành vi thực tế (đăng nhập được hay không) và xác nhận với BA | ☐ |
| LOGIN-06 | P1 | Sai mật khẩu 5 lần liên tiếp | Tài khoản bị khóa 15 phút | ☐ |
| LOGIN-07 | P1 | Sau khi khóa, nhập **đúng** mật khẩu | Vẫn bị từ chối cho tới khi hết 15 phút hoặc Admin bấm "Mở khóa" | ☐ |
| LOGIN-08 | P2 | Sai 4 lần → đúng 1 lần → sai 4 lần | Chưa bị khóa (bộ đếm reset khi đăng nhập thành công) | ☐ |
| LOGIN-09 | P1 | Đăng nhập tài khoản đã bị "Ngừng hoạt động" | Từ chối, câu báo như sai mật khẩu | ☐ |
| LOGIN-10 | P2 | Đã đăng nhập, mở lại `/login` | Chuyển về trang chủ (không hiện lại form) | ☐ |
| LOGIN-11 | P2 | Chưa đăng nhập, mở `/inventory?view=transfers` → đăng nhập | Về trang chủ `/` (302 `/`). Không quay lại URL ban đầu | ☐ |
| LOGIN-12 | P1 | `/login?ReturnUrl=https://example.com` → đăng nhập | Không chuyển ra domain ngoài | ☐ |
| LOGIN-13 | P2 | Bấm Enter trong ô mật khẩu | Submit như bấm nút | ☐ |
| LOGIN-14 | P2 | Double-click nút Đăng nhập | Không lỗi, chỉ 1 phiên | ☐ |
| LOGIN-15 | P3 | Ô mật khẩu | Ký tự bị che; không tự điền mật khẩu vào URL/log | ☐ |

### 1.2 Phân quyền / bảo mật
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| LOGIN-P01 | P1 | Chưa đăng nhập, gõ lần lượt `/`, `/inventory`, `/purchasing`, `/suppliers`, `/catalog`, `/employees`, `/locations`, `/audit`, `/profile`, `/customers` | Tất cả chuyển về `/login`, không lộ nội dung | ☐ |
| LOGIN-P02 | P1 | Chưa đăng nhập, gọi trực tiếp link tải file (`/inventory?handler=TransferFile&id=…`, link tải chứng từ đơn mua) | Chuyển về đăng nhập, không tải được file | ☐ |
| LOGIN-P03 | P1 | Gửi form đăng nhập thiếu token chống giả mạo (xóa input `__RequestVerificationToken` bằng DevTools) | Bị từ chối (400) | ☐ |
| LOGIN-P04 | P2 | Nhập `<script>alert(1)</script>` vào ô mã NV | Không chạy script; hiển thị dạng text | ☐ |
| LOGIN-P05 | P2 | Khóa tài khoản Admin bằng cách cố tình sai 5 lần từ máy khác | Theo tài liệu có giới hạn theo IP để không khóa Admin có chủ đích — ghi lại hành vi thực tế **[xác nhận]** | ☐ |

---

## 2. Màn hình Xác thực hai bước (sau bước mật khẩu)

Thành phần: ô **Mã xác thực**, nút **Xác nhận**, nút **Quay lại đăng nhập** (POST, hủy phiên chờ). Chỉ hiện với tài khoản đã bật 2FA.

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| 2FA-L01 | P1 | Tài khoản bật 2FA: nhập đúng mật khẩu | Chuyển sang màn nhập mã, **chưa** vào được hệ thống | ☐ |
| 2FA-L02 | P1 | Ở màn nhập mã, gõ thẳng `/inventory` trên thanh địa chỉ | Vẫn bị chặn (chưa có phiên chính thức) | ☐ |
| 2FA-L03 | P1 | Nhập đúng mã 6 số từ app Authenticator | Vào hệ thống | ☐ |
| 2FA-L04 | P1 | Nhập sai mã | Báo sai mã, ở lại màn hình | ☐ |
| 2FA-L05 | P1 | Dùng lại đúng mã vừa đăng nhập thành công (đăng xuất, đăng nhập lại ngay trong 30 giây) | Bị từ chối – mỗi mã chỉ dùng 1 lần | ☐ |
| 2FA-L06 | P1 | Nhập sai mã 5 lần | Tài khoản bị khóa như sai mật khẩu | ☐ |
| 2FA-L07 | P1 | Chờ quá 5 phút ở màn nhập mã rồi nhập mã đúng | Phải đăng nhập lại từ đầu | ☐ |
| 2FA-L08 | P1 | Nhập **mã khôi phục** thay cho mã 6 số | Vào được; dùng lại mã khôi phục đó lần 2 → bị từ chối | ☐ |
| 2FA-L09 | P2 | Bấm "Quay lại đăng nhập" | Về `/login`, phiên chờ 2FA bị hủy | ☐ |
| 2FA-L10 | P2 | Mở thẳng URL màn nhập mã khi chưa qua bước mật khẩu | Chuyển về `/login` | ☐ |
| 2FA-L11 | P1 | Trong lúc chờ nhập mã, Admin "Ngừng hoạt động" tài khoản | Nhập mã đúng vẫn bị từ chối | ☐ |

---

## 3. Màn hình Hồ sơ của tôi (`/profile`)

Ba khối: **Thông tin cá nhân** (Họ tên*, Email*, Điện thoại*, Địa chỉ – nút "Lưu hồ sơ"), **Đổi mật khẩu** (Mật khẩu hiện tại, Mật khẩu mới – nút "Đổi mật khẩu"), **Xác thực hai bước (tùy chọn)**.

### 3.1 Thông tin cá nhân
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PROF-01 | P1 | Sửa họ tên, email, điện thoại, địa chỉ → Lưu hồ sơ | Lưu thành công; tên mới hiện ở sidebar/trang chủ | ☐ |
| PROF-02 | P1 | Bỏ trống Họ tên / Email / Điện thoại | Báo thiếu, không lưu | ☐ |
| PROF-03 | P2 | Email sai định dạng | Báo lỗi | ☐ |
| PROF-04 | P2 | Đổi họ tên → **mã nhân viên không đổi** | Mã NV giữ nguyên | ☐ |
| PROF-05 | P1 | Trang hồ sơ **không** có ô sửa Vai trò / Chức vụ / Kho | Không có; ép thêm field `Role`, `StockLocationId` vào request → không có tác dụng | ☐ |
| PROF-06 | P2 | Nhập HTML/script vào Địa chỉ | Hiển thị dạng text | ☐ |

### 3.2 Đổi mật khẩu
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PROF-10 | P1 | Mật khẩu hiện tại đúng, mật khẩu mới ≥ 8 ký tự | Đổi thành công; đăng nhập lại bằng mật khẩu mới được, mật khẩu cũ không được | ☐ |
| PROF-11 | P1 | Mật khẩu hiện tại sai | Báo sai mật khẩu, không đổi | ☐ |
| PROF-12 | P1 | Mật khẩu mới 7 ký tự | Báo "Mật khẩu mới cần từ 8 đến 128 ký tự." | ☐ |
| PROF-13 | P2 | Mật khẩu mới đúng 8 ký tự / đúng 128 ký tự / 129 ký tự | 8 ✔, 128 ✔, 129 ✘ với câu "Mật khẩu mới cần từ 8 đến 128 ký tự." | ☐ |
| PROF-14 | P1 | Đổi mật khẩu ở trình duyệt A khi đang đăng nhập cùng tài khoản ở trình duyệt B | Phiên ở B mất hiệu lực ở request kế tiếp **[xác nhận]** | ☐ |
| PROF-15 | P2 | Mật khẩu mới trùng mật khẩu cũ | Ghi lại hành vi thực tế | ☐ |

### 3.3 Xác thực hai bước
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PROF-20 | P1 | Bấm "Bật xác thực hai bước" | Hiện mã QR / khóa bí mật và ô "Mã 6 số" | ☐ |
| PROF-21 | P1 | Quét QR, nhập mã đúng → "Xác nhận và bật" | 2FA bật; hiển thị **10 mã khôi phục** (chỉ hiện 1 lần) | ☐ |
| PROF-22 | P1 | Nhập mã sai khi xác nhận | Báo sai, 2FA chưa bật | ☐ |
| PROF-23 | P2 | Bấm "Hủy" giữa chừng | 2FA vẫn tắt; đăng nhập lần sau không hỏi mã | ☐ |
| PROF-24 | P1 | Tắt 2FA: nhập đúng mật khẩu hiện tại + mã xác thực | Tắt thành công | ☐ |
| PROF-25 | P1 | Tắt 2FA: mật khẩu sai, hoặc mã sai | Không tắt | ☐ |
| PROF-26 | P2 | Tắt 2FA bằng mã khôi phục | Tắt được | ☐ |
| PROF-27 | P2 | Gửi lại request "Bật" khi đã bật (2 tab) | Báo đã bật, không sinh khóa mới | ☐ |
| PROF-28 | P2 | Tải lại trang sau khi bật | Mã khôi phục không hiển thị lại | ☐ |

### 3.4 Phân quyền
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PROF-P01 | P1 | Cả 7 vai trò mở `/profile` | Đều vào được, chỉ thấy hồ sơ **của mình** | ☐ |
| PROF-P02 | P1 | Thêm tham số `?id=<id người khác>` hoặc sửa hidden field id trong form | Không xem/sửa được hồ sơ người khác | ☐ |
| PROF-P03 | P1 | CSKH (chưa có module): sửa hồ sơ, đổi mật khẩu, bật 2FA | Làm được đầy đủ | ☐ |

---

## 4. Đăng xuất và phiên làm việc

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| SESS-01 | P1 | Bấm "Đăng xuất" | Về `/login`; bấm Back không xem lại được trang cũ | ☐ |
| SESS-02 | P1 | Mở 2 tab, đăng xuất ở tab 1, thao tác ở tab 2 | Tab 2 bị chuyển về đăng nhập. Thiết kế: đăng xuất chỉ xóa cookie trên trình duyệt hiện tại. Bản sao cookie có giới hạn tuổi tuyệt đối 12 giờ | ☐ |
| SESS-03 | P1 | Đang đăng nhập, Admin **Ngừng hoạt động** tài khoản | Request kế tiếp bị đăng xuất | ☐ |
| SESS-04 | P1 | Đang đăng nhập, Admin **đổi vai trò** hoặc **đổi kho** | Request kế tiếp: phiên cũ mất hiệu lực hoặc quyền mới áp dụng ngay; không còn dùng quyền cũ | ☐ |
| SESS-05 | P1 | Đang đăng nhập, Admin **đặt lại mật khẩu** | Phiên cũ mất hiệu lực | ☐ |
| SESS-06 | P3 | Để yên quá 8 giờ | Phải đăng nhập lại (khó test, có thể bỏ qua) | ☐ |
| SESS-07 | P2 | Gọi đăng xuất bằng GET (gõ URL) | Không đăng xuất bằng GET (chỉ POST) | ☐ |

## 5. Trang từ chối truy cập

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| DENY-01 | P1 | Sales gõ `/purchasing` | Trang "từ chối truy cập" tiếng Việt, vẫn có layout/sidebar, không lỗi 500 | ☐ |
| DENY-02 | P2 | Từ trang từ chối, bấm về trang chủ | Hoạt động bình thường, vẫn đăng nhập | ☐ |
