using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Areas.SuppliesTracking.Models;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using System.Data;

namespace Resource_Management_System.Areas.SuppliesTracking.Controllers
{
    [Area("SuppliesTracking")]
    [Authorize(Roles = "Admin,IT,Approver")]
    public class SuppliesController : Controller
    {
        private readonly DbHelper _dbHelper;
        private readonly ILogger<SuppliesController> _logger;

        public SuppliesController(DbHelper dbHelper, ILogger<SuppliesController> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        #region 1. Index / Dashboard & Item Catalog

        [HttpGet]
        public IActionResult Index(int? categoryId, string? trackType, string? searchKeyword)
        {
            var model = new SuppliesIndexViewModel
            {
                SelectedCategoryID = categoryId,
                SelectedTrackType = trackType,
                SearchKeyword = searchKeyword
            };

            // Populate categories dropdown
            var catTable = _dbHelper.ExecuteQuery("SELECT CategoryID, CategoryName FROM dbo.SupplyCategories WHERE IsActive = 1 ORDER BY CategoryID");
            model.Categories = catTable.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["CategoryID"].ToString(),
                Text = r["CategoryName"].ToString()
            }).ToList();

            // Fetch Catalog Items
            var itemsSql = @"
                SELECT i.ItemID, i.CategoryID, i.ItemCode, i.ItemName, i.BrandModel, 
                       i.CompatiblePrinters, i.UnitName, i.TrackType, i.CurrentStock, 
                       i.TotalReceived, i.TotalIssued, i.LowStockThreshold, i.IsActive,
                       c.CategoryName, c.RequiresPrinterModel
                FROM dbo.SupplyItems i
                INNER JOIN dbo.SupplyCategories c ON i.CategoryID = c.CategoryID
                WHERE i.IsActive = 1";

            var parameters = new List<SqlParameter>();

