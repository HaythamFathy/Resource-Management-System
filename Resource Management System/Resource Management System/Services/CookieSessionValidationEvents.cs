using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Data;
using System.Security.Claims;

namespace Resource_Management_System.Services;

public static class AccountSessionClaims
{
    public const string AuthenticationVersion = "AuthenticationVersion";
}

public sealed class CookieSessionValidationEvents : CookieAuthenticationEvents
{
    private readonly DbHelper _db;

    public CookieSessionValidationEvents(DbHelper db)
    {
        _db = db;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        string? userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? versionValue = principal?.FindFirstValue(AccountSessionClaims.AuthenticationVersion);

        if (!int.TryParse(userIdValue, out int userId)
            || !long.TryParse(versionValue, out long cookieVersion))
        {
            await RejectSessionAsync(context);
            return;
        }

        var sessionState = _db.ExecuteQuery("sp_GetUserSessionState",
            new SqlParameter("@UserID", userId));

        if (sessionState.Rows.Count == 0)
        {
            await RejectSessionAsync(context);
            return;
        }

        var row = sessionState.Rows[0];
        bool isActive = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]);
        long currentVersion = Convert.ToInt64(row["AuthenticationVersion"]);
        string currentUserName = row["UserName"].ToString() ?? "";
        string currentRole = row["RoleName"].ToString() ?? "";
        string currentDepartment = row["DepartmentID"] == DBNull.Value
            ? ""
            : row["DepartmentID"].ToString() ?? "";
        bool currentMustChangePassword = row["MustChangePassword"] != DBNull.Value
            && Convert.ToBoolean(row["MustChangePassword"]);

        string cookieUserName = principal?.FindFirstValue(ClaimTypes.Name) ?? "";
        string cookieRole = principal?.FindFirstValue(ClaimTypes.Role) ?? "";
        string cookieDepartment = principal?.FindFirstValue("DepartmentID") ?? "";
        bool cookieMustChangePassword = bool.TryParse(
            principal?.FindFirstValue("MustChangePassword"),
            out bool parsedMustChangePassword) && parsedMustChangePassword;

        if (!isActive
            || currentVersion != cookieVersion
            || !string.Equals(currentUserName, cookieUserName, StringComparison.Ordinal)
            || !string.Equals(currentRole, cookieRole, StringComparison.Ordinal)
            || !string.Equals(currentDepartment, cookieDepartment, StringComparison.Ordinal)
            || currentMustChangePassword != cookieMustChangePassword)
        {
            await RejectSessionAsync(context);
        }
    }

    private static async Task RejectSessionAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
