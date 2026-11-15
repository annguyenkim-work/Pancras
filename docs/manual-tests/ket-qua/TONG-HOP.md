# Tổng hợp kết quả test thủ công (14/11/2026)

Chạy bằng HTTP (curl) trên http://localhost:5010, từng lượt một. Các case giao diện/JS (hộp thoại, bố cục, định dạng ô tiền, confirm) **chưa chạy** vì cần trình duyệt. Chi tiết và cách tái hiện nằm trong từng file cùng thư mục. Các lỗi dưới đây là kết quả agent ghi nhận; chỉ BAL-W10 đã được đối chiếu thêm với code.

## Số liệu

| Lượt | File kết quả | Đạt | Lỗi | Chặn | Chưa chạy |
|---|---|---|---|---|---|
| 01–02 | 01-02.md | xem file | xem file | | |
| 03 | 03.md | xem file | xem file | | |
| 04–05 | 04-05.md | xem file | xem file | | |
| 06 | 06.md | 107 | 5 | 0 | 15 |
| 07–08 | 07-08.md | 148 | 6 | 4 | 1 |
| 09 | 09.md | 63 | 9 | 0 | 5 |
| 10–11 | 10-11.md | 104 | 7 | 1 | 0 |

## Lỗi nghiêm trọng (ưu tiên fix)

1. **Duyệt kiểm kê không đổi tồn (BAL-W10, STK-W02/W03/W06–W08, E2E-02, X-01).** `ApproveStocktakeCommandHandler` chỉ đổi trạng thái, nhãn vẫn ghi "Đã duyệt cập nhật tồn". Điều chỉnh và phiếu hủy thì có đổi tồn. Cần chốt rule: ghi đè theo số đếm hay áp chênh lệch, và có chặn dưới số đang giữ chỗ không.
2. **Nhận chuyển kho đồng thời bị ghi hai lần (TRF-C06/C08).** Hai `ReceiveTransfer` cùng lúc đều được ghi; tồn kho đích cộng dư.
3. **Sửa và duyệt phiếu chuyển đồng thời (TRF-C03).** Phiếu Đã duyệt số lượng mới nhưng giữ chỗ theo số cũ.
4. **Duyệt điều chỉnh đồng thời áp hai lần (ADJ-W05, thỉnh thoảng).**
5. **Xóa kho khi còn đơn mua chưa nhập hoặc phiếu chuyển Đã duyệt trỏ tới kho (LOC-12).** Hàng nhập vào kho đã xóa sẽ "biến mất"; phiếu chuyển bị kẹt, giữ chỗ không nhả.

## Lỗi vừa / nhẹ

- Trả về nháp với lý do rỗng được chấp nhận (PUR-W05).
- Sửa đơn mua đồng thời: người lưu sau ghi đè không cảnh báo (PUR-55).
- Số quá lớn gây lỗi 500: điều chỉnh chi phí (PUR-A08), Danh mục.
- Thông báo lỗi sai ngữ cảnh: PUR-13, PUR-16, "Tồn không được âm." ở nhiều chỗ của kho, "Cần lý do và chứng từ." cho chuỗi quá dài.
- Duyệt thất bại không hiện thông báo (do `OpenTransferId` bị gán nên ẩn banner); ép request từ vai trò không thấy phiếu trả 200 không thông báo.
- Số đếm kiểm kê để trống/không phải số lưu thành 0 (STK-05).
- Loại kho không hợp lệ được lưu thành "Kho" và có thể biến Showroom thành Kho (LOC-06).
- Kho đã xóa: phiếu cũ và lịch sử tồn hiện tên kho trống (LOC-13).
- Phân trang lịch sử mặt hàng `page=2` trả lại 50 dòng đầu.
- Lịch sử mặt hàng thiếu mã phiếu (TRF-54); phiếu hủy ghi nhãn "Xuất bán".
- Bấm đúp "Tạo phiếu" ra hai phiếu; đính kèm vào phiếu đã hủy/từ chối vẫn nhận.
- "VNĐ" trong Danh mục; tìm kiếm không dấu không ra kết quả.
- Đã xử lý ở 01–02: đăng nhập luôn về `/` (LOGIN-11), hủy phiên chờ 2FA (2FA-L09), trang 404 tiếng Việt (SOON-02), câu mật khẩu 8–128, rate limit đăng nhập 120/phút kèm trang 429, phiên tối đa 12 giờ.
- Hành động bị từ chối không để lại dòng nhật ký.
- Giao diện (xem TRINH-DUYET.md): hộp thoại tạo đơn mua dùng `<dialog open>` nên không phải modal, Esc không đóng và Tab thoát ra ngoài (UI-1); hộp thoại Kiểm kê, Điều chỉnh, Phiếu hủy cuộn cả hộp nên tiêu đề trôi mất (UI-2).

## Câu hỏi cho BA

- Giá vốn Admin sửa tay được không; Kế toán tạo mặt hàng có giá nhưng không sửa được giá.
- Nhận hàng một phần: có trong handler nhưng chưa nối vào giao diện.
- Tự duyệt đơn mua của chính mình (PUR-W33).
- Buộc đổi mật khẩu sau khi Admin đặt lại (EMP-38); đua khi kiểm tra Admin cuối cùng (EMP-P24).
- Trưởng kho/Thủ kho vào được `/purchasing` nhưng menu không có mục.
- File xuất tồn kho của Admin/GĐ/KT không có cột giá vốn (BAL-P09) — có cần không?
- Sau duyệt chỉ còn người cập nhật cuối, mất người lập (STK-W09) — chủ ý?
- Tài khoản "KimAN" (Sales, thật) đang được gán vào kho test `ZZ Test R10 Showroom F`; không phải do test gán, nên kiểm tra lại.

## Chưa chạy / bị chặn

- Mọi case giao diện và JS (cần trình duyệt): STK-01, STK-11, DOC-05, TRF-23, xem trước giá vốn, thêm/bớt dòng, định dạng ô tiền, hộp xác nhận.
- Cần dữ liệu không tạo được: Thủ kho chưa gán kho (TRF-P07, BAL-P10, STK-P10), danh sách phiếu rỗng (TRF-01, STK-10), nhân viên dùng thử (TRF-P27, P18/P20 của Mua hàng).
- X-07 bị chặn vì các lượt khác còn phiếu treo.

## Dữ liệu test

Không dọn theo ý bạn (DB local). Tiền tố `ZZT-`, `ZZ Test`, `Zz Test`; sao lưu trước khi seed tại `App_Data/backups/pancras-truoc-test-2026-11-14.dump`. Tồn của vài mặt hàng `ZZT-R08-*` bị lệch do lỗi đồng thời, và còn nhiều phiếu treo.
