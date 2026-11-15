# PancrasERP – Bộ test thủ công theo màn hình

Mỗi file là một màn hình (hoặc một tab), gồm 3 phần: **Chức năng**, **Phân quyền**, **Workflow**.
Kỳ vọng viết theo `docs/04-domain/roles-and-permissions.md` (và docs module ở `docs/04-domain/modules/`) và hành vi đọc từ code ngày 14/11/2026. Chỗ nào code và tài liệu chưa khớp, hoặc tài liệu còn ghi "Cần xác nhận", được đánh dấu **[xác nhận]** – ghi lại hành vi thực tế rồi hỏi BA, đừng tự kết luận Pass/Fail.

## Mục lục

| # | File | Màn hình | Trọng tâm |
|---|---|---|---|
| 01 | [01-tai-khoan.md](01-tai-khoan.md) | Đăng nhập, Xác thực hai bước, Hồ sơ, Đăng xuất | Khóa tài khoản, 2FA, phiên |
| 02 | [02-khung-giao-dien.md](02-khung-giao-dien.md) | Sidebar, Trang chủ, trang "Sắp có" | Mục nào hiện với vai trò nào |
| 03 | [03-nhan-su.md](03-nhan-su.md) | Nhân sự & Phân quyền | Ai tạo/sửa được ai; Admin cuối cùng |
| 04 | [04-danh-muc.md](04-danh-muc.md) | Danh mục sản phẩm và giá | Quyền sửa bảng giá |
| 05 | [05-nha-cung-cap.md](05-nha-cung-cap.md) | Nhà cung cấp | Ẩn tài khoản ngân hàng; ngừng hợp tác |
| 06 | [06-mua-hang.md](06-mua-hang.md) | Mua hàng & Container | **Workflow đơn mua**, giá vốn, kho chỉ thấy đơn kho mình |
| 07 | [07-kho-tra-cuu-ton.md](07-kho-tra-cuu-ton.md) | Kho – Tra cứu tồn kho | Ẩn giá vốn; bảng đối chiếu số liệu |
| 08 | [08-kho-chuyen-kho.md](08-kho-chuyen-kho.md) | Kho – Chuyển kho | **Workflow chuyển kho**: ai làm bước nào |
| 09 | [09-kho-kiem-ke-dieu-chinh-huy.md](09-kho-kiem-ke-dieu-chinh-huy.md) | Kho – Kiểm kê & điều chỉnh | **Workflow duyệt** kiểm kê, điều chỉnh, phiếu hủy |
| 10 | [10-kho-showroom-va-nhat-ky.md](10-kho-showroom-va-nhat-ky.md) | Kho & Showroom; Nhật ký hệ thống | Điều kiện xóa kho; nhật ký đủ và không lộ bí mật |
| 11 | [11-workflow-lien-man-hinh.md](11-workflow-lien-man-hinh.md) | Kịch bản đầu–cuối | Chỗ nối giữa các màn hình |

## Ký hiệu

| Ký hiệu | Nghĩa |
|---|---|
| AD, GĐ, KT | Admin, Giám đốc, Kế toán (gọi chung **TC** – nhóm tài chính, thấy mọi kho) |
| TK | Trưởng Showroom/Kho (gắn 1 kho; mỗi kho tối đa 1 người) |
| ThK | Thủ kho (gắn 1 kho) |
| SA, CS | Sales (gắn 1 kho), CSKH |
| TK-A, ThK-B… | Trưởng kho của kho A, Thủ kho của kho B… |
| P1 / P2 / P3 | Chặn phát hành / quan trọng / phụ |
| KQ | ☐ chưa chạy · ✔ đạt · ✘ lỗi (ghi mã bug) · ? cần xác nhận |
| 🔎 | Bước phải đối chiếu số liệu |

## Chuẩn bị

**Tài khoản** (Admin tạo trên môi trường test; không ghi mật khẩu vào file này hay vào repo):

