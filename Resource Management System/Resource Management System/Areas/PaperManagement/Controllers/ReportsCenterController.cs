using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using Resource_Management_System.Models;
using Resource_Management_System.Services;
using System.Data;

namespace Resource_Management_System.Areas.PaperManagement.Controllers
{
    [Area("PaperManagement")]
    [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
    public class ReportsCenterController : Controller
    {
        private readonly DbHelper _db;
        public ReportsCenterController(DbHelper db) { _db = db; }

        [HttpGet]
        public IActionResult Index(string tab = "contracts", int? yearFrom = null, int? yearTo = null,
            int? contractId = null, string detail = "summary", int[]? departmentIds = null,
            string period = "year", int? year = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!new[] { "contracts", "official", "contract", "period" }.Contains(tab)) tab = "contracts";
            if (!new[] { "summary", "departments" }.Contains(detail)) detail = "summary";
            var model = NewModel(tab, yearFrom, yearTo, contractId, detail, departmentIds, period, year, fromDate, toDate);
            if (tab == "contracts")
            {
                if (!ValidateContractYears(model)) return View(model);
                model.VisibleContracts = FilterContracts(model).ToList();
            }
            else if (tab == "contract" && contractId.HasValue)
            {
                var selected = model.Contracts.FirstOrDefault(c => c.ContractID == contractId.Value);
                if (selected == null) model.Error = "العقد المحدد غير موجود.";
                else LoadContractDetails(model, selected.ContractID);
            }
            else if (tab == "period")
            {
                if (ResolvePeriod(model, out var start, out var finish)) LoadPeriodDetails(model, start, finish);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Export(string tab = "contracts", int? yearFrom = null, int? yearTo = null,
            int? contractId = null, string detail = "summary", int[]? departmentIds = null,
            string period = "year", int? year = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (tab != "contracts" && tab != "contract" && tab != "period") return BadRequest();
            var model = NewModel(tab, yearFrom, yearTo, contractId, detail, departmentIds, period, year, fromDate, toDate);
            string[] headers;
            List<object?[]> rows;
            if (tab == "contracts")
            {
                if (!ValidateContractYears(model)) return BadRequest(model.Error);
                headers = new[] { "العقد", "رقم العقد", "تاريخ البداية", "تاريخ النهاية", "الحالة", "كمية تعاقد A4", "كمية تعاقد A3", "افتتاحي", "المورد", "المصروف", "الرصيد" };
                rows = FilterContracts(model).Select(c => new object?[] { c.Name, c.Number, c.StartDate.ToString("yyyy/MM/dd"),
                    c.EndDate?.ToString("yyyy/MM/dd") ?? "—", c.Status, c.ContractedA4, c.ContractedA3,
                    c.Opening, c.Received, c.Issued, c.Balance }).ToList();
            }
            else if (tab == "contract")
            {
                if (!contractId.HasValue || model.Contracts.All(c => c.ContractID != contractId.Value)) return BadRequest("اختر العقد أولاً.");
                LoadContractDetails(model, contractId.Value);
                return ExportMovements(model, "تقرير العقد", detail, model.DepartmentIds);
            }
            else
            {
                if (!ResolvePeriod(model, out var start, out var finish)) return BadRequest(model.Error);
                LoadPeriodDetails(model, start, finish);
                return ExportMovements(model, "تقرير الفترة", detail, model.DepartmentIds);
            }
            return Excel("سجل العقود", headers, rows);
        }

        private IActionResult ExportMovements(ReportsCenterModel model, string title, string detail, List<int> departmentIds)
        {
            var selectedDepartments = model.DepartmentIssues
                .Where(d => departmentIds.Count == 0 || departmentIds.Contains(d.ID)).ToList();
            var types = model.PaperTypes.Where(type => selectedDepartments.Any(d => d.Quantity(type) > 0)).ToList();
            if (detail == "departments")
            {
                var includeTotal = types.Count > 1 || types.Any(t => !string.Equals(t, "A4", StringComparison.OrdinalIgnoreCase));
                var headers = new[] { "الجهة", "تصنيف الجهة" }.Concat(types)
                    .Concat(includeTotal ? new[] { "إجمالي المصروف" } : Array.Empty<string>()).ToArray();
                var rows = selectedDepartments
                    .Select(d => new object?[] { d.Name, d.GroupName }.Concat(types.Select(t => (object?)d.Quantity(t)))
                        .Concat(includeTotal ? new object?[] { d.Total } : Array.Empty<object?>()).ToArray()).ToList();
                return Excel(title + " الجهات", headers, rows);
            }
            else
            {
                var headers = new[] { "نوع الورق", "الرصيد الافتتاحي", "المورد", "المصروف", "الرصيد الختامي / الحالي" };
                var rows = model.PaperTotals.Select(p => new object?[] { p.Name, p.Opening, p.Received, p.Issued,
                    p.Balance }).ToList();
                rows.Add(new object?[] { "إدارات وأفرع الشركة - المصروف", null, null, model.Group1Issued, null });
                rows.Add(new object?[] { "القطاعات والوحدات التابعة - المصروف", null, null, model.Group2Issued, null });
                return Excel(title, headers, rows);
            }
        }

        private static IActionResult Excel(string title, string[] headers, IEnumerable<object?[]> rows)
        {
            var bytes = SimpleXlsxWriter.Create(title, headers, rows);
            return new FileContentResult(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            { FileDownloadName = $"{(title.Contains("العقود") ? "Contracts" : "PaperReport")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx" };
        }

        private ReportsCenterModel NewModel(string tab, int? yearFrom, int? yearTo, int? contractId,
            string detail, int[]? departmentIds, string period, int? year, DateTime? fromDate, DateTime? toDate)
        {
            var m = new ReportsCenterModel
            {
                Tab = tab,
                YearFrom = yearFrom,
                YearTo = yearTo,
                ContractId = contractId,
                Detail = detail,
                DepartmentIds = departmentIds?.Distinct().ToList() ?? new List<int>(),
                Period = period,
                Year = year,
                FromDate = fromDate,
                ToDate = toDate
            };
            var contractsDt = _db.ExecuteQuery("sp_GetInventoryContracts");
            foreach (DataRow r in contractsDt.Rows)
            {
                var c = new ReportContract
                {
                    ContractID = Convert.ToInt32(r["ContractID"]),
                    Name = r["ContractName"]?.ToString() ?? "",
                    Number = r["ContractNumber"] == DBNull.Value ? "" : r["ContractNumber"].ToString() ?? "",
                    StartDate = Convert.ToDateTime(r["StartDate"]),
                    EndDate = r["EndDate"] == DBNull.Value ? null : Convert.ToDateTime(r["EndDate"]),
                    IsActive = Convert.ToBoolean(r["IsActive"]),
                    Opening = Number(r, "OpeningStock"),
                    Received = Number(r, "ReceivedQuantity"),
                    Issued = Number(r, "IssuedQuantity"),
                    Closing = r["ClosingStock"] == DBNull.Value ? null : Number(r, "ClosingStock"),
                    ContractedA4 = r.Table.Columns.Contains("QuantityA4") && r["QuantityA4"] != DBNull.Value ? Number(r, "QuantityA4") : null,
                    ContractedA3 = r.Table.Columns.Contains("QuantityA3") && r["QuantityA3"] != DBNull.Value ? Number(r, "QuantityA3") : null
                };
                m.Contracts.Add(c);
            }
            // The existing contract-list stored procedure may omit optional quantity/state
            // columns even when the contract history screen has them. Read them from the
            // contract header when present, without requiring any migration or edits to it.
            var optional = _db.ExecuteQuery(@"SELECT
                COL_LENGTH(N'dbo.InventoryContracts', N'QuantityA4') AS HasA4,
                COL_LENGTH(N'dbo.InventoryContracts', N'QuantityA3') AS HasA3,
                COL_LENGTH(N'dbo.InventoryContracts', N'ActivationState') AS HasActivationState");
            if (optional.Rows.Count > 0)
            {
                var flags = optional.Rows[0];
                var hasA4 = flags["HasA4"] != DBNull.Value;
                var hasA3 = flags["HasA3"] != DBNull.Value;
                var hasState = flags["HasActivationState"] != DBNull.Value;
                if (hasA4 || hasA3 || hasState)
                {
                    var fields = new List<string> { "ContractID" };
                    if (hasA4) fields.Add("QuantityA4");
                    if (hasA3) fields.Add("QuantityA3");
                    if (hasState) fields.Add("ActivationState");
                    var extras = _db.ExecuteQuery("SELECT " + string.Join(", ", fields) + " FROM dbo.InventoryContracts");
                    var byId = m.Contracts.ToDictionary(c => c.ContractID);
                    foreach (DataRow extra in extras.Rows)
                    {
                        if (!byId.TryGetValue(Convert.ToInt32(extra["ContractID"]), out var entry)) continue;
                        if (hasA4 && extra["QuantityA4"] != DBNull.Value) entry.ContractedA4 = Convert.ToInt64(extra["QuantityA4"]);
                        if (hasA3 && extra["QuantityA3"] != DBNull.Value) entry.ContractedA3 = Convert.ToInt64(extra["QuantityA3"]);
                        if (hasState) entry.Scheduled = string.Equals(extra["ActivationState"]?.ToString(), "Pending", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            var dt = _db.ExecuteQuery("SELECT PaperTypeID, PaperTypeName FROM dbo.PaperTypes ORDER BY PaperTypeID");
            m.PaperTypes = dt.Rows.Cast<DataRow>().Select(r => r["PaperTypeName"].ToString() ?? "").ToList();
            // These options follow actual registered history, including contracts dated in the future.
            var dates = _db.ExecuteQuery(@"
                SELECT MIN(YearNumber) AS Earliest, MAX(YearNumber) AS Latest FROM
                (
                    SELECT YEAR(StartDate) AS YearNumber FROM dbo.InventoryContracts
                    UNION ALL SELECT YEAR(dbo.fn_EgyptUtcToLocal(CONVERT(datetime2(7), ReceivedDate))) FROM dbo.InventoryReceipt
                    UNION ALL SELECT YEAR(dbo.fn_EgyptUtcToLocal(CONVERT(datetime2(7), IssueDate))) FROM dbo.PaperTransaction
                ) years WHERE YearNumber BETWEEN 1900 AND 2100");
            var todayYear = DateTime.Today.Year;
            var firstYear = dates.Rows.Count > 0 && dates.Rows[0]["Earliest"] != DBNull.Value
                ? Math.Min(Convert.ToInt32(dates.Rows[0]["Earliest"]), todayYear) : todayYear;
            var lastYear = dates.Rows.Count > 0 && dates.Rows[0]["Latest"] != DBNull.Value
                ? Math.Max(Convert.ToInt32(dates.Rows[0]["Latest"]), todayYear) : todayYear;
            m.YearOptions = Enumerable.Range(firstYear, lastYear - firstYear + 1).ToList();
            m.FirstYear = firstYear;
            var depts = _db.ExecuteQuery("SELECT DepartmentID, DepartmentName FROM dbo.Departments ORDER BY DepartmentID");
            m.Departments = depts.Rows.Cast<DataRow>().Select(r => new ReportDepartment
            { ID = Convert.ToInt32(r["DepartmentID"]), Name = r["DepartmentName"].ToString() ?? "" }).ToList();
            return m;
        }

        private static long Number(DataRow row, string column) =>
            row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToInt64(row[column]) : 0;

        private static bool ValidateContractYears(ReportsCenterModel m)
        {
            if (m.YearFrom.HasValue && (m.YearFrom < 2000 || m.YearFrom > 2100) ||
                m.YearTo.HasValue && (m.YearTo < 2000 || m.YearTo > 2100) ||
                m.YearFrom.HasValue && m.YearTo.HasValue && m.YearFrom > m.YearTo)
            { m.Error = "يرجى اختيار نطاق سنوات صحيح."; return false; }
            return true;
        }
        private static IEnumerable<ReportContract> FilterContracts(ReportsCenterModel m) => m.Contracts.Where(c =>
            (!m.YearFrom.HasValue || c.StartDate.Year >= m.YearFrom.Value) &&
            (!m.YearTo.HasValue || c.StartDate.Year <= m.YearTo.Value));

        private static bool ResolvePeriod(ReportsCenterModel m, out DateTime start, out DateTime finish)
        {
            start = finish = DateTime.MinValue;
            var thisYear = DateTime.Today.Year;
            var today = DateTime.Today;
            if (m.Period == "year")
            {
                var y = m.Year ?? thisYear;
                if (y < 2000 || y > 2100) { m.Error = "السنة غير صحيحة."; return false; }
                m.Year = y;
                start = new DateTime(y, 1, 1); finish = new DateTime(y, 12, 31);
                if (finish > today) finish = today;
            }
            else if (m.Period == "years")
            {
                if (!m.YearFrom.HasValue || !m.YearTo.HasValue || !ValidateContractYears(m))
                { m.Error = "اختر سنة البداية وسنة النهاية."; return false; }
                start = new DateTime(m.YearFrom.Value, 1, 1);
                finish = new DateTime(m.YearTo.Value, 12, 31);
                if (finish > today) finish = today;
            }
            else if (m.Period == "dates")
            {
                if (!m.FromDate.HasValue || !m.ToDate.HasValue)
                { m.Error = "اختر تاريخ البداية وتاريخ النهاية."; return false; }
                start = m.FromDate.Value.Date; finish = m.ToDate.Value.Date;
            }
            else { m.Error = "نوع الفترة غير صحيح."; return false; }
            if (start > finish) { m.Error = "تاريخ البداية يجب ألا يتجاوز تاريخ النهاية أو تاريخ اليوم."; return false; }
            return true;
        }

        private void LoadContractDetails(ReportsCenterModel m, int id)
        {
            m.SelectedContract = m.Contracts.First(c => c.ContractID == id);
            var dt = _db.ExecuteQuery(@"
                SELECT pt.PaperTypeName AS Name,
                    ISNULL(b.OpeningStock, 0) AS Opening,
                    ISNULL(ir.Qty, 0) AS Received,
                    ISNULL(tx.Qty, 0) AS Issued,
                    CASE WHEN c.IsActive = 0 AND c.ClosedUtc IS NOT NULL AND b.ClosingStock IS NOT NULL
                        THEN CONVERT(bigint, b.ClosingStock)
                        ELSE CONVERT(bigint, ISNULL(b.OpeningStock, 0)) + ISNULL(ir.Qty, 0) - ISNULL(tx.Qty, 0)
                    END AS Balance
                FROM dbo.InventoryContracts c CROSS JOIN dbo.PaperTypes pt
                LEFT JOIN dbo.InventoryContractBalances b ON b.ContractID = c.ContractID AND b.PaperTypeID = pt.PaperTypeID
                OUTER APPLY (SELECT SUM(CONVERT(bigint, QuantityReceived)) AS Qty FROM dbo.InventoryReceipt
                    WHERE InventoryContractID = c.ContractID AND PaperTypeID = pt.PaperTypeID) ir
                OUTER APPLY (SELECT SUM(CONVERT(bigint, QuantityIssued)) AS Qty FROM dbo.PaperTransaction
                    WHERE InventoryContractID = c.ContractID AND PaperTypeID = pt.PaperTypeID) tx
                WHERE c.ContractID = @ContractID ORDER BY pt.PaperTypeID",
                new SqlParameter("@ContractID", id));
            m.PaperTotals = ReadPaperTotals(dt);
            LoadDepartments(m, @"SELECT tx.DepartmentID, d.DepartmentName, pt.PaperTypeName,
                   SUM(CONVERT(bigint, tx.QuantityIssued)) AS Qty
                FROM dbo.PaperTransaction tx
                JOIN dbo.Departments d ON d.DepartmentID = tx.DepartmentID
                JOIN dbo.PaperTypes pt ON pt.PaperTypeID = tx.PaperTypeID
                WHERE tx.InventoryContractID = @ContractID
                GROUP BY tx.DepartmentID, d.DepartmentName, pt.PaperTypeName",
                new SqlParameter("@ContractID", id));
        }

        private void LoadPeriodDetails(ReportsCenterModel m, DateTime start, DateTime finish)
        {
            m.PeriodStart = start; m.PeriodEnd = finish;
            // Calendar reporting uses real receipt and issue dates, irrespective of contract assignment.
            // An opening/closing balance cannot be inferred reliably if historical inventory data was deleted;
            // therefore period reports deliberately show receipts/issues, not invented stock snapshots.
            var dt = _db.ExecuteQuery(@"
                SELECT pt.PaperTypeName AS Name, CONVERT(bigint,0) AS Opening,
                   ISNULL(ir.Qty, 0) AS Received, ISNULL(tx.Qty, 0) AS Issued,
                   CAST(NULL AS bigint) AS Balance
                FROM dbo.PaperTypes pt
                OUTER APPLY (SELECT SUM(CONVERT(bigint, QuantityReceived)) AS Qty FROM dbo.InventoryReceipt
                    WHERE PaperTypeID = pt.PaperTypeID AND ReceivedDate >= dbo.fn_EgyptDateStartUtc(@StartDate)
                    AND ReceivedDate < dbo.fn_EgyptDateStartUtc(DATEADD(day, 1, @FinishDate))) ir
                OUTER APPLY (SELECT SUM(CONVERT(bigint, QuantityIssued)) AS Qty FROM dbo.PaperTransaction
                    WHERE PaperTypeID = pt.PaperTypeID AND IssueDate >= dbo.fn_EgyptDateStartUtc(@StartDate)
                    AND IssueDate < dbo.fn_EgyptDateStartUtc(DATEADD(day, 1, @FinishDate))) tx
                ORDER BY pt.PaperTypeID",
                new SqlParameter("@StartDate", start), new SqlParameter("@FinishDate", finish));
            m.PaperTotals = ReadPaperTotals(dt);
            LoadDepartments(m, @"SELECT tx.DepartmentID, d.DepartmentName, pt.PaperTypeName,
                   SUM(CONVERT(bigint, tx.QuantityIssued)) AS Qty
                FROM dbo.PaperTransaction tx
                JOIN dbo.Departments d ON d.DepartmentID = tx.DepartmentID
                JOIN dbo.PaperTypes pt ON pt.PaperTypeID = tx.PaperTypeID
                WHERE tx.IssueDate >= dbo.fn_EgyptDateStartUtc(@StartDate)
                  AND tx.IssueDate < dbo.fn_EgyptDateStartUtc(DATEADD(day, 1, @FinishDate))
                GROUP BY tx.DepartmentID, d.DepartmentName, pt.PaperTypeName",
                new SqlParameter("@StartDate", start), new SqlParameter("@FinishDate", finish));
        }

        private static List<ReportPaperTotal> ReadPaperTotals(DataTable dt) => dt.Rows.Cast<DataRow>().Select(r =>
            new ReportPaperTotal
            {
                Name = r["Name"]?.ToString() ?? "",
                Opening = Number(r, "Opening"),
                Received = Number(r, "Received"),
                Issued = Number(r, "Issued"),
                Balance = r["Balance"] == DBNull.Value ? null : Number(r, "Balance")
            }).ToList();

        private void LoadDepartments(ReportsCenterModel m, string sql, params SqlParameter[] parameters)
        {
            var dt = _db.ExecuteQuery(sql, parameters);
            var groups = dt.Rows.Cast<DataRow>().GroupBy(r => Convert.ToInt32(r["DepartmentID"]));
            m.DepartmentIssues = groups.Select(g =>
            {
                var d = new ReportDepartment { ID = g.Key, Name = g.First()["DepartmentName"]?.ToString() ?? "" };
                foreach (var r in g) d.ByPaper[r["PaperTypeName"]?.ToString() ?? ""] = Number(r, "Qty");
                return d;
            }).OrderBy(d => d.ID).ToList();
            // Keep the same department grouping as the EXISTING official report. Its layout and code remain unchanged.
            m.Group1Issued = m.DepartmentIssues.Where(d => d.ID <= 24).Sum(d => d.Total);
            m.Group2Issued = m.DepartmentIssues.Where(d => d.ID > 24).Sum(d => d.Total);
        }
    }
}
