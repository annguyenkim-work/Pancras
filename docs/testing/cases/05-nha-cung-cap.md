# 05 – Nhà cung cấp nhập khẩu (`/suppliers`)

Thành phần màn hình:
- Ô tìm "Tìm theo mã, tên, người liên hệ, email, điện thoại, quốc gia...", bộ lọc (quốc gia / trạng thái), nút thêm nhà cung cấp.
- Bảng: Nhà cung cấp · Quốc gia · Liên hệ · **Tài khoản** (chỉ vai trò quản lý) · Đơn mua · Dấu vết · Thao tác.
- Nút dòng: **Sửa**, **Disable** (bị mờ kèm chú thích khi còn lô chưa nhập kho), **Active** (khôi phục NCC đã Disable, chỉ Admin).
- Hộp thoại **Thêm / Sửa nhà cung cấp**: Mã NCC*, Quốc gia/khu vực*, Tên nhà cung cấp*, Người liên hệ*, Điện thoại, Email, Ngân hàng, Số tài khoản/IBAN, Mã SWIFT/BIC.
- Hộp thoại **Disable nhà cung cấp** (xác nhận; nút bỏ qua là **Quay lại**).

---

## 1. Chức năng

### 1.1 Danh sách
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| SUP-01 | P2 | Tìm theo mã, tên có dấu/không dấu, người liên hệ, email, điện thoại, quốc gia | Đúng kết quả | ☐ |
| SUP-02 | P2 | Tìm không ra | "Không tìm thấy nhà cung cấp." + "Bỏ lọc" | ☐ |
| SUP-03 | P1 | Cột Tài khoản (vai trò quản lý) | Số tài khoản bị che, chỉ hiện 4 số cuối; số đầy đủ chỉ thấy trong hộp thoại Sửa | ☐ |
| SUP-04 | P2 | Cột Đơn mua | Số đơn / số lô chưa nhập khớp màn hình Mua hàng; bấm vào (nếu là link) mở Mua hàng đã lọc theo NCC | ☐ |
| SUP-05 | P2 | NCC đã Disable | Hiển thị phân biệt; lọc theo trạng thái được | ☐ |

### 1.2 Thêm / sửa
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| SUP-10 | P1 | Nhập đủ → Lưu nhà cung cấp | Thêm thành công; xuất hiện trong danh sách chọn NCC ở đơn mua | ☐ |
| SUP-11 | P1 | Bỏ trống Mã / Quốc gia / Tên / Người liên hệ | Báo thiếu ngay tại ô, ô bị tô đỏ | ☐ |
| SUP-12 | P1 | Mã trùng NCC đang hoạt động (khác hoa/thường) | Báo trùng mã | ☐ |
| SUP-13 | P1 | Mã trùng NCC **đã Disable** | Thông báo riêng: mã thuộc NCC đã Disable, gợi ý Active lại | ☐ |
| SUP-14 | P2 | Điện thoại: `0901234567`, `+84901234567` (hợp lệ); `abc`, quá ngắn, quá 30 ký tự (không hợp lệ) | Đúng như mô tả | ☐ |
| SUP-15 | P2 | Email sai định dạng; email dài hơn 254 ký tự | Báo lỗi ô Email | ☐ |
| SUP-16 | P2 | SWIFT: 8 ký tự `BKCHCNBJ` ✔, 11 ký tự ✔; 7, 9, 10, 12 ký tự ✘; có ký tự đặc biệt ✘ | Đúng như mô tả | ☐ |
| SUP-17 | P2 | Để trống Điện thoại, Email, Ngân hàng, Tài khoản, SWIFT | Lưu được (không bắt buộc) | ☐ |
| SUP-18 | P2 | Tên / Người liên hệ / Ngân hàng quá dài | Báo quá dài kèm giới hạn | ☐ |
| SUP-19 | P2 | Nhiều lỗi cùng lúc | Hiện đủ danh sách lỗi; con trỏ/tô đỏ đúng các ô | ☐ |
| SUP-20 | P1 | Sửa NCC: đổi tên, liên hệ, tài khoản | Lưu được; cột Dấu vết cập nhật người sửa | ☐ |
| SUP-21 | P1 | Sửa NCC: đổi Mã thành mã đã có | Báo trùng | ☐ |
| SUP-22 | P2 | Sửa NCC đang có đơn mua | Đơn mua giữ tên NCC lúc lập, không đổi theo tên mới | ☐ |

