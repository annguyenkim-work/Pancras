# PancrasERP — Bộ tài liệu chuẩn

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Đây là **nguồn sự thật duy nhất** về luật nghiệp vụ, quy tắc code, quy tắc UI/UX và quy trình của dự án. Mọi thứ khác (specs cũ, rules cũ, file bàn giao) không còn hiệu lực.

PancrasERP là ERP tiếng Việt cho doanh nghiệp thương mại thiết bị vệ sinh và nội thất, có nhập khẩu container (.NET 10, EF Core 10, PostgreSQL, Razor Pages). Xem [00-overview/product.md](00-overview/product.md).

## Cách dùng bộ docs này

1. **Bắt đầu việc gì thì đọc đúng file của việc đó** (bảng bên dưới). Đừng đọc hết.
2. **Một sự thật chỉ nằm ở một nơi.** Nơi khác chỉ link, không chép lại. Quyền ở [roles-and-permissions.md](04-domain/roles-and-permissions.md); luật từng module ở `04-domain/modules/`; thuật ngữ ở [glossary.md](00-overview/glossary.md).
3. **Chủ dự án quyết định cái gì được ghi vào docs.** Người và AI không tự thêm luật. Chỗ chưa chốt nằm ở mục **"Chờ chốt"** cuối mỗi file và **không có hiệu lực**.
4. **Docs mô tả cái đang chạy cộng với nợ.** Chỗ code chưa theo docs ghi ở mục **"Nợ lệch"** của từng file. Khi sửa xong thì xóa dòng nợ; khi phát sinh nợ mới thì thêm.

## Bản đồ: khi nào đọc file nào

### 00 — Tổng quan
| Việc | Đọc |
| --- | --- |
| Hiểu sản phẩm, module nào đã làm | [product.md](00-overview/product.md) |
| Gọi tên một khái niệm cho đúng (code, UI, docs) | [glossary.md](00-overview/glossary.md) |

### 01 — Kiến trúc
| Việc | Đọc |
| --- | --- |
| Bức tranh tổng thể, luồng một yêu cầu, cấu trúc thư mục | [overview.md](01-architecture/overview.md) |
| Phiên bản công nghệ, thư viện được phép, thêm thư viện | [tech-stack.md](01-architecture/tech-stack.md) |
| Vì sao quyết định như vậy (ADR) | [decisions.md](01-architecture/decisions.md) |

### 02 — Quy tắc code
| Việc | Đọc |
| --- | --- |
| Đặt tên class, handler, file, kiểu | [naming.md](02-coding-rules/naming.md) |
| Tầng nào được/không được làm gì | [layering-and-dependencies.md](02-coding-rules/layering-and-dependencies.md) |
| Viết một use case: handler, Result, FluentValidation, audit | [use-cases-and-results.md](02-coding-rules/use-cases-and-results.md) |
| EF Core, repository, concurrency, migration, xóa mềm | [data-access.md](02-coding-rules/data-access.md) |
| Async, log, xử lý lỗi, trang 500 | [async-logging-errors.md](02-coding-rules/async-logging-errors.md) |
| Phân quyền ở server, mật khẩu, secret, tệp tải lên | [security-and-authorization.md](02-coding-rules/security-and-authorization.md) |
| Viết test | [testing.md](02-coding-rules/testing.md) |
| Lệnh build/test/run, CSS, commit, nhánh | [build-and-git.md](02-coding-rules/build-and-git.md) |

### 03 — UI/UX
| Việc | Đọc |
| --- | --- |
| Triết lý và điều không được làm | [principles.md](03-ui-ux/principles.md) |
| Màu, phông, cỡ chữ, bo góc | [design-tokens.md](03-ui-ux/design-tokens.md) |
| Sidebar, header, trang chủ, bố cục một trang | [app-shell.md](03-ui-ux/app-shell.md) |
| Nút, form, modal, bảng, tab, phân trang, toast, badge | [components.md](03-ui-ux/components.md) |
| Danh sách kiểm UI trước khi báo xong (rút từ lỗi UAT) | [ui-checklist.md](03-ui-ux/ui-checklist.md) |
| Tiền, ngày, số, giọng văn, thông điệp | [formatting-and-copy.md](03-ui-ux/formatting-and-copy.md) |
| Ẩn/hiện theo vai, 403, dữ liệu nhạy cảm | [role-based-ui.md](03-ui-ux/role-based-ui.md) |
| Nút trợ giúp và modal hướng dẫn sử dụng | [in-app-guide.md](03-ui-ux/in-app-guide.md) |

