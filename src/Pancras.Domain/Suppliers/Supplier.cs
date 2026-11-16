namespace Pancras.Domain.Suppliers;

public sealed class Supplier : Pancras.Domain.Auditing.VersionedAuditStamped
{
    public const int CodeMaxLength = 32;

    public const int NameMaxLength = 200;

    public const int CountryMaxLength = 100;

    public const int ContactNameMaxLength = 200;

    public const int PhoneMaxLength = 30;

    public const int EmailMaxLength = 254;

    public const int AccountMaxLength = 64;

    public const int BankNameMaxLength = 200;

    public const int SwiftMaxLength = 11;

    private Supplier()
    {
        Code = string.Empty;
        NormalizedCode = string.Empty;
        Name = string.Empty;
        Country = string.Empty;
        ContactName = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        BankName = string.Empty;
        Account = string.Empty;
        SwiftCode = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public string Name { get; private set; }

    public string Country { get; private set; }

    public string ContactName { get; private set; }

    /// <summary>Tùy chọn. Rỗng nghĩa là chưa có.</summary>
    public string Phone { get; private set; }

    /// <summary>Tùy chọn. Rỗng nghĩa là chưa có.</summary>
    public string Email { get; private set; }

    /// <summary>Tên ngân hàng thụ hưởng. Tùy chọn.</summary>
    public string BankName { get; private set; }

    /// <summary>Số tài khoản hoặc IBAN. Tùy chọn.</summary>
    public string Account { get; private set; }

    /// <summary>Mã SWIFT/BIC (chữ hoa). Tùy chọn.</summary>
    public string SwiftCode { get; private set; }

    /// <summary>Ngừng hợp tác: ẩn khỏi danh sách chọn khi lập đơn mua nhưng vẫn giữ để xem lại lịch sử.</summary>
    public bool IsInactive { get; private set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static Supplier Create(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account,
        string bankName = "",
        string swiftCode = "")
    {
        var profile = ReadProfile(code, name, country, contactName, phone, email, account, bankName, swiftCode);
        var supplier = new Supplier { Id = Guid.CreateVersion7() };
        supplier.Apply(profile);
        return supplier;
    }

    public void Update(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account,
        string bankName = "",
        string swiftCode = "") =>
        Apply(ReadProfile(code, name, country, contactName, phone, email, account, bankName, swiftCode));

    public void Deactivate() => IsInactive = true;

    public void Reactivate() => IsInactive = false;

    private void Apply(Profile profile)
    {
        Code = profile.Code;
        NormalizedCode = profile.NormalizedCode;
        Name = profile.Name;
        Country = profile.Country;
        ContactName = profile.ContactName;
        Phone = profile.Phone;
        Email = profile.Email;
        BankName = profile.BankName;
        Account = profile.Account;
        SwiftCode = profile.SwiftCode;
    }

    private readonly record struct Profile(
        string Code,
        string NormalizedCode,
        string Name,
        string Country,
        string ContactName,
        string Phone,
        string Email,
        string BankName,
        string Account,
        string SwiftCode);

    private static Profile ReadProfile(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account,
        string bankName,
        string swiftCode)
    {
        var trimmedCode = Require(code, CodeMaxLength, nameof(code));
        var normalizedCode = NormalizeCode(trimmedCode);
        if (normalizedCode.Length is < 1 or > CodeMaxLength)
        {
            throw new ArgumentException("Code is too long.", nameof(code));
        }

        var trimmedEmail = Optional(email, EmailMaxLength, nameof(email));
        if (trimmedEmail.Length > 0 && !SupplierEmail.IsAddress(trimmedEmail))
        {
            throw new ArgumentException("Email is not an address.", nameof(email));
        }

        var trimmedPhone = Optional(phone, PhoneMaxLength, nameof(phone));
        if (trimmedPhone.Length > 0 && !SupplierPhone.IsNumber(trimmedPhone))
        {
            throw new ArgumentException("Phone is not a number.", nameof(phone));
        }

        var trimmedSwift = Optional(swiftCode, SwiftMaxLength, nameof(swiftCode));
        if (trimmedSwift.Length > 0 && !SupplierSwift.IsCode(trimmedSwift))
        {
            throw new ArgumentException("SWIFT code is not valid.", nameof(swiftCode));
        }

        return new Profile(
            trimmedCode,
            normalizedCode,
            Require(name, NameMaxLength, nameof(name)),
            Require(country, CountryMaxLength, nameof(country)),
            Require(contactName, ContactNameMaxLength, nameof(contactName)),
            trimmedPhone,
            trimmedEmail,
            Optional(bankName, BankNameMaxLength, nameof(bankName)),
            Optional(account, AccountMaxLength, nameof(account)),
            trimmedSwift.ToUpperInvariant());
    }

    private static string Require(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException("Value is too long.", paramName);
        }

        return trimmed;
    }

    private static string Optional(string? value, int maxLength, string paramName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException("Value is too long.", paramName);
        }

        return trimmed;
    }
}
