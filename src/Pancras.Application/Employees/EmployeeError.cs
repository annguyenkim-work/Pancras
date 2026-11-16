namespace Pancras.Application.Common;

public enum EmployeeError
{
    None = 0,
    ProfileIncomplete,
    InvalidRole,
    RoleRequired,
    NotFound,
    NameUnusable,
    CodeExhausted,
    Forbidden,
    LocationRequired,
    LocationNotFound,
    LeadExists,
    LastAdmin,
    WeakPassword,
    WrongPassword,
    InvalidCode,
    TwoFactorAlreadyEnabled,
    TwoFactorNotEnabled,
    VersionConflict
}
