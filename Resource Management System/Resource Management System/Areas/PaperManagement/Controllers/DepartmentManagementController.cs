using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using Resource_Management_System.Models;
using System.Data;

namespace Resource_Management_System.Areas.PaperManagement.Controllers
{
    [Area("PaperManagement")]
    [Authorize(Roles = Roles.Admin)]
    public class DepartmentManagementController : Controller
    {
        private readonly DbHelper _db;
        public DepartmentManagementController(DbHelper db) => _db = db;

        [HttpGet]
        public IActionResult Index()
        {
            var departments = _db.ExecuteQuery("sp_GetDepartmentPaperQuotas").AsEnumerable()
                .GroupBy(row => new { DepartmentID = Convert.ToInt32(row["DepartmentID"]), DepartmentName = row["DepartmentName"].ToString() ?? string.Empty })
                .Select(group => new DepartmentAdminViewModel
                {
                    DepartmentID = group.Key.DepartmentID,
                    DepartmentName = group.Key.DepartmentName,
                    PaperQuotas = group.Select(row => new DepartmentPaperQuotaViewModel
                    {
                        PaperTypeID = Convert.ToInt32(row["PaperTypeID"]),
                        PaperTypeName = row["PaperTypeName"].ToString() ?? string.Empty,
                        QuotaLimit = Convert.ToInt32(row["QuotaLimit"])
                    }).ToList()
                }).OrderBy(d => d.DepartmentName).ToList();

            try
            {
                var approverUsers = _db.ExecuteQuery("sp_GetApproverUsers");
                ViewBag.ApproverUsers = approverUsers.AsEnumerable().Select(row => new SelectListItem
                {
                    Value = row["UserID"].ToString(),
                    Text = row["UserName"].ToString()
                }).ToList();

                var assignments = _db.ExecuteQuery("sp_GetFixedRequestApprovers");
                ViewBag.FixedApprovers = assignments.AsEnumerable().ToDictionary(
                    row => Convert.ToInt32(row["ApprovalStage"]),
                    row => Convert.ToInt32(row["UserID"]));
            }
            catch (SqlException)
            {
                ViewBag.ApproverUsers = new List<SelectListItem>();
                ViewBag.FixedApprovers = new Dictionary<int, int>();
            }

            return View(departments);
        }

        [HttpGet]
        public IActionResult CreateDepartment() => View(new CreateDepartmentViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateDepartment(CreateDepartmentViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var newIdParam = new SqlParameter("@NewDepartmentID", SqlDbType.Int) { Direction = ParameterDirection.Output };
                _db.ExecuteNonQuery("sp_CreateDepartment",
                    new SqlParameter("@DepartmentName", model.DepartmentName),
                    new SqlParameter("@QuotaLimit", model.QuotaLimit),
                    newIdParam);

                int newDepartmentId = Convert.ToInt32(newIdParam.Value);
                TempData["SuccessMessage"] = "تمت إضافة القسم. أضف الآن مستخدم القسم.";
                return RedirectToAction("Create", "UserManagement", new
                {
                    area = "",
                    departmentId = newDepartmentId,
                    fromDepartment = true
                });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "تعذر إضافة القسم. يرجى المحاولة لاحقاً.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateDepartmentQuota(int departmentId, int paperTypeId, int quotaLimit)
        {
            try
            {
                _db.ExecuteNonQuery("sp_UpdateDepartmentPaperQuota",
                    new SqlParameter("@DepartmentID", departmentId),
                    new SqlParameter("@PaperTypeID", paperTypeId),
                    new SqlParameter("@QuotaLimit", quotaLimit));
                TempData["SuccessMessage"] = "تم تحديث الحصة الشهرية لنوع الورق.";
            }
            catch (Exception) { TempData["ErrorMessage"] = "تعذر تحديث الحصة. يرجى المحاولة لاحقاً."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteDepartment(int departmentId)
        {
            try
            {
                _db.ExecuteNonQuery("sp_DeleteDepartment", new SqlParameter("@DepartmentID", departmentId));
                TempData["SuccessMessage"] = "تم حذف القسم.";
            }
            catch (Exception) { TempData["ErrorMessage"] = "تعذر حذف القسم. يرجى المحاولة لاحقاً."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetFixedApprover(int approvalStage, int userId)
        {
            try
            {
                _db.ExecuteNonQuery("sp_SetFixedRequestApprover",
                    new SqlParameter("@ApprovalStage", approvalStage),
                    new SqlParameter("@UserID", userId));
                TempData["SuccessMessage"] = "تم حفظ تعيين المعتمد لمرحلة الاعتماد.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر حفظ تعيين المعتمد. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
