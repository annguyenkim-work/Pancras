# 02 – Khung giao diện: Sidebar, Trang chủ, trang "Sắp có"

Màn hình: sidebar trái (mọi trang), trang chủ `/` ("Tổng quan điều hành"), các trang chưa làm (`/customers`, `/sales`, `/quotes`, `/combos`, `/receivables`, `/warranty`).

---

## 1. Sidebar – mục nào hiện với vai trò nào

Đăng nhập lần lượt 7 vai trò, đối chiếu bảng. ✔ = có mục, ✘ = không có mục.

| Mục sidebar | AD | GĐ | KT | TK | ThK | SA | CS |
|---|---|---|---|---|---|---|---|
| Tổng quan điều hành | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Quản lý Kho & Điều chuyển | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ |
| Mua hàng & Container Nhập | ✔ | ✔ | ✔ | ✘ (*) | ✘ (*) | ✘ | ✘ |
| Nhà cung cấp nhập khẩu | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ |
| Danh mục sản phẩm & giá | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Các mục "Sắp có" (khách hàng, bán hàng, báo giá, combo, công nợ, bảo hành) | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Nhân sự & Phân quyền | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ |
| Kho & Showroom | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Nhật ký Hệ thống (Audit Log) | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ |

(*) **Nghi vấn bug**: Trưởng kho và Thủ kho vào được `/purchasing` bằng URL (để nhận hàng đơn về kho mình) nhưng sidebar và lối tắt trang chủ không có mục này → họ không có đường vào màn hình. Xem README mục "Nghi vấn".

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| NAV-01 | P1 | Mỗi vai trò: so sidebar với bảng trên | Khớp từng ô | ☐ |
| NAV-02 | P1 | Mỗi vai trò: gõ URL của các mục **không hiện** | Bị từ chối (trừ ô (*) – ghi lại) | ☐ |
| NAV-03 | P2 | Thứ tự các nhóm và mục trong sidebar | Cố định, giống nhau ở mọi trang | ☐ |
| NAV-04 | P2 | Mục đang mở | Được tô nền xanh; mục "Sắp có" có nhãn "Sắp có" và chữ mờ hơn | ☐ |
| NAV-05 | P2 | Cuối sidebar | Hiện tên người dùng (link về `/profile`) và nút "Đăng xuất" | ☐ |
| NAV-06 | P2 | Thu hẹp cửa sổ xuống cỡ điện thoại | Sidebar ẩn; nút menu mở/đóng sidebar; có link "Hồ sơ" và "Đăng xuất" ở thanh trên | ☐ |
| NAV-07 | P1 | Admin đổi vai trò của user đang đăng nhập → user tải lại trang | Sidebar đổi theo vai trò mới (hoặc bị buộc đăng nhập lại) | ☐ |

## 2. Trang chủ (`/`)

Thành phần: tiêu đề "Xin chào, <tên>", khối "Lối tắt".

| Lối tắt | AD | GĐ | KT | TK | ThK | SA | CS |
|---|---|---|---|---|---|---|---|
| Kho & Điều chuyển | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ |
| Mua hàng & Container | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Nhà cung cấp | ✔ | ✔ | ✔ | ✘ (**) | ✘ (**) | ✘ | ✘ |
| Danh mục sản phẩm & giá | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Nhân sự & Phân quyền | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ |

(**) Trưởng kho/Thủ kho có mục Nhà cung cấp ở sidebar nhưng không có lối tắt ở trang chủ – ghi nhận, mức thấp.

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| HOME-01 | P1 | Mỗi vai trò: so lối tắt với bảng | Khớp | ☐ |
| HOME-02 | P2 | Bấm từng lối tắt | Mở đúng màn hình | ☐ |
| HOME-03 | P2 | CSKH đăng nhập | Trang chủ không có lối tắt nào; không lỗi, bố cục không vỡ | ☐ |
| HOME-04 | P3 | Tên dài / có ký tự đặc biệt | Hiển thị đúng, không vỡ bố cục | ☐ |

## 3. Trang "Sắp có"

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| SOON-01 | P2 | Mở `/customers`, `/sales`, `/quotes`, `/combos`, `/receivables`, `/warranty` | Trang thông báo chức năng sắp có, không lỗi 500 | ☐ |
| SOON-02 | P2 | Đã đăng nhập, mở URL không tồn tại (`/abc`) | Trang 404 tiếng Việt "Không tìm thấy trang", có nút "Về trang chủ", không dùng sidebar, không lộ stack trace | ☐ |
| SOON-03 | P2 | Chưa đăng nhập mở `/customers` | Chuyển về đăng nhập | ☐ |

## 4. Quy ước giao diện chung (kiểm ở mọi màn hình)

| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| UI-01 | P2 | Tiền | Dạng `1.234.567 VNĐ`; không xuất hiện "VND" hay ký hiệu `đ` | ☐ |
| UI-02 | P2 | Hộp thoại thêm/sửa | Có nền mờ phía sau; tiêu đề + nút × đứng yên, chỉ phần thân cuộn; Esc đóng được; không cuộn được trang phía sau | ☐ |
| UI-03 | P2 | Cửa sổ thấp (~600px) với hộp thoại dài | Nút Lưu vẫn bấm được (cuộn trong hộp thoại) | ☐ |
| UI-04 | P2 | Khi lưu lỗi | Hộp thoại mở lại, dữ liệu đã nhập còn nguyên, lỗi hiện trong hộp thoại | ☐ |
| UI-05 | P2 | Trạng thái rỗng | Có câu hướng dẫn thay vì bảng trống | ☐ |
| UI-06 | P2 | Dấu vết (ai tạo, ai sửa, lúc nào) | Hiện ở các bảng có cột "Dấu vết"; giờ theo giờ Việt Nam | ☐ |
| UI-07 | P3 | Chrome / Edge / Firefox | Hành vi giống nhau | ☐ |
| UI-08 | P2 | Bấm đúp nút lưu/duyệt/nhận hàng | Chỉ ghi 1 lần | ☐ |
| UI-09 | P1 | Thu nhỏ và mở rộng menu ở màn 1024 và 1920 | Nội dung căn giữa, không dính trái, không hở khoảng trống bất thường; có độ rộng tối thiểu | ☐ |
| UI-10 | P1 | Mọi ô tiền VNĐ ở mọi form (Danh mục, Mua hàng, Kho): gõ số lớn, tab ra, mở lại | Luôn dạng `1.234.567`; USD, phần trăm, số lượng không bị nhóm sai | ☐ |
