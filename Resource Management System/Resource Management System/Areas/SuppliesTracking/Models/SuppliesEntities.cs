using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resource_Management_System.Areas.SuppliesTracking.Models
{
    #region Entities (Mapped to Database Tables)

    /// <summary>
    /// Represents a supply category (e.g., Inks, Drums, Printers, Laptops, Accessories)
    /// </summary>
    [Table("SupplyCategories")]
    public class SupplyCategory
    {
        [Key]
        public int CategoryID { get; set; }

        [Required, MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// 0 = Consumables (tracked by total quantity)
        /// 1 = Devices / Serialized items (tracked by individual units with barcodes)
        /// </summary>
        public bool TrackBySerial { get; set; }

        /// <summary>
        /// 1 = Requires selecting/entering a printer model when issuing (e.g., Inks, Toners, Drums)
        /// </summary>
        public bool RequiresPrinterModel { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Catalog item representing a specific consumable or device model
    /// </summary>
    [Table("SupplyItems")]
    public class SupplyItem
    {
        [Key]
        public int ItemID { get; set; }

        public int CategoryID { get; set; }

        [Required, MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BrandModel { get; set; }

        [MaxLength(250)]
        public string? CompatiblePrinters { get; set; }

        [MaxLength(50)]
        public string UnitName { get; set; } = "قطعة";

        /// <summary>
        /// 0 = Consumables, 1 = Devices (Serialized)
        /// </summary>
        public bool TrackType { get; set; }

        public int CurrentStock { get; set; } = 0;
        public int TotalReceived { get; set; } = 0;
        public int TotalIssued { get; set; } = 0;
        public int LowStockThreshold { get; set; } = 5;
        public bool IsActive { get; set; } = true;

        // Navigation helpers for display
        [NotMapped]
        public string CategoryName { get; set; } = string.Empty;

        [NotMapped]
        public bool RequiresPrinterModel { get; set; }

        [NotMapped]
        public bool IsLowStock => CurrentStock <= LowStockThreshold;
    }

    /// <summary>
    /// Physical serialized device unit tracked with an individual unique barcode
    /// </summary>
    [Table("SupplyDeviceUnits")]
    public class SupplyDeviceUnit
    {
        [Key]
        public int UnitID { get; set; }

        public int ItemID { get; set; }

        [Required, MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Unit lifecycle status: InStock, Issued, UnderMaintenance, Scrapped
        /// </summary>
        [Required, MaxLength(30)]
        public string Status { get; set; } = "InStock";

        public int? CurrentDepartmentID { get; set; }

        [MaxLength(150)]
        public string? AssignedPrinterOrUser { get; set; }

        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastMovementDate { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        // Navigation display properties
        [NotMapped]
        public string ItemName { get; set; } = string.Empty;

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string? DepartmentName { get; set; }
    }

    /// <summary>
    /// Movement ledger header (Receive, Issue, Return, Maintenance, Scrap)
    /// </summary>
    [Table("SupplyTransactions")]
    public class SupplyTransaction
    {
        [Key]
        public int TransactionID { get; set; }

        [Required, MaxLength(50)]
        public string TransactionNumber { get; set; } = string.Empty;

        /// <summary>
        /// Movement Type: Receive, Issue, Return, Maintenance, Scrap
        /// </summary>
        [Required, MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;

        public int ItemID { get; set; }
        public int Quantity { get; set; } = 1;
        public int? DepartmentID { get; set; }

        [MaxLength(150)]
        public string? RecipientName { get; set; }

        [MaxLength(100)]
        public string? PrinterModel { get; set; }

        [MaxLength(150)]
        public string? SupplierOrReference { get; set; }

        public string? Notes { get; set; }
        public int? PerformedBy { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        // Navigation display properties
        [NotMapped]
        public string ItemName { get; set; } = string.Empty;

        [NotMapped]
        public string? DepartmentName { get; set; }

        [NotMapped]
        public string? PerformedByName { get; set; }

        [NotMapped]
        public List<SupplyDeviceUnit> Units { get; set; } = new();
    }

    /// <summary>
    /// Junction table linking transaction header with specific individual device units
    /// </summary>
    [Table("SupplyTransactionUnits")]
    public class SupplyTransactionUnit
    {
        public int TransactionID { get; set; }
        public int UnitID { get; set; }
    }

    #endregion
}
