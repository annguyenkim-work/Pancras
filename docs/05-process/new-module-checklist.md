# Quy trình làm tính năng và module mới

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Thay cho spec-kit (`specs/`, `.specify/`, các skill `speckit-*`).

**Mục đích:** Các bước từ yêu cầu đến xong, theo đúng thứ tự, để mỗi tính năng đi vào code đã được chốt luật trước.

## Nguyên tắc

- **Docs-first nhẹ:** luật nghiệp vụ được chốt trong docs của module **trước khi** viết code. Không có spec riêng cho từng tính năng.
- **Không bịa luật.** Chỗ chưa chốt ghi `[CHỜ CHỐT]` ngay trong docs module (mục "Chờ chốt"); người và AI không tự quyết thay chủ dự án.
- **Mọi quyết định của chủ dự án được ghi vào docs** (không để trong chat hay file bàn giao).

## Các bước

| # | Bước | Kết quả |
| --- | --- | --- |
| 0 | **Phạm vi.** Chỉ làm khi chủ dự án yêu cầu rõ làm tính năng/module này ([working-with-ai.md](working-with-ai.md) mục 0). Chưa yêu cầu thì dừng ở bước 1 (viết spec) | Có yêu cầu làm |
| 1 | **Chốt luật.** Cập nhật `04-domain/modules/<module>.md` (thực thể, vòng đời, bất biến, quy tắc dễ vi phạm) và `04-domain/roles-and-permissions.md` (ai làm gì). Chủ dự án chốt từng mục chưa rõ, theo đợt câu hỏi ([working-with-ai.md](working-with-ai.md) mục 2) | Docs module ở trạng thái "Đã chốt"; phần chưa rõ nằm trong "Chờ chốt" |
| 2 | **Viết test** (xem [testing.md](../02-coding-rules/testing.md)): test Domain cho bất biến, test Application cho từng mã lỗi và quyền; sửa lỗi thì test tái hiện trước | Test đỏ đúng lý do |
| 3 | **Implement theo tầng, từ trong ra ngoài:** Domain → Application (handler + validator + enum lỗi) → Infrastructure (repository, cấu hình, migration) → Web (PageModel, `.cshtml`) | Build được |
| 4 | **Kết nối:** đăng ký DI trong `Program.cs`, thêm mục vào danh sách menu, `<Module>Access` + `CanX`, thông báo/nhật ký theo module, bỏ khỏi `ComingSoonModel.Routes` nếu là module từng "Sắp có" | Màn hình chạy được, quyền đúng |
| 5 | **UI** theo [03-ui-ux](../03-ui-ux/principles.md): modal, bảng, toast, định dạng VNĐ/ngày | Giao diện đúng chuẩn |
| 6 | **Chạy test và báo cáo trong chat:** build `--warnaserror` và `dotnet test`; nêu số test đạt, test đỏ (tên, lý do), điều chưa kiểm | Báo cáo kết quả |
| 7 | **Vòng sửa lỗi:** có test đỏ hoặc cảnh báo thì sửa rồi chạy lại và báo cáo lại. **Tối đa 3 lần**; sau lần thứ 3 vẫn đỏ thì **dừng và hỏi chủ dự án**, không tự đổi hướng hay sửa test cho xanh | Test xanh, hoặc dừng để hỏi |
| 8 | **Cập nhật test thủ công** của module (`docs/testing/cases/`) và chạy ([manual-testing-and-review.md](manual-testing-and-review.md)) | Case đạt hoặc lỗi được ghi |
| 9 | **Review mã** (mọi thay đổi, xem [manual-testing-and-review.md](manual-testing-and-review.md) mục 5), **Definition of Done** ([definition-of-done.md](definition-of-done.md)), rồi commit | Xong |

## Checklist thêm module mới (tóm tắt)

- [ ] Docs module + dòng trong ma trận quyền (`roles-and-permissions.md`)
- [ ] Handler: một file một handler, `HandleAsync`, FluentValidation, enum lỗi `<Module>Errors.cs`, ghi audit
- [ ] Repository + cấu hình EF (`IEntityTypeConfiguration`), `Version` nếu có sửa đồng thời, migration và áp lên DB local
- [ ] Đăng ký DI (handler, repository) trong `Program.cs`
- [ ] `<Module>Access` trong Domain, `CanX` trong PageModel, view chỉ đọc `Model.CanX`
- [ ] Mục menu (danh sách dữ liệu), trang 403 cho người không có quyền
- [ ] Giao diện theo chuẩn UI; danh sách có phân trang 20/50
- [ ] Test tự động (Domain, Application, Web phân quyền) và test case thủ công
- [ ] Cập nhật `00-overview/product.md` (trạng thái module)

## Chờ chốt (chưa có hiệu lực)

- Mẫu cụ thể cho cấu trúc thư mục khi tạo module mới (có thể sinh từ module `catalog` hoặc `suppliers`).
- Hình thức review mã (ai review, ghi ở đâu) chưa chốt; quy tắc "review mọi thay đổi" đã chốt 2026-11-16 và nằm ở bước 9 và [manual-testing-and-review.md](manual-testing-and-review.md) mục 5. Không bắt buộc PR hay bảo vệ nhánh `main`.
