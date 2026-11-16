namespace Pancras.Application.Common;

/// <summary>Cỡ trang chuẩn của mọi danh sách: 20 (mặc định) hoặc 50 dòng/trang. Giá trị khác bị đưa về mặc định.</summary>
public static class PageSizes
{
    public const int Default = 20;

    public static IReadOnlyList<int> Allowed { get; } = [20, 50];

    public static int Normalize(int? size) => size is 20 or 50 ? size.Value : Default;
}
