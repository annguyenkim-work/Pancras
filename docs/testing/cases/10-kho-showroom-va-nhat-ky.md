# 10 – Kho & Showroom (`/locations`) và Nhật ký hệ thống (`/audit`)

---

## A. Kho & Showroom (`/locations`)

Thành phần: ô tìm "Tìm theo tên kho hoặc showroom...", nút **Thêm kho/showroom**, bảng Tên · Loại · Dấu vết · Thao tác (**Sửa**, **Disable**), hộp thoại **Thêm kho/showroom** và **Sửa kho/showroom** (Tên*, Loại: Kho / Showroom).

### A.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| LOC-01 | P1 | Thêm kho mới (Tên, Loại = Kho) | "Đã thêm kho/showroom."; xuất hiện ở: bộ lọc kho màn hình Kho, kho nhận ở đơn mua, kho ở form nhân viên, kho xuất/nơi nhận ở phiếu chuyển | ☐ |
| LOC-02 | P1 | Thêm Showroom | Cột Loại hiện "Showroom" | ☐ |
| LOC-03 | P1 | Bỏ trống tên / chỉ khoảng trắng | "Cần nhập tên." | ☐ |
| LOC-04 | P1 | Tên trùng (khác hoa/thường, thừa khoảng trắng) | "Đã có kho/showroom trùng tên." | ☐ |
| LOC-05 | P2 | Tên 200 ký tự / 201 ký tự (ép request) | 200 ✔; 201 → "Tên quá dài (tối đa 200 ký tự)." | ☐ |
| LOC-06 | P2 | Ép request Loại không hợp lệ | "Loại không hợp lệ." | ☐ |
| LOC-07 | P1 | Sửa tên, đổi loại | "Đã cập nhật kho/showroom."; tên mới hiện ở mọi màn hình liên quan và trên phiếu cũ | ☐ |
| LOC-08 | P1 | Sửa tên thành tên kho khác | Báo trùng | ☐ |
| LOC-09 | P1 | Disable kho **trống** (không tồn, không nhân viên) | "Đã Disable kho/showroom." | ☐ |
| LOC-10 | P1 | Disable kho **còn tồn hàng** | "Không Disable được: kho/showroom còn tồn hàng. Hãy chuyển hết hàng đi trước." | ☐ |
| LOC-11 | P1 | Disable kho **còn nhân viên đang hoạt động** | "Không Disable được: còn nhân viên đang hoạt động gắn với địa điểm này." | ☐ |
| LOC-12 | P1 | Kho tồn 0 nhưng có hàng **đang chuyển tới** hoặc **đang giữ chỗ**, hoặc là kho nhận của đơn mua chưa nhập | Bị chặn: "Không Disable được: còn đơn mua hoặc phiếu chuyển chưa xử lý liên quan đến kho này." (xem `inventory.md`); không lỗi 500. Hàng **đang chuyển tới** hoặc **đang giữ chỗ** mà không có đơn/phiếu mở: ghi lại kết quả | ☐ |
| LOC-13 | P2 | Kho đã từng có phiếu (đã xong) rồi bị Disable | Phiếu cũ, lịch sử tồn vẫn hiện tên kho | ☐ |
| LOC-14 | P2 | Tìm theo tên | Đúng; không ra → "Không có kho hoặc showroom khớp từ khóa." | ☐ |
| LOC-15 | P2 | Nút Disable | Có bước xác nhận trước khi Disable – ghi lại nếu Disable ngay không hỏi | ☐ |
| LOC-16 | P2 | Cột Dấu vết | Ghi người tạo/sửa, giờ | ☐ |

### A.2 Phân quyền
| Hành động | AD | GĐ | KT | TK / ThK / SA / CS |
|---|---|---|---|---|
| Vào `/locations`, xem danh sách | ✔ | ✔ | ✔ | ✘ |
| Thêm, Sửa, Disable | ✔ | ✔ | ✘ | – |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| LOC-P01 | P1 | TK, ThK, SA, CS | Gõ `/locations` | Từ chối truy cập | ☐ |
| LOC-P02 | P1 | KT | Mở `/locations` | Thấy danh sách; **không** có nút Thêm, cột Thao tác | ☐ |
| LOC-P03 | P1 | KT | Gõ `/locations?form=1`, `/locations?edit=<id>` | Không mở hộp thoại | ☐ |
| LOC-P04 | P1 | KT | Ép request thêm / sửa / Disable | "Bạn không có quyền…"; dữ liệu không đổi | ☐ |
| LOC-P05 | P1 | AD, GĐ | Thêm, sửa, Disable | Được | ☐ |

