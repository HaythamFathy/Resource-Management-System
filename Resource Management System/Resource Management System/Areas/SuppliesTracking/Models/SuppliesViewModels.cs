using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Resource_Management_System.Areas.SuppliesTracking.Models
{
    #region ViewModels

    /// <summary>
    /// Dashboard KPIs, filtering options, and master catalog item list
    /// </summary>
    public class SuppliesIndexViewModel
    {
        // KPI Summary Cards
        public int TotalItemsCount { get; set; }
        public int ConsumablesStockCount { get; set; }
        public int DevicesInStockCount { get; set; }
        public int DevicesIssuedCount { get; set; }
        public int DevicesUnderMaintenanceCount { get; set; }
        public int LowStockItemsCount { get; set; }

        // Filters
        [Display(Name = "التصنيف")]
        public int? SelectedCategoryID { get; set; }

        [Display(Name = "نوع التتبع")]
        public string? SelectedTrackType { get; set; } // "all", "consumable", "device", "lowstock"

        [Display(Name = "بحث")]
        public string? SearchKeyword { get; set; }

        // Dropdown Lists
        public List<SelectListItem> Categories { get; set; } = new();

        // Catalog List
        public List<SupplyItem> Items { get; set; } = new();

        // Recent Movements Preview
        public List<SupplyTransaction> RecentTransactions { get; set; } = new();
    }

    /// <summary>
    /// Form ViewModel for receiving inbound stock (Consumables by quantity, Devices with auto-barcodes)
    /// </summary>
    public class ReceiveViewModel
    {
        [Required(ErrorMessage = "يرجى اختيار الصنف.")]
        [Display(Name = "الصنف")]
        public int ItemID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الكمية الواردة.")]
        [Range(1, 10000, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل.")]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "اسم المورد / رقم الفاتورة أو إذن التوريد")]
        [MaxLength(150)]
        public string? SupplierOrReference { get; set; }

        [Display(Name = "ملاحظات التوريد")]
        public string? Notes { get; set; }

        /// <summary>
        /// Comma- or newline-separated optional manufacturer serial numbers for serialized devices
        /// </summary>
        [Display(Name = "الأرقام التسلسلية للأجهزة (اختياري - مفصولة بفواصل أو أسطر)")]
        public string? SerialNumbersText { get; set; }

        // Contextual Display & Dropdown helpers
        public bool TrackType { get; set; } // 0: Consumable, 1: Device
        public string? SelectedItemName { get; set; }
        public List<SelectListItem> AvailableItems { get; set; } = new();
    }

    /// <summary>
    /// Form ViewModel for executing inventory movements (Issue, Return, Maintenance, Scrap)
    /// </summary>
    public class MovementViewModel
    {
        [Required(ErrorMessage = "يرجى تحديد نوع الحركة.")]
        [Display(Name = "نوع الحركة")]
        public string MovementType { get; set; } = "Issue"; // Issue, Return, Maintenance, Scrap

        [Required(ErrorMessage = "يرجى اختيار الصنف.")]
        [Display(Name = "الصنف")]
        public int ItemID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الكمية.")]
        [Range(1, 10000, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل.")]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; } = 1;

        // Department Target (Required for Issue, or Return from Department)
        [Display(Name = "القسم / الجهة المستفيدة")]
        public int? DepartmentID { get; set; }

        [Display(Name = "اسم الموظف المستلم")]
        [MaxLength(150)]
        public string? RecipientName { get; set; }

        // Required when item is ink/toner/drum and requires printer model
        [Display(Name = "موديل الطابعة المستهدفة")]
        [MaxLength(100)]
        public string? PrinterModel { get; set; }

        [Display(Name = "رقم المرجع / إذن الحركة")]
        [MaxLength(150)]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "ملاحظات الحركة")]
        public string? Notes { get; set; }

        /// <summary>
        /// Specific selected UnitIDs for Serialized Devices (when issuing, returning, or sending to maintenance)
        /// </summary>
        [Display(Name = "الأجهزة المحددة (بالباركود)")]
        public List<int> SelectedUnitIDs { get; set; } = new();

        // Dropdown Lists
        public List<SelectListItem> AvailableItems { get; set; } = new();
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SupplyDeviceUnit> AvailableDeviceUnits { get; set; } = new();
    }

    /// <summary>
    /// ViewModel for printing generated barcode labels for serialized device units
    /// </summary>
    public class PrintBarcodesViewModel
    {
        public int TransactionID { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string? BrandModel { get; set; }
        public DateTime ReceiveDate { get; set; }
        public List<SupplyDeviceUnit> Units { get; set; } = new();
    }

    /// <summary>
    /// ViewModel for printing official handover receipt voucher
    /// </summary>
    public class PrintVoucherViewModel
    {
        public int TransactionID { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string TransactionTypeArabic { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string? BrandModel { get; set; }
        public int Quantity { get; set; }
        public string UnitName { get; set; } = "قطعة";
        public string? DepartmentName { get; set; }
        public string? RecipientName { get; set; }
        public string? PrinterModel { get; set; }
        public string? SupplierOrReference { get; set; }
        public string? PerformedByName { get; set; }
        public string? Notes { get; set; }
        public List<SupplyDeviceUnit> Units { get; set; } = new();
    }

    /// <summary>
    /// ViewModel for the Reports and Movement History screen
    /// </summary>
    public class SuppliesReportsViewModel
    {
        [Display(Name = "من تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [Display(Name = "إلى تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [Display(Name = "القسم")]
        public int? DepartmentID { get; set; }

        [Display(Name = "الصنف")]
        public int? ItemID { get; set; }

        [Display(Name = "نوع الحركة")]
        public string? MovementType { get; set; }

        [Display(Name = "بحث بالباركود أو رقم الإذن")]
        public string? SearchKeyword { get; set; }

        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Items { get; set; } = new();
        public List<SelectListItem> MovementTypes { get; set; } = new();

        public List<SupplyTransaction> Transactions { get; set; } = new();
    }

    #endregion
}
