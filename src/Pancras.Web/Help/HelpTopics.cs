using Pancras.Application.Inventory;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Web.Help;

/// <summary>
/// Nội dung hướng dẫn tích hợp, lưu trong code (docs/03-ui-ux/in-app-guide.md). Thêm màn hình mới = thêm một <see cref="HelpTopic"/>
/// và một dòng trong <see cref="All"/>. Bảng phân quyền tính từ các hàm Access, không gõ tay.
/// </summary>
public static class HelpTopics
{
    public const string InventoryBalances = "inventory.balances";

    public const string InventoryTransfers = "inventory.transfers";

    public const string InventoryStocktakes = "inventory.stocktakes";

    public const string Purchasing = "purchasing";

    public static HelpTopic? Find(string? key) =>
        key is null ? null : All.FirstOrDefault(topic => string.Equals(topic.Key, key, StringComparison.Ordinal));

    private static HelpTopic Balances { get; } = new(
        InventoryBalances,
        "Tra cứu tồn kho",
        "Xem mỗi mặt hàng còn bao nhiêu ở từng kho và showroom để biết có đủ hàng bán, chuyển hoặc kiểm kê.",
        [
            "Xem bốn số cho mỗi mặt hàng: Tồn thực tế, Đang giữ chỗ, Khả dụng (tồn thực tế trừ đang giữ chỗ) và Đang chuyển.",
            "Xem tồn của từng địa điểm và lịch sử biến động của một mặt hàng.",
            "Lọc theo địa điểm, ngành hàng; tìm theo mã, tên, thương hiệu (danh sách lọc ngay khi gõ).",
            "Xuất tệp CSV đúng theo bộ lọc đang chọn (vai có quyền).",
            "Cột giá vốn chỉ hiện với Admin, Giám đốc, Kế toán."
        ],
        [
            "Chọn địa điểm hoặc ngành hàng ở bộ lọc, hoặc gõ mã, tên, thương hiệu vào ô tìm kiếm.",
            "Đọc số Khả dụng để biết còn bao nhiêu hàng có thể bán hoặc chuyển. Hàng Đang chuyển chưa tính vào khả dụng của kho nào.",
            "Bấm Chi tiết ở một mặt hàng để xem tồn theo từng địa điểm và lịch sử biến động.",
            "Bấm Xuất CSV để tải danh sách đang lọc (nếu bạn có quyền)."
        ],
        [
            new("Tra cứu tồn kho (mọi kho và showroom)", role => HelpProbe.Role(role, InventoryAccess.CanView)),
            new("Xem giá vốn", role => HelpProbe.Role(role, InventoryAccess.CanSeeCost)),
            new("Xuất CSV tồn kho", role => InventoryRoles.IsFinance(role)
                ? HelpGrant.Yes
                : HelpProbe.Role(role, InventoryAccess.CanExport, "Kho mình"))
        ]);

