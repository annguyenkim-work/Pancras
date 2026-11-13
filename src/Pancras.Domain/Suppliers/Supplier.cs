namespace Pancras.Domain.Suppliers;

public sealed class Supplier : Pancras.Domain.Auditing.AuditStamped
{
    public const int CodeMaxLength = 32;

    public const int NameMaxLength = 200;

    public const int CountryMaxLength = 100;

    public const int ContactNameMaxLength = 200;

    public const int PhoneMaxLength = 30;

    public const int EmailMaxLength = 254;

    public const int AccountMaxLength = 64;

    private Supplier()
    {
        Code = string.Empty;
        NormalizedCode = string.Empty;
        Name = string.Empty;
        Country = string.Empty;
        ContactName = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Account = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public string Name { get; private set; }

    public string Country { get; private set; }

    public string ContactName { get; private set; }

    public string Phone { get; private set; }

    public string Email { get; private set; }

    public string Account { get; private set; }

    public bool IsDeleted { get; private set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static Supplier Create(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account)
    {
        var profile = ReadProfile(code, name, country, contactName, phone, email, account);
        return new Supplier
        {
            Id = Guid.CreateVersion7(),
            Code = profile.Code,
            NormalizedCode = profile.NormalizedCode,
            Name = profile.Name,
            Country = profile.Country,
            ContactName = profile.ContactName,
            Phone = profile.Phone,
            Email = profile.Email,
            Account = profile.Account
        };
    }

    public void Update(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account)
    {
        var profile = ReadProfile(code, name, country, contactName, phone, email, account);
        Code = profile.Code;
        NormalizedCode = profile.NormalizedCode;
        Name = profile.Name;
        Country = profile.Country;
        ContactName = profile.ContactName;
        Phone = profile.Phone;
        Email = profile.Email;
        Account = profile.Account;
    }

    public void MarkDeleted() => IsDeleted = true;

    private readonly record struct Profile(
        string Code,
        string NormalizedCode,
        string Name,
        string Country,
        string ContactName,
        string Phone,
        string Email,
        string Account);

    private static Profile ReadProfile(
        string code,
        string name,
        string country,
        string contactName,
        string phone,
        string email,
        string account)
    {
        var trimmedCode = Require(code, CodeMaxLength, nameof(code));
        var normalizedCode = NormalizeCode(trimmedCode);
        if (normalizedCode.Length is < 1 or > CodeMaxLength)
        {
            throw new ArgumentException("Code is too long.", nameof(code));
        }

        var trimmedEmail = Require(email, EmailMaxLength, nameof(email));
        if (!SupplierEmail.IsAddress(trimmedEmail))
        {
            throw new ArgumentException("Email is not an address.", nameof(email));
        }

        var trimmedPhone = Require(phone, PhoneMaxLength, nameof(phone));
        if (!SupplierPhone.IsNumber(trimmedPhone))
        {
            throw new ArgumentException("Phone is not a number.", nameof(phone));
        }

        return new Profile(
            trimmedCode,
            normalizedCode,
            Require(name, NameMaxLength, nameof(name)),
            Require(country, CountryMaxLength, nameof(country)),
            Require(contactName, ContactNameMaxLength, nameof(contactName)),
            trimmedPhone,
            trimmedEmail,
            Require(account, AccountMaxLength, nameof(account)));
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
}
