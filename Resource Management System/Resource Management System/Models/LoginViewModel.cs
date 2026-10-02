using System.ComponentModel.DataAnnotations;

namespace Resource_Management_System.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال اسم المستخدم")]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى إدخال كلمة المرور")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "تذكرني")]
        public bool RememberMe { get; set; }
    }

    public class ChangeInitialPasswordViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال كلمة مرور جديدة")]
        [MinLength(6, ErrorMessage = "يجب أن تكون كلمة المرور 6 أحرف على الأقل")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الجديدة")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى تأكيد كلمة المرور")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين")]
        [Display(Name = "تأكيد كلمة المرور")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
