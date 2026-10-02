using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using Resource_Management_System.Models;
using System.Data;

namespace Resource_Management_System.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UserManagementController : Controller
    {
        private readonly DbHelper _db;
        public UserManagementController(DbHelper db) => _db = db;

        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery("sp_GetAllUsersWithDetails");
            var list = new List<UserListItemViewModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new UserListItemViewModel
                {
                    UserID = Convert.ToInt32(row["UserID"]),
                    UserName = row["UserName"].ToString() ?? "",
                    RoleName = row["RoleName"].ToString() ?? "",
                    DepartmentName = row["DepartmentName"] != DBNull.Value ? row["DepartmentName"].ToString() : null,
                    IsActive = Convert.ToBoolean(row["IsActive"])
                });
            }
            return View(list);
        }

        [HttpGet]
        public IActionResult Create(int? departmentId, string? fromDepartment = null)
        {
            bool isFromDepartment = string.Equals(fromDepartment, "true", StringComparison.OrdinalIgnoreCase);
            var model = new CreateUserViewModel
            {
                DepartmentID = departmentId,
                Departments = GetDepartmentsSelectList(),
                Roles = GetRolesSelectList()
            };

            if (isFromDepartment)
            {
                model.RoleID = GetRolesSelectList()
                    .First(r => r.Text == Roles.Department)
                    .Value is string id ? int.Parse(id) : null;
            }

            ViewBag.FromDepartment = isFromDepartment;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateUserViewModel model, string? fromDepartment = null)
        {
            bool isFromDepartment = string.Equals(fromDepartment, "true", StringComparison.OrdinalIgnoreCase);

            var selectedRole = GetRolesSelectList().FirstOrDefault(r => r.Value == model.RoleID?.ToString());
            bool isDepartmentRole = selectedRole != null &&
                string.Equals(selectedRole.Text, Roles.Department, StringComparison.OrdinalIgnoreCase);

            if (isDepartmentRole && model.DepartmentID is null)
            {
                ModelState.AddModelError(nameof(model.DepartmentID), "يرجى اختيار القسم لهذا النوع من الحسابات.");
            }

            if (!ModelState.IsValid)
            {
                model.Departments = GetDepartmentsSelectList();
                model.Roles = GetRolesSelectList();
                ViewBag.FromDepartment = isFromDepartment;
                return View(model);
            }

            try
            {
                string hashedPassword = PasswordHelper.Hash(model.Password);

                // Non-department roles (Admin/IT/Approver) never carry a DepartmentID,
                // even if one somehow arrived on the form.
                int? departmentIdToSave = isDepartmentRole ? model.DepartmentID : null;

                _db.ExecuteNonQuery("sp_CreateUser",
                    new SqlParameter("@UserName", model.UserName),
                    new SqlParameter("@PasswordHash", hashedPassword),
                    new SqlParameter("@RoleID", model.RoleID!.Value),
                    new SqlParameter("@DepartmentID", (object?)departmentIdToSave ?? DBNull.Value));

                TempData["SuccessMessage"] = "تمت إضافة المستخدم بنجاح.";

                if (isFromDepartment)
                {
                    return RedirectToAction("Index", "DepartmentManagement", new { area = "PaperManagement" });
                }

                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                if (ex.Message.Contains("UQ_Users_OneUserPerDepartment"))
                    ModelState.AddModelError(nameof(model.DepartmentID), "هذا القسم لديه مستخدم بالفعل، لا يمكن إضافة أكثر من مستخدم واحد لكل قسم.");
                else
                    ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم هذا مستخدم بالفعل، الرجاء اختيار اسم آخر.");

                model.Departments = GetDepartmentsSelectList();
                model.Roles = GetRolesSelectList();
                ViewBag.FromDepartment = isFromDepartment;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DisableUser(int userId)
        {
            try
            {
                _db.ExecuteNonQuery(
                    "sp_DeactivateUser",
                    new SqlParameter("@UserID", userId));

                TempData["SuccessMessage"] = "تم تعطيل الحساب بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر تعطيل الحساب. يرجى المحاولة لاحقاً.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReactivateUser(int userId)
        {
            try
            {
                _db.ExecuteNonQuery(
                    "sp_ReactivateUser",
                    new SqlParameter("@UserID", userId));

                TempData["SuccessMessage"] = "تم تفعيل الحساب بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر تفعيل الحساب. يرجى المحاولة لاحقاً.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int userId)
        {
            try
            {
                _db.ExecuteNonQuery("sp_DeleteUser", new SqlParameter("@UserID", userId));
                TempData["SuccessMessage"] = "تم حذف الحساب.";
            }
            catch (Exception) { TempData["ErrorMessage"] = "تعذر حذف الحساب. يرجى المحاولة لاحقاً."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.";
                return RedirectToAction(nameof(Index));
            }

            string hashedPassword = PasswordHelper.Hash(model.NewPassword);
            _db.ExecuteNonQuery("sp_ResetUserPassword",
                new SqlParameter("@UserID", model.UserID),
                new SqlParameter("@NewPasswordHash", hashedPassword));

            TempData["SuccessMessage"] = "تم تحديث كلمة المرور بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private IEnumerable<SelectListItem> GetRolesSelectList()
        {
            var dt = _db.ExecuteQuery("sp_GetRoles");
            var list = new List<SelectListItem>();
            foreach (DataRow row in dt.Rows)
                list.Add(new SelectListItem { Value = row["RoleID"].ToString(), Text = row["RoleName"].ToString() });
            return list;
        }

        private IEnumerable<SelectListItem> GetDepartmentsSelectList()
        {
            var dt = _db.ExecuteQuery("sp_GetDepartments");
            var list = new List<SelectListItem>();
            foreach (DataRow row in dt.Rows)
                list.Add(new SelectListItem { Value = row["DepartmentID"].ToString(), Text = row["DepartmentName"].ToString() });
            return list;
        }
    }
}
