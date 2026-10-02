using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using Resource_Management_System.Models;
using Resource_Management_System.Services;
using System.Data;
using System.Security.Claims;

namespace Resource_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbHelper _db;
        public AccountController(DbHelper db) => _db = db;

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var loginResult = _db.ExecuteQuery("sp_GetUserForLogin",
                new SqlParameter("@UserName", model.UserName.Trim()));

            // Keep this deliberately generic to avoid confirming valid usernames.
            if (loginResult.Rows.Count != 1)
            {
                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة.");
                return View(model);
            }

            var row = loginResult.Rows[0];
            bool isActive = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]);
            string passwordHash = row["PasswordHash"].ToString() ?? string.Empty;
            if (!isActive || string.IsNullOrWhiteSpace(passwordHash) || !PasswordHelper.Verify(model.Password, passwordHash))
            {
                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة.");
                return View(model);
            }

            bool mustChangePassword = row["MustChangePassword"] != DBNull.Value
                && Convert.ToBoolean(row["MustChangePassword"]);

            var authProperties = new AuthenticationProperties { IsPersistent = model.RememberMe };
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                CreatePrincipal(row),
                authProperties);

            return mustChangePassword
                ? RedirectToAction(nameof(ChangePassword))
                : RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangeInitialPasswordViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangeInitialPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var authenticationResult = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            string newPasswordHash = PasswordHelper.Hash(model.NewPassword);
            _db.ExecuteNonQuery("sp_ChangeOwnPassword",
                new SqlParameter("@UserID", userId),
                new SqlParameter("@NewPasswordHash", newPasswordHash));

            var sessionState = _db.ExecuteQuery("sp_GetUserSessionState",
                new SqlParameter("@UserID", userId));
            if (sessionState.Rows.Count == 0 || !Convert.ToBoolean(sessionState.Rows[0]["IsActive"]))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                CreatePrincipal(sessionState.Rows[0]),
                authenticationResult.Properties ?? new AuthenticationProperties());
            TempData["SuccessMessage"] = "تم تغيير كلمة المرور بنجاح.";
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();

        private static ClaimsPrincipal CreatePrincipal(DataRow row)
        {
            bool mustChangePassword = row["MustChangePassword"] != DBNull.Value
                && Convert.ToBoolean(row["MustChangePassword"]);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, row["UserName"].ToString() ?? ""),
                new(ClaimTypes.NameIdentifier, row["UserID"].ToString() ?? ""),
                new(ClaimTypes.Role, row["RoleName"].ToString() ?? ""),
                new("MustChangePassword", mustChangePassword.ToString()),
                new(AccountSessionClaims.AuthenticationVersion,
                    row["AuthenticationVersion"].ToString() ?? "")
            };

            if (row["DepartmentID"] != DBNull.Value)
                claims.Add(new Claim("DepartmentID", row["DepartmentID"].ToString() ?? ""));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }
    }
}