### 04 — Nghiệp vụ (luật)
| Việc | Đọc |
| --- | --- |
| Ai được làm gì (ma trận quyền) | [roles-and-permissions.md](04-domain/roles-and-permissions.md) |
| Nhân viên, đăng nhập, 2FA | [accounts-and-employees.md](04-domain/modules/accounts-and-employees.md) |
| Mặt hàng và bảng giá | [catalog.md](04-domain/modules/catalog.md) |
| Nhà cung cấp | [suppliers.md](04-domain/modules/suppliers.md) |
| Đơn mua, giá vốn, nhận hàng | [purchasing.md](04-domain/modules/purchasing.md) |
| Tồn kho, chuyển kho, kiểm kê, điều chỉnh, hủy | [inventory.md](04-domain/modules/inventory.md) |
| Thông báo và nhật ký hệ thống | [notifications-and-audit.md](04-domain/modules/notifications-and-audit.md) |
| Viết docs cho module mới | [_TEMPLATE.md](04-domain/modules/_TEMPLATE.md) |

### 05 — Quy trình
| Việc | Đọc |
| --- | --- |
| Làm một tính năng hoặc module mới | [new-module-checklist.md](05-process/new-module-checklist.md) |
| Khi nào coi là xong | [definition-of-done.md](05-process/definition-of-done.md) |
| Test thủ công, dữ liệu test, review | [manual-testing-and-review.md](05-process/manual-testing-and-review.md) |
| Quy tắc cho AI agent | [working-with-ai.md](05-process/working-with-ai.md) |
| Chạy test thủ công theo màn hình (11 bộ case) | [testing/cases/README.md](testing/cases/README.md) |

### 06 — Vận hành
| Việc | Đọc |
| --- | --- |
| Chạy dự án trên máy dev | [local-setup.md](06-operations/local-setup.md) |
| Triển khai, sao lưu, cập nhật production | [deployment.md](06-operations/deployment.md) |

## Quy ước trong mỗi file

- **Đầu file:** dòng "Trạng thái" cho biết file đã chốt hay chốt một phần, và ngày chốt.
- **"Nợ lệch so với …":** bảng chỗ code hiện tại chưa theo docs. Không phải luật; là việc cần làm.
- **"Chờ chốt (chưa có hiệu lực)":** những điều chưa được chủ dự án quyết. **Không làm theo, không đoán**; hỏi chủ dự án.
- Docs viết bằng tiếng Việt; tên class, lệnh, code mẫu giữ tiếng Anh.

## Luật cứng (tóm tắt, chi tiết ở các file trên)

- Web không gọi Infrastructure ngoài `Program.cs`; Domain/Application không định dạng chữ, tiền, ngày.
- Lỗi nghiệp vụ trả bằng `Result`, không ném exception; một handler một file, `HandleAsync`; đăng ký DI bằng tay.
- Quyền kiểm ở **cả UI (`CanX`) và handler**; dữ liệu nhạy cảm không đưa vào HTML khi thiếu quyền.
- Không bao giờ để mật khẩu, chuỗi kết nối, token trong mã nguồn, docs hay test.
- Build `--warnaserror` và test xanh trước khi commit; đổi class Tailwind thì `npm run build:css`.
- Tiền hiển thị `VNĐ` và `USD`; ngày `dd/MM/yyyy HH:mm` giờ Việt Nam; nút nhận hàng mua giữ nhãn `Nhập Kho & Cập Nhật Giá Vốn`.
