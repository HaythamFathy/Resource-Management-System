using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Resource_Management_System.Models
{
    public class UserListItemViewModel
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DepartmentAdminViewModel
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public List<DepartmentPaperQuotaViewModel> PaperQuotas { get; set; } = new();
    }

    public class DepartmentPaperQuotaViewModel
    {
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
    }

    public class AdminUsersViewModel
    {
        public List<UserListItemViewModel> Users { get; set; } = new();
        public List<DepartmentAdminViewModel> Departments { get; set; } = new();
    }

    public class CreateDepartmentViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال اسم القسم")]
        [Display(Name = "اسم القسم")]
        public string DepartmentName { get; set; } = string.Empty;

        [Range(1, 100000, ErrorMessage = "يجب أن تكون الحصة أكبر من صفر")]
        [Display(Name = "الحصة الشهرية (رزمة)")]
        public int QuotaLimit { get; set; } = 50;
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال اسم المستخدم")]
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى إدخال كلمة المرور")]
        [MinLength(6, ErrorMessage = "يجب أن تكون كلمة المرور 6 أحرف على الأقل")]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "القسم")]
        public int? DepartmentID { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الصلاحية")]
        [Display(Name = "الصلاحية")]
        public int? RoleID { get; set; }

        public IEnumerable<SelectListItem>? Departments { get; set; }

        public IEnumerable<SelectListItem>? Roles { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public int UserID { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "يجب أن تكون كلمة المرور 6 أحرف على الأقل")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
