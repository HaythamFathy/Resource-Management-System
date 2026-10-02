using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Resource_Management_System.Models
{
    public class NewRequestViewModel
    {
        [Required(ErrorMessage = "يرجى اختيار نوع الورق")]
        [Display(Name = "نوع الورق")]
        public int PaperTypeID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الكمية المطلوبة")]
        [Range(1, 100000, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        [Display(Name = "الكمية المطلوبة")]
        public int RequestedQuantity { get; set; }
        public int QuotaLimit { get; set; }

        public int UsedQuota { get; set; }

        public int RemainingQuota =>
            Math.Max(0, QuotaLimit - UsedQuota);

        [Display(Name = "ملاحظات")]
        [StringLength(500, ErrorMessage = "الملاحظات يجب ألا تتجاوز 500 حرف")]
        public string? Notes { get; set; }
        public string? ITReviewNotes { get; set; }

        public Guid SubmissionKey { get; set; } = Guid.NewGuid();

        // Only used when an IT/Admin user submits on behalf of a department
        [Display(Name = "القسم المستفيد")]
        public int? DepartmentID { get; set; }

        public IEnumerable<SelectListItem>? PaperTypes { get; set; }
        public IEnumerable<SelectListItem>? Departments { get; set; }
    }
}