| Mã gợi nhớ | Vai trò | Kho |
|---|---|---|
| U-AD1, U-AD2 | Admin (cần 2 người cho case "Admin cuối cùng") | – |
| U-GD | Giám đốc | – |
| U-KT1, U-KT2 | Kế toán (2 người cho case đồng thời) | – |
| U-TK-A, U-TK-B | Trưởng Showroom/Kho | A, B |
| U-ThK-A, U-ThK-B, U-ThK-C | Thủ kho | A, B, C |
| U-ThK-0 | Thủ kho **chưa gán kho** (nếu hệ thống không cho tạo thì ghi nhận và bỏ các case liên quan) | – |
| U-SA-A | Sales | A |
| U-CS | CSKH | – |
| U-OFF | Bất kỳ, đã ngừng hoạt động | – |
| U-2FA | Bất kỳ, đã bật xác thực hai bước | – |

**Dữ liệu**: 3 kho/showroom (A, B là kho; C là showroom); ≥ 5 mặt hàng, trong đó 1 mặt hàng thiếu khối lượng/thể tích; 2 nhà cung cấp đang hợp tác + 1 đã ngừng; mỗi mặt hàng có tồn ở A và B qua 1 đơn mua đã nhập.

**File mẫu**: PDF, JPG, PNG, xlsx, docx hợp lệ (< 10 MB); 1 file > 10 MB; 1 file `.exe` đổi đuôi thành `.pdf`.

**Môi trường**: http://localhost:5010 (`dotnet run --project src/Pancras.Web --launch-profile http`). Dùng 2 trình duyệt hoặc 2 cửa sổ ẩn danh cho các case đồng thời.

## Cách test phân quyền (áp dụng cho mọi file)

Mỗi quyền phải được kiểm ở **3 lớp**. Thiếu lớp nào cũng là lỗi:

1. **Menu / nút**: vai trò không có quyền thì không thấy mục sidebar, nút, ô nhập.
2. **Gõ URL trực tiếp**: `/trang`, `/trang?form=1`, `/trang?edit=<id>`, `?order=<id>`… bị từ chối hoặc không mở dữ liệu.
3. **Ép request**: gửi thẳng thao tác mà giao diện không cho.
   - Cách A: đăng nhập vai trò có quyền, mở DevTools → Network, bấm nút, chuột phải request → *Copy as fetch*. Đăng nhập vai trò không có quyền, lấy token chống giả mạo của phiên này (ô ẩn `__RequestVerificationToken` trong bất kỳ form nào), dán lệnh fetch vào Console sau khi thay token.
   - Cách B: ở vai trò đang test, tìm một form có sẵn trên trang, dùng DevTools sửa `action` (ví dụ `/inventory?handler=DecideTransfer`) và các ô ẩn (`DocumentId`, `orderId`, `action`) rồi submit.
   - Kỳ vọng: thông báo từ chối bằng tiếng Việt, **dữ liệu không đổi**, không có thông báo/nhật ký sai.

Ngoài ra với dữ liệu bị ẩn (giá vốn, tài khoản ngân hàng, tiền đơn mua): kiểm cả **mã nguồn trang** (Ctrl+U), tab **Network** và **file xuất** – không chỉ nhìn bằng mắt.

## Cách test workflow (áp dụng cho 06, 08, 09)

Với mỗi luồng có trạng thái, chạy đủ 5 nhóm:

1. **Đường đi đúng**: từng bước hợp lệ, đúng vai trò.
2. **Sai người**: đúng trạng thái nhưng vai trò / kho / người lập không đúng.
3. **Sai trạng thái**: đúng người nhưng nhảy cóc hoặc lặp lại bước (ép request).
4. **Thiếu điều kiện**: thiếu lý do, thiếu file, thiếu tồn.
5. **Đồng thời**: 2 người bấm 2 thao tác xung đột gần như cùng lúc → chỉ 1 thành công, số liệu không bị ghi 2 lần.

Sau mỗi bước ghi lại: trạng thái mới, số liệu tồn, lịch sử phiếu, thông báo gửi cho ai, dòng nhật ký.

## Thứ tự chạy đề xuất

1. 01 → 02 (đăng nhập được và menu đúng thì mới test tiếp).
2. 10A (tạo kho) → 03 (tạo đủ tài khoản) → 04 → 05.
3. 06 (tạo tồn kho bằng đơn mua) → 07 mục 1–2.
4. 08 → 09 → 07 mục 3.
5. 10B (nhật ký) → 11.

