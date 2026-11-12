using Pancras.Application.Common;

namespace Pancras.Web.Pages.Suppliers;

public static class SupplierErrorMessages
{
    public static IReadOnlyList<string> Describe(SupplierError error, IReadOnlyList<SupplierIssue> issues, string? code)
    {
        if (error == SupplierError.NotFound)
        {
            return ["Không tìm thấy nhà cung cấp này. Có thể nhà cung cấp đã bị xóa."];
        }

        var messages = issues.Select(issue => Sentence(issue, code)).ToArray();
        if (messages.Length > 0)
        {
            return messages;
        }

        return error switch
        {
            SupplierError.Incomplete => ["Cần nhập đủ mã, tên, quốc gia, người liên hệ, điện thoại, email và tài khoản."],
            SupplierError.TooLong => ["Một mục dài hơn mức cho phép."],
            SupplierError.InvalidEmail => ["Email không đúng dạng địa chỉ thư."],
            SupplierError.InvalidPhone => ["Điện thoại không đúng định dạng."],
            SupplierError.DuplicateCode => ["Mã nhà cung cấp đã có trong danh bạ."],
            _ => []
        };
    }

    private static string Sentence(SupplierIssue issue, string? code)
    {
        var label = Label(issue.Field);
        return issue.Kind switch
        {
            SupplierIssueKind.Missing => $"{label} chưa được nhập.",
            SupplierIssueKind.TooLong => $"{label} chỉ được tối đa {issue.Limit} ký tự.",
            SupplierIssueKind.InvalidEmail =>
                "Email không đúng dạng địa chỉ thư. Cần đúng một dấu @, không có khoảng trắng, và tên miền phải có dấu chấm. Ví dụ: ten@congty.com.",
            SupplierIssueKind.InvalidPhone =>
                "Điện thoại không đúng định dạng. Cần 8–15 chữ số, có thể bắt đầu bằng 0 hoặc +. Ví dụ: 0901234567, +84901234567.",
            SupplierIssueKind.Duplicate =>
                $"Mã nhà cung cấp \"{code?.Trim()}\" đã có trong danh bạ. Mã không phân biệt chữ hoa và chữ thường. Hãy nhập mã khác.",
            _ => $"{label} không hợp lệ."
        };
    }

    private static string Label(SupplierField field) => field switch
    {
        SupplierField.Code => "Mã nhà cung cấp",
        SupplierField.Name => "Tên nhà cung cấp",
        SupplierField.Country => "Quốc gia / khu vực",
        SupplierField.ContactName => "Người liên hệ",
        SupplierField.Phone => "Điện thoại",
        SupplierField.Email => "Email",
        SupplierField.Account => "Tài khoản",
        _ => "Mục"
    };
}
