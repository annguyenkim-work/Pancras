# Làm việc với Claude / AI agent

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Quy tắc cho AI agent (Claude Code và công cụ tương tự) khi làm việc trên repo này, để chủ dự án giữ quyền quyết định.

## 0. Phạm vi của AI agent trong dự án

- Mục tiêu khi làm việc với agent: **chuẩn hóa quy trình xây app bằng AI agent, xây dựng spec (docs), và sửa lỗi hoặc chỗ lệch nhỏ** (mục "Nợ lệch", code lệch docs).
- **Không tự làm tính năng mới.** Khi một quyết định đã được chốt, agent ghi vào docs rồi dừng; không đề xuất hay bắt đầu code tính năng mới (module, màn hình, endpoint, thư viện) khi chủ dự án chưa yêu cầu rõ. Chủ dự án muốn làm thì sẽ nói, và khi đó đi theo [new-module-checklist.md](new-module-checklist.md).

## 1. Nguồn luật

- **Bộ docs này (`docs/`) là nguồn duy nhất.** Không còn `.cursorrules`, `.cursor/`, `.specify/`, `specs/`, `ARCHITECTURE.md`, `code-review.md`.
- **`CLAUDE.md` chỉ còn khoảng 40 dòng:** tên dự án, lệnh chính (build, test, run, migration), vài luật cứng không được phá, và **link vào [README](../README.md) của docs**. Không lặp lại nội dung docs để tránh hai nơi lệch nhau.
- Khi sửa một module, **đọc docs của module đó và ma trận quyền trước**, đọc `03-ui-ux` nếu đụng giao diện.

## 2. AI không tự quyết

- **Không bịa luật nghiệp vụ.** Mục `[CHỜ CHỐT]` hoặc "Chờ chốt" là **việc của chủ dự án**; AI nêu lựa chọn kèm khuyến nghị và **hỏi**, không tự chọn.
- Chủ dự án quyết định cái gì được ghi vào docs. AI đề xuất nội dung, không tự ghi quyết định mới.
- **Chốt các mục "Chờ chốt" theo đợt:** AI gom tối đa vài câu hỏi một đợt, mỗi câu nêu lựa chọn kèm khuyến nghị; chủ dự án trả lời; AI ghi **đúng câu trả lời** vào docs kèm ngày (`**Đã chốt YYYY-MM-DD:**`) và nói rõ `(chưa làm)` nếu chưa có code. Câu trả lời mơ hồ hoặc tự mâu thuẫn thì hỏi lại, không đoán; ghi xong báo lại bằng lời mình hiểu để chủ dự án sửa nếu sai.
- **Quy ước kỹ thuật nhỏ** (đặt tên, thư mục, chỉ mục...) chủ dự án có thể giao AI chốt: AI đề xuất theo cách code đang làm, ghi với nhãn `Chốt kỹ thuật (đề xuất của AI, chủ dự án có thể đổi)` để chủ dự án duyệt.
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
- Sửa lỗi và chỗ lệch nhỏ trong phạm vi yêu cầu, thêm test, cập nhật docs liên quan đến thay đổi vừa làm (mục "Nợ lệch", trạng thái module).
- Thêm test bảo vệ quy tắc đã chốt (kiến trúc, UI, ẩn dữ liệu theo vai) mà không đổi hành vi của ứng dụng.

## 6. Cách báo cáo

- Nêu đã làm gì, test nào chạy và kết quả, điều gì **chưa** kiểm hoặc chưa làm. Không nói "xong" khi test đỏ hoặc cảnh báo còn.
- **Báo cáo test ngay trong chat** (không tạo file báo cáo): sau mỗi lần chạy nêu số đạt/đỏ, tên và lý do test đỏ.
- **Vòng sửa lỗi tối đa 3 lần** (sửa, chạy lại, báo cáo). Hết 3 lần mà còn đỏ thì dừng và hỏi chủ dự án; không sửa test cho khớp code hay bỏ test.
- Chỗ nghi ngờ ghi rõ; không che giấu bước bị bỏ qua.
- Không để lại file bàn giao trong repo; ngữ cảnh làm việc nằm trong commit và docs.
- Khi phiên làm việc sắp hết ngữ cảnh hoặc hạn mức (khoảng 95%), **báo chủ dự án** và, nếu cần bàn giao, ghi file bàn giao **ngoài git** (ví dụ `App_Data/`).
- **Khi báo "xong", phân biệt rõ ba mức:** đã ghi vào docs; đã có code và test tự động xanh; đã xem bằng trình duyệt khi đăng nhập. Chưa xem bằng trình duyệt thì nói thẳng.

## Nợ lệch so với quy trình

| Vấn đề | Hiện trạng |
| --- | --- |
| Mục "Chờ chốt" lẫn dòng đã chốt | Nhiều file docs còn các dòng `Đã chốt YYYY-MM-DD` nằm trong mục "Chờ chốt". Quy ước đúng: quyết định đã chốt **chuyển vào thân docs** (đúng mục của nó) rồi xóa khỏi "Chờ chốt"; việc chưa làm ghi ở "Nợ lệch". Cần dọn dần khi sửa từng file |

## Chờ chốt (chưa có hiệu lực)

- Skill/lệnh riêng của dự án (ví dụ lệnh chạy bộ test thủ công, sinh module mới) nếu muốn đóng gói.