            if (categoryId.HasValue)
            {
                itemsSql += " AND i.CategoryID = @CategoryID";
                parameters.Add(new SqlParameter("@CategoryID", categoryId.Value));
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                itemsSql += " AND (i.ItemCode LIKE @Search OR i.ItemName LIKE @Search OR i.BrandModel LIKE @Search OR i.CompatiblePrinters LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{searchKeyword.Trim()}%"));
            }

            if (trackType == "consumable")
            {
                itemsSql += " AND i.TrackType = 0";
            }
            else if (trackType == "device")
            {
                itemsSql += " AND i.TrackType = 1";
            }
            else if (trackType == "lowstock")
            {
                itemsSql += " AND i.CurrentStock <= i.LowStockThreshold";
            }

            itemsSql += " ORDER BY i.ItemID DESC";

            var itemsTable = _dbHelper.ExecuteQuery(itemsSql, parameters.ToArray());
            foreach (DataRow row in itemsTable.Rows)
            {
                model.Items.Add(new SupplyItem
                {
                    ItemID = Convert.ToInt32(row["ItemID"]),
                    CategoryID = Convert.ToInt32(row["CategoryID"]),
                    ItemCode = row["ItemCode"].ToString() ?? "",
                    ItemName = row["ItemName"].ToString() ?? "",
                    BrandModel = row["BrandModel"]?.ToString(),
                    CompatiblePrinters = row["CompatiblePrinters"]?.ToString(),
                    UnitName = row["UnitName"].ToString() ?? "قطعة",
                    TrackType = Convert.ToBoolean(row["TrackType"]),
                    CurrentStock = Convert.ToInt32(row["CurrentStock"]),
                    TotalReceived = Convert.ToInt32(row["TotalReceived"]),
                    TotalIssued = Convert.ToInt32(row["TotalIssued"]),
                    LowStockThreshold = Convert.ToInt32(row["LowStockThreshold"]),
                    CategoryName = row["CategoryName"].ToString() ?? "",
                    RequiresPrinterModel = Convert.ToBoolean(row["RequiresPrinterModel"])
                });
            }

            // Calculate KPIs
            var kpiTable = _dbHelper.ExecuteQuery(@"
                SELECT 
                    (SELECT COUNT(*) FROM dbo.SupplyItems WHERE IsActive = 1) AS TotalItems,
                    (SELECT ISNULL(SUM(CurrentStock), 0) FROM dbo.SupplyItems WHERE TrackType = 0 AND IsActive = 1) AS ConsumablesStock,
                    (SELECT COUNT(*) FROM dbo.SupplyDeviceUnits WHERE Status = 'InStock') AS DevicesInStock,
                    (SELECT COUNT(*) FROM dbo.SupplyDeviceUnits WHERE Status = 'Issued') AS DevicesIssued,
                    (SELECT COUNT(*) FROM dbo.SupplyDeviceUnits WHERE Status = 'UnderMaintenance') AS DevicesUnderMaintenance,
                    (SELECT COUNT(*) FROM dbo.SupplyItems WHERE CurrentStock <= LowStockThreshold AND IsActive = 1) AS LowStockItems");

            if (kpiTable.Rows.Count > 0)
            {
                var r = kpiTable.Rows[0];
                model.TotalItemsCount = Convert.ToInt32(r["TotalItems"]);
                model.ConsumablesStockCount = Convert.ToInt32(r["ConsumablesStock"]);
                model.DevicesInStockCount = Convert.ToInt32(r["DevicesInStock"]);
                model.DevicesIssuedCount = Convert.ToInt32(r["DevicesIssued"]);
                model.DevicesUnderMaintenanceCount = Convert.ToInt32(r["DevicesUnderMaintenance"]);
                model.LowStockItemsCount = Convert.ToInt32(r["LowStockItems"]);
            }

            // Fetch recent 10 transactions
            var recentTxnTable = _dbHelper.ExecuteQuery(@"
                SELECT TOP 10 t.TransactionID, t.TransactionNumber, t.TransactionType, t.Quantity,
                              t.TransactionDate, i.ItemName, d.DepartmentName, t.RecipientName, t.PrinterModel
                FROM dbo.SupplyTransactions t
                INNER JOIN dbo.SupplyItems i ON t.ItemID = i.ItemID
                LEFT JOIN dbo.Departments d ON t.DepartmentID = d.DepartmentID
                ORDER BY t.TransactionID DESC");

            foreach (DataRow row in recentTxnTable.Rows)
            {
                model.RecentTransactions.Add(new SupplyTransaction
                {
                    TransactionID = Convert.ToInt32(row["TransactionID"]),
                    TransactionNumber = row["TransactionNumber"].ToString() ?? "",
                    TransactionType = row["TransactionType"].ToString() ?? "",
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    TransactionDate = Convert.ToDateTime(row["TransactionDate"]),
                    ItemName = row["ItemName"].ToString() ?? "",
                    DepartmentName = row["DepartmentName"]?.ToString(),
                    RecipientName = row["RecipientName"]?.ToString(),
                    PrinterModel = row["PrinterModel"]?.ToString()
                });
            }

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,IT")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateItem(SupplyItem newItem)
        {
            if (string.IsNullOrWhiteSpace(newItem.ItemName) || newItem.CategoryID <= 0)
            {
                TempData["ErrorMessage"] = "يرجى إدخال اسم الصنف واختيار التصنيف.";
                return RedirectToAction(nameof(Index));
            }

            // Auto-generate ItemCode if not provided
            if (string.IsNullOrWhiteSpace(newItem.ItemCode))
            {
                var prefix = newItem.TrackType ? "DEV" : "CON";
                newItem.ItemCode = $"{prefix}-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            }

            try
            {
                _dbHelper.ExecuteNonQuery(@"
                    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, LowStockThreshold, IsActive)
                    VALUES (@CategoryID, @ItemCode, @ItemName, @BrandModel, @CompatiblePrinters, @UnitName, @TrackType, @LowStockThreshold, 1)",
                    new SqlParameter("@CategoryID", newItem.CategoryID),
                    new SqlParameter("@ItemCode", newItem.ItemCode.Trim()),
                    new SqlParameter("@ItemName", newItem.ItemName.Trim()),
                    new SqlParameter("@BrandModel", (object?)newItem.BrandModel?.Trim() ?? DBNull.Value),
                    new SqlParameter("@CompatiblePrinters", (object?)newItem.CompatiblePrinters?.Trim() ?? DBNull.Value),
                    new SqlParameter("@UnitName", string.IsNullOrWhiteSpace(newItem.UnitName) ? "قطعة" : newItem.UnitName.Trim()),
                    new SqlParameter("@TrackType", newItem.TrackType),
                    new SqlParameter("@LowStockThreshold", newItem.LowStockThreshold <= 0 ? 5 : newItem.LowStockThreshold)
                );

                TempData["SuccessMessage"] = $"تمت إضافة الصنف بنجاح (كود: {newItem.ItemCode}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supply item");
                TempData["ErrorMessage"] = "حدث خطأ أثناء حفظ الصنف. تأكد أن كود الصنف غير مكرر.";
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region 2. Receive (Inbound Stock & Barcode Generation)

        [HttpGet]
        [Authorize(Roles = "Admin,IT")]
        public IActionResult Receive(int? itemId)
        {
            var model = new ReceiveViewModel();
            PopulateReceiveDropdowns(model, itemId);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,IT")]
        [ValidateAntiForgeryToken]
        public IActionResult Receive(ReceiveViewModel model)
        {
            if (!ModelState.IsValid || model.Quantity <= 0 || model.ItemID <= 0)
            {
                PopulateReceiveDropdowns(model, model.ItemID);
                TempData["ErrorMessage"] = "يرجى التأكد من اختيار الصنف وإدخال كمية صحيحة.";
                return View(model);
            }

            var itemTable = _dbHelper.ExecuteQuery("SELECT ItemID, ItemName, TrackType FROM dbo.SupplyItems WHERE ItemID = @ItemID",
                new SqlParameter("@ItemID", model.ItemID));

            if (itemTable.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "الصنف المحدد غير موجود.";
                return RedirectToAction(nameof(Receive));
            }

            var trackType = Convert.ToBoolean(itemTable.Rows[0]["TrackType"]);
            var currentUserId = GetCurrentUserId();

            // Auto-generate TransactionNumber: TRN-YYYY-XXXXX
            var countTable = _dbHelper.ExecuteQuery("SELECT ISNULL(MAX(TransactionID), 0) + 1 FROM dbo.SupplyTransactions");
            int nextTxnId = Convert.ToInt32(countTable.Rows[0][0]);
            string txnNumber = $"TRN-{DateTime.UtcNow:yyyy}-{nextTxnId:D5}";

            try
            {
                // 1. Insert Transaction Header
                var txnInsertTable = _dbHelper.ExecuteQuery(@"
                    INSERT INTO dbo.SupplyTransactions 
                        (TransactionNumber, TransactionType, ItemID, Quantity, SupplierOrReference, Notes, PerformedBy, TransactionDate)
                    OUTPUT INSERTED.TransactionID
                    VALUES 
                        (@TxnNumber, 'Receive', @ItemID, @Quantity, @SupplierOrReference, @Notes, @PerformedBy, SYSUTCDATETIME())",
                    new SqlParameter("@TxnNumber", txnNumber),
                    new SqlParameter("@ItemID", model.ItemID),
                    new SqlParameter("@Quantity", model.Quantity),
                    new SqlParameter("@SupplierOrReference", (object?)model.SupplierOrReference?.Trim() ?? DBNull.Value),
                    new SqlParameter("@Notes", (object?)model.Notes?.Trim() ?? DBNull.Value),
                    new SqlParameter("@PerformedBy", currentUserId.HasValue ? currentUserId.Value : DBNull.Value)
                );

                int createdTxnId = Convert.ToInt32(txnInsertTable.Rows[0]["TransactionID"]);

                // 2. Handle Consumables vs Serialized Devices
                if (!trackType)
                {
                    // Consumables: Update stock quantity directly
                    _dbHelper.ExecuteNonQuery(@"
                        UPDATE dbo.SupplyItems 
                        SET CurrentStock = CurrentStock + @Qty,
                            TotalReceived = TotalReceived + @Qty
                        WHERE ItemID = @ItemID",
                        new SqlParameter("@Qty", model.Quantity),
                        new SqlParameter("@ItemID", model.ItemID));

                    TempData["SuccessMessage"] = $"تم توريد عدد {model.Quantity} بنجاح برقم إذن ({txnNumber}).";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Serialized Devices: Auto-generate N unique barcodes and insert into SupplyDeviceUnits
                    var parsedSerials = new List<string>();
                    if (!string.IsNullOrWhiteSpace(model.SerialNumbersText))
                    {
                        parsedSerials = model.SerialNumbersText
                            .Split(new[] { ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToList();
                    }

                    // Get current max UnitID for unique barcode generation
                    var unitCountTable = _dbHelper.ExecuteQuery("SELECT ISNULL(MAX(UnitID), 0) FROM dbo.SupplyDeviceUnits");
                    int lastUnitId = Convert.ToInt32(unitCountTable.Rows[0][0]);

                    for (int i = 0; i < model.Quantity; i++)
                    {
                        int nextUnitNum = lastUnitId + i + 1;
                        string barcode = $"DEV-{DateTime.UtcNow:yyyy}-{nextUnitNum:D5}";
                        string? serial = i < parsedSerials.Count ? parsedSerials[i] : null;

                        var unitInsertTable = _dbHelper.ExecuteQuery(@"
                            INSERT INTO dbo.SupplyDeviceUnits
                                (ItemID, Barcode, SerialNumber, Status, ReceivedDate, LastMovementDate, Notes)
                            OUTPUT INSERTED.UnitID
                            VALUES
                                (@ItemID, @Barcode, @SerialNumber, 'InStock', SYSUTCDATETIME(), SYSUTCDATETIME(), @Notes)",
                            new SqlParameter("@ItemID", model.ItemID),
                            new SqlParameter("@Barcode", barcode),
                            new SqlParameter("@SerialNumber", (object?)serial ?? DBNull.Value),
                            new SqlParameter("@Notes", (object?)model.Notes ?? DBNull.Value));

                        int unitId = Convert.ToInt32(unitInsertTable.Rows[0]["UnitID"]);

                        // Link in junction table
                        _dbHelper.ExecuteNonQuery(@"
                            INSERT INTO dbo.SupplyTransactionUnits (TransactionID, UnitID)
                            VALUES (@TxnID, @UnitID)",
                            new SqlParameter("@TxnID", createdTxnId),
                            new SqlParameter("@UnitID", unitId));
                    }

                    // Update Item Stock totals
                    _dbHelper.ExecuteNonQuery(@"
                        UPDATE dbo.SupplyItems 
                        SET CurrentStock = CurrentStock + @Qty,
                            TotalReceived = TotalReceived + @Qty
                        WHERE ItemID = @ItemID",
                        new SqlParameter("@Qty", model.Quantity),
                        new SqlParameter("@ItemID", model.ItemID));

                    TempData["SuccessMessage"] = $"تم توريد عدد {model.Quantity} أجهزة وتوليد باركود لكل قطعة بنجاح!";
                    return RedirectToAction(nameof(PrintBarcodes), new { transactionId = createdTxnId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Receive transaction");
                TempData["ErrorMessage"] = "حدث خطأ أثناء حفظ حركة التوريد: " + ex.Message;
                PopulateReceiveDropdowns(model, model.ItemID);
                return View(model);
            }
        }

        #endregion

        #region 3. Movements (Issue, Return, Maintenance, Scrap)

        [HttpGet]
        [Authorize(Roles = "Admin,IT")]
        public IActionResult Movements(string? type, int? itemId)
        {
            var model = new MovementViewModel
            {
                MovementType = string.IsNullOrWhiteSpace(type) ? "Issue" : type
            };

            PopulateMovementDropdowns(model, itemId);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,IT")]
        [ValidateAntiForgeryToken]
        public IActionResult Movements(MovementViewModel model)
        {
            if (model.ItemID <= 0 || model.Quantity <= 0)
            {
                TempData["ErrorMessage"] = "يرجى التأكد من اختيار الصنف وإدخال كمية صحيحة.";
                PopulateMovementDropdowns(model, model.ItemID);
                return View(model);
            }

            var itemTable = _dbHelper.ExecuteQuery(@"
                SELECT i.ItemID, i.ItemName, i.TrackType, i.CurrentStock, c.RequiresPrinterModel 
                FROM dbo.SupplyItems i
                INNER JOIN dbo.SupplyCategories c ON i.CategoryID = c.CategoryID
                WHERE i.ItemID = @ItemID",
                new SqlParameter("@ItemID", model.ItemID));

            if (itemTable.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "الصنف المحدد غير موجود.";
                return RedirectToAction(nameof(Movements));
            }

            var itemRow = itemTable.Rows[0];
            bool trackType = Convert.ToBoolean(itemRow["TrackType"]);
            int currentStock = Convert.ToInt32(itemRow["CurrentStock"]);
            bool requiresPrinter = Convert.ToBoolean(itemRow["RequiresPrinterModel"]);

            // Validate printer model requirement
            if (requiresPrinter && model.MovementType == "Issue" && string.IsNullOrWhiteSpace(model.PrinterModel))
            {
                TempData["ErrorMessage"] = "يرجى تحديد أو كتابة موديل الطابعة المستهدفة لصرف الحبر/الدرام.";
                PopulateMovementDropdowns(model, model.ItemID);
                return View(model);
            }

            // Validate department for Issue
            if (model.MovementType == "Issue" && (!model.DepartmentID.HasValue || model.DepartmentID.Value <= 0))
            {
                TempData["ErrorMessage"] = "يرجى اختيار القسم / الجهة المستفيدة للصرف.";
                PopulateMovementDropdowns(model, model.ItemID);
                return View(model);
            }

            // Stock Check for outbound movements (Issue, Scrap, Maintenance)
            if (model.MovementType is "Issue" or "Scrap" or "Maintenance" && currentStock < model.Quantity)
            {
                TempData["ErrorMessage"] = $"رصيد المخزن الحالي ({currentStock}) غير كافٍ لصرف أو تحويل كمية ({model.Quantity}).";
                PopulateMovementDropdowns(model, model.ItemID);
                return View(model);
            }

            var currentUserId = GetCurrentUserId();

            // Auto-generate Transaction Number
            var countTable = _dbHelper.ExecuteQuery("SELECT ISNULL(MAX(TransactionID), 0) + 1 FROM dbo.SupplyTransactions");
            int nextTxnId = Convert.ToInt32(countTable.Rows[0][0]);
            string txnNumber = $"TRN-{DateTime.UtcNow:yyyy}-{nextTxnId:D5}";

            try
            {
                // 1. Insert Transaction Record
                var txnInsertTable = _dbHelper.ExecuteQuery(@"
                    INSERT INTO dbo.SupplyTransactions 
                        (TransactionNumber, TransactionType, ItemID, Quantity, DepartmentID, RecipientName, PrinterModel, SupplierOrReference, Notes, PerformedBy, TransactionDate)
                    OUTPUT INSERTED.TransactionID
                    VALUES 
                        (@TxnNumber, @Type, @ItemID, @Quantity, @DeptID, @Recipient, @Printer, @Reference, @Notes, @PerformedBy, SYSUTCDATETIME())",
                    new SqlParameter("@TxnNumber", txnNumber),
                    new SqlParameter("@Type", model.MovementType),
                    new SqlParameter("@ItemID", model.ItemID),
                    new SqlParameter("@Quantity", model.Quantity),
                    new SqlParameter("@DeptID", model.DepartmentID.HasValue ? model.DepartmentID.Value : DBNull.Value),
                    new SqlParameter("@Recipient", (object?)model.RecipientName?.Trim() ?? DBNull.Value),
                    new SqlParameter("@Printer", (object?)model.PrinterModel?.Trim() ?? DBNull.Value),
                    new SqlParameter("@Reference", (object?)model.ReferenceNumber?.Trim() ?? DBNull.Value),
                    new SqlParameter("@Notes", (object?)model.Notes?.Trim() ?? DBNull.Value),
                    new SqlParameter("@PerformedBy", currentUserId.HasValue ? currentUserId.Value : DBNull.Value));

                int createdTxnId = Convert.ToInt32(txnInsertTable.Rows[0]["TransactionID"]);

                // 2. Process according to TrackType and MovementType
                if (!trackType)
                {
                    // Consumables: Adjust stock count directly
                    if (model.MovementType == "Issue")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock - @Qty,
                                TotalIssued = TotalIssued + @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                    else if (model.MovementType == "Return")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock + @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                    else if (model.MovementType == "Scrap")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock - @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                }
                else
                {
                    // Serialized Devices: Update specific Unit records and junction table
                    List<int> unitIdsToUpdate = model.SelectedUnitIDs ?? new List<int>();

                    // If user didn't pick specific barcodes, auto-select available units matching the movement
                    if (unitIdsToUpdate.Count < model.Quantity)
                    {
                        string neededStatus = model.MovementType switch
                        {
                            "Issue" => "InStock",
                            "Maintenance" => "InStock",
                            "Scrap" => "InStock",
                            "Return" => "Issued",
                            _ => "InStock"
                        };

                        var autoUnitsTable = _dbHelper.ExecuteQuery(@"
                            SELECT TOP (@Needed) UnitID 
                            FROM dbo.SupplyDeviceUnits 
                            WHERE ItemID = @ItemID AND Status = @Status
                            ORDER BY UnitID ASC",
                            new SqlParameter("@Needed", model.Quantity - unitIdsToUpdate.Count),
                            new SqlParameter("@ItemID", model.ItemID),
                            new SqlParameter("@Status", neededStatus));

                        foreach (DataRow row in autoUnitsTable.Rows)
                        {
                            unitIdsToUpdate.Add(Convert.ToInt32(row["UnitID"]));
                        }
                    }

                    string newStatus = model.MovementType switch
                    {
                        "Issue" => "Issued",
                        "Return" => "InStock",
                        "Maintenance" => "UnderMaintenance",
                        "Scrap" => "Scrapped",
                        _ => "InStock"
                    };

                    int? newDeptId = model.MovementType == "Issue" ? model.DepartmentID : (model.MovementType == "Return" ? null : model.DepartmentID);

                    foreach (var uId in unitIdsToUpdate.Take(model.Quantity))
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyDeviceUnits
                            SET Status = @NewStatus,
                                CurrentDepartmentID = @DeptID,
                                LastMovementDate = SYSUTCDATETIME()
                            WHERE UnitID = @UnitID",
                            new SqlParameter("@NewStatus", newStatus),
                            new SqlParameter("@DeptID", newDeptId.HasValue ? newDeptId.Value : DBNull.Value),
                            new SqlParameter("@UnitID", uId));

                        _dbHelper.ExecuteNonQuery(@"
                            INSERT INTO dbo.SupplyTransactionUnits (TransactionID, UnitID)
                            VALUES (@TxnID, @UnitID)",
                            new SqlParameter("@TxnID", createdTxnId),
                            new SqlParameter("@UnitID", uId));
                    }

                    // Update SupplyItems totals
                    if (model.MovementType == "Issue")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock - @Qty,
                                TotalIssued = TotalIssued + @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                    else if (model.MovementType == "Return")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock + @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                    else if (model.MovementType == "Scrap" || model.MovementType == "Maintenance")
                    {
                        _dbHelper.ExecuteNonQuery(@"
                            UPDATE dbo.SupplyItems 
                            SET CurrentStock = CurrentStock - @Qty
                            WHERE ItemID = @ItemID",
                            new SqlParameter("@Qty", model.Quantity),
                            new SqlParameter("@ItemID", model.ItemID));
                    }
                }

                TempData["SuccessMessage"] = $"تم تسجيل حركة ({GetArabicMovementType(model.MovementType)}) برقم ({txnNumber}) بنجاح.";

                if (model.MovementType == "Issue")
                {
                    return RedirectToAction(nameof(PrintVoucher), new { transactionId = createdTxnId });
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inventory movement");
                TempData["ErrorMessage"] = "حدث خطأ أثناء تنفيذ الحركة: " + ex.Message;
                PopulateMovementDropdowns(model, model.ItemID);
                return View(model);
            }
        }

        #endregion

        #region 4. Printing (Barcodes & Official Handover Voucher)

        [HttpGet]
        public IActionResult PrintBarcodes(int transactionId)
        {
            var txnTable = _dbHelper.ExecuteQuery(@"
                SELECT t.TransactionID, t.TransactionNumber, t.TransactionDate,
                       i.ItemID, i.ItemCode, i.ItemName, i.BrandModel
                FROM dbo.SupplyTransactions t
                INNER JOIN dbo.SupplyItems i ON t.ItemID = i.ItemID
                WHERE t.TransactionID = @TxnID",
                new SqlParameter("@TxnID", transactionId));

            if (txnTable.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "حركة التوريد غير موجودة.";
                return RedirectToAction(nameof(Index));
            }

            var row = txnTable.Rows[0];
            var model = new PrintBarcodesViewModel
            {
                TransactionID = Convert.ToInt32(row["TransactionID"]),
                TransactionNumber = row["TransactionNumber"].ToString() ?? "",
                ItemCode = row["ItemCode"].ToString() ?? "",
                ItemName = row["ItemName"].ToString() ?? "",
                BrandModel = row["BrandModel"]?.ToString(),
                ReceiveDate = Convert.ToDateTime(row["TransactionDate"])
            };

            var unitsTable = _dbHelper.ExecuteQuery(@"
                SELECT u.UnitID, u.Barcode, u.SerialNumber, u.Status
                FROM dbo.SupplyTransactionUnits tu
                INNER JOIN dbo.SupplyDeviceUnits u ON tu.UnitID = u.UnitID
                WHERE tu.TransactionID = @TxnID
                ORDER BY u.UnitID ASC",
                new SqlParameter("@TxnID", transactionId));

            foreach (DataRow uRow in unitsTable.Rows)
            {
                model.Units.Add(new SupplyDeviceUnit
                {
                    UnitID = Convert.ToInt32(uRow["UnitID"]),
                    Barcode = uRow["Barcode"].ToString() ?? "",
                    SerialNumber = uRow["SerialNumber"]?.ToString(),
                    Status = uRow["Status"].ToString() ?? "InStock"
                });
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult PrintVoucher(int transactionId)
        {
            var txnTable = _dbHelper.ExecuteQuery(@"
                SELECT t.TransactionID, t.TransactionNumber, t.TransactionType, t.TransactionDate,
                       t.Quantity, t.RecipientName, t.PrinterModel, t.SupplierOrReference, t.Notes,
                       i.ItemCode, i.ItemName, i.BrandModel, i.UnitName,
                       d.DepartmentName, u.UserName AS PerformedByName
                FROM dbo.SupplyTransactions t
                INNER JOIN dbo.SupplyItems i ON t.ItemID = i.ItemID
                LEFT JOIN dbo.Departments d ON t.DepartmentID = d.DepartmentID
                LEFT JOIN dbo.Users u ON t.PerformedBy = u.UserID
                WHERE t.TransactionID = @TxnID",
                new SqlParameter("@TxnID", transactionId));

            if (txnTable.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "الحركة غير موجودة.";
                return RedirectToAction(nameof(Index));
            }

            var row = txnTable.Rows[0];
            string type = row["TransactionType"].ToString() ?? "";

            var model = new PrintVoucherViewModel
            {
                TransactionID = Convert.ToInt32(row["TransactionID"]),
                TransactionNumber = row["TransactionNumber"].ToString() ?? "",
                TransactionType = type,
                TransactionTypeArabic = GetArabicMovementType(type),
                TransactionDate = Convert.ToDateTime(row["TransactionDate"]),
                ItemCode = row["ItemCode"].ToString() ?? "",
                ItemName = row["ItemName"].ToString() ?? "",
                BrandModel = row["BrandModel"]?.ToString(),
                Quantity = Convert.ToInt32(row["Quantity"]),
                UnitName = row["UnitName"].ToString() ?? "قطعة",
                DepartmentName = row["DepartmentName"]?.ToString(),
                RecipientName = row["RecipientName"]?.ToString(),
                PrinterModel = row["PrinterModel"]?.ToString(),
                SupplierOrReference = row["SupplierOrReference"]?.ToString(),
                PerformedByName = row["PerformedByName"]?.ToString(),
                Notes = row["Notes"]?.ToString()
            };

            var unitsTable = _dbHelper.ExecuteQuery(@"
                SELECT u.UnitID, u.Barcode, u.SerialNumber, u.Status
                FROM dbo.SupplyTransactionUnits tu
                INNER JOIN dbo.SupplyDeviceUnits u ON tu.UnitID = u.UnitID
                WHERE tu.TransactionID = @TxnID
                ORDER BY u.UnitID ASC",
                new SqlParameter("@TxnID", transactionId));

            foreach (DataRow uRow in unitsTable.Rows)
            {
                model.Units.Add(new SupplyDeviceUnit
                {
                    UnitID = Convert.ToInt32(uRow["UnitID"]),
                    Barcode = uRow["Barcode"].ToString() ?? "",
                    SerialNumber = uRow["SerialNumber"]?.ToString(),
                    Status = uRow["Status"].ToString() ?? ""
                });
            }

            return View(model);
        }

        #endregion

        #region 5. Reports & Search

        [HttpGet]
        public IActionResult Reports(DateTime? fromDate, DateTime? toDate, int? departmentId, int? itemId, string? movementType, string? searchKeyword)
        {
            var model = new SuppliesReportsViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                DepartmentID = departmentId,
                ItemID = itemId,
                MovementType = movementType,
                SearchKeyword = searchKeyword
            };

            // Dropdowns
            var deptsTable = _dbHelper.ExecuteQuery("SELECT DepartmentID, DepartmentName FROM dbo.Departments ORDER BY DepartmentID ASC");
            model.Departments = deptsTable.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["DepartmentID"].ToString(),
                Text = r["DepartmentName"].ToString()
            }).ToList();

            var itemsTable = _dbHelper.ExecuteQuery("SELECT ItemID, ItemName FROM dbo.SupplyItems WHERE IsActive = 1 ORDER BY ItemName ASC");
            model.Items = itemsTable.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["ItemID"].ToString(),
                Text = r["ItemName"].ToString()
            }).ToList();

            model.MovementTypes = new List<SelectListItem>
            {
                new SelectListItem("توريد (استلام)", "Receive"),
                new SelectListItem("صرف لقسم", "Issue"),
                new SelectListItem("إرجاع للمخزن", "Return"),
                new SelectListItem("صيانة", "Maintenance"),
                new SelectListItem("تكهين / تالف", "Scrap")
            };

            // Query
            var sql = @"
                SELECT t.TransactionID, t.TransactionNumber, t.TransactionType, t.Quantity,
                       t.TransactionDate, i.ItemName, d.DepartmentName, t.RecipientName,
                       t.PrinterModel, t.SupplierOrReference, t.Notes, u.UserName AS PerformedByName
                FROM dbo.SupplyTransactions t
                INNER JOIN dbo.SupplyItems i ON t.ItemID = i.ItemID
                LEFT JOIN dbo.Departments d ON t.DepartmentID = d.DepartmentID
                LEFT JOIN dbo.Users u ON t.PerformedBy = u.UserID
                WHERE 1 = 1";

            var parameters = new List<SqlParameter>();

            if (fromDate.HasValue)
            {
                sql += " AND t.TransactionDate >= @FromDate";
                parameters.Add(new SqlParameter("@FromDate", fromDate.Value.Date));
            }

            if (toDate.HasValue)
            {
                sql += " AND t.TransactionDate < @ToDate";
                parameters.Add(new SqlParameter("@ToDate", toDate.Value.Date.AddDays(1)));
            }

            if (departmentId.HasValue)
            {
                sql += " AND t.DepartmentID = @DeptID";
                parameters.Add(new SqlParameter("@DeptID", departmentId.Value));
            }

            if (itemId.HasValue)
            {
                sql += " AND t.ItemID = @ItemID";
                parameters.Add(new SqlParameter("@ItemID", itemId.Value));
            }

            if (!string.IsNullOrWhiteSpace(movementType))
            {
                sql += " AND t.TransactionType = @TxnType";
                parameters.Add(new SqlParameter("@TxnType", movementType));
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                sql += " AND (t.TransactionNumber LIKE @Search OR t.RecipientName LIKE @Search OR t.SupplierOrReference LIKE @Search OR t.Notes LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{searchKeyword.Trim()}%"));
            }

            sql += " ORDER BY t.TransactionID DESC";

            var resultTable = _dbHelper.ExecuteQuery(sql, parameters.ToArray());
            foreach (DataRow row in resultTable.Rows)
            {
                model.Transactions.Add(new SupplyTransaction
                {
                    TransactionID = Convert.ToInt32(row["TransactionID"]),
                    TransactionNumber = row["TransactionNumber"].ToString() ?? "",
                    TransactionType = row["TransactionType"].ToString() ?? "",
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    TransactionDate = Convert.ToDateTime(row["TransactionDate"]),
                    ItemName = row["ItemName"].ToString() ?? "",
                    DepartmentName = row["DepartmentName"]?.ToString(),
                    RecipientName = row["RecipientName"]?.ToString(),
                    PrinterModel = row["PrinterModel"]?.ToString(),
                    SupplierOrReference = row["SupplierOrReference"]?.ToString(),
                    Notes = row["Notes"]?.ToString(),
                    PerformedByName = row["PerformedByName"]?.ToString()
                });
            }

            return View(model);
        }

        #endregion

        #region Helpers

        private void PopulateReceiveDropdowns(ReceiveViewModel model, int? selectedItemId)
        {
            var table = _dbHelper.ExecuteQuery(@"
                SELECT i.ItemID, i.ItemCode, i.ItemName, i.TrackType, c.CategoryName
                FROM dbo.SupplyItems i
                INNER JOIN dbo.SupplyCategories c ON i.CategoryID = c.CategoryID
                WHERE i.IsActive = 1
                ORDER BY i.ItemName ASC");

            model.AvailableItems = table.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["ItemID"].ToString(),
                Text = $"[{r["CategoryName"]}] {r["ItemName"]} ({r["ItemCode"]})",
                Selected = selectedItemId.HasValue && Convert.ToInt32(r["ItemID"]) == selectedItemId.Value
            }).ToList();

            if (selectedItemId.HasValue)
            {
                var row = table.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["ItemID"]) == selectedItemId.Value);
                if (row != null)
                {
                    model.ItemID = selectedItemId.Value;
                    model.TrackType = Convert.ToBoolean(row["TrackType"]);
                    model.SelectedItemName = row["ItemName"].ToString();
                }
            }
        }

        private void PopulateMovementDropdowns(MovementViewModel model, int? selectedItemId)
        {
            var itemsTable = _dbHelper.ExecuteQuery(@"
                SELECT i.ItemID, i.ItemCode, i.ItemName, i.TrackType, i.CurrentStock, c.CategoryName, c.RequiresPrinterModel
                FROM dbo.SupplyItems i
                INNER JOIN dbo.SupplyCategories c ON i.CategoryID = c.CategoryID
                WHERE i.IsActive = 1
                ORDER BY i.ItemName ASC");

            model.AvailableItems = itemsTable.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["ItemID"].ToString(),
                Text = $"{r["ItemName"]} (المخزن: {r["CurrentStock"]})",
                Selected = selectedItemId.HasValue && Convert.ToInt32(r["ItemID"]) == selectedItemId.Value
            }).ToList();

            var deptsTable = _dbHelper.ExecuteQuery("SELECT DepartmentID, DepartmentName FROM dbo.Departments ORDER BY DepartmentID ASC");
            model.Departments = deptsTable.AsEnumerable().Select(r => new SelectListItem
            {
                Value = r["DepartmentID"].ToString(),
                Text = r["DepartmentName"].ToString()
            }).ToList();

            if (selectedItemId.HasValue)
            {
                model.ItemID = selectedItemId.Value;

                // Load device units if serialized
                var unitsTable = _dbHelper.ExecuteQuery(@"
                    SELECT u.UnitID, u.Barcode, u.SerialNumber, u.Status, d.DepartmentName
                    FROM dbo.SupplyDeviceUnits u
                    LEFT JOIN dbo.Departments d ON u.CurrentDepartmentID = d.DepartmentID
                    WHERE u.ItemID = @ItemID
                    ORDER BY u.UnitID ASC",
                    new SqlParameter("@ItemID", selectedItemId.Value));

                foreach (DataRow row in unitsTable.Rows)
                {
                    model.AvailableDeviceUnits.Add(new SupplyDeviceUnit
                    {
                        UnitID = Convert.ToInt32(row["UnitID"]),
                        Barcode = row["Barcode"].ToString() ?? "",
                        SerialNumber = row["SerialNumber"]?.ToString(),
                        Status = row["Status"].ToString() ?? "",
                        DepartmentName = row["DepartmentName"]?.ToString()
                    });
                }
            }
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }

            var name = User.Identity?.Name;
            if (!string.IsNullOrEmpty(name))
            {
                var table = _dbHelper.ExecuteQuery("SELECT UserID FROM dbo.Users WHERE UserName = @UserName",
                    new SqlParameter("@UserName", name));
                if (table.Rows.Count > 0)
                {
                    return Convert.ToInt32(table.Rows[0]["UserID"]);
                }
            }

            return null;
        }

        private static string GetArabicMovementType(string type) => type switch
        {
            "Receive" => "توريد للمخزن",
            "Issue" => "صرف لقسم",
            "Return" => "إرجاع للمخزن",
            "Maintenance" => "تحويل للصيانة",
            "Scrap" => "تكهين / تالف",
            _ => type
        };

        #endregion
    }
}