## Nghi vấn tìm thấy khi đọc code (cần dev/BA trả lời trước hoặc trong lúc test)

| # | Màn hình | Nghi vấn | Case liên quan |
|---|---|---|---|
| 1 | Sidebar, Trang chủ | Trưởng kho và Thủ kho được vào `/purchasing` để nhận hàng đơn về kho mình, nhưng sidebar và lối tắt trang chủ không có mục Mua hàng → không có đường vào màn hình. | NAV-01, PUR-P02 |
| 2 | Danh mục | Đã chốt: Kế toán được sửa giá và tên. Trưởng kho vẫn không vào được `/catalog`. | CAT-P02 … P04 |
| 3 | Mua hàng | Đã chốt: trả về nháp và hủy đều bắt buộc lý do. Lý do dài hơn 500 ký tự báo "Lý do chỉ được tối đa 500 ký tự." | PUR-W05, PUR-W09 |
| 4 | Mua hàng | Đã chốt: mỗi dòng có ô số thực nhận. Nhận thiếu mà chưa đóng đơn thì "Đã nhận một phần". Đóng đơn khi còn thiếu thì bắt buộc lý do. | PUR-R10 |
| 5 | Mua hàng | Tài liệu dự án ghi VAT không vào giá vốn; màn hình mặc định **cộng** VAT vào giá vốn (bật "VAT được khấu trừ" mới loại ra). Cần chốt mặc định. | PUR-36, PUR-37 |
| 6 | Mua hàng | Đã chốt: một người được vừa gửi duyệt vừa duyệt đơn của mình. Lịch sử ghi người thực hiện từng bước. | PUR-W33 |
| 7 | Kho – Kiểm kê, Phiếu hủy | Nút "Duyệt biên bản" / "Duyệt hủy" có thể hiện với Trưởng kho trên phiếu do chính họ lập (bấm sẽ bị từ chối). | STK-P06, DSP-P03 |
| 8 | Kho – Kiểm kê | Khi tồn sổ sách đổi giữa lúc lập và lúc duyệt: ghi đè theo số đếm hay áp phần chênh lệch? | STK-W06 |
| 9 | Kho | Kiểm kê / điều chỉnh / hủy có được phép kéo tồn xuống dưới số **đang giữ chỗ** cho phiếu chuyển đã duyệt không? | TRF-W11, STK-W07, ADJ-W07, DSP-W07 |
| 10 | Kho – Điều chỉnh | Nhập chênh lệch = 0 có thể ra thông báo "Tồn không được âm." (câu chưa đúng ý). | ADJ-04 |
| 11 | Kho & Showroom | Xóa kho đang có hàng "đang chuyển tới", đang giữ chỗ, hoặc là kho nhận của đơn mua chưa nhập: có bị chặn không? | LOC-12 |
| 12 | Nhân sự | Ràng buộc "mỗi kho 1 Trưởng kho" có tính người đã ngừng hoạt động không? | EMP-W06 |
| 13 | Nhân sự | Đã chốt: sau khi Admin đặt lại mật khẩu, nhân viên không bị buộc đổi mật khẩu ở lần đăng nhập đầu. | EMP-38 |
| 14 | Tài khoản | Giới hạn đăng nhập theo IP (để không khóa Admin có chủ đích) đã có chưa? | LOGIN-P05 |

## Mẫu ghi lỗi

```
Mã lỗi:        BUG-___
Test case:     (ví dụ TRF-P16)
Màn hình:      (ví dụ Kho – Chuyển kho, hộp thoại chi tiết phiếu)
Tài khoản:     vai trò + kho (không ghi mật khẩu)
Trạng thái dữ liệu trước khi thao tác: (ví dụ phiếu A→B đang Chờ duyệt, tồn X tại A = 5)
Bước tái hiện: 1) … 2) … 3) …
Kỳ vọng:       …
Thực tế:       … (kèm câu thông báo nguyên văn)
Đính kèm:      ảnh màn hình, request/response, giờ xảy ra
Mức độ:        Chặn / Nghiêm trọng / Vừa / Nhẹ
```
