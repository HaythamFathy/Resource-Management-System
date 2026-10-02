using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resource_Management_System.Common;

namespace Resource_Management_System.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Department users only have access to their own paper-management work.
            // Sending them straight to that workspace removes an unnecessary choice
            // while preserving the multi-system portal for Admin/IT/other roles.
            if (User.IsInRole(Roles.Department))
            {
                return RedirectToAction(
                    "DepartmentHome",
                    "Request",
                    new { area = "PaperManagement" });
            }

            return View();
        }
    }
}
