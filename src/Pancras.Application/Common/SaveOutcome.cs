namespace Pancras.Application.Common;

/// <summary>Kết quả lưu của repository: lưu được, trùng khóa duy nhất (ví dụ mã), hoặc có người khác lưu chồng lên cùng bản ghi.</summary>
public enum SaveOutcome
{
    Saved = 0,
    Duplicate,
    Conflict
}