    private static HelpTopic Transfers { get; } = new(
        InventoryTransfers,
        "Chuyển kho",
        "Chuyển hàng giữa các kho và showroom theo quy trình có duyệt, có lịch sử từng bước và đối chiếu số thực nhận.",
        [
            "Lập phiếu đề nghị chuyển kho: chọn kho xuất, nơi nhận, mặt hàng và số lượng.",
            "Duyệt hoặc từ chối phiếu (từ chối bắt buộc nhập lý do). Duyệt sẽ giữ hàng ở kho xuất.",
            "Xuất kho: trừ tồn kho xuất, hàng chuyển sang trạng thái đang vận chuyển; ghi số seal và người vận chuyển (ghi chú).",
            "Nhận hàng: nhập số thực nhận từng dòng; nhận lệch thì ghi lý do, đính kèm tệp và chờ xử lý chênh lệch.",
            "Hủy phiếu (trước khi xuất kho) và Trả về kho xuất (khi đang vận chuyển), đều bắt buộc lý do.",
            "In phiếu xuất kho từ khi phiếu đã duyệt (phiếu chờ duyệt, bị từ chối hoặc đã hủy không in được).",
            "Xem lịch sử từng bước: ai làm gì, lúc nào, lý do."
        ],
        [
            "Lập phiếu: bấm Gửi Đề Nghị Chuyển Kho sau khi chọn kho xuất, nơi nhận và mặt hàng. Kho xuất hoặc nơi nhận phải là kho của bạn. Phiếu chờ duyệt chưa ảnh hưởng tồn kho; chỉ người lập sửa hoặc hủy được lúc này.",
            "Duyệt: người có quyền bấm Duyệt (hoặc Từ chối kèm lý do). Hệ thống kiểm tồn khả dụng ở kho xuất rồi giữ hàng.",
            "Xuất kho: kho xuất soạn hàng, nhập số seal và người vận chuyển rồi bấm Xuất kho. Bấm In phiếu xuất kho để in phiếu đi cùng hàng.",
            "Nhận hàng: kho nhận nhập số thực nhận từng dòng. Nhận đủ thì phiếu hoàn tất.",
            "Nhận lệch: bắt buộc ghi lý do và đính kèm ít nhất một tệp; Admin, Giám đốc hoặc Kế toán bấm Xác nhận đã xử lý để đóng phiếu.",
            "Cần dừng giữa chừng: Hủy phiếu (chưa xuất kho) hoặc Trả về kho xuất (đang vận chuyển, chỉ Admin, Giám đốc, Kế toán)."
        ],
        [
            new("Lập phiếu đề nghị chuyển kho", role => InventoryRoles.IsFinance(role)
                ? HelpGrant.Yes
                : HelpProbe.Role(role, InventoryAccess.CanOperate, "Kho xuất hoặc kho nhận là kho mình")),
            new("Duyệt hoặc từ chối phiếu chuyển", role => HelpProbe.Scope(
                role,
                DecideTransferCommandHandler.CanDecide,
                "Phiếu có kho xuất là kho mình")),
            new("Xuất kho", role => HelpProbe.Scope(
                role,
                (actor, location) => InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, location),
                "Phiếu có kho xuất là kho mình")),
            new("Nhận hàng chuyển kho", role => HelpProbe.Scope(
                role,
                (actor, location) => InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, location),
                "Phiếu có nơi nhận là kho mình")),
            new("Xử lý chênh lệch khi nhận; trả hàng đang vận chuyển", role => HelpProbe.Role(role, InventoryRoles.IsFinance)),
            new("In phiếu xuất kho", role => HelpProbe.Scope(
                role,
                (actor, location) => TransferAccess.CanPrint(actor, TransferStatus.Approved, location),
                "Chỉ phiếu có kho xuất là kho mình"))
        ]);

    private static HelpTopic Stocktakes { get; } = new(
        InventoryStocktakes,
        "Kiểm kê, điều chỉnh và hủy hàng",
        "Giữ tồn kho trên phần mềm khớp với hàng thực tế: đếm kho, điều chỉnh số lượng và hủy hàng hỏng, đều có lý do và chứng từ.",
        [
            "Lập biên bản kiểm kê: so số sổ sách với số đếm thực tế, ghi chênh lệch, lý do và chứng từ.",
            "Lập phiếu điều chỉnh số lượng (tăng hoặc giảm) khi cần sửa tồn.",
            "Lập phiếu hủy hàng cho hàng hỏng, hết hạn hoặc mất.",
            "Duyệt các phiếu chờ duyệt; đính kèm ảnh hoặc biên bản làm chứng từ.",
            "Phiếu điều chỉnh ghi nhận tự động khi nhận hàng chuyển kho bị lệch chỉ để lưu vết, không cộng trừ tồn thêm."
        ],
        [
            "Bấm + Lập Biên Bản Kiểm Kê (hoặc + Phiếu điều chỉnh, + Phiếu hủy), chọn kho và mặt hàng, nhập số đếm hoặc số lượng.",
            "Ghi lý do và số chứng từ hoặc đính kèm ảnh. Chênh lệch, điều chỉnh và hủy bắt buộc có lý do và chứng từ.",
            "Admin, Giám đốc, Kế toán ghi thẳng, không cần duyệt. Thủ kho và Trưởng Showroom/Kho lập phiếu và chờ duyệt.",
            "Người có quyền bấm Duyệt biên bản, Duyệt điều chỉnh hoặc Duyệt hủy. Chỉ khi duyệt, tồn kho mới thay đổi.",
            "Kiểm tra lại số tồn ở tab Tra cứu tồn kho sau khi duyệt."
        ],
        [
            new("Lập biên bản kiểm kê, điều chỉnh, phiếu hủy", role => InventoryRoles.IsFinance(role)
                ? HelpGrant.Scoped("Ghi thẳng, không cần duyệt")
                : HelpProbe.Role(role, InventoryAccess.CanOperate, "Lập và chờ duyệt, kho mình")),
            new("Duyệt kiểm kê và phiếu hủy", role => HelpProbe.Scope(
                role,
                (actor, location) => InventoryAccess.CanApprove(actor, EmployeeRole.WarehouseKeeper, location),
                "Chỉ phiếu của Thủ kho trong kho mình")),
            new("Duyệt phiếu điều chỉnh số lượng", role => HelpProbe.Role(role, InventoryAccess.CanApproveAdjustment))
        ]);

    private static HelpTopic PurchasingTopic { get; } = new(
        Purchasing,
        "Mua hàng và container nhập khẩu",
        "Lập đơn mua nhập khẩu theo container, tự phân bổ chi phí và thuế vào giá vốn từng sản phẩm rồi nhập kho khi hàng về.",
        [
            "Lập đơn mua nhiều mặt hàng trong cùng một container, kèm nhà cung cấp, kho nhận, số container, B/L, cảng, tỷ giá và VAT.",
            "Thêm các khoản chi phí (cước biển, THC, D/O, kéo xe...) và chọn cách chia xuống từng dòng hàng.",
            "Xem giá vốn tạm tính của từng dòng trước khi nhận hàng (chỉ Admin, Giám đốc, Kế toán).",
            "Gửi duyệt, duyệt, báo hàng đang về, nhận hàng một hoặc nhiều lần, hủy đơn khi chưa nhận.",
            "Đính kèm chứng từ: tờ khai, hóa đơn nhà cung cấp, vận đơn, packing list, hóa đơn logistics.",
            "Điều chỉnh chi phí sau khi đã nhận đủ (hóa đơn về muộn), có lý do và lịch sử.",
            "Cảnh báo khi số container hoặc B/L trùng với một đơn khác."
        ],
        [
            "Bấm nút tạo đơn mua hàng (dấu + ở đầu trang), nhập thông tin đơn, thêm mặt hàng và FOB (tổng USD của dòng), thuế suất từng dòng.",
            "Thêm dòng chi phí, chọn nhóm (điều chỉnh trị giá hải quan, logistics, thuế được khấu trừ, thuế không được khấu trừ) và cách chia (giá trị FOB, số lượng, cân nặng, thể tích, nhập tay).",
            "Bấm nút gửi duyệt, sau đó nút duyệt đơn. Đơn còn sửa được cho tới khi nhận hàng lần đầu; chưa nhận món nào thì hủy đơn được (bắt buộc lý do).",
            "Khi container lên đường bấm nút hàng đang về; đính kèm chứng từ khi có.",
            "Khi hàng về, kho nhận nhập số thực nhận từng dòng rồi bấm Nhập Kho & Cập Nhật Giá Vốn. Có thể nhận nhiều lần; lần cuối lệch số đặt phải ghi lý do.",
            "Hóa đơn về muộn sau khi nhận đủ: dùng nút ghi điều chỉnh với lý do và số tiền có dấu."
        ],
        [
            new("Vào trang Mua hàng", role => HelpProbe.Role(role, PurchaseAccess.CanOpenPage)),
            new("Lập, sửa, duyệt, hủy đơn; điều chỉnh chi phí; quản lý chứng từ", role => HelpProbe.Role(
                role,
                value => PurchaseAccess.CanManage(Sample(value, Guid.NewGuid())))),
            new("Xem FOB, thuế và giá vốn", role => HelpProbe.Role(
                role,
                value => PurchaseAccess.CanViewCost(Sample(value, Guid.NewGuid())))),
            new("Xem đơn đã duyệt trở đi", role => HelpProbe.Scope(
                role,
                (actor, location) => PurchaseAccess.CanView(actor, location, PurchaseStatus.Approved),
                "Chỉ đơn về kho mình")),
            new("Nhận hàng (Nhập Kho & Cập Nhật Giá Vốn)", role => HelpProbe.Scope(
                role,
                PurchaseAccess.CanReceive,
                "Chỉ đơn về kho mình"))
        ]);

    /// <summary>Phải khai báo sau các chủ đề ở trên: thuộc tính tĩnh được khởi tạo theo thứ tự khai báo.</summary>
    public static IReadOnlyList<HelpTopic> All { get; } = [Balances, Transfers, Stocktakes, PurchasingTopic];

    private static EmployeeActor Sample(string role, Guid location) => new(Guid.NewGuid(), role, location);
}