### A.3 Workflow liên màn hình
| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| LOC-W01 | P1 | AD thêm kho C → tạo Trưởng kho C, Thủ kho C → KT lập đơn mua về kho C → duyệt → ThK-C nhập kho → ThK-C lập phiếu chuyển C→A | Trọn luồng; kho C hoạt động như kho A, B | ☐ |
| LOC-W02 | P1 | Dọn kho C: chuyển hết hàng đi (nhận xong), ngừng hoạt động nhân viên kho C → Disable kho C | Disable được sau khi thỏa cả hai điều kiện; thử Disable ở từng bước trung gian đều bị chặn với đúng câu thông báo | ☐ |
| LOC-W03 | P1 | 2 tab: tab 1 mở form nhân viên đã chọn kho C; tab 2 Disable kho C; tab 1 Lưu | Bị từ chối (kho không tìm thấy) | ☐ |
| LOC-W04 | P1 | Thêm/sửa/Disable kho | Có dòng ở `/audit` | ☐ |

---

## B. Nhật ký hệ thống (`/audit`)

Thành phần: ô tìm "Tìm theo mã nhân viên, đối tượng, chi tiết, IP...", bảng Thời điểm · Người thực hiện · Hành động · Đối tượng · Chi tiết · IP. Chỉ xem, không có thao tác.

### B.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| AUD-01 | P1 | Mở trang | Dòng mới nhất ở trên; giờ theo giờ Việt Nam | ☐ |
| AUD-02 | P2 | Tìm theo mã nhân viên, đối tượng, chi tiết, IP | Đúng kết quả | ☐ |
| AUD-03 | P2 | Chưa có dòng nào / tìm không ra | "Chưa có dòng nhật ký nào." | ☐ |
| AUD-04 | P1 | Không có nút sửa, xóa; thử gửi POST tới `/audit` | Không có cách nào sửa/xóa nhật ký | ☐ |
| AUD-05 | P2 | Rất nhiều dòng | Có phân trang hoặc giới hạn; tải nhanh | ☐ |

### B.2 Bảng đối chiếu – thao tác nào phải có nhật ký
Thực hiện từng thao tác rồi tìm dòng tương ứng (đúng người, đúng đối tượng, đúng giờ, đúng IP).

| ID | P | Thao tác | Có dòng? | KQ |
|---|---|---|---|---|
| AUD-10 | P1 | Đăng nhập thành công | ✔ | ☐ |
| AUD-11 | P1 | Đăng nhập sai / tài khoản bị khóa | ✔ | ☐ |
| AUD-12 | P1 | Tạo nhân viên; đổi vai trò; đổi kho | ✔ | ☐ |
| AUD-13 | P1 | Đặt lại mật khẩu; mở khóa; tắt 2FA của người khác | ✔ | ☐ |
| AUD-14 | P1 | Ngừng hoạt động / kích hoạt nhân viên | ✔ | ☐ |
| AUD-15 | P1 | Tự đổi mật khẩu; bật/tắt 2FA | ✔ | ☐ |
| AUD-16 | P1 | Đổi giá ở Danh mục | ✔ | ☐ |
| AUD-17 | P1 | Thêm / sửa / ngừng / khôi phục nhà cung cấp | ✔ | ☐ |
| AUD-18 | P1 | Đơn mua: tạo, sửa, đổi trạng thái (gửi duyệt, duyệt, trả về nháp, đang về, hủy), nhập kho, điều chỉnh chi phí, đính kèm/xóa chứng từ | ✔ | ☐ |
| AUD-19 | P1 | Phiếu chuyển: lập, sửa, duyệt, từ chối, xuất, nhận (có ghi chú khi lệch), xử lý chênh lệch, hủy, trả hàng | ✔ | ☐ |
| AUD-20 | P1 | Duyệt kiểm kê, điều chỉnh, phiếu hủy | ✔ | ☐ |
| AUD-21 | P1 | Thêm / sửa / Disable kho | ✔ | ☐ |
| AUD-22 | P2 | Thao tác bị từ chối vì thiếu quyền | Ghi lại: có dòng hay không | ☐ |

### B.3 Nội dung nhạy cảm
| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| AUD-30 | P1 | Cột Chi tiết của các dòng đăng nhập, đổi/đặt lại mật khẩu, 2FA | **Không** có mật khẩu, mã 2FA, mã khôi phục, khóa bí mật | ☐ |
| AUD-31 | P1 | Dòng nhà cung cấp | Không có số tài khoản đầy đủ | ☐ |
| AUD-32 | P2 | Nhập `<script>` vào tên nhân viên / lý do hủy rồi xem ở `/audit` | Hiển thị dạng text | ☐ |

### B.4 Phân quyền
| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| AUD-P01 | P1 | AD, GĐ | Mở `/audit` | Vào được | ☐ |
| AUD-P02 | P1 | KT, TK, ThK, SA, CS | Gõ `/audit`, `/audit?q=…` | Từ chối truy cập | ☐ |
| AUD-P03 | P1 | GĐ | Thấy nhật ký về Admin | Thấy (chỉ xem) | ☐ |