### 1.3 Disable / Active (khôi phục)
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| SUP-30 | P1 | NCC không có đơn mua dang dở → Disable → xác nhận | Thành công; NCC **không còn** trong danh sách chọn khi lập đơn mua mới | ☐ |
| SUP-31 | P1 | NCC còn đơn mua chưa nhập kho (Nháp / Chờ duyệt / Đã duyệt / Đang về) | Nút "Disable" bị mờ, chú thích "Còn N lô chưa nhập kho…" | ☐ |
| SUP-32 | P1 | Ép request Disable với NCC ở SUP-31 | Bị từ chối | ☐ |
| SUP-33 | P1 | Bấm "Quay lại" ở hộp thoại xác nhận | Không đổi gì | ☐ |
| SUP-34 | P1 | Admin bấm "Active" | NCC hoạt động lại, chọn được ở đơn mua | ☐ |
| SUP-35 | P2 | Đơn mua cũ của NCC đã Disable | Vẫn xem được, hiện đúng tên NCC | ☐ |

---

## 2. Phân quyền

| Hành động | AD | GĐ | KT | TK | ThK | SA / CS |
|---|---|---|---|---|---|---|
| Vào `/suppliers` | ✔ | ✔ | ✔ | ✔ (chỉ xem) | ✔ (chỉ xem) | ✘ |
| Thêm, Sửa, Disable | ✔ | ✔ | ✔ | ✘ | ✘ | – |
| Thấy cột Tài khoản / thông tin ngân hàng | ✔ | ✔ | ✔ | ✘ | ✘ | – |
| Active lại NCC đã Disable | ✔ | ✘ | ✘ | ✘ | ✘ | – |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| SUP-P01 | P1 | SA, CS | Gõ `/suppliers` | Từ chối truy cập | ☐ |
| SUP-P02 | P1 | TK, ThK | Mở `/suppliers` | Có dòng chú thích chỉ được xem; **không** có nút thêm, cột Thao tác, cột Tài khoản | ☐ |
| SUP-P03 | P1 | TK, ThK | Xem mã nguồn trang (Ctrl+U) và tab Network | **Không** có tên ngân hàng, số tài khoản, SWIFT ở bất kỳ đâu trong phản hồi | ☐ |
| SUP-P04 | P1 | TK, ThK | Gõ `/suppliers?form=1`, `/suppliers?edit=<id>`, `/suppliers?stop=<id>` | Không mở hộp thoại; không lộ dữ liệu ngân hàng | ☐ |
| SUP-P05 | P1 | TK, ThK | Ép request POST tạo / sửa / Stop / Reactivate | Tạo và sửa chuyển tới `/access-denied`, dữ liệu không đổi. Stop và Reactivate báo "Bạn không có quyền thực hiện thao tác này." | ☐ |
| SUP-P06 | P1 | GĐ, KT | NCC đã Disable | Không có nút "Active"; ép request Reactivate bị từ chối | ☐ |
| SUP-P07 | P1 | AD, GĐ, KT | Thêm, sửa, Disable | Làm được | ☐ |
| SUP-P08 | P2 | TK, ThK | Tìm kiếm theo số tài khoản / tên ngân hàng | Không ra kết quả (không dò được thông tin ẩn qua ô tìm) | ☐ |

---

## 3. Workflow: vòng đời nhà cung cấp

```
Thêm → Active ──(không còn đơn chưa nhập kho)──▶ Disabled ──(chỉ Admin, nút Active)──▶ Active
```

| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| SUP-W01 | P1 | KT thêm NCC → lập đơn mua Nháp cho NCC → thử Disable | Bị chặn | ☐ |
| SUP-W02 | P1 | Tiếp SUP-W01: **hủy** đơn mua → Disable | Được | ☐ |
| SUP-W03 | P1 | NCC có 1 đơn Đang về: nhập kho đơn đó (Đã nhận) → Disable | Được | ☐ |
| SUP-W04 | P1 | 2 tab: tab 1 mở hộp thoại Disable NCC X; tab 2 lập đơn mua mới cho X; quay lại tab 1 xác nhận | Bị từ chối (còn đơn chưa nhập kho) | ☐ |
| SUP-W05 | P1 | 2 tab: tab 1 mở form lập đơn mua đã chọn NCC X; tab 2 Disable X; quay lại tab 1 Lưu | Đơn không được tạo cho NCC đã Disable (báo lỗi rõ) | ☐ |
| SUP-W06 | P2 | KT Disable NCC → KT thêm NCC mới cùng mã | Bị báo mã thuộc NCC đã Disable → Admin Active lại | ☐ |
| SUP-W07 | P1 | Mọi thao tác thêm/sửa/ngừng/khôi phục | Có dòng ở `/audit`; không ghi số tài khoản đầy đủ | ☐ |
