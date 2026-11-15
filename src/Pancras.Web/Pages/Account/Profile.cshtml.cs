using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Web.Security;
using QRCoder;

namespace Pancras.Web.Pages.Account;

[Authorize]
public class ProfileModel(
    GetEmployeeHandler getEmployee,
    UpdateOwnProfileCommandHandler updateProfile,
    ChangeOwnPasswordCommandHandler changePassword,
    BeginTwoFactorSetupHandler beginTwoFactor,
    ConfirmTwoFactorSetupCommandHandler confirmTwoFactor,
    DisableTwoFactorCommandHandler disableTwoFactor,
    EmployeeSignIn signIn) : PageModel
{
    [BindProperty]
    public UpdateOwnProfileCommand Profile { get; set; } = new();

    [BindProperty]
    public ChangeOwnPasswordCommand Password { get; set; } = new();

    public string Code { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string JobTitle { get; private set; } = string.Empty;

    public bool TwoFactorEnabled { get; private set; }

    public int RecoveryCodesLeft { get; private set; }

    public TwoFactorSetup? Setup { get; private set; }

    public string? QrSvg { get; private set; }

    public IReadOnlyList<string> NewRecoveryCodes { get; private set; } = [];

    [BindProperty]
    public ConfirmTwoFactorCommand ConfirmCode { get; set; } = new();

    [BindProperty]
    public DisableTwoFactorCommand DisableCode { get; set; } = new();

    [BindProperty]
    public string? SetupSecretText { get; set; }

    public EmployeeError Error { get; private set; }

    public bool ProfileSaved { get; private set; }

    public bool PasswordChanged { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await LoadAsync(cancellationToken, loadForm: true) ? Page() : RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostProfileAsync(CancellationToken cancellationToken)
    {
        var result = await updateProfile.HandleAsync(Profile, User.ToActor(), cancellationToken);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            await LoadAsync(cancellationToken, loadForm: false);
            return Page();
        }

        await signIn.SignInAsync(result.Value!);
        TempData["ProfileSaved"] = "1";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken cancellationToken)
    {
        var result = await changePassword.HandleAsync(Password, User.ToActor(), cancellationToken);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            Password = new ChangeOwnPasswordCommand();
            await LoadAsync(cancellationToken, loadForm: true);
            return Page();
        }

        await signIn.SignInAsync(result.Value!);
        TempData["PasswordChanged"] = "1";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBeginTwoFactorAsync(CancellationToken cancellationToken)
    {
        var begin = await beginTwoFactor.HandleAsync(User.ToActor(), cancellationToken);
        await LoadAsync(cancellationToken, loadForm: true);
        if (begin.IsFailure)
        {
            Error = begin.EmployeeError;
            return Page();
        }

        ShowSetup(begin.Value!);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmTwoFactorAsync(CancellationToken cancellationToken)
    {
        var result = await confirmTwoFactor.HandleAsync(ConfirmCode, User.ToActor(), cancellationToken);
        await LoadAsync(cancellationToken, loadForm: true);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            if (!string.IsNullOrEmpty(ConfirmCode.ProtectedSecret) && !string.IsNullOrEmpty(SetupSecretText))
            {
                ShowSetup(new TwoFactorSetup(SetupSecretText, ConfirmCode.ProtectedSecret, Pancras.Domain.Employees.Totp.BuildUri(BeginTwoFactorSetupHandler.Issuer, Code, SetupSecretText)));
            }

            return Page();
        }

        await signIn.SignInAsync(result.Value!.Session);
        TwoFactorEnabled = true;
        RecoveryCodesLeft = result.Value.RecoveryCodes.Count;
        NewRecoveryCodes = result.Value.RecoveryCodes;
        return Page();
    }

    public async Task<IActionResult> OnPostDisableTwoFactorAsync(CancellationToken cancellationToken)
    {
        var result = await disableTwoFactor.HandleAsync(DisableCode, User.ToActor(), cancellationToken);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            await LoadAsync(cancellationToken, loadForm: true);
            return Page();
        }

        await signIn.SignInAsync(result.Value!);
        TempData["TwoFactorOff"] = "1";
        return RedirectToPage();
    }

    public bool TwoFactorOff => TempData.ContainsKey("TwoFactorOff");

    private void ShowSetup(TwoFactorSetup setup)
    {
        Setup = setup;
        SetupSecretText = setup.Secret;
        ConfirmCode.ProtectedSecret = setup.ProtectedSecret;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(setup.OtpAuthUri, QRCodeGenerator.ECCLevel.M);
        QrSvg = new SvgQRCode(data).GetGraphic(5, "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute); // không gán width/height cố định: mã tự co theo khung w-44, không tràn sang phần hướng dẫn
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken, bool loadForm)
    {
        ProfileSaved = TempData.ContainsKey("ProfileSaved");
        PasswordChanged = TempData.ContainsKey("PasswordChanged");
        var result = await getEmployee.HandleAsync(User.ToActor().Id, User.ToActor(), cancellationToken);
        if (result.IsFailure)
        {
            return false;
        }

        var profile = result.Value!;
        Code = profile.Code;
        Role = profile.Role;
        JobTitle = profile.JobTitle;
        TwoFactorEnabled = profile.TwoFactorEnabled;
        RecoveryCodesLeft = profile.RecoveryCodesLeft;
        if (loadForm)
        {
            Profile = new UpdateOwnProfileCommand
            {
                FullName = profile.FullName,
                Email = profile.Email,
                Phone = profile.Phone,
                Address = profile.Address
            };
        }

        return true;
    }
}
