namespace Pancras.Application.Common;

/// <summary>
/// Kết quả use case: thành công mang <see cref="Value"/>, thất bại mang <see cref="Error"/> là enum lỗi riêng của module
/// (giá trị 0 là <c>None</c>). <see cref="Issues"/> là lỗi theo trường của module đó (kiểu riêng như <c>SupplierIssue</c>).
/// </summary>
public sealed class Result<T, TError>
    where TError : struct, Enum
{
    private Result(bool isSuccess, T? value, TError error, string? detail, IReadOnlyList<object>? issues)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Detail = detail;
        Issues = issues ?? [];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public TError Error { get; }

    /// <summary>Chi tiết ngắn đi kèm lỗi (ví dụ mã SKU thiếu hàng). Là dữ liệu thô, trang tự ghép thành câu.</summary>
    public string? Detail { get; }

    public IReadOnlyList<object> Issues { get; }

    public IReadOnlyList<TIssue> IssuesOf<TIssue>() => Issues.OfType<TIssue>().ToArray();

    public static Result<T, TError> Ok(T value) => new(true, value, default, null, null);

    public static Result<T, TError> Fail(TError error) => new(false, default, error, null, null);

    public static Result<T, TError> Fail(TError error, string detail) => new(false, default, error, detail, null);

    public static Result<T, TError> Fail(TError error, IReadOnlyList<object> issues) => new(false, default, error, null, issues);
}
