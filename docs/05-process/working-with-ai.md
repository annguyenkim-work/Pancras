# Làm việc với Claude / AI agent

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Quy tắc cho AI agent (Claude Code và công cụ tương tự) khi làm việc trên repo này, để chủ dự án giữ quyền quyết định.

## 1. Nguồn luật

- **Bộ docs này (`docs/`) là nguồn duy nhất.** Không còn `.cursorrules`, `.cursor/`, `.specify/`, `specs/`, `ARCHITECTURE.md`, `code-review.md`.
- **`CLAUDE.md` chỉ còn khoảng 40 dòng:** tên dự án, lệnh chính (build, test, run, migration), vài luật cứng không được phá, và **link vào [README](../README.md) của docs**. Không lặp lại nội dung docs để tránh hai nơi lệch nhau.
- Khi sửa một module, **đọc docs của module đó và ma trận quyền trước**, đọc `03-ui-ux` nếu đụng giao diện.

## 2. AI không tự quyết

- **Không bịa luật nghiệp vụ.** Mục `[CHỜ CHỐT]` hoặc "Chờ chốt" là **việc của chủ dự án**; AI nêu lựa chọn kèm khuyến nghị và **hỏi**, không tự chọn.
- Chủ dự án quyết định cái gì được ghi vào docs. AI đề xuất nội dung, không tự ghi quyết định mới.
- Khi code và docs mâu thuẫn: **báo cho chủ dự án**, không âm thầm sửa bên nào.
- Khi yêu cầu mơ hồ ở điểm quyết định (quyền, giá, trạng thái, xóa dữ liệu): hỏi trước, đừng đoán.

## 3. Luật cứng khi sửa mã

- Tuân thủ phân tầng ([layering-and-dependencies.md](../02-coding-rules/layering-and-dependencies.md)); Web không gọi Infrastructure ngoài `Program.cs`.
- Build `--warnaserror` và test xanh trước khi báo xong ([definition-of-done.md](definition-of-done.md)).
- **Không đưa mật khẩu, chuỗi kết nối, token vào mã nguồn, docs, test hay lệnh.** Không seed tài khoản trong repo.
- **Nút nhận hàng mua giữ nhãn** `Nhập Kho & Cập Nhật Giá Vốn` (có test ràng buộc).
- Đổi giao diện phải theo `03-ui-ux`; không restyle sang Bootstrap mặc định, không thêm dashboard.

## 4. Việc cần hỏi trước (không làm khi chưa được đồng ý)

- Xóa hoặc ghi đè dữ liệu, SQL trực tiếp vào database, khôi phục bản sao lưu, viết lại lịch sử git, đẩy lên remote, tạo/sửa PR.
- Thêm thư viện hoặc dịch vụ ngoài; đổi cấu hình bảo mật; thay đổi quyền.
- Xóa file docs hay file cấu hình không do AI vừa tạo.

## 5. Việc được làm không cần hỏi

- **Áp migration lên DB local ngay sau khi tạo** (`dotnet ef database update`), nhưng nêu trong báo cáo migration nào đã áp, và **nói rõ nếu migration xóa cột/bảng** (mất dữ liệu). Dừng `Pancras.Web.exe` trước khi build/áp migration.
- Sửa lỗi trong phạm vi yêu cầu, thêm test, cập nhật docs liên quan đến thay đổi vừa làm (mục "Nợ lệch", trạng thái module).

## 6. Cách báo cáo

- Nêu đã làm gì, test nào chạy và kết quả, điều gì **chưa** kiểm hoặc chưa làm. Không nói "xong" khi test đỏ hoặc cảnh báo còn.
- **Báo cáo test ngay trong chat** (không tạo file báo cáo): sau mỗi lần chạy nêu số đạt/đỏ, tên và lý do test đỏ.
- **Vòng sửa lỗi tối đa 3 lần** (sửa, chạy lại, báo cáo). Hết 3 lần mà còn đỏ thì dừng và hỏi chủ dự án; không sửa test cho khớp code hay bỏ test.
- Chỗ nghi ngờ ghi rõ; không che giấu bước bị bỏ qua.
- Không để lại file bàn giao trong repo; ngữ cảnh làm việc nằm trong commit và docs.

## Chờ chốt (chưa có hiệu lực)

- Nội dung cụ thể của `CLAUDE.md` sau khi rút gọn (viết ở bước dọn docs).
- Skill/lệnh riêng của dự án (ví dụ lệnh chạy bộ test thủ công, sinh module mới) nếu muốn đóng gói.
