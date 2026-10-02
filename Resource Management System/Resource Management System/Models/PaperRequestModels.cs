using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Resource_Management_System.Models
{
    public class RequestFilterViewModel
    {
        [Display(Name = "القسم")]
        public int? DepartmentID { get; set; }

        [Display(Name = "نوع الورق")]
        public int? PaperTypeID { get; set; }

        [Display(Name = "حالة الطلب")]
        public string? Status { get; set; }

        [Display(Name = "مرحلة الانتظار")]
        public string? PendingStages { get; set; }

        // Live counts for the three pending sub-stages. These are populated using
        // the rest of the active filter criteria so the badges always match the page scope.
        public int PendingITReviewCount { get; set; }
        public int PendingApproverOneCount { get; set; }
        public int PendingApproverTwoCount { get; set; }

        [Display(Name = "من تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [Display(Name = "إلى تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [Display(Name = "بحث")]
        public string? SearchKeyword { get; set; }

        [Display(Name = "تصنيف الجهة")]
        public string? DepartmentGroup { get; set; }

        public List<int>? SelectedDepartmentIDs { get; set; } = new();

        public IEnumerable<SelectListItem>? Departments { get; set; }
        public IEnumerable<SelectListItem>? PaperTypes { get; set; }
        public IEnumerable<SelectListItem>? Statuses { get; set; }
    }

    public class RequestItemViewModel
    {
        public int RequestID { get; set; }
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; } = 50;
        public int DepartmentUsedQuota { get; set; } = 0;
        public int RemainingQuota => Math.Max(0, QuotaLimit - DepartmentUsedQuota);
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? ITReviewNotes { get; set; }
        public string? RejectionNote { get; set; }
        public string? IssueNotes { get; set; }
        public bool IsException { get; set; }
        public string? ApproverOneNote { get; set; }
        public string? ApproverTwoNote { get; set; }
        public DateTime? ApproverOneApprovedDate { get; set; }
        public DateTime? ApproverTwoApprovedDate { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? ITReviewedDate { get; set; }
        public string? ITReviewedByName { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? RejectedDate { get; set; }
        public string? RejectedByName { get; set; }
        public DateTime? IssuedDate { get; set; }
        public string? IssuedByName { get; set; }
    }

    public class RequestQueueViewModel
    {
        public List<RequestItemViewModel> Requests { get; set; } = new();
        public HashSet<int> ActionableRequestIds { get; set; } = new();
        public bool IsITQueue { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class DepartmentItemViewModel
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
        public int UsedQuota { get; set; }
        public int RemainingQuota => Math.Max(0, QuotaLimit - UsedQuota);
    }

    public class ReceiveStockViewModel
    {
        [Required(ErrorMessage = "يرجى اختيار نوع الورق")]
        [Display(Name = "نوع الورق")]
        public int PaperTypeID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الكمية المضافة")]
        [Range(1, 100000, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        [Display(Name = "الكمية المضافة للمخزون")]
        public int QuantityReceived { get; set; }

        public IEnumerable<SelectListItem>? PaperTypes { get; set; }
    }

    public class InventoryStatusViewModel
    {
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int TotalReceived { get; set; }
        public int CurrentStock { get; set; }
        public DateTime LastUpdated { get; set; }
        public int LowStockThreshold { get; set; } = 50;
        public bool IsLowStock => CurrentStock <= LowStockThreshold;

        public int? ActiveContractID { get; set; }
        public string? ActiveContractNumber { get; set; }
        public string? ActiveContractName { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public int OpeningStockUnderActiveContract { get; set; }
        public int ReceivedUnderActiveContract { get; set; }
        public int IssuedUnderActiveContract { get; set; }
        public bool HasActiveContract => ActiveContractID.HasValue;
    }

    public class InventoryReceiptRow
    {
        public int ReceiptID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int QuantityReceived { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string ReceivedByName { get; set; } = string.Empty;
        public int? ContractID { get; set; }
        public string? ContractNumber { get; set; }
        public string? ContractName { get; set; }
        public string ContractDisplay => ContractID.HasValue
            ? (!string.IsNullOrWhiteSpace(ContractNumber) ? ContractNumber! : ContractName ?? $"#{ContractID.Value}")
            : "خارج فترة عقد";
    }

    public class StartInventoryContractViewModel
    {
        [Display(Name = "رقم العقد")]
        [StringLength(100)]
        public string? ContractNumber { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم العقد")]
        [Display(Name = "اسم العقد")]
        [StringLength(200)]
        public string ContractName { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى تحديد تاريخ بداية العقد")]
        [Display(Name = "تاريخ البداية")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ النهاية")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "كمية A4 (رزمة)")]
        [Range(0, int.MaxValue, ErrorMessage = "يرجى إدخال كمية صحيحة")]
        public int? QuantityA4 { get; set; }

        [Display(Name = "كمية A3 (رزمة)")]
        [Range(0, int.MaxValue, ErrorMessage = "يرجى إدخال كمية صحيحة")]
        public int? QuantityA3 { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class InventoryContractViewModel
    {
        public int ContractID { get; set; }
        public string? ContractNumber { get; set; }
        public string ContractName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? QuantityA4 { get; set; }
        public int? QuantityA3 { get; set; }
        public bool IsActive { get; set; }
        public bool IsScheduled { get; set; } // Registered, but its official start date has not taken effect.
        public string? Notes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string? ClosedByName { get; set; }
        public string? CloseNote { get; set; }
        public long OpeningStock { get; set; }
        public long ReceivedQuantity { get; set; }
        public long IssuedQuantity { get; set; }
        public long? ClosingStock { get; set; }
        public long CurrentPhysicalStock { get; set; }
        public InventoryContractTypeSummaryViewModel? A4Summary { get; set; }
        public InventoryContractTypeSummaryViewModel? A3Summary { get; set; }
        public long CurrentContractBalance => OpeningStock + ReceivedQuantity - IssuedQuantity;
        public string DisplayName => !string.IsNullOrWhiteSpace(ContractNumber)
            ? $"{ContractName} - {ContractNumber}"
            : ContractName;
    }

    public class InventoryContractBalanceViewModel
    {
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int OpeningStock { get; set; }
        public int ReceivedQuantity { get; set; }
        public int IssuedQuantity { get; set; }
        public int? ClosingStock { get; set; }
        public int CurrentPhysicalStock { get; set; }
        public int CalculatedContractBalance { get; set; }
    }

    public class InventoryContractTypeSummaryViewModel
    {
        public string Label { get; set; } = string.Empty;
        public long OpeningStock { get; set; }
        public long ReceivedQuantity { get; set; }
        public long IssuedQuantity { get; set; }
        public long? ClosingStock { get; set; }
        public long CurrentPhysicalStock { get; set; }
        public long BalanceDisplay => ClosingStock ?? (OpeningStock + ReceivedQuantity - IssuedQuantity);
    }

    public class InventoryContractsPageViewModel
    {
        public InventoryContractViewModel? ActiveContract { get; set; }
        public List<InventoryContractViewModel> Contracts { get; set; } = new();
        public List<InventoryContractBalanceViewModel> ActiveBalances { get; set; } = new();
        public StartInventoryContractViewModel NewContract { get; set; } = new();
    }

    public class InventoryMovementRow
    {
        public DateTime EventDate { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public int? PaperTypeID { get; set; }
        public string? PaperTypeName { get; set; }
        public long? QuantityChange { get; set; }
        public int? ContractID { get; set; }
        public string? ContractNumber { get; set; }
        public string? ContractName { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceID { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string? Details { get; set; }

        public string ContractDisplay => ContractID.HasValue
            ? (!string.IsNullOrWhiteSpace(ContractNumber) ? ContractNumber! : ContractName ?? $"#{ContractID.Value}")
            : "خارج فترة عقد";
    }

    public class InventoryYearlyLedgerRow
    {
        public int Year { get; set; }
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public long OpeningStock { get; set; }
        public long ReceivedDuringYear { get; set; }
        public long IssuedDuringYear { get; set; }
        public long AvailableDuringYear => OpeningStock + ReceivedDuringYear;
        public long ClosingStock { get; set; }
    }

    public class InventoryYearSummary
    {
        public int Year { get; set; }
        public long OpeningStock { get; set; }
        public long ReceivedDuringYear { get; set; }
        public long IssuedDuringYear { get; set; }
        public long AvailableDuringYear => OpeningStock + ReceivedDuringYear;
        public long ClosingStock { get; set; }
    }

    public class InventoryYearlyLedgerViewModel
    {
        public int SelectedYear { get; set; }
        public int CurrentYear { get; set; }
        public List<int> AvailableYears { get; set; } = new();
        public List<InventoryYearlyLedgerRow> Rows { get; set; } = new();
        public List<InventoryYearSummary> YearSummaries { get; set; } = new();

        public long OpeningStock => Rows.Sum(r => r.OpeningStock);
        public long ReceivedDuringYear => Rows.Sum(r => r.ReceivedDuringYear);
        public long IssuedDuringYear => Rows.Sum(r => r.IssuedDuringYear);
        public long AvailableDuringYear => OpeningStock + ReceivedDuringYear;
        public long ClosingStock => Rows.Sum(r => r.ClosingStock);
        public bool IsCurrentYear => SelectedYear == CurrentYear;
    }

    public class DepartmentConsumptionSummary
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public int A4Quantity { get; set; }
        public int A3Quantity { get; set; }
    }

    public class DepartmentQuotaSummaryViewModel
    {
        public int PaperTypeID { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
        public int UsedQuota { get; set; }
        public int RemainingQuota { get; set; }
        public int UsagePercent => QuotaLimit > 0
            ? Math.Min(100, (int)Math.Round(UsedQuota * 100d / QuotaLimit))
            : 0;
        public bool IsExhausted => RemainingQuota <= 0;
    }

    public class DepartmentHomeViewModel
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalRequestCount { get; set; }
        public int PendingRequestCount { get; set; }
        public int ReadyToIssueRequestCount { get; set; }
        public int IssuedRequestCount { get; set; }
        public int RejectedRequestCount { get; set; }
        public int IssuedThisMonthCount { get; set; }
        public int IssuedThisMonthQuantity { get; set; }
        public List<DepartmentQuotaSummaryViewModel> Quotas { get; set; } = new();
        public List<RequestItemViewModel> RecentRequests { get; set; } = new();
    }

    public class DashboardViewModel
    {
        // Current operational pipeline
        public int PendingITCount { get; set; }
        public int PendingApproverCount { get; set; }
        public int PendingApproverOneCount { get; set; }
        public int PendingApproverTwoCount { get; set; }
        public int PendingCount => PendingITCount + PendingApproverCount;
        public int ApprovedCount { get; set; }

        // System-wide snapshot (not affected by dashboard filters)
        public int TotalRequestCount { get; set; }
        public int IssuedRequestCount { get; set; }
        public int RejectedCount { get; set; }
        public int ExceptionRequestCount { get; set; }
        public int ExceptionIssuedQuantity { get; set; }
        public int TotalIssuedQuantity { get; set; }
        public int DepartmentCount { get; set; }
        public int LowStockCount { get; set; }
        public int TotalCurrentStock { get; set; }

        // Today / current-month activity
        public int IssuedTodayCount { get; set; }
        public int IssuedTodayQuantity { get; set; }
        public int IssuedMonthCount { get; set; }
        public int IssuedMonthQuantity { get; set; }

        // Current-year dashboard totals. These reset automatically with the calendar year,
        // while TotalIssuedQuantity and the group cumulative totals remain all-time figures.
        public int IssuedYearQuantity { get; set; }
        public int A4IssuedYearQuantity { get; set; }
        public int A3IssuedYearQuantity { get; set; }
        public int CompanyBranchesIssuedYearQuantity { get; set; }
        public int SectorsUnitsIssuedYearQuantity { get; set; }
        public int CompanyBranchesIssuedTotalQuantity { get; set; }
        public int SectorsUnitsIssuedTotalQuantity { get; set; }

        // Current analytical filter result
        public int FilteredRequestCount { get; set; }
        public int FilteredIssuedQuantity { get; set; }

        public RequestFilterViewModel Filter { get; set; } = new();
        public List<InventoryStatusViewModel> InventoryList { get; set; } = new();
        public List<RequestItemViewModel> RecentRequests { get; set; } = new();

        // Decision-analytics workspace. This is intentionally independent from the
        // operational/report filter above so management can explore a different scope
        // without disturbing the rest of the dashboard.
        public List<RequestItemViewModel> AnalyticsRequests { get; set; } = new();
        public List<RequestItemViewModel> AnalyticsCurrentRequests { get; set; } = new();
        public string AnalyticsScope { get; set; } = "all";
        public List<int> AnalyticsDepartmentIDs { get; set; } = new();
        public int? AnalyticsPaperTypeID { get; set; }
        public string AnalyticsPeriod { get; set; } = "12m";
        public DateTime? AnalyticsCustomFromDate { get; set; }
        public DateTime? AnalyticsCustomToDate { get; set; }
        public DateTime? AnalyticsEffectiveFromDate { get; set; }
        public DateTime? AnalyticsEffectiveToDate { get; set; }

        public List<DepartmentConsumptionSummary> DepartmentConsumption { get; set; } = new();
        public List<DepartmentConsumptionSummary> DepartmentConsumptionAll { get; set; } = new();

        // Current-year issued quantity by department for the full dashboard distribution chart.
        // Keeping this separate from DepartmentConsumptionAll prevents the chart from mixing
        // current-year figures with cumulative consumption.
        public List<DepartmentConsumptionSummary> DepartmentIssuedYear { get; set; } = new();
    }

    // Approver dashboards reuse the same read-only lookup/analytics components as the
    // IT/Admin dashboard. Inheriting DashboardViewModel keeps those shared components
    // strongly typed while the approver-specific action cards stay separate below.
    public class ApproverDashboardViewModel : DashboardViewModel
    {
        // Personal approval workspace
        public int ApprovalStage { get; set; }
        public string ApprovalStageName { get; set; } = string.Empty;
        public int PendingMyApprovalCount { get; set; }
        public int ExceptionPendingMyApprovalCount { get; set; }
        public int ApprovedTodayCount { get; set; }
        public int TotalApprovedCount { get; set; }

        // General system snapshot - read only for approvers
        public int SystemOpenRequestCount { get; set; }
        public int SystemPendingITCount { get; set; }
        public int SystemPendingApproverCount { get; set; }
        public int SystemReadyToIssueCount { get; set; }
        public int IssuedThisMonthQuantity { get; set; }

        public List<RequestItemViewModel> PendingRequests { get; set; } = new();
    }

    public class UsageReportRow
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
        public string PaperTypeName { get; set; } = string.Empty;
        public int TotalQuantityUsed { get; set; }
        public int CurrentStock { get; set; }
        public DateTime? LastIssueDate { get; set; }
    }

    public class DepartmentLatestIssueRow
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
        public DateTime? LastIssueDate { get; set; }
    }

    public class DepartmentNotificationViewModel
    {
        public int NotificationID { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class DepartmentTotalRow
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int QuotaLimit { get; set; }
        public int TotalIssuedInPeriod { get; set; }
    }

    public class PivotedDepartmentRow
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public Dictionary<string, int> QuantityByPaperType { get; set; } = new();
        public int RowTotal { get; set; }
    }

    public class UsageReportViewModel
    {
        public RequestFilterViewModel Filter { get; set; } = new();
        public List<UsageReportRow> UsageRows { get; set; } = new();
        public List<string> PaperTypeColumns { get; set; } = new();
        public List<PivotedDepartmentRow> PivotRows { get; set; } = new();
        public Dictionary<string, int> ColumnTotals { get; set; } = new();
        public int GrandTotal { get; set; }
    }

    public class CreatePaperTypeViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال اسم نوع الورق")]
        [StringLength(20, ErrorMessage = "الاسم طويل جداً (20 حرف كحد أقصى)")]
        public string PaperTypeName { get; set; } = string.Empty;

        [Range(0, 100000)]
        public int DefaultQuotaLimit { get; set; } = 0;
    }
}
