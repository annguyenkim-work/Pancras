# Module: <Tên module> (`/<route>`)

> Trạng thái: **Khung** / **Chốt một phần** / **Đã chốt** (phiên <yyyy-MM-dd>). Quyền theo vai: [../roles-and-permissions.md](../roles-and-permissions.md) mục <n>.

**Mục đích:** Một hai câu: module làm gì và luật nào nằm ở đây. Code: `Domain/<Module>`, `Application/<Module>`, `Infrastructure/Persistence/<Module>Repository.cs`, `Web/Pages/<Module>`.

## 1. Thực thể

| Thực thể | Vai trò |
| --- | --- |
| `<Entity>` | Một dòng mô tả |

- Trường bắt buộc, giới hạn độ dài, tính duy nhất (mã), `Version` nếu hai người sửa cùng lúc, dấu vết bản ghi.

## 2. Vòng đời / trạng thái

Sơ đồ trạng thái (khối mã) và quy tắc chuyển trạng thái: ai làm, điều kiện, lý do bắt buộc, tác động đến dữ liệu khác.

## 3. Luật nghiệp vụ và bất biến

Đánh số từng luật; mỗi luật một câu kiểm chứng được bằng test (xem [testing.md](../../02-coding-rules/testing.md)).

## 4. Quyền và dữ liệu nhạy cảm

Chỉ trỏ tới [roles-and-permissions.md](../roles-and-permissions.md); nêu riêng dữ liệu nào bị ẩn theo vai ([role-based-ui.md](../../03-ui-ux/role-based-ui.md)).

## 5. Màn hình

Route, danh sách (cột, bộ lọc, phân trang 20/50), modal tạo/sửa, nút theo [components.md](../../03-ui-ux/components.md), thông điệp theo [formatting-and-copy.md](../../03-ui-ux/formatting-and-copy.md).

## 6. Thông báo và nhật ký

Sự kiện nào gửi thông báo cho ai; mã hành động audit (`nhóm.sự_kiện`). Xem [notifications-and-audit.md](notifications-and-audit.md).

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| | |

## Quyết định đã chốt

- Quyết định của chủ dự án kèm ngày; liên quan code thì đánh dấu `[Đã làm]` hoặc `[Chưa làm]`. Xóa khỏi "Chờ chốt" khi đã chốt.

## Chờ chốt (chưa có hiệu lực)

- Việc chưa được chủ dự án quyết; **không đoán**, hỏi chủ dự án.
