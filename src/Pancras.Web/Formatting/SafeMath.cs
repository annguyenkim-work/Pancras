namespace Pancras.Web.Formatting;

/// <summary>Phép nhân tiền không ném <see cref="OverflowException"/>: tràn thì trả giá trị tối đa để handler từ chối bằng lỗi "vượt giới hạn".</summary>
public static class SafeMath
{
    public static decimal MultiplyOrMax(decimal amount, int quantity)
    {
        if (quantity == 0 || amount == 0)
        {
            return 0;
        }

        var limit = decimal.MaxValue / Math.Abs((decimal)quantity);
        return Math.Abs(amount) > limit ? decimal.MaxValue : amount * quantity;
    }
}
