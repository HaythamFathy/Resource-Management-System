using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Resource_Management_System.Common;
using Resource_Management_System.Data;
using Resource_Management_System.Models;
using Resource_Management_System.Services;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace Resource_Management_System.Areas.PaperManagement.Controllers
{
    [Area("PaperManagement")]
    [Authorize]
    public class RequestController : Controller
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;
        private readonly QueueUpdateNotifier _queueUpdates;

        public RequestController(
            DbHelper db,
            IConfiguration config,
            QueueUpdateNotifier queueUpdates)
        {
            _db = db;
            _config = config;
            _queueUpdates = queueUpdates;
        }

        // GET: /PaperManagement/Request/Index
        // Main Landing / Admin Dashboard
        [Authorize]
        public IActionResult Index(
            RequestFilterViewModel filter,
            string? analyticsScope = null,
            List<int>? analyticsDepartmentIDs = null,
            int? analyticsPaperTypeID = null,
            string? analyticsPeriod = null,
            DateTime? analyticsFromDate = null,
            DateTime? analyticsToDate = null)
        {
            ActivateDueInventoryContractIfPossible();

            if (User.IsInRole(Roles.Department))
            {
                return RedirectToAction(nameof(DepartmentHome));
            }

            if (!User.IsInRole(Roles.IT) && !User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Approver))
            {
                return Forbid();
            }

            if (filter.FromDate.HasValue && filter.ToDate.HasValue &&
                filter.FromDate.Value > filter.ToDate.Value)
            {
                TempData["ErrorMessage"] = "يرجى تحديد تاريخ صحيح: تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.";
                filter.FromDate = null;
                filter.ToDate = null;
            }

            PopulateFilterLists(filter, includeStatusFilter: false);
            ViewBag.Filter = filter;

            // The database procedure intentionally caps one page at 200 rows.
            // Dashboard analytics need the complete matching set, so page through
            // the procedure instead of silently showing stats for only the first page.
            var mappedRequests = FetchAllFilteredRequestItems(filter);

            if (filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Any())
            {
                var allDeptCount = filter.Departments?.Count() ?? 0;
                if (allDeptCount > 0 && filter.SelectedDepartmentIDs.Count < allDeptCount)
                {
                    mappedRequests = mappedRequests.Where(r => filter.SelectedDepartmentIDs.Contains(r.DepartmentID)).ToList();
                }
            }

            int? deptIdParam = filter.DepartmentID;
            if (!deptIdParam.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
            {
                deptIdParam = filter.SelectedDepartmentIDs[0];
            }

            object toDateParam = (filter.ToDate.HasValue && filter.ToDate.Value.TimeOfDay == TimeSpan.Zero)
                ? (object)filter.ToDate.Value.Date.AddDays(1)
                : ((object?)filter.ToDate ?? DBNull.Value);

            var consumptionDt = _db.ExecuteQuery("sp_GetDepartmentConsumptionSummary",
                new SqlParameter("@DepartmentID", (object?)deptIdParam ?? DBNull.Value),
                new SqlParameter("@FromDate", (object?)filter.FromDate ?? DBNull.Value),
                new SqlParameter("@ToDate", toDateParam)
            );

            var consumptionList = new List<DepartmentConsumptionSummary>();
            foreach (DataRow row in consumptionDt.Rows)
            {
                var dId = Convert.ToInt32(row["DepartmentID"]);
                if (filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Any())
                {
                    var allDeptCount = filter.Departments?.Count() ?? 0;
                    if (allDeptCount > 0 && filter.SelectedDepartmentIDs.Count < allDeptCount && !filter.SelectedDepartmentIDs.Contains(dId))
                    {
                        continue;
                    }
                }

                consumptionList.Add(new DepartmentConsumptionSummary
                {
                    DepartmentID = dId,
                    DepartmentName = row["DepartmentName"].ToString() ?? "",
                    A4Quantity = GetInt(row, "A4Quantity", 0),
                    A3Quantity = GetInt(row, "A3Quantity", 0),
                    TotalQuantity = GetInt(row, "TotalQuantity", 0)
                });
            }

            var allConsumptionDt = _db.ExecuteQuery("sp_GetDepartmentConsumptionSummary",
                new SqlParameter("@DepartmentID", DBNull.Value),
                new SqlParameter("@FromDate", DBNull.Value),
                new SqlParameter("@ToDate", DBNull.Value)
            );

            var allConsumptionList = new List<DepartmentConsumptionSummary>();
            foreach (DataRow row in allConsumptionDt.Rows)
            {
                allConsumptionList.Add(new DepartmentConsumptionSummary
                {
                    DepartmentID = Convert.ToInt32(row["DepartmentID"]),
                    DepartmentName = row["DepartmentName"].ToString() ?? "",
                    A4Quantity = GetInt(row, "A4Quantity", 0),
                    A3Quantity = GetInt(row, "A3Quantity", 0),
                    TotalQuantity = GetInt(row, "TotalQuantity", 0)
                });
            }

            var inventoryDt = _db.ExecuteQuery("sp_GetInventoryStatus");
            var inventoryList = new List<InventoryStatusViewModel>();
            foreach (DataRow row in inventoryDt.Rows)
            {
                inventoryList.Add(new InventoryStatusViewModel
                {
                    PaperTypeID = Convert.ToInt32(row["PaperTypeID"]),
                    PaperTypeName = row["PaperTypeName"].ToString() ?? "",
                    CurrentStock = Convert.ToInt32(row["CurrentStock"]),
                    LowStockThreshold = row.Table.Columns.Contains("LowStockThreshold") && row["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(row["LowStockThreshold"]) : 50,
                    TotalReceived = row.Table.Columns.Contains("TotalReceived") && row["TotalReceived"] != DBNull.Value ? Convert.ToInt32(row["TotalReceived"]) : 0,
                    LastUpdated = row.Table.Columns.Contains("LastUpdated") && row["LastUpdated"] != DBNull.Value ? Convert.ToDateTime(row["LastUpdated"]) : DateTime.Now,
                    ActiveContractID = row.Table.Columns.Contains("ActiveContractID") && row["ActiveContractID"] != DBNull.Value
                        ? Convert.ToInt32(row["ActiveContractID"])
                        : null,
                    ActiveContractNumber = row.Table.Columns.Contains("ActiveContractNumber") && row["ActiveContractNumber"] != DBNull.Value
                        ? row["ActiveContractNumber"].ToString()
                        : null,
                    ActiveContractName = row.Table.Columns.Contains("ActiveContractName") && row["ActiveContractName"] != DBNull.Value
                        ? row["ActiveContractName"].ToString()
                        : null,
                    ContractStartDate = row.Table.Columns.Contains("ContractStartDate") && row["ContractStartDate"] != DBNull.Value
                        ? Convert.ToDateTime(row["ContractStartDate"])
                        : null,
                    OpeningStockUnderActiveContract = GetInt(row, "OpeningStockUnderActiveContract", 0),
                    ReceivedUnderActiveContract = GetInt(row, "ReceivedUnderActiveContract", 0),
                    IssuedUnderActiveContract = GetInt(row, "IssuedUnderActiveContract", 0)
                });
            }

            // System-wide snapshot is deliberately independent from the filters below.
            // This keeps the top of the dashboard useful as a real operational control centre.
            var allRequests = FetchAllFilteredRequestItems(new RequestFilterViewModel());
            var egyptNow = DateTime.UtcNow.ToEgyptTime();

            // Decision analytics has its own scope and time window. It deliberately does
            // not reuse the operational dashboard filter, so changing an analytics view
            // never destroys the user's report/drill-down selection.
            var validAnalyticsScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "all", "group1", "group2", "custom"
            };
            var normalizedAnalyticsScope = !string.IsNullOrWhiteSpace(analyticsScope) && validAnalyticsScopes.Contains(analyticsScope)
                ? analyticsScope.ToLowerInvariant()
                : "all";

            var analyticsDeptIds = (analyticsDepartmentIDs ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedAnalyticsScope == "custom" && analyticsDeptIds.Count == 0)
            {
                normalizedAnalyticsScope = "all";
            }

            var validAnalyticsPeriods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "week", "month", "3m", "6m", "12m", "year", "all", "custom"
            };
            var normalizedAnalyticsPeriod = !string.IsNullOrWhiteSpace(analyticsPeriod) && validAnalyticsPeriods.Contains(analyticsPeriod)
                ? analyticsPeriod.ToLowerInvariant()
                : "12m";

            var analyticsToday = egyptNow.Date;
            DateTime? analyticsEffectiveFrom = null;
            DateTime? analyticsEffectiveTo = analyticsToday;

            switch (normalizedAnalyticsPeriod)
            {
                case "week":
                    {
                        // Current business week in Egypt: Saturday through today.
                        // Do not show a future Friday as the end of the analytics window.
                        int daysSinceSaturday = ((int)analyticsToday.DayOfWeek + 1) % 7;
                        analyticsEffectiveFrom = analyticsToday.AddDays(-daysSinceSaturday);
                        analyticsEffectiveTo = analyticsToday;
                        break;
                    }
                case "month":
                    analyticsEffectiveFrom = new DateTime(analyticsToday.Year, analyticsToday.Month, 1);
                    break;
                case "3m":
                    analyticsEffectiveFrom = analyticsToday.AddMonths(-3).AddDays(1);
                    break;
                case "6m":
                    analyticsEffectiveFrom = analyticsToday.AddMonths(-6).AddDays(1);
                    break;
                case "12m":
                    analyticsEffectiveFrom = analyticsToday.AddMonths(-12).AddDays(1);
                    break;
                case "year":
                    analyticsEffectiveFrom = new DateTime(analyticsToday.Year, 1, 1);
                    break;
                case "all":
                    analyticsEffectiveFrom = null;
                    analyticsEffectiveTo = null;
                    break;
                case "custom":
                    analyticsEffectiveFrom = analyticsFromDate?.Date;
                    analyticsEffectiveTo = analyticsToDate?.Date ?? analyticsToday;

                    // Analytics is historical/current data. Clamp accidental future dates to today.
                    if (analyticsEffectiveFrom.HasValue && analyticsEffectiveFrom.Value > analyticsToday)
                        analyticsEffectiveFrom = analyticsToday;
                    if (analyticsEffectiveTo.HasValue && analyticsEffectiveTo.Value > analyticsToday)
                        analyticsEffectiveTo = analyticsToday;

                    if (analyticsEffectiveFrom.HasValue && analyticsEffectiveTo.HasValue && analyticsEffectiveFrom.Value > analyticsEffectiveTo.Value)
                    {
                        var temp = analyticsEffectiveFrom;
                        analyticsEffectiveFrom = analyticsEffectiveTo;
                        analyticsEffectiveTo = temp;
                    }
                    break;
            }

            bool AnalyticsScopeMatches(RequestItemViewModel r)
            {
                if (analyticsPaperTypeID.HasValue && r.PaperTypeID != analyticsPaperTypeID.Value)
                    return false;

                return normalizedAnalyticsScope switch
                {
                    "group1" => r.DepartmentID <= 24,
                    "group2" => r.DepartmentID > 24,
                    "custom" => analyticsDeptIds.Contains(r.DepartmentID),
                    _ => true
                };
            }

            bool AnalyticsDateMatches(DateTime localDate)
            {
                var date = localDate.Date;
                return (!analyticsEffectiveFrom.HasValue || date >= analyticsEffectiveFrom.Value)
                    && (!analyticsEffectiveTo.HasValue || date <= analyticsEffectiveTo.Value);
            }

            var analyticsCurrentRequests = allRequests
                .Where(AnalyticsScopeMatches)
                .ToList();

            // Request-based analytics always use the request creation date.
            // Issuance-based analytics are filtered separately in the dashboard view by IssuedDate.
            // This prevents a request from moving between periods merely because its status changed.
            var analyticsRequests = analyticsCurrentRequests
                .Where(r => AnalyticsDateMatches(r.RequestDate.ToEgyptTime()))
                .ToList();

            int pendingIT = allRequests.Count(r => r.Status == RequestStatus.PendingITReview || r.Status == RequestStatus.PendingITReReview);
            int pendingApproverOne = allRequests.Count(r => r.Status == RequestStatus.PendingApproverOne);
            int pendingApproverTwo = allRequests.Count(r => r.Status == RequestStatus.PendingApproverTwo);
            int pendingApprover = pendingApproverOne + pendingApproverTwo;
            int approvedCount = allRequests.Count(r => r.Status == RequestStatus.Approved);
            int rejectedCount = allRequests.Count(r => r.Status == RequestStatus.Rejected);
            int issuedRequestCount = allRequests.Count(r => r.Status == RequestStatus.Issued);
            int exceptionRequestCount = allRequests.Count(r => r.IsException);
            int exceptionIssuedQuantity = allRequests
                .Where(r => r.Status == RequestStatus.Issued && r.IsException)
                .Sum(r => r.RequestedQuantity);

            var issuedTodayRequests = allRequests
                .Where(r => r.Status == RequestStatus.Issued
                    && r.IssuedDate.HasValue
                    && r.IssuedDate.Value.ToEgyptTime().Date == egyptNow.Date)
                .ToList();

            var issuedMonthRequests = allRequests
                .Where(r => r.Status == RequestStatus.Issued
                    && r.IssuedDate.HasValue
                    && r.IssuedDate.Value.ToEgyptTime().Month == egyptNow.Month
                    && r.IssuedDate.Value.ToEgyptTime().Year == egyptNow.Year)
                .ToList();

            // Management requested yearly dashboard figures with a cumulative reference.
            // The yearly values automatically start again on 1 January, while the
            // cumulative values continue from the beginning of the system.
            var issuedYearRequests = allRequests
                .Where(r => r.Status == RequestStatus.Issued
                    && r.IssuedDate.HasValue
                    && r.IssuedDate.Value.ToEgyptTime().Year == egyptNow.Year)
                .ToList();

            int totalIssuedQuantity = allConsumptionList.Sum(x => x.TotalQuantity);
            int totalCurrentStock = inventoryList.Sum(x => x.CurrentStock);
            int companyBranchesIssuedTotal = allConsumptionList
                .Where(x => x.DepartmentID <= 24)
                .Sum(x => x.TotalQuantity);
            int sectorsUnitsIssuedTotal = allConsumptionList
                .Where(x => x.DepartmentID > 24)
                .Sum(x => x.TotalQuantity);

            var activeInventoryContractInfo = inventoryList.FirstOrDefault(x => x.HasActiveContract);
            int? activeInventoryContractId = activeInventoryContractInfo?.ActiveContractID;
            bool hasActiveInventoryContract = activeInventoryContractId.HasValue;
            var activeInventoryContractDisplay = !string.IsNullOrWhiteSpace(activeInventoryContractInfo?.ActiveContractNumber)
                ? activeInventoryContractInfo!.ActiveContractNumber!
                : (!string.IsNullOrWhiteSpace(activeInventoryContractInfo?.ActiveContractName)
                    ? activeInventoryContractInfo!.ActiveContractName!
                    : "لا يوجد عقد نشط");

            bool hasLastClosedInventoryContractForDashboard = false;
            int dashboardIssuedQuantity = 0;
            int dashboardA4IssuedQuantity = 0;
            int dashboardA3IssuedQuantity = 0;
            int dashboardCompanyBranchesIssuedQuantity = 0;
            int dashboardSectorsUnitsIssuedQuantity = 0;
            var departmentIssuedYearList = allConsumptionList
                .Select(department => new DepartmentConsumptionSummary
                {
                    DepartmentID = department.DepartmentID,
                    DepartmentName = department.DepartmentName,
                    TotalQuantity = 0,
                    A4Quantity = 0,
                    A3Quantity = 0
                })
                .ToList();

            if (hasActiveInventoryContract)
            {
                dashboardIssuedQuantity = inventoryList.Sum(x => x.IssuedUnderActiveContract);
                dashboardA4IssuedQuantity = inventoryList
                    .Where(x => string.Equals(x.PaperTypeName, "A4", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.IssuedUnderActiveContract);
                dashboardA3IssuedQuantity = inventoryList
                    .Where(x => string.Equals(x.PaperTypeName, "A3", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.IssuedUnderActiveContract);

                var dashboardIssuedByDepartmentDt = _db.ExecuteQuery("sp_GetInventoryContractConsumptionByDepartment",
                    new SqlParameter("@ContractID", activeInventoryContractId.Value));

                var issuedByDepartmentLookup = dashboardIssuedByDepartmentDt.Rows.Cast<DataRow>()
                    .ToDictionary(
                        row => Convert.ToInt32(row["DepartmentID"]),
                        row => new
                        {
                            A4Quantity = GetInt(row, "A4Quantity", 0),
                            A3Quantity = GetInt(row, "A3Quantity", 0),
                            TotalQuantity = GetInt(row, "TotalQuantity", 0)
                        });

                departmentIssuedYearList = allConsumptionList
                    .Select(department =>
                    {
                        issuedByDepartmentLookup.TryGetValue(department.DepartmentID, out var deptTotals);
                        return new DepartmentConsumptionSummary
                        {
                            DepartmentID = department.DepartmentID,
                            DepartmentName = department.DepartmentName,
                            TotalQuantity = deptTotals?.TotalQuantity ?? 0,
                            A4Quantity = deptTotals?.A4Quantity ?? 0,
                            A3Quantity = deptTotals?.A3Quantity ?? 0
                        };
                    })
                    .OrderByDescending(x => x.TotalQuantity)
                    .ThenBy(x => x.DepartmentName)
                    .ToList();

                dashboardCompanyBranchesIssuedQuantity = departmentIssuedYearList
                    .Where(x => x.DepartmentID <= 24)
                    .Sum(x => x.TotalQuantity);
                dashboardSectorsUnitsIssuedQuantity = departmentIssuedYearList
                    .Where(x => x.DepartmentID > 24)
                    .Sum(x => x.TotalQuantity);
            }
            else
            {
                // No active contract — fall back to the most recently closed contract so the
                // dashboard keeps showing meaningful figures until a new contract is started.
                var lastClosedDt = _db.ExecuteQuery("sp_GetLastClosedInventoryContract");
                if (lastClosedDt.Rows.Count > 0)
                {
                    var lcRow = lastClosedDt.Rows[0];
                    int lastClosedContractId = Convert.ToInt32(lcRow["ContractID"]);
                    string lastClosedDisplay = !string.IsNullOrWhiteSpace(lcRow["ContractNumber"]?.ToString())
                        ? lcRow["ContractNumber"]!.ToString()!
                        : (!string.IsNullOrWhiteSpace(lcRow["ContractName"]?.ToString())
                            ? lcRow["ContractName"]!.ToString()!
                            : "آخر عقد مغلق");
                    activeInventoryContractDisplay = lastClosedDisplay;
                    hasLastClosedInventoryContractForDashboard = true;

                    // Display the last closed contract until a NEW contract actually becomes active.
                    // This changes dashboard presentation only: the physical inventory is always live.
                    // A newly registered future contract does NOT reset these displayed figures yet.
                    var lastClosedBalancesDt = _db.ExecuteQuery("sp_GetInventoryContractBalances",
                        new SqlParameter("@ContractID", lastClosedContractId));
                    foreach (DataRow balanceRow in lastClosedBalancesDt.Rows)
                    {
                        int paperTypeId = Convert.ToInt32(balanceRow["PaperTypeID"]);
                        var inventoryItem = inventoryList.FirstOrDefault(x => x.PaperTypeID == paperTypeId);
                        if (inventoryItem == null) continue;
                        inventoryItem.ReceivedUnderActiveContract = GetInt(balanceRow, "ReceivedQuantity", 0);
                        inventoryItem.IssuedUnderActiveContract = GetInt(balanceRow, "IssuedQuantity", 0);
                    }

                    dashboardIssuedQuantity = inventoryList.Sum(x => x.IssuedUnderActiveContract);
                    dashboardA4IssuedQuantity = inventoryList
                        .Where(x => string.Equals(x.PaperTypeName, "A4", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.IssuedUnderActiveContract);
                    dashboardA3IssuedQuantity = inventoryList
                        .Where(x => string.Equals(x.PaperTypeName, "A3", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.IssuedUnderActiveContract);

                    var fallbackDt = _db.ExecuteQuery("sp_GetInventoryContractConsumptionByDepartment",
                        new SqlParameter("@ContractID", lastClosedContractId));

                    var fallbackLookup = fallbackDt.Rows.Cast<DataRow>()
                        .ToDictionary(
                            row => Convert.ToInt32(row["DepartmentID"]),
                            row => new
                            {
                                A4Quantity = GetInt(row, "A4Quantity", 0),
                                A3Quantity = GetInt(row, "A3Quantity", 0),
                                TotalQuantity = GetInt(row, "TotalQuantity", 0)
                            });

                    departmentIssuedYearList = allConsumptionList
                        .Select(department =>
                        {
                            fallbackLookup.TryGetValue(department.DepartmentID, out var deptTotals);
                            return new DepartmentConsumptionSummary
                            {
                                DepartmentID = department.DepartmentID,
                                DepartmentName = department.DepartmentName,
                                TotalQuantity = deptTotals?.TotalQuantity ?? 0,
                                A4Quantity = deptTotals?.A4Quantity ?? 0,
                                A3Quantity = deptTotals?.A3Quantity ?? 0
                            };
                        })
                        .OrderByDescending(x => x.TotalQuantity)
                        .ThenBy(x => x.DepartmentName)
                        .ToList();

                    dashboardCompanyBranchesIssuedQuantity = departmentIssuedYearList
                        .Where(x => x.DepartmentID <= 24)
                        .Sum(x => x.TotalQuantity);
                    dashboardSectorsUnitsIssuedQuantity = departmentIssuedYearList
                        .Where(x => x.DepartmentID > 24)
                        .Sum(x => x.TotalQuantity);
                }
                else
                {
                    departmentIssuedYearList = departmentIssuedYearList
                        .OrderBy(x => x.DepartmentName)
                        .ToList();
                }
            }

            ViewBag.DashboardHasActiveInventoryContract = hasActiveInventoryContract;
            ViewBag.DashboardHasClosedInventoryContract = hasLastClosedInventoryContractForDashboard;
            ViewBag.DashboardActiveInventoryContractDisplay = activeInventoryContractDisplay;

            var model = new DashboardViewModel
            {
                PendingITCount = pendingIT,
                PendingApproverCount = pendingApprover,
                PendingApproverOneCount = pendingApproverOne,
                PendingApproverTwoCount = pendingApproverTwo,
                ApprovedCount = approvedCount,
                TotalRequestCount = allRequests.Count,
                IssuedRequestCount = issuedRequestCount,
                RejectedCount = rejectedCount,
                ExceptionRequestCount = exceptionRequestCount,
                ExceptionIssuedQuantity = exceptionIssuedQuantity,
                TotalIssuedQuantity = totalIssuedQuantity,
                DepartmentCount = allConsumptionList.Count,
                LowStockCount = inventoryList.Count(x => x.IsLowStock),
                TotalCurrentStock = totalCurrentStock,
                IssuedTodayCount = issuedTodayRequests.Count,
                IssuedTodayQuantity = issuedTodayRequests.Sum(r => r.RequestedQuantity),
                IssuedMonthCount = issuedMonthRequests.Count,
                IssuedMonthQuantity = issuedMonthRequests.Sum(r => r.RequestedQuantity),
                IssuedYearQuantity = dashboardIssuedQuantity,
                A4IssuedYearQuantity = dashboardA4IssuedQuantity,
                A3IssuedYearQuantity = dashboardA3IssuedQuantity,
                CompanyBranchesIssuedYearQuantity = dashboardCompanyBranchesIssuedQuantity,
                SectorsUnitsIssuedYearQuantity = dashboardSectorsUnitsIssuedQuantity,
                CompanyBranchesIssuedTotalQuantity = companyBranchesIssuedTotal,
                SectorsUnitsIssuedTotalQuantity = sectorsUnitsIssuedTotal,
                FilteredRequestCount = mappedRequests.Count,
                FilteredIssuedQuantity = mappedRequests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .Sum(r => r.RequestedQuantity),
                Filter = filter,
                DepartmentConsumption = consumptionList,
                DepartmentConsumptionAll = allConsumptionList,
                DepartmentIssuedYear = departmentIssuedYearList,
                InventoryList = inventoryList,
                AnalyticsRequests = analyticsRequests,
                AnalyticsCurrentRequests = analyticsCurrentRequests,
                AnalyticsScope = normalizedAnalyticsScope,
                AnalyticsDepartmentIDs = analyticsDeptIds,
                AnalyticsPaperTypeID = analyticsPaperTypeID,
                AnalyticsPeriod = normalizedAnalyticsPeriod,
                AnalyticsCustomFromDate = analyticsFromDate?.Date,
                AnalyticsCustomToDate = analyticsToDate?.Date,
                AnalyticsEffectiveFromDate = analyticsEffectiveFrom,
                AnalyticsEffectiveToDate = analyticsEffectiveTo,
                RecentRequests = mappedRequests
                    .OrderByDescending(r => r.RequestDate)
                    .ThenByDescending(r => r.RequestID)
                    .Take(12)
                    .ToList()
            };

            return View("Index", model);
        }

        // GET: /PaperManagement/Request/ExportDashboardToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ExportDashboardToExcel(RequestFilterViewModel filter)
        {
            int? deptIdParam = filter.DepartmentID;
            if (!deptIdParam.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
            {
                deptIdParam = filter.SelectedDepartmentIDs[0];
            }

            object toDateParam = (filter.ToDate.HasValue && filter.ToDate.Value.TimeOfDay == TimeSpan.Zero)
                ? (object)filter.ToDate.Value.Date.AddDays(1)
                : ((object?)filter.ToDate ?? DBNull.Value);

            var consumptionDt = _db.ExecuteQuery("sp_GetDepartmentConsumptionSummary",
                new SqlParameter("@DepartmentID", (object?)deptIdParam ?? DBNull.Value),
                new SqlParameter("@FromDate", (object?)filter.FromDate ?? DBNull.Value),
                new SqlParameter("@ToDate", toDateParam)
            );

            var headers = new[] { "كود القسم", "اسم القسم / الإدارة", "ورق A4 (رزمة)", "ورق A3 (رزمة)", "إجمالي المصروف (رزمة)" };
            var rows = new List<object?[]>();
            foreach (DataRow row in consumptionDt.Rows)
            {
                var dId = Convert.ToInt32(row["DepartmentID"]);
                if (filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Any())
                {
                    if (!filter.SelectedDepartmentIDs.Contains(dId))
                        continue;
                }
                rows.Add(new object?[]
                {
                    dId,
                    row["DepartmentName"].ToString(),
                    GetInt(row, "A4Quantity", 0),
                    GetInt(row, "A3Quantity", 0),
                    GetInt(row, "TotalQuantity", 0)
                });
            }

            var content = SimpleXlsxWriter.Create("استهلاك الأقسام", headers, rows);
            var fileName = $"Department_Consumption_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: /PaperManagement/Request/ExportSelectedDepartmentsLookupToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult ExportSelectedDepartmentsLookupToExcel([FromQuery] List<int>? departmentIds)
        {
            var selectedIds = (departmentIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .Take(200)
                .ToList();

            if (selectedIds.Count == 0)
            {
                return BadRequest("يرجى اختيار جهة واحدة على الأقل للتصدير.");
            }

            var departmentNames = new Dictionary<int, string>();
            foreach (var option in GetDepartmentsSelectList())
            {
                if (int.TryParse(option.Value, out int id) && selectedIds.Contains(id))
                {
                    departmentNames[id] = option.Text ?? string.Empty;
                }
            }

            var allRequests = FetchAllFilteredRequestItems(new RequestFilterViewModel
            {
                SelectedDepartmentIDs = selectedIds
            });

            var groupedRequests = allRequests
                .Where(r => selectedIds.Contains(r.DepartmentID))
                .GroupBy(r => r.DepartmentID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var headers = new[]
            {
                "الجهة",
                "إجمالي المصروف",
                "A4",
                "A3",
                "آخر صرف"
            };

            var rows = new List<object?[]>();
            foreach (var departmentId in selectedIds)
            {
                groupedRequests.TryGetValue(departmentId, out var departmentRequests);
                departmentRequests ??= new List<RequestItemViewModel>();

                var issuedRequests = departmentRequests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .ToList();

                int totalIssued = issuedRequests.Sum(r => r.RequestedQuantity);
                int a4Issued = issuedRequests
                    .Where(r => string.Equals((r.PaperTypeName ?? string.Empty).Trim(), "A4", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.RequestedQuantity);
                int a3Issued = issuedRequests
                    .Where(r => string.Equals((r.PaperTypeName ?? string.Empty).Trim(), "A3", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.RequestedQuantity);

                var lastIssued = issuedRequests
                    .OrderByDescending(r => r.IssuedDate ?? r.RequestDate)
                    .ThenByDescending(r => r.RequestID)
                    .FirstOrDefault();

                string lastIssuedDate = string.Empty;
                if (lastIssued != null)
                {
                    var issuedAt = lastIssued.IssuedDate ?? lastIssued.RequestDate;
                    lastIssuedDate = issuedAt.ToEgyptTime().ToString("yyyy-MM-dd");
                }

                string departmentName = departmentNames.TryGetValue(departmentId, out var name)
                    ? name
                    : departmentRequests.FirstOrDefault()?.DepartmentName ?? $"جهة #{departmentId}";

                rows.Add(new object?[]
                {
                    departmentName,
                    totalIssued,
                    a4Issued,
                    a3Issued,
                    lastIssuedDate
                });
            }

            var content = SimpleXlsxWriter.Create("بيان المصروف", headers, rows);
            var fileName = $"Selected_Departments_Issued_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: /PaperManagement/Request/ApproverDashboard
        // Backward-compatible route: approvers now use the exact same dashboard as IT/Admin.
        [Authorize(Roles = Roles.Approver)]
        public IActionResult ApproverDashboard(
            RequestFilterViewModel filter,
            string? analyticsScope = null,
            List<int>? analyticsDepartmentIDs = null,
            int? analyticsPaperTypeID = null,
            string? analyticsPeriod = null,
            DateTime? analyticsFromDate = null,
            DateTime? analyticsToDate = null)
        {
            return Index(
                filter,
                analyticsScope,
                analyticsDepartmentIDs,
                analyticsPaperTypeID,
                analyticsPeriod,
                analyticsFromDate,
                analyticsToDate);
        }

        // GET: /PaperManagement/Request/DepartmentHome
        // Simple, task-first landing page for Department users only.
        [HttpGet]
        [Authorize(Roles = Roles.Department)]
        public IActionResult DepartmentHome()
        {
            if (!int.TryParse(User.FindFirst("DepartmentID")?.Value, out int departmentId) || departmentId <= 0)
            {
                return Forbid();
            }

            var requests = FetchAllFilteredRequestItems(new RequestFilterViewModel
            {
                DepartmentID = departmentId
            })
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.RequestID)
                .ToList();

            string departmentName = requests.FirstOrDefault()?.DepartmentName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                departmentName = GetDepartmentsSelectList()
                    .FirstOrDefault(d => d.Value == departmentId.ToString())?.Text ?? "الجهة";
            }

            var quotaRows = new List<DepartmentQuotaSummaryViewModel>();
            foreach (var paperType in GetPaperTypesSelectList())
            {
                if (!int.TryParse(paperType.Value, out int paperTypeId)) continue;

                var quotaDt = _db.ExecuteQuery(
                    "sp_GetDepartmentPaperQuotaStatus",
                    new SqlParameter("@DepartmentID", departmentId),
                    new SqlParameter("@PaperTypeID", paperTypeId));

                quotaRows.Add(new DepartmentQuotaSummaryViewModel
                {
                    PaperTypeID = paperTypeId,
                    PaperTypeName = paperType.Text ?? string.Empty,
                    QuotaLimit = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "QuotaLimit", 0) : 0,
                    UsedQuota = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "UsedQuota", 0) : 0,
                    RemainingQuota = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "RemainingQuota", 0) : 0
                });
            }

            var egyptNow = DateTime.UtcNow.ToEgyptTime();
            var issuedThisMonth = requests
                .Where(r => r.Status == RequestStatus.Issued
                    && r.IssuedDate.HasValue
                    && r.IssuedDate.Value.ToEgyptTime().Year == egyptNow.Year
                    && r.IssuedDate.Value.ToEgyptTime().Month == egyptNow.Month)
                .ToList();

            bool IsDepartmentPending(string status) =>
                status == RequestStatus.PendingITReview ||
                status == RequestStatus.PendingITReReview ||
                status == RequestStatus.PendingApproverOne ||
                status == RequestStatus.PendingApproverTwo;

            return View(new DepartmentHomeViewModel
            {
                DepartmentID = departmentId,
                DepartmentName = departmentName,
                TotalRequestCount = requests.Count,
                PendingRequestCount = requests.Count(r => IsDepartmentPending(r.Status)),
                ReadyToIssueRequestCount = requests.Count(r => r.Status == RequestStatus.Approved),
                IssuedRequestCount = requests.Count(r => r.Status == RequestStatus.Issued),
                RejectedRequestCount = requests.Count(r => r.Status == RequestStatus.Rejected || r.Status == "RejectedByIT"),
                IssuedThisMonthCount = issuedThisMonth.Count,
                IssuedThisMonthQuantity = issuedThisMonth.Sum(r => r.RequestedQuantity),
                Quotas = quotaRows,
                RecentRequests = requests.Take(6).ToList()
            });
        }

        // GET: /PaperManagement/Request/Create
        [HttpGet]
        [Authorize(Roles = Roles.DepartmentOrITOrAdmin)]
        public IActionResult Create()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var availablePapers = GetPaperTypesSelectList(excludeA3: true).ToList();
            var defaultPaper = availablePapers.FirstOrDefault();
            int defaultPaperId = defaultPaper != null && int.TryParse(defaultPaper.Value, out int pid) ? pid : 1;

            var model = new NewRequestViewModel
            {
                PaperTypeID = defaultPaperId,
                PaperTypes = availablePapers
            };

            if (userRole != Roles.Department)
            {
                model.Departments = GetDepartmentsSelectList();
            }

            return View(model);
        }

        // AJAX helper for Create.cshtml
        [HttpGet]
        [Authorize(Roles = Roles.DepartmentOrITOrAdmin)]
        public IActionResult GetPaperQuotaStatus(
            int? departmentId,
            int paperTypeId)
        {
            if (paperTypeId <= 0)
            {
                return Json(new
                {
                    quotaLimit = 0,
                    usedQuota = 0,
                    remainingQuota = 0
                });
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            int targetDepartmentId = 0;

            if (userRole == Roles.Department)
            {
                if (!int.TryParse(User.FindFirst("DepartmentID")?.Value, out targetDepartmentId))
                {
                    return Json(new
                    {
                        quotaLimit = 0,
                        usedQuota = 0,
                        remainingQuota = 0
                    });
                }
            }
            else
            {
                if (!departmentId.HasValue || departmentId.Value <= 0)
                {
                    return Json(new
                    {
                        quotaLimit = 0,
                        usedQuota = 0,
                        remainingQuota = 0
                    });
                }
                targetDepartmentId = departmentId.Value;
            }

            try
            {
                var dt = _db.ExecuteQuery(
                    "sp_GetDepartmentPaperQuotaStatus",
                    new SqlParameter("@DepartmentID", targetDepartmentId),
                    new SqlParameter("@PaperTypeID", paperTypeId)
                );

                if (dt.Rows.Count == 0)
                {
                    return Json(new
                    {
                        quotaLimit = 0,
                        usedQuota = 0,
                        remainingQuota = 0
                    });
                }

                var row = dt.Rows[0];
                return Json(new
                {
                    quotaLimit = Convert.ToInt32(row["QuotaLimit"]),
                    usedQuota = Convert.ToInt32(row["UsedQuota"]),
                    remainingQuota = Convert.ToInt32(row["RemainingQuota"])
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "تعذر تحميل بيانات الحصة حالياً." });
            }
        }

        // POST: /PaperManagement/Request/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.DepartmentOrITOrAdmin)]
        public IActionResult Create(NewRequestViewModel model)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            // Default to first non-A3 paper (A4) if PaperTypeID was not set
            if (model.PaperTypeID <= 0)
            {
                var availablePapers = GetPaperTypesSelectList(excludeA3: true).ToList();
                var defaultPaper = availablePapers.FirstOrDefault();
                if (defaultPaper != null && int.TryParse(defaultPaper.Value, out int pid))
                {
                    model.PaperTypeID = pid;
                }
            }

            // Reject A3 paper from normal requests
            var allPaperTypes = GetPaperTypesSelectList(excludeA3: false);
            var selectedType = allPaperTypes.FirstOrDefault(p => p.Value == model.PaperTypeID.ToString());
            if (selectedType != null && IsA3Paper(selectedType.Text))
            {
                ModelState.AddModelError(nameof(model.PaperTypeID), "ورق A3 متاح فقط عبر طلبات الاستثناء.");
            }

            if (!ModelState.IsValid)
            {
                model.PaperTypes = GetPaperTypesSelectList(excludeA3: true);
                if (userRole != Roles.Department) model.Departments = GetDepartmentsSelectList();
                return View(model);
            }

            int departmentId;
            if (userRole == Roles.Department)
            {
                if (!int.TryParse(User.FindFirst("DepartmentID")?.Value, out departmentId))
                {
                    ModelState.AddModelError("", "لا يمكن تحديد القسم الخاص بحسابك.");
                    model.PaperTypes = GetPaperTypesSelectList(excludeA3: true);
                    return View(model);
                }
            }
            else
            {
                if (!model.DepartmentID.HasValue || model.DepartmentID.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.DepartmentID), "يرجى اختيار القسم المستفيد");
                    model.PaperTypes = GetPaperTypesSelectList(excludeA3: true);
                    model.Departments = GetDepartmentsSelectList();
                    return View(model);
                }
                departmentId = model.DepartmentID.Value;
            }

            try
            {
                var newIdParam = new SqlParameter("@NewRequestID", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                var submissionKey = model.SubmissionKey == Guid.Empty ? Guid.NewGuid() : model.SubmissionKey;

                _db.ExecuteNonQuery("sp_SubmitRequest",
                    new SqlParameter("@DepartmentID", departmentId),
                    new SqlParameter("@PaperTypeID", model.PaperTypeID),
                    new SqlParameter("@RequestedQuantity", model.RequestedQuantity),
                    new SqlParameter("@Notes", (object?)model.Notes ?? DBNull.Value),
                    newIdParam,
                    new SqlParameter("@SubmittedByID", GetCurrentUserId()),
                    new SqlParameter("@SubmissionKey", submissionKey)
                );

                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.MyRequests);

                TempData["SuccessMessage"] = "تم تقديم طلب صرف الورق بنجاح وإرساله للمراجعة والاعتماد.";
                return userRole == Roles.Department
                    ? RedirectToAction(nameof(DepartmentHome))
                    : RedirectToAction(nameof(Create));
            }
            catch (SqlException ex)
            {
                // sp_SubmitRequest throws specific error numbers (see ScriptV15.sql / ScriptV19.sql):
                //   51002 = department paper-type quota would be exceeded
                //   51005 = A3 paper is only available via exception requests
                ModelState.AddModelError(string.Empty, ex.Number switch
                {
                    51002 => "الكمية المطلوبة تتجاوز الحصة الشهرية المتبقية للجهة. يمكنك تقديم طلب استثناء عند نفاد الحصة الشهرية عبر طلب استثناء.",
                    51005 => "ورق A3 متاح فقط عبر طلبات الاستثناء.",
                    _ => "تعذر تقديم طلب الصرف حالياً. يرجى المحاولة لاحقاً."
                });

                model.PaperTypes = GetPaperTypesSelectList(excludeA3: true);
                if (userRole != Roles.Department) model.Departments = GetDepartmentsSelectList();
                return View(model);
            }
        }

        // GET: /PaperManagement/Request/ExceptionRequest
        [HttpGet]
        [Authorize(Roles = Roles.DepartmentOrITOrAdmin)]
        public IActionResult ExceptionRequest()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var model = new NewRequestViewModel { PaperTypes = GetPaperTypesSelectList(excludeA3: false) };
            if (userRole != Roles.Department)
            {
                model.Departments = GetDepartmentsSelectList();
            }
            return View(model);
        }

        // POST: /PaperManagement/Request/ExceptionRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.DepartmentOrITOrAdmin)]
        public IActionResult ExceptionRequest(NewRequestViewModel model)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(model.Notes))
            {
                ModelState.AddModelError(nameof(model.Notes), "يجب توضيح سبب طلب الاستثناء.");
            }

            int departmentId = 0;
            if (userRole == Roles.Department)
            {
                if (!int.TryParse(User.FindFirst("DepartmentID")?.Value, out departmentId))
                {
                    ModelState.AddModelError("", "لا يمكن تحديد القسم الخاص بحسابك.");
                }
            }
            else
            {
                if (!model.DepartmentID.HasValue || model.DepartmentID.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.DepartmentID), "يرجى اختيار القسم المستفيد");
                }
                else
                {
                    departmentId = model.DepartmentID.Value;
                }
            }

            if (!ModelState.IsValid)
            {
                model.PaperTypes = GetPaperTypesSelectList(excludeA3: false);
                if (userRole != Roles.Department) model.Departments = GetDepartmentsSelectList();
                return View(model);
            }

            // Check if requested paper is A3 (A3 is exclusively requested via ExceptionRequest,
            // so it is not subject to normal monthly quota exhaustion)
            var paperTypes = GetPaperTypesSelectList(excludeA3: false);
            var selectedPaper = paperTypes.FirstOrDefault(p => p.Value == model.PaperTypeID.ToString());
            bool isA3 = selectedPaper != null && IsA3Paper(selectedPaper.Text);

            if (!isA3)
            {
                // Check if department has remaining quota before accepting exception request for standard papers (e.g. A4)
                var quotaDt = _db.ExecuteQuery("sp_GetDepartmentPaperQuotaStatus",
                    new SqlParameter("@DepartmentID", departmentId),
                    new SqlParameter("@PaperTypeID", model.PaperTypeID));
                if (quotaDt.Rows.Count > 0)
                {
                    int remaining = Convert.ToInt32(quotaDt.Rows[0]["RemainingQuota"]);
                    if (remaining > 0)
                    {
                        ModelState.AddModelError(string.Empty, $"لا يمكن تقديم طلب استثناء لهذا القسم لأن الحصة الشهرية المقررة لم تنفد بعد (المتبقي من الحصة: {remaining} {QuantityHelper.RezmaUnit(remaining)}). يرجى تقديم طلب صرف عادي.");
                        model.PaperTypes = GetPaperTypesSelectList(excludeA3: false);
                        if (userRole != Roles.Department) model.Departments = GetDepartmentsSelectList();
                        return View(model);
                    }
                }
            }

            try
            {
                var newIdParam = new SqlParameter("@NewRequestID", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var submissionKey = model.SubmissionKey == Guid.Empty ? Guid.NewGuid() : model.SubmissionKey;
                _db.ExecuteNonQuery("sp_SubmitExceptionRequest",
                    new SqlParameter("@DepartmentID", departmentId),
                    new SqlParameter("@PaperTypeID", model.PaperTypeID),
                    new SqlParameter("@RequestedQuantity", model.RequestedQuantity),
                    new SqlParameter("@Notes", model.Notes),
                    newIdParam,
                    new SqlParameter("@SubmittedByID", GetCurrentUserId()),
                    new SqlParameter("@SubmissionKey", submissionKey));

                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.MyRequests);

                TempData["SuccessMessage"] = "تم تقديم طلب الاستثناء بنجاح وهو قيد المراجعة.";
                return userRole == Roles.Department
                    ? RedirectToAction(nameof(DepartmentHome))
                    : RedirectToAction(nameof(ReviewQueue));
            }
            catch (SqlException ex)
            {
                // sp_SubmitExceptionRequest throws 51043 specifically when the
                // department's monthly quota is not yet exhausted (see ScriptV15.sql).
                // Matching on ex.Number instead of ex.Message text is more robust,
                // since the Arabic message wording can change independently of the
                // error number.
                ModelState.AddModelError("", ex.Number switch
                {
                    51043 => "لا يمكن تقديم طلب استثناء قبل استنفاد الحصة الشهرية.",
                    _ => "تعذر تقديم طلب الاستثناء حالياً. يرجى المحاولة لاحقاً."
                });
                model.PaperTypes = GetPaperTypesSelectList(excludeA3: false);
                if (userRole != Roles.Department) model.Departments = GetDepartmentsSelectList();
                return View(model);
            }
        }

        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult DepartmentHistory(int departmentId, int? paperTypeId)
        {
            var dt = _db.ExecuteQuery("sp_GetDepartmentRequestHistory",
                new SqlParameter("@DepartmentID", departmentId),
                new SqlParameter("@PaperTypeID", (object?)paperTypeId ?? DBNull.Value));

            var rows = dt.AsEnumerable().Select(row =>
            {
                var status = row["Status"].ToString() ?? "";
                var badge = Common.StatusDisplayHelper.GetBadgeInfo(status);
                return new
                {
                    requestID = Convert.ToInt32(row["RequestID"]),
                    paperTypeName = row["PaperTypeName"].ToString(),
                    requestedQuantity = Convert.ToInt32(row["RequestedQuantity"]),
                    isException = dt.Columns.Contains("IsException")
                        && row["IsException"] != DBNull.Value
                        && Convert.ToBoolean(row["IsException"]),
                    statusLabel = badge.Label,
                    statusClasses = badge.CssClasses,
                    requestDate = Convert.ToDateTime(row["RequestDate"]).ToString("yyyy/MM/dd")
                };
            }).ToList();

            int total = dt.Rows.Count > 0 && dt.Columns.Contains("TotalQuantity")
                ? Convert.ToInt32(dt.Rows[0]["TotalQuantity"])
                : rows.Sum(r => r.requestedQuantity);

            int issued = dt.Rows.Count > 0 && dt.Columns.Contains("IssuedQuantity")
                ? Convert.ToInt32(dt.Rows[0]["IssuedQuantity"])
                : dt.AsEnumerable()
                    .Where(row => row["Status"].ToString() == RequestStatus.Issued)
                    .Sum(row => Convert.ToInt32(row["RequestedQuantity"]));

            return Json(new { rows, total, issued });
        }

        // GET: /PaperManagement/Request/GetDepartmentDashboardDetail
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult GetDepartmentDashboardDetail(int departmentId, DateTime? fromDate, DateTime? toDate)
        {
            if (departmentId <= 0)
            {
                return BadRequest(new { success = false, message = "يرجى تحديد جهة صحيحة." });
            }

            try
            {
                // Full department history is used for the service-desk snapshot
                // (latest request, current workflow position, totals, etc.).
                var allRequests = FetchAllFilteredRequestItems(new RequestFilterViewModel
                {
                    DepartmentID = departmentId
                })
                    .OrderByDescending(r => r.RequestDate)
                    .ThenByDescending(r => r.RequestID)
                    .ToList();

                // The rows shown in the expandable analytics panel may optionally
                // respect the dashboard's date range.
                List<RequestItemViewModel> requests;
                if (fromDate.HasValue || toDate.HasValue)
                {
                    requests = FetchAllFilteredRequestItems(new RequestFilterViewModel
                    {
                        DepartmentID = departmentId,
                        FromDate = fromDate,
                        ToDate = toDate
                    })
                        .OrderByDescending(r => r.RequestDate)
                        .ThenByDescending(r => r.RequestID)
                        .ToList();
                }
                else
                {
                    requests = allRequests;
                }

                string? departmentName = allRequests.FirstOrDefault()?.DepartmentName;
                if (string.IsNullOrWhiteSpace(departmentName))
                {
                    departmentName = GetDepartmentsSelectList()
                        .FirstOrDefault(d => d.Value == departmentId.ToString())?.Text ?? "";
                }

                // Current-month quota is paper-type specific. Returning every type
                // avoids misleading users with whichever quota happened to appear
                // on the first request row.
                var quotaRows = new List<object>();
                int quotaLimit = 0;
                int usedQuota = 0;
                int remainingQuota = 0;

                foreach (var paperType in GetPaperTypesSelectList())
                {
                    if (!int.TryParse(paperType.Value, out int paperTypeId)) continue;

                    var quotaDt = _db.ExecuteQuery(
                        "sp_GetDepartmentPaperQuotaStatus",
                        new SqlParameter("@DepartmentID", departmentId),
                        new SqlParameter("@PaperTypeID", paperTypeId));

                    int typeLimit = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "QuotaLimit", 0) : 0;
                    int typeUsed = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "UsedQuota", 0) : 0;
                    int typeRemaining = quotaDt.Rows.Count > 0 ? GetInt(quotaDt.Rows[0], "RemainingQuota", 0) : 0;

                    quotaLimit += typeLimit;
                    usedQuota += typeUsed;
                    remainingQuota += typeRemaining;

                    quotaRows.Add(new
                    {
                        paperTypeId,
                        paperTypeName = paperType.Text ?? "",
                        quotaLimit = typeLimit,
                        usedQuota = typeUsed,
                        remainingQuota = typeRemaining,
                        usagePercent = typeLimit > 0
                            ? Math.Min(100, (int)Math.Round(typeUsed * 100d / typeLimit))
                            : 0
                    });
                }

                int totalIssued = requests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .Sum(r => r.RequestedQuantity);

                int lifetimeIssued = allRequests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .Sum(r => r.RequestedQuantity);

                // Lifetime issued totals by paper type are returned for the compact
                // printable department report in the dashboard lookup.
                int lifetimeIssuedA4 = allRequests
                    .Where(r => r.Status == RequestStatus.Issued &&
                        string.Equals((r.PaperTypeName ?? string.Empty).Trim(), "A4", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.RequestedQuantity);

                int lifetimeIssuedA3 = allRequests
                    .Where(r => r.Status == RequestStatus.Issued &&
                        string.Equals((r.PaperTypeName ?? string.Empty).Trim(), "A3", StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.RequestedQuantity);

                int totalExceptionIssued = requests
                    .Where(r => r.Status == RequestStatus.Issued && r.IsException)
                    .Sum(r => r.RequestedQuantity);

                int lifetimeExceptionIssued = allRequests
                    .Where(r => r.Status == RequestStatus.Issued && r.IsException)
                    .Sum(r => r.RequestedQuantity);

                var now = DateTime.Now;
                var currentMonthStart = new DateTime(now.Year, now.Month, 1);
                var currentMonthEnd = currentMonthStart.AddMonths(1);

                var monthRequests = FetchAllFilteredRequestItems(new RequestFilterViewModel
                {
                    DepartmentID = departmentId,
                    FromDate = currentMonthStart,
                    ToDate = currentMonthEnd
                });

                int monthTotalIssued = monthRequests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .Sum(r => r.RequestedQuantity);

                int monthExceptionIssued = monthRequests
                    .Where(r => r.Status == RequestStatus.Issued && r.IsException)
                    .Sum(r => r.RequestedQuantity);

                int monthRegularIssued = Math.Max(0, monthTotalIssued - monthExceptionIssued);

                int pendingIT = allRequests.Count(r =>
                    r.Status == RequestStatus.PendingITReview ||
                    r.Status == RequestStatus.PendingITReReview);
                int pendingApprover = allRequests.Count(r =>
                    r.Status == RequestStatus.PendingApproverOne ||
                    r.Status == RequestStatus.PendingApproverTwo);
                int readyToIssue = allRequests.Count(r => r.Status == RequestStatus.Approved);
                int issuedCount = allRequests.Count(r => r.Status == RequestStatus.Issued);
                int rejectedCount = allRequests.Count(r => r.Status == RequestStatus.Rejected);
                int exceptionCount = allRequests.Count(r => r.IsException);

                RequestItemViewModel? latestRequest = allRequests.FirstOrDefault();
                RequestItemViewModel? latestOpenRequest = allRequests.FirstOrDefault(r =>
                    r.Status != RequestStatus.Issued &&
                    r.Status != RequestStatus.Rejected);
                RequestItemViewModel? lastIssuedRequest = allRequests
                    .Where(r => r.Status == RequestStatus.Issued)
                    .OrderByDescending(r => r.IssuedDate ?? r.RequestDate)
                    .ThenByDescending(r => r.RequestID)
                    .FirstOrDefault();

                object? BuildRequestSnapshot(RequestItemViewModel? request)
                {
                    if (request == null) return null;
                    var badge = Common.StatusDisplayHelper.GetBadgeInfo(request.Status);
                    return new
                    {
                        requestId = request.RequestID,
                        paperTypeName = request.PaperTypeName,
                        quantity = request.RequestedQuantity,
                        status = request.Status,
                        statusLabel = badge.Label,
                        statusClasses = badge.CssClasses,
                        isException = request.IsException,
                        needsReReview = request.Status == RequestStatus.PendingITReReview,
                        requestDate = request.RequestDate.ToEgyptTime().ToString("yyyy-MM-dd HH:mm"),
                        issuedDate = request.IssuedDate.HasValue
                            ? request.IssuedDate.Value.ToEgyptTime().ToString("yyyy-MM-dd HH:mm")
                            : null
                    };
                }

                var rows = requests.Take(12).Select(r =>
                {
                    var badge = Common.StatusDisplayHelper.GetBadgeInfo(r.Status);
                    return new
                    {
                        requestId = r.RequestID,
                        paperTypeName = r.PaperTypeName,
                        quantity = r.RequestedQuantity,
                        status = r.Status,
                        statusLabel = badge.Label,
                        statusClasses = badge.CssClasses,
                        isException = r.IsException,
                        needsReReview = r.Status == RequestStatus.PendingITReReview,
                        date = r.RequestDate.ToEgyptTime().ToString("yyyy-MM-dd")
                    };
                }).ToList();

                return Json(new
                {
                    success = true,
                    departmentId,
                    departmentName,
                    quotaLimit,
                    usedQuota,
                    remainingQuota,
                    quotas = quotaRows,
                    totalIssued,
                    lifetimeIssued,
                    lifetimeIssuedA4,
                    lifetimeIssuedA3,
                    totalExceptionIssued,
                    lifetimeExceptionIssued,
                    monthTotalIssued,
                    monthExceptionIssued,
                    monthRegularIssued,
                    pendingIT,
                    pendingApprover,
                    pendingTotal = pendingIT + pendingApprover,
                    readyToIssue,
                    issuedCount,
                    rejectedCount,
                    exceptionCount,
                    latestRequest = BuildRequestSnapshot(latestRequest),
                    latestOpenRequest = BuildRequestSnapshot(latestOpenRequest),
                    lastIssuedRequest = BuildRequestSnapshot(lastIssuedRequest),
                    count = requests.Count,
                    lifetimeRequestCount = allRequests.Count,
                    requests = rows
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "تعذر تحميل تفاصيل القسم حالياً." });
            }
        }

        // GET: /PaperManagement/Request/MyRequests
        [HttpGet]
        [Authorize(Roles = Roles.Department)]
        public IActionResult MyRequests()
        {
            if (!int.TryParse(User.FindFirst("DepartmentID")?.Value, out int departmentId))
                return RedirectToAction(nameof(Create));

            // Department users only need a simple recent-status view, not a historical analytics archive.
            // Keep this intentionally limited to the latest 25 requests.
            var list = MapDataTableToRequests(FetchFilteredRequests(new RequestFilterViewModel { DepartmentID = departmentId }, pageNumber: 1, pageSize: 25))
                .OrderByDescending(r => r.RequestDate)
                .ToList();

            return View(list);
        }

        // GET: /PaperManagement/Request/ReviewQueue
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ReviewQueue(RequestFilterViewModel filter, int page = 1, bool countOnly = false)
        {
            if (countOnly)
            {
                // Ask for one row only; TotalCount is calculated by SQL before pagination.
                // This avoids the old 1000-row request being clamped to 25 by the stored procedure.
                var countDt = FetchFilteredRequests(filter, pageNumber: 1, pageSize: 1);
                int queueTotalCount = countDt.Rows.Count > 0 && countDt.Columns.Contains("TotalCount")
                    ? Convert.ToInt32(countDt.Rows[0]["TotalCount"])
                    : 0;
                return Json(new { totalCount = queueTotalCount });
            }

            PopulateFilterLists(filter);
            PopulatePendingStageCounts(filter);
            ViewBag.Filter = filter;
            const int pageSize = 25;
            var dt = FetchFilteredRequests(filter, page, pageSize,
                prioritizeStatus1: RequestStatus.PendingITReview,
                prioritizeStatus2: RequestStatus.PendingITReReview);
            int totalCount = dt.Rows.Count > 0 && dt.Columns.Contains("TotalCount") ? Convert.ToInt32(dt.Rows[0]["TotalCount"]) : 0;
            var pageItems = MapDataTableToRequests(dt);


            var actionableIds = pageItems
                .Where(r =>
                    r.Status == RequestStatus.PendingITReview ||
                    r.Status == RequestStatus.PendingITReReview)
                .Select(r => r.RequestID)
                .ToHashSet();

            return View(new RequestQueueViewModel
            {
                Requests = pageItems,
                ActionableRequestIds = actionableIds,
                IsITQueue = true,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult Review(int requestId, int reviewedQuantity, string? notes)
        {
            try
            {
                _db.ExecuteNonQuery("sp_ITReviewRequest",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@ReviewedByID", GetCurrentUserId()),
                    new SqlParameter("@ReviewedQuantity", reviewedQuantity),
                    new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));
                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.Approver,
                    QueueAudience.MyRequests);
                TempData["SuccessMessage"] = "تمت مراجعة الطلب وإرساله للاعتماد.";
            }
            catch (SqlException ex)
            {
                // sp_ITReviewRequest throws specific error numbers (see ScriptV15.sql):
                //   51003 = reviewed quantity is less than 1
                //   51004 = request is no longer PendingITReview (already reviewed/rejected
                //           by someone else, or the page/queue was stale)
                //   51005 = the (possibly IT-adjusted) quantity would exceed the
                //           department's remaining monthly quota for this paper type
                TempData["ErrorMessage"] = ex.Number switch
                {
                    51003 => "الكمية بعد المراجعة يجب أن تكون رزمة واحدة على الأقل.",
                    51004 => "تعذرت المراجعة لأن الطلب لم يعد بانتظار مراجعة تقنية المعلومات (ربما تمت مراجعته أو رفضه بالفعل من مستخدم آخر). يرجى تحديث الصفحة.",
                    51005 => "الكمية بعد المراجعة تتجاوز الحصة الشهرية المتبقية لهذا القسم من نوع الورق المطلوب.",
                    _ => "تعذرت مراجعة الطلب. يرجى المحاولة لاحقاً."
                };
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذرت مراجعة الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(ReviewQueue));
        }

        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult ResendForApproval(int requestId, int reviewedQuantity, string? notes)
        {
            try
            {
                _db.ExecuteNonQuery("sp_ResendForApproval",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@ReviewedByID", GetCurrentUserId()),
                    new SqlParameter("@ReviewedQuantity", reviewedQuantity),
                    new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));
                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.Approver,
                    QueueAudience.MyRequests);
                TempData["SuccessMessage"] = "تمت إعادة إرسال الطلب للاعتماد.";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = ex.Number switch
                {
                    51021 => "الكمية المعدلة يجب أن تكون رزمة واحدة على الأقل.",
                    51022 => "الطلب لم يعد بانتظار إعادة المراجعة. يرجى تحديث الصفحة.",
                    51023 => "الكمية المعدلة تتجاوز الحصة الشهرية المتبقية لهذا القسم.",
                    _ => "تعذر إعادة الإرسال. يرجى المحاولة لاحقاً."
                };
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر إعادة الإرسال. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(ReviewQueue));
        }

        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult FinalizeRejection(int requestId, string? note)
        {
            note = note?.Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["ErrorMessage"] = "يجب كتابة سبب الإغلاق النهائي قبل رفض الطلب.";
                return RedirectToAction(nameof(ReviewQueue));
            }

            try
            {
                // Use the established IT final-rejection procedure. It accepts both
                // PendingITReview and PendingITReReview, writes the activity log,
                // and avoids deployments failing when the newer
                // sp_FinalizeRejection procedure has not yet been installed.
                _db.ExecuteNonQuery("sp_RejectRequestByIT",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@RejectorID", GetCurrentUserId()),
                    new SqlParameter("@Note", note));

                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.Approver,
                    QueueAudience.MyRequests);
                TempData["SuccessMessage"] = "تم رفض الطلب وإغلاقه نهائياً.";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = ex.Number switch
                {
                    51111 => "يجب كتابة سبب الإغلاق النهائي قبل رفض الطلب.",
                    51020 => "الطلب لم يعد قيد المراجعة. يرجى تحديث الصفحة.",
                    _ => "تعذر إغلاق الطلب. يرجى المحاولة لاحقاً."
                };
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر إغلاق الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(ReviewQueue));
        }

        // GET: /PaperManagement/Request/Pending
        [Authorize(Roles = Roles.Approver)]
        public IActionResult Pending(RequestFilterViewModel filter, int page = 1)
        {
            PopulateFilterLists(filter);
            PopulatePendingStageCounts(filter);
            ViewBag.Filter = filter;

            int currentUserId = GetCurrentUserId();
            int myStage = 0;
            var assignments = _db.ExecuteQuery("sp_GetFixedRequestApprovers");

            foreach (DataRow row in assignments.Rows)
            {
                if (Convert.ToInt32(row["UserID"]) == currentUserId)
                {
                    myStage = Convert.ToInt32(row["ApprovalStage"]);
                    break;
                }
            }

            string myStatus = myStage == 1
                ? RequestStatus.PendingApproverOne
                : myStage == 2
                    ? RequestStatus.PendingApproverTwo
                    : string.Empty;

            const int pageSize = 25;
            var dt = FetchApproverRequests(
                currentUserId,
                filter,
                page,
                pageSize);

            int totalCount =
                dt.Rows.Count > 0 &&
                dt.Columns.Contains("TotalCount")
                    ? Convert.ToInt32(dt.Rows[0]["TotalCount"])
                    : 0;

            var pageItems = MapDataTableToRequests(dt);


            var actionableIds = pageItems
                .Where(r =>
                    !string.IsNullOrEmpty(myStatus) &&
                    r.Status == myStatus)
                .Select(r => r.RequestID)
                .ToHashSet();

            ViewBag.ApprovalStage = myStage;
            return View(new RequestQueueViewModel
            {
                Requests = pageItems,
                ActionableRequestIds = actionableIds,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        // POST: /PaperManagement/Request/Approve
        [HttpPost]
        [Authorize(Roles = Roles.Approver)]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int requestId, string? note)
        {
            try
            {
                int userId = GetCurrentUserId();
                var resultStageParam = new SqlParameter("@ResultStage", SqlDbType.TinyInt) { Direction = ParameterDirection.Output };
                _db.ExecuteNonQuery("sp_ApproveRequest",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@ApproverID", userId),
                    new SqlParameter("@Note", (object?)note ?? DBNull.Value),
                    resultStageParam);

                var resultStage = resultStageParam.Value != DBNull.Value ? Convert.ToInt32(resultStageParam.Value) : 0;

                _queueUpdates.Notify(
                    resultStage == 2
                        ? new[] { QueueAudience.Approver, QueueAudience.Issue, QueueAudience.MyRequests }
                        : new[] { QueueAudience.Approver });

                TempData["SuccessMessage"] = resultStage == 2
                    ? "تمت الموافقة على الطلب بنجاح وهو الآن جاهز للصرف من قبل أمين العهدة."
                    : "تمت الموافقة على الطلب.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر اعتماد الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Pending));
        }

        // POST: /PaperManagement/Request/Reject
        [HttpPost]
        [Authorize(Roles = Roles.Approver)]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int requestId, string? note)
        {
            try
            {
                int userId = GetCurrentUserId();
                _db.ExecuteNonQuery("sp_RejectRequest",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@RejectorID", userId),
                    new SqlParameter("@Note", (object?)note ?? DBNull.Value));

                _queueUpdates.Notify(
                    QueueAudience.Approver,
                    QueueAudience.Review);

                TempData["SuccessMessage"] = "تم رفض الطلب بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر رفض الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Pending));
        }

        // GET: /PaperManagement/Request/NotificationFeed
        [HttpGet]
        [Authorize(Roles = Roles.Approver)]
        public IActionResult NotificationFeed()
        {
            int currentUserId = GetCurrentUserId();
            var dt = _db.ExecuteQuery("sp_GetPendingApprovalSummary",
                new SqlParameter("@ApproverID", currentUserId));

            var notifications = dt.AsEnumerable().Select(row => new
            {
                requestID = Convert.ToInt32(row["RequestID"]),
                departmentName = row["DepartmentName"].ToString(),
                paperTypeName = row["PaperTypeName"].ToString(),
                requestedQuantity = Convert.ToInt32(row["RequestedQuantity"]),
                requestDate = Convert.ToDateTime(row["RequestDate"]).ToString("yyyy/MM/dd HH:mm"),
                message = $"طلب جديد من {row["DepartmentName"]}: {row["PaperTypeName"]} ({row["RequestedQuantity"]})"
            }).ToList();

            return Json(new { count = notifications.Count, items = notifications });
        }

        // GET: /PaperManagement/Request/LowStockFeed
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult LowStockFeed()
        {
            var dt = _db.ExecuteQuery("sp_GetInventoryStatus");
            var lowStock = dt.AsEnumerable()
                .Where(r => Convert.ToInt32(r["CurrentStock"]) <= (r.Table.Columns.Contains("LowStockThreshold") && r["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(r["LowStockThreshold"]) : 50))
                .Select(r => new
                {
                    paperTypeId = Convert.ToInt32(r["PaperTypeID"]),
                    paperTypeName = r["PaperTypeName"].ToString(),
                    availableStock = Convert.ToInt32(r["CurrentStock"]),
                    threshold = r.Table.Columns.Contains("LowStockThreshold") && r["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(r["LowStockThreshold"]) : 50
                }).ToList();

            return Json(new { count = lowStock.Count, items = lowStock });
        }

        // POST: /PaperManagement/Request/RejectByIT
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult RejectByIT(int requestId, string? note)
        {
            note = note?.Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["ErrorMessage"] = "يجب كتابة سبب رفض الطلب.";
                return RedirectToAction(nameof(ReviewQueue));
            }

            try
            {
                _db.ExecuteNonQuery("sp_RejectRequestByIT",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@RejectorID", GetCurrentUserId()),
                    new SqlParameter("@Note", note));

                _queueUpdates.Notify(
                    QueueAudience.Review,
                    QueueAudience.Approver,
                    QueueAudience.MyRequests);

                TempData["SuccessMessage"] = "تم رفض الطلب بنجاح.";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = ex.Number switch
                {
                    51111 => "يجب كتابة سبب رفض الطلب.",
                    51020 => "الطلب لم يعد قيد المراجعة. يرجى تحديث الصفحة.",
                    _ => "تعذر رفض الطلب. يرجى المحاولة لاحقاً."
                };
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر رفض الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(ReviewQueue));
        }

        // GET: /PaperManagement/Request/IssueQueue
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult IssueQueue(RequestFilterViewModel filter, int page = 1)
        {
            // The issue queue only exposes requests which are ready to issue (Approved).
            // Do not render a status filter that suggests other states are issuable.
            filter.Status = RequestStatus.Approved;
            filter.PendingStages = null;
            PopulateFilterLists(filter, includeStatusFilter: false);
            ViewBag.Filter = filter;
            const int pageSize = 25;

            var dt = FetchFilteredRequests(
                filter,
                page,
                pageSize,
                prioritizeStatus1: RequestStatus.Approved);

            int totalCount =
                dt.Rows.Count > 0 &&
                dt.Columns.Contains("TotalCount")
                    ? Convert.ToInt32(dt.Rows[0]["TotalCount"])
                    : 0;

            var pageItems = MapDataTableToRequests(dt);


            var actionableIds = pageItems
                .Where(r => r.Status == RequestStatus.Approved)
                .Select(r => r.RequestID)
                .ToHashSet();

            return View(new RequestQueueViewModel
            {
                Requests = pageItems,
                ActionableRequestIds = actionableIds,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        // POST: /PaperManagement/Request/Issue
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult Issue(int requestId, string? notes)
        {
            try
            {
                ActivateDueInventoryContractIfPossible();
                _db.ExecuteNonQuery("sp_IssueRequest",
                    new SqlParameter("@RequestID", requestId),
                    new SqlParameter("@IssuedByID", GetCurrentUserId()),
                    // sp_IssueRequest stores issue notes as @Notes.
                    new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));

                _queueUpdates.Notify(
                    QueueAudience.Issue,
                    QueueAudience.Review,
                    QueueAudience.MyRequests);

                TempData["SuccessMessage"] = "تم تسليم الطلب بنجاح.";
            }
            catch (SqlException ex)
            {
                // sp_IssueRequest throws 51013 specifically for insufficient
                // physical stock (see ScriptV15.sql). Matching on ex.Number
                // instead of ex.Message text is more robust to wording changes.
                TempData["ErrorMessage"] = ex.Number == 51013
                    ? "عفواً، الكمية المتوفرة في المخزن غير كافية لصرف هذا الطلب. يرجى تحديث المخزن أولاً."
                    : "تعذر تسليم الطلب. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(IssueQueue));
        }

        // GET: /PaperManagement/Request/History
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult History(RequestFilterViewModel filter)
        {
            PopulateFilterLists(filter);
            ViewBag.Filter = filter;

            var dt = FetchFilteredRequests(filter);
            var pageItems = MapDataTableToRequests(dt);

            return View(pageItems);
        }

        // GET: /PaperManagement/Request/Reports
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult Reports(RequestFilterViewModel filter)
        {
            PopulateFilterLists(filter, includeStatusFilter: false);
            ViewBag.Filter = filter;

            int? deptIdParam = filter.DepartmentID;
            if (!deptIdParam.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
            {
                deptIdParam = filter.SelectedDepartmentIDs[0];
            }

            DateTime? fromDateParam = filter.FromDate.HasValue ? filter.FromDate.Value.Date : null;
            DateTime? toDateParam = filter.ToDate.HasValue ? filter.ToDate.Value.Date.AddDays(1) : null;

            var consumptionDt = _db.ExecuteQuery("sp_GetDepartmentConsumptionSummary",
                new SqlParameter("@DepartmentID", (object?)deptIdParam ?? DBNull.Value),
                new SqlParameter("@FromDate", (object?)fromDateParam ?? DBNull.Value),
                new SqlParameter("@ToDate", (object?)toDateParam ?? DBNull.Value)
            );

            var pivotRows = new List<PivotedDepartmentRow>();
            int totalA4 = 0;
            int totalA3 = 0;
            int grandTotal = 0;

            int allDeptCount = filter.Departments?.Count() ?? 0;
            bool hasCustomDeptFilter = filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Any() &&
                                       (allDeptCount == 0 || filter.SelectedDepartmentIDs.Count < allDeptCount);

            var sortedRows = consumptionDt.Rows.Cast<DataRow>()
                .OrderBy(r => Convert.ToInt32(r["DepartmentID"]))
                .ToList();

            foreach (DataRow row in sortedRows)
            {
                var dId = Convert.ToInt32(row["DepartmentID"]);
                if (hasCustomDeptFilter && !filter.SelectedDepartmentIDs!.Contains(dId))
                {
                    continue;
                }

                int a4 = GetInt(row, "A4Quantity", 0);
                int a3 = GetInt(row, "A3Quantity", 0);
                int rowTotal = GetInt(row, "TotalQuantity", 0);

                totalA4 += a4;
                totalA3 += a3;
                grandTotal += rowTotal;

                var pivRow = new PivotedDepartmentRow
                {
                    DepartmentID = dId,
                    DepartmentName = (row["DepartmentName"].ToString() ?? "").Trim(),
                    RowTotal = rowTotal
                };
                pivRow.QuantityByPaperType["A4"] = a4;
                pivRow.QuantityByPaperType["A3"] = a3;
                pivotRows.Add(pivRow);
            }

            var model = new UsageReportViewModel
            {
                Filter = filter,
                PaperTypeColumns = new List<string> { "A4", "A3" },
                PivotRows = pivotRows,
                ColumnTotals = new Dictionary<string, int>
                {
                    ["A4"] = totalA4,
                    ["A3"] = totalA3
                },
                GrandTotal = grandTotal
            };

            return View(model);
        }

        // GET: /PaperManagement/Request/ExportReportToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult ExportReportToExcel(RequestFilterViewModel filter)
        {
            try
            {
                PopulateFilterLists(filter, includeStatusFilter: false);

                int? deptIdParam = filter.DepartmentID;
                if (!deptIdParam.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
                {
                    deptIdParam = filter.SelectedDepartmentIDs[0];
                }

                DateTime? fromDateParam = filter.FromDate.HasValue ? filter.FromDate.Value.Date : null;
                DateTime? toDateParam = filter.ToDate.HasValue ? filter.ToDate.Value.Date.AddDays(1) : null;

                var consumptionDt = _db.ExecuteQuery("sp_GetDepartmentConsumptionSummary",
                    new SqlParameter("@DepartmentID", (object?)deptIdParam ?? DBNull.Value),
                    new SqlParameter("@FromDate", (object?)fromDateParam ?? DBNull.Value),
                    new SqlParameter("@ToDate", (object?)toDateParam ?? DBNull.Value)
                );

                var headers = new[]
                {
                    "كود القسم",
                    "اسم القسم / الإدارة",
                    "التصنيف",
                    "ورق A4 (رزمة)",
                    "ورق A3 (رزمة)",
                    "إجمالي المصروف (رزمة)"
                };

                var rows = new List<object?[]>();

                int allDeptCount = filter.Departments?.Count() ?? 0;
                bool hasCustomDeptFilter = filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Any() &&
                                           (allDeptCount == 0 || filter.SelectedDepartmentIDs.Count < allDeptCount);

                var sortedRows = consumptionDt.Rows.Cast<DataRow>()
                    .OrderBy(r => Convert.ToInt32(r["DepartmentID"]))
                    .ToList();

                foreach (DataRow row in sortedRows)
                {
                    var dId = Convert.ToInt32(row["DepartmentID"]);
                    if (hasCustomDeptFilter && !filter.SelectedDepartmentIDs!.Contains(dId))
                    {
                        continue;
                    }

                    int a4 = GetInt(row, "A4Quantity", 0);
                    int a3 = GetInt(row, "A3Quantity", 0);
                    int total = GetInt(row, "TotalQuantity", 0);
                    string deptName = (row["DepartmentName"].ToString() ?? "").Trim();
                    string groupName = dId <= 24 ? "إدارات الشبكة والأفرع" : "القطاعات والأقسام المقدمة والوحدات";

                    rows.Add(new object?[]
                    {
                        dId,
                        deptName,
                        groupName,
                        a4,
                        a3,
                        total
                    });
                }

                var content = SimpleXlsxWriter.Create("بيان استهلاك الورق", headers, rows);
                var fromStr = filter.FromDate?.ToString("yyyyMMdd") ?? "Start";
                var toStr = filter.ToDate?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
                var fileName = $"Paper_Consumption_{fromStr}_to_{toStr}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "تعذر إعداد تقرير الاستهلاك حالياً.";
                return RedirectToAction(nameof(Reports), filter);
            }
        }

        // GET: /PaperManagement/Request/LastIssued
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult LastIssued(RequestFilterViewModel filter, string viewMode = "all")
        {
            PopulateFilterLists(filter, includeStatusFilter: false);
            ViewBag.Filter = filter;
            ViewBag.ViewMode = viewMode;

            var dt = _db.ExecuteQuery("sp_GetLastIssuedRequests",
                new SqlParameter("@DepartmentID", (object?)filter.DepartmentID ?? DBNull.Value),
                new SqlParameter("@PaperTypeID", (object?)filter.PaperTypeID ?? DBNull.Value),
                new SqlParameter("@ViewMode", viewMode)
            );

            var list = MapDataTableToRequests(dt);
            return View(list);
        }

        // GET: /PaperManagement/Request/Inventory
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult Inventory()
        {
            ActivateDueInventoryContractIfPossible();

            var dt = _db.ExecuteQuery("sp_GetInventoryStatus");
            var list = new List<InventoryStatusViewModel>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new InventoryStatusViewModel
                {
                    PaperTypeID = Convert.ToInt32(row["PaperTypeID"]),
                    PaperTypeName = row["PaperTypeName"].ToString() ?? string.Empty,
                    CurrentStock = Convert.ToInt32(row["CurrentStock"]),
                    TotalReceived = row.Table.Columns.Contains("TotalReceived") && row["TotalReceived"] != DBNull.Value
                        ? Convert.ToInt32(row["TotalReceived"])
                        : 0,
                    LowStockThreshold = row.Table.Columns.Contains("LowStockThreshold") && row["LowStockThreshold"] != DBNull.Value
                        ? Convert.ToInt32(row["LowStockThreshold"])
                        : 50,
                    LastUpdated = row.Table.Columns.Contains("LastUpdated") && row["LastUpdated"] != DBNull.Value
                        ? Convert.ToDateTime(row["LastUpdated"])
                        : DateTime.UtcNow,
                    ActiveContractID = row.Table.Columns.Contains("ActiveContractID") && row["ActiveContractID"] != DBNull.Value
                        ? Convert.ToInt32(row["ActiveContractID"])
                        : null,
                    ActiveContractNumber = row.Table.Columns.Contains("ActiveContractNumber") && row["ActiveContractNumber"] != DBNull.Value
                        ? row["ActiveContractNumber"].ToString()
                        : null,
                    ActiveContractName = row.Table.Columns.Contains("ActiveContractName") && row["ActiveContractName"] != DBNull.Value
                        ? row["ActiveContractName"].ToString()
                        : null,
                    ContractStartDate = row.Table.Columns.Contains("ContractStartDate") && row["ContractStartDate"] != DBNull.Value
                        ? Convert.ToDateTime(row["ContractStartDate"])
                        : null,
                    OpeningStockUnderActiveContract = GetInt(row, "OpeningStockUnderActiveContract", 0),
                    ReceivedUnderActiveContract = GetInt(row, "ReceivedUnderActiveContract", 0),
                    IssuedUnderActiveContract = GetInt(row, "IssuedUnderActiveContract", 0)
                });
            }

            var activeContractDt = _db.ExecuteQuery("sp_GetActiveInventoryContract");
            ViewBag.ActiveInventoryContract = activeContractDt.Rows.Count > 0
                ? MapInventoryContractRow(activeContractDt.Rows[0])
                : null;

            ViewBag.ReceiveModel = new ReceiveStockViewModel
            {
                PaperTypes = list.Select(i => new SelectListItem
                {
                    Value = i.PaperTypeID.ToString(),
                    Text = i.PaperTypeName
                }).ToList()
            };

            return View(list);
        }

        // GET: /PaperManagement/Request/InventoryContracts
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult InventoryContracts()
        {
            ActivateDueInventoryContractIfPossible();

            var contractsDt = _db.ExecuteQuery("sp_GetInventoryContracts");
            var contracts = contractsDt.Rows.Cast<DataRow>()
                .Select(MapInventoryContractRow)
                .ToList();

            MarkScheduledContracts(contracts);
            EnrichInventoryContractsWithTypeSummaries(contracts);

            var activeContract = contracts.FirstOrDefault(c => c.IsActive);
            var balances = new List<InventoryContractBalanceViewModel>();

            if (activeContract != null)
            {
                var balancesDt = _db.ExecuteQuery("sp_GetInventoryContractBalances",
                    new SqlParameter("@ContractID", activeContract.ContractID));

                balances = balancesDt.Rows.Cast<DataRow>()
                    .Select(r => new InventoryContractBalanceViewModel
                    {
                        PaperTypeID = Convert.ToInt32(r["PaperTypeID"]),
                        PaperTypeName = r["PaperTypeName"].ToString() ?? string.Empty,
                        OpeningStock = Convert.ToInt32(r["OpeningStock"]),
                        ReceivedQuantity = Convert.ToInt32(r["ReceivedQuantity"]),
                        IssuedQuantity = Convert.ToInt32(r["IssuedQuantity"]),
                        ClosingStock = r["ClosingStock"] != DBNull.Value ? Convert.ToInt32(r["ClosingStock"]) : null,
                        CurrentPhysicalStock = Convert.ToInt32(r["CurrentPhysicalStock"]),
                        CalculatedContractBalance = Convert.ToInt32(r["CalculatedContractBalance"])
                    })
                    .ToList();
            }

            var model = new InventoryContractsPageViewModel
            {
                ActiveContract = activeContract,
                Contracts = contracts,
                ActiveBalances = balances,
                NewContract = new StartInventoryContractViewModel
                {
                    StartDate = DateTime.UtcNow.ToEgyptTime().Date
                }
            };

            return View(model);
        }

        // GET: /PaperManagement/Request/ExportInventoryContractsToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ExportInventoryContractsToExcel()
        {
            ActivateDueInventoryContractIfPossible();

            var contractsDt = _db.ExecuteQuery("sp_GetInventoryContracts");
            var contracts = contractsDt.Rows.Cast<DataRow>()
                .Select(MapInventoryContractRow)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            MarkScheduledContracts(contracts);
            EnrichInventoryContractsWithTypeSummaries(contracts);

            if (contracts.Count == 0)
            {
                TempData["ErrorMessage"] = "لا توجد عقود مسجلة لتصديرها.";
                return RedirectToAction(nameof(InventoryContracts));
            }

            var headers = new[]
            {
                "العقد",
                "رقم العقد",
                "الحالة",
                "تاريخ البداية",
                "تاريخ النهاية",
                "كمية تعاقد A4",
                "كمية تعاقد A3",
                "إجمالي الرصيد الافتتاحي",
                "إجمالي المورد",
                "إجمالي المصروف",
                "إجمالي الرصيد الختامي",
                "A4 رصيد افتتاحي",
                "A4 مورد",
                "A4 مصروف",
                "A4 رصيد ختامي",
                "A3 رصيد افتتاحي",
                "A3 مورد",
                "A3 مصروف",
                "A3 رصيد ختامي"
            };

            var rows = contracts.Select(c => new object?[]
            {
                c.ContractName,
                c.ContractNumber,
                c.IsActive ? "نشط" : (c.IsScheduled ? "لم يبدأ بعد" : "مغلق"),
                c.StartDate.ToString("yyyy/MM/dd"),
                c.EndDate?.ToString("yyyy/MM/dd") ?? "—",
                c.QuantityA4?.ToString() ?? "—",
                c.QuantityA3?.ToString() ?? "—",
                c.OpeningStock,
                c.ReceivedQuantity,
                c.IssuedQuantity,
                c.ClosingStock ?? c.CurrentContractBalance,
                c.A4Summary?.OpeningStock ?? 0,
                c.A4Summary?.ReceivedQuantity ?? 0,
                c.A4Summary?.IssuedQuantity ?? 0,
                c.A4Summary?.BalanceDisplay ?? 0,
                c.A3Summary?.OpeningStock ?? 0,
                c.A3Summary?.ReceivedQuantity ?? 0,
                c.A3Summary?.IssuedQuantity ?? 0,
                c.A3Summary?.BalanceDisplay ?? 0
            }).ToList();

            var content = SimpleXlsxWriter.Create("سجل العقود", headers, rows);
            var fileName = $"Inventory_Contracts_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // POST: /PaperManagement/Request/StartInventoryContract
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult StartInventoryContract(StartInventoryContractViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "يرجى إدخال اسم العقد وتاريخ بداية صحيح.";
                return RedirectToAction(nameof(InventoryContracts));
            }

            if (model.EndDate.HasValue && model.EndDate.Value.Date < model.StartDate.Date)
            {
                TempData["ErrorMessage"] = "تاريخ نهاية العقد يجب أن يكون بعد أو يساوي تاريخ البداية.";
                return RedirectToAction(nameof(InventoryContracts));
            }

            try
            {
                bool startsLater = model.StartDate.Date > DateTime.UtcNow.ToEgyptTime().Date;
                _db.ExecuteNonQuery(startsLater ? "sp_ScheduleInventoryContract" : "sp_StartInventoryContract",
                    new SqlParameter("@ContractNumber", string.IsNullOrWhiteSpace(model.ContractNumber) ? DBNull.Value : model.ContractNumber.Trim()),
                    new SqlParameter("@ContractName", model.ContractName.Trim()),
                    new SqlParameter("@StartDate", model.StartDate.Date),
                    new SqlParameter("@EndDate", model.EndDate.HasValue ? model.EndDate.Value.Date : DBNull.Value),
                    new SqlParameter("@QuantityA4", (object?)model.QuantityA4 ?? DBNull.Value),
                    new SqlParameter("@QuantityA3", (object?)model.QuantityA3 ?? DBNull.Value),
                    new SqlParameter("@Notes", string.IsNullOrWhiteSpace(model.Notes) ? DBNull.Value : model.Notes.Trim()),
                    new SqlParameter("@CreatedBy", GetCurrentUserId()));

                TempData["SuccessMessage"] = startsLater
                    ? "تم تسجيل العقد بنجاح بحالة «لم يبدأ بعد». سيبدأ في تاريخه عند أول استخدام للنظام، بشرط عدم وجود عقد نشط متداخل."
                    : "تم بدء العقد وتسجيل الرصيد الموجود بالمخزن كرصد افتتاحي تلقائياً.";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر بدء العقد. يرجى المحاولة لاحقاً.";
            }

            return RedirectToAction(nameof(InventoryContracts));
        }

        // POST: /PaperManagement/Request/CloseInventoryContract
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult CloseInventoryContract(int contractId, DateTime endDate, string? closeNote = null)
        {
            try
            {
                _db.ExecuteNonQuery("sp_CloseInventoryContract",
                    new SqlParameter("@ContractID", contractId),
                    new SqlParameter("@EndDate", endDate.Date),
                    new SqlParameter("@ClosedBy", GetCurrentUserId()),
                    new SqlParameter("@CloseNote", string.IsNullOrWhiteSpace(closeNote) ? DBNull.Value : closeNote.Trim()));

                TempData["SuccessMessage"] = "تم إنهاء العقد. الرصيد الفعلي بالمخزن لم يتغير وتم تسجيل الرصيد الختامي للعقد.";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر إنهاء العقد. يرجى المحاولة لاحقاً.";
            }

            return RedirectToAction(nameof(InventoryContracts));
        }

        // GET: /PaperManagement/Request/InventoryMovementLog
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult InventoryMovementLog(
            int? contractId = null,
            int? paperTypeId = null,
            string? movementType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            ActivateDueInventoryContractIfPossible();

            NormalizeReceiptLogDates(ref fromDate, ref toDate);

            var rows = GetInventoryMovementRows(contractId, paperTypeId, movementType, fromDate, toDate);

            var contractsDt = _db.ExecuteQuery("sp_GetInventoryContracts");
            ViewBag.Contracts = contractsDt.Rows.Cast<DataRow>()
                .Select(r => new SelectListItem
                {
                    Value = Convert.ToInt32(r["ContractID"]).ToString(),
                    Text = !string.IsNullOrWhiteSpace(r["ContractNumber"]?.ToString())
                        ? $"{r["ContractName"]} - {r["ContractNumber"]}"
                        : r["ContractName"].ToString() ?? $"عقد #{r["ContractID"]}"
                })
                .ToList();
            ViewBag.PaperTypes = GetPaperTypesSelectList().ToList();
            ViewBag.SelectedContractId = contractId;
            ViewBag.SelectedPaperTypeId = paperTypeId;
            ViewBag.SelectedMovementType = movementType;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.TotalMovementCount = rows.Count;
            ViewBag.TotalReceiptQuantity = rows.Where(x => string.Equals(x.MovementType, "Receipt", StringComparison.OrdinalIgnoreCase)).Sum(x => x.QuantityChange ?? 0);
            ViewBag.TotalIssuedQuantity = Math.Abs(rows.Where(x => string.Equals(x.MovementType, "Issue", StringComparison.OrdinalIgnoreCase)).Sum(x => x.QuantityChange ?? 0));

            return View(rows);
        }

        // GET: /PaperManagement/Request/ExportInventoryMovementLogToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ExportInventoryMovementLogToExcel(
            int? contractId = null,
            int? paperTypeId = null,
            string? movementType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            NormalizeReceiptLogDates(ref fromDate, ref toDate);

            var rows = GetInventoryMovementRows(contractId, paperTypeId, movementType, fromDate, toDate)
                .OrderByDescending(x => x.EventDate)
                .ToList();

            if (rows.Count == 0)
            {
                TempData["ErrorMessage"] = "لا توجد حركات مطابقة للفلاتر الحالية لتصديرها.";
                return RedirectToAction(nameof(InventoryMovementLog), new { contractId, paperTypeId, movementType, fromDate, toDate });
            }

            var headers = new[]
            {
                "التاريخ",
                "الحركة",
                "نوع الورق",
                "الكمية",
                "العقد",
                "المرجع",
                "بواسطة",
                "تفاصيل"
            };

            string GetMovementLabel(string type) => type switch
            {
                "Receipt" => "استلام",
                "Issue" => "صرف",
                "ContractStarted" => "بدء عقد",
                "ContractClosed" => "إنهاء عقد",
                _ => type
            };

            var exportRows = rows.Select(item => new object?[]
            {
                item.EventDate.ToEgyptTime().ToString("yyyy/MM/dd HH:mm"),
                GetMovementLabel(item.MovementType),
                item.PaperTypeName ?? "—",
                item.QuantityChange,
                item.ContractDisplay,
                item.ReferenceType == "Request" ? $"طلب #{item.ReferenceID}" :
                    item.ReferenceType == "InventoryReceipt" ? $"استلام #{item.ReferenceID}" :
                    item.ReferenceID != null ? $"عقد #{item.ReferenceID}" : "—",
                item.PerformedByName,
                item.Details
            }).ToList();

            var content = SimpleXlsxWriter.Create("سجل حركة المخزون", headers, exportRows);
            var fileName = $"Inventory_Movement_Log_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: /PaperManagement/Request/InventoryYearlyLedger
        // Calendar-year reporting view. The physical stock is never reset or deleted at
        // year-end; only the yearly movement counters restart, while ClosingStock becomes
        // the opening balance carried into the following year.
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult InventoryYearlyLedger(int? year = null)
        {
            var dt = _db.ExecuteQuery("sp_GetInventoryYearlyLedger");
            var allRows = new List<InventoryYearlyLedgerRow>();

            foreach (DataRow row in dt.Rows)
            {
                allRows.Add(new InventoryYearlyLedgerRow
                {
                    Year = Convert.ToInt32(row["YearNumber"]),
                    PaperTypeID = Convert.ToInt32(row["PaperTypeID"]),
                    PaperTypeName = row["PaperTypeName"].ToString() ?? string.Empty,
                    OpeningStock = Convert.ToInt64(row["OpeningStock"]),
                    ReceivedDuringYear = Convert.ToInt64(row["ReceivedDuringYear"]),
                    IssuedDuringYear = Convert.ToInt64(row["IssuedDuringYear"]),
                    ClosingStock = Convert.ToInt64(row["ClosingStock"])
                });
            }

            var currentYear = DateTime.UtcNow.ToEgyptTime().Year;
            var availableYears = allRows
                .Select(r => r.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (availableYears.Count == 0)
            {
                availableYears.Add(currentYear);
            }

            int selectedYear = year.HasValue && availableYears.Contains(year.Value)
                ? year.Value
                : (availableYears.Contains(currentYear) ? currentYear : availableYears[0]);

            var yearSummaries = allRows
                .GroupBy(r => r.Year)
                .Select(g => new InventoryYearSummary
                {
                    Year = g.Key,
                    OpeningStock = g.Sum(x => x.OpeningStock),
                    ReceivedDuringYear = g.Sum(x => x.ReceivedDuringYear),
                    IssuedDuringYear = g.Sum(x => x.IssuedDuringYear),
                    ClosingStock = g.Sum(x => x.ClosingStock)
                })
                .OrderByDescending(x => x.Year)
                .ToList();

            var model = new InventoryYearlyLedgerViewModel
            {
                SelectedYear = selectedYear,
                CurrentYear = currentYear,
                AvailableYears = availableYears,
                Rows = allRows
                    .Where(r => r.Year == selectedYear)
                    .OrderBy(r => r.PaperTypeName)
                    .ToList(),
                YearSummaries = yearSummaries
            };

            return View(model);
        }

        // GET: /PaperManagement/Request/ExportInventoryYearlyLedgerToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ExportInventoryYearlyLedgerToExcel(int year)
        {
            var dt = _db.ExecuteQuery("sp_GetInventoryYearlyLedger");
            var rowsForYear = dt.Rows.Cast<DataRow>()
                .Where(r => Convert.ToInt32(r["YearNumber"]) == year)
                .OrderBy(r => r["PaperTypeName"].ToString())
                .ToList();

            if (rowsForYear.Count == 0)
            {
                return BadRequest("لا توجد بيانات مخزون مسجلة للسنة المحددة.");
            }

            var currentYear = DateTime.UtcNow.ToEgyptTime().Year;
            var closingHeader = year == currentYear
                ? "الرصيد الحالي"
                : $"الرصيد المرحل إلى {year + 1}";

            var headers = new[]
            {
                "نوع الورق",
                "رصيد أول السنة",
                "الوارد خلال السنة",
                "إجمالي المتاح",
                "المصروف خلال السنة",
                closingHeader
            };

            var exportRows = new List<object?[]>();
            long totalOpening = 0;
            long totalReceived = 0;
            long totalIssued = 0;
            long totalClosing = 0;

            foreach (var row in rowsForYear)
            {
                long opening = Convert.ToInt64(row["OpeningStock"]);
                long received = Convert.ToInt64(row["ReceivedDuringYear"]);
                long issued = Convert.ToInt64(row["IssuedDuringYear"]);
                long closing = Convert.ToInt64(row["ClosingStock"]);

                totalOpening += opening;
                totalReceived += received;
                totalIssued += issued;
                totalClosing += closing;

                exportRows.Add(new object?[]
                {
                    row["PaperTypeName"].ToString() ?? string.Empty,
                    opening,
                    received,
                    opening + received,
                    issued,
                    closing
                });
            }

            exportRows.Add(new object?[]
            {
                "الإجمالي",
                totalOpening,
                totalReceived,
                totalOpening + totalReceived,
                totalIssued,
                totalClosing
            });

            var content = SimpleXlsxWriter.Create($"المخزون {year}", headers, exportRows);
            var fileName = $"Inventory_Year_{year}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // POST: /PaperManagement/Request/CreatePaperType
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePaperType(CreatePaperTypeViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.PaperTypeName))
            {
                TempData["ErrorMessage"] = "يرجى إدخال اسم صحيح لنوع الورق (مثل A4 أو A3).";
                return RedirectToAction(nameof(Inventory));
            }

            try
            {
                _db.ExecuteNonQuery("sp_AddPaperType",
                    new SqlParameter("@PaperTypeName", model.PaperTypeName.Trim()),
                    new SqlParameter("@DefaultQuotaLimit", model.DefaultQuotaLimit));

                TempData["SuccessMessage"] = $"تمت إضافة نوع الورق \"{model.PaperTypeName}\" بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذرت إضافة نوع الورق. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Inventory));
        }

        // POST: /PaperManagement/Request/DeletePaperType
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePaperType(int paperTypeId)
        {
            try
            {
                _db.ExecuteNonQuery("sp_DeletePaperType",
                    new SqlParameter("@PaperTypeID", paperTypeId));
                TempData["SuccessMessage"] = "تم حذف نوع الورق بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر حذف نوع الورق. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Inventory));
        }

        // GET: /PaperManagement/Request/ReceiptLog
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ReceiptLog(int? paperTypeId = null, int? year = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            ActivateDueInventoryContractIfPossible();

            NormalizeReceiptLogDates(ref fromDate, ref toDate);

            var paperTypes = GetPaperTypesSelectList().ToList();
            var allReceipts = GetStockReceiptLogRows();
            var availableYears = BuildReceiptYears(allReceipts);
            var receipts = FilterReceiptLog(allReceipts, paperTypeId, year, fromDate, toDate, paperTypes);

            ViewBag.PaperTypes = paperTypes;
            ViewBag.ReceiptYears = availableYears;
            ViewBag.SelectedPaperTypeId = paperTypeId;
            ViewBag.SelectedYear = year;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.FilteredReceiptCount = receipts.Count;
            ViewBag.FilteredReceiptQuantity = receipts.Sum(r => r.QuantityReceived);

            return View(receipts);
        }

        // GET: /PaperManagement/Request/ExportReceiptLogToExcel
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin)]
        public IActionResult ExportReceiptLogToExcel(int? paperTypeId = null, int? year = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            NormalizeReceiptLogDates(ref fromDate, ref toDate);

            var paperTypes = GetPaperTypesSelectList().ToList();
            var allReceipts = GetStockReceiptLogRows();
            var receipts = FilterReceiptLog(allReceipts, paperTypeId, year, fromDate, toDate, paperTypes);

            if (receipts.Count == 0)
            {
                TempData["ErrorMessage"] = "لا توجد بيانات استلام مطابقة للفلاتر الحالية لتصديرها.";
                return RedirectToAction(nameof(ReceiptLog), new { paperTypeId, year, fromDate, toDate });
            }

            var headers = new[]
            {
                "نوع الورق",
                "الكمية المستلمة",
                "العقد",
                "تاريخ الاستلام"
            };

            var rows = receipts.Select(r => new object?[]
            {
                r.PaperTypeName,
                r.QuantityReceived,
                r.ContractDisplay,
                r.ReceivedDate.ToEgyptTime().ToString("yyyy/MM/dd HH:mm")
            }).ToList();

            var content = SimpleXlsxWriter.Create("سجل استلام المخزون", headers, rows);
            var paperPart = paperTypeId.HasValue ? $"Paper_{paperTypeId.Value}" : "All_Papers";
            var yearPart = year.HasValue ? $"Year_{year.Value}" : "All_Years";
            var fromPart = fromDate?.ToString("yyyyMMdd") ?? "Start";
            var toPart = toDate?.ToString("yyyyMMdd") ?? "Today";
            var fileName = $"Inventory_Receipt_Log_{paperPart}_{yearPart}_{fromPart}_to_{toPart}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private List<InventoryReceiptRow> GetStockReceiptLogRows()
        {
            var dt = _db.ExecuteQuery("sp_GetStockReceiptLog");
            return dt.AsEnumerable().Select(r => new InventoryReceiptRow
            {
                ReceiptID = Convert.ToInt32(r["ReceiptID"]),
                PaperTypeName = r["PaperTypeName"].ToString() ?? "",
                QuantityReceived = Convert.ToInt32(r["QuantityReceived"]),
                ReceivedByName = r.Table.Columns.Contains("ReceivedByName") && r["ReceivedByName"] != DBNull.Value ? r["ReceivedByName"].ToString()! : "",
                ReceivedDate = Convert.ToDateTime(r["ReceivedDate"]),
                ContractID = r.Table.Columns.Contains("ContractID") && r["ContractID"] != DBNull.Value ? Convert.ToInt32(r["ContractID"]) : null,
                ContractNumber = r.Table.Columns.Contains("ContractNumber") && r["ContractNumber"] != DBNull.Value ? r["ContractNumber"].ToString() : null,
                ContractName = r.Table.Columns.Contains("ContractName") && r["ContractName"] != DBNull.Value ? r["ContractName"].ToString() : null
            }).ToList();
        }

        private static List<int> BuildReceiptYears(IEnumerable<InventoryReceiptRow> receipts)
        {
            return receipts
                .Select(r => r.ReceivedDate.ToEgyptTime().Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
        }

        private List<InventoryReceiptRow> FilterReceiptLog(
            IEnumerable<InventoryReceiptRow> source,
            int? paperTypeId,
            int? year,
            DateTime? fromDate,
            DateTime? toDate,
            IReadOnlyCollection<SelectListItem>? paperTypes = null)
        {
            var receipts = source.ToList();

            if (paperTypeId.HasValue)
            {
                paperTypes ??= GetPaperTypesSelectList().ToList();
                var selectedPaperName = paperTypes
                    .FirstOrDefault(p => p.Value == paperTypeId.Value.ToString())?.Text;

                if (!string.IsNullOrWhiteSpace(selectedPaperName))
                {
                    receipts = receipts
                        .Where(r => string.Equals(r.PaperTypeName.Trim(), selectedPaperName.Trim(), StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                else
                {
                    receipts.Clear();
                }
            }

            if (year.HasValue)
            {
                receipts = receipts
                    .Where(r => r.ReceivedDate.ToEgyptTime().Year == year.Value)
                    .ToList();
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                receipts = receipts
                    .Where(r => r.ReceivedDate.ToEgyptTime().Date >= from)
                    .ToList();
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                receipts = receipts
                    .Where(r => r.ReceivedDate.ToEgyptTime().Date <= to)
                    .ToList();
            }

            return receipts
                .OrderByDescending(r => r.ReceivedDate)
                .ThenByDescending(r => r.ReceiptID)
                .ToList();
        }

        private static void NormalizeReceiptLogDates(ref DateTime? fromDate, ref DateTime? toDate)
        {
            fromDate = fromDate?.Date;
            toDate = toDate?.Date;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }
        }

        // POST: /PaperManagement/Request/ReceiveStock
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult ReceiveStock(ReceiveStockViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "يرجى التأكد من إدخال كافة الحقول المطلوبة بشكل صحيح.";
                return RedirectToAction(nameof(Inventory));
            }

            try
            {
                ActivateDueInventoryContractIfPossible();
                _db.ExecuteNonQuery("sp_AddStockReceipt",
                    new SqlParameter("@PaperTypeID", model.PaperTypeID),
                    new SqlParameter("@QuantityReceived", model.QuantityReceived),
                    new SqlParameter("@ReceivedByID", GetCurrentUserId()));

                TempData["SuccessMessage"] = "تمت إضافة الكمية إلى المخزن وتحديث الرصيد بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر تسجيل الشحنة. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Inventory));
        }

        // POST: /PaperManagement/Request/UpdateLowStockThreshold
        [HttpPost]
        [Authorize(Roles = Roles.ITOrAdmin)]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateLowStockThreshold(int paperTypeId, int threshold)
        {
            try
            {
                _db.ExecuteNonQuery("sp_UpdateLowStockThreshold",
                    new SqlParameter("@PaperTypeID", paperTypeId),
                    new SqlParameter("@Threshold", threshold));

                TempData["SuccessMessage"] = "تم تحديث حد التنبيه لنفاد المخزن بنجاح.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "تعذر تحديث حد التنبيه. يرجى المحاولة لاحقاً.";
            }
            return RedirectToAction(nameof(Inventory));
        }

        // SSE Endpoint
        [HttpGet]
        [Authorize(Roles = Roles.Department + "," + Roles.ITOrAdmin + "," + Roles.Approver)]
        public async Task LiveQueueEvents(
            string? queue,
            CancellationToken cancellationToken)
        {
            // Department users may listen only to the harmless MyRequests change signal.
            // The event contains no request data; it only tells their own page to refresh.
            var audience = User.IsInRole(Roles.Department)
                ? QueueAudience.MyRequests
                : ResolveQueueAudience(queue);

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache, no-store";
            if (!Request.Protocol.StartsWith("HTTP/2", StringComparison.OrdinalIgnoreCase) &&
                !Request.Protocol.StartsWith("HTTP/3", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers.Connection = "keep-alive";
            }
            Response.Headers["X-Accel-Buffering"] = "no";

            var version = _queueUpdates.GetCurrentVersion(audience);

            await WriteQueueEventAsync("connected", version, cancellationToken);

            var changeTask = _queueUpdates.WaitForChangeAsync(
                audience,
                version,
                cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var heartbeatTask = Task.Delay(
                        TimeSpan.FromSeconds(15),
                        cancellationToken);

                    var completedTask = await Task.WhenAny(
                        changeTask,
                        heartbeatTask);

                    if (completedTask == changeTask)
                    {
                        version = await changeTask;
                        await WriteQueueEventAsync(
                            "queueChanged",
                            version,
                            cancellationToken);

                        changeTask = _queueUpdates.WaitForChangeAsync(
                            audience,
                            version,
                            cancellationToken);
                    }
                    else
                    {
                        await Response.WriteAsync(
                            ": keep-alive\n\n",
                            cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task WriteQueueEventAsync(
            string eventType,
            long version,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new
            {
                eventType,
                version,
                serverTimeUtc = DateTime.UtcNow
            });

            await Response.WriteAsync(
                $"event: {eventType}\ndata: {payload}\n\n",
                cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        private static string ResolveQueueAudience(string? queue) =>
            queue?.Trim().ToLowerInvariant() switch
            {
                "approver" => QueueAudience.Approver,
                "issue" => QueueAudience.Issue,
                "myrequests" => QueueAudience.MyRequests,
                _ => QueueAudience.Review
            };

        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult ActivityLog(int requestId)
        {
            var dt = _db.ExecuteQuery("sp_GetRequestActivityLog",
                new SqlParameter("@RequestID", requestId));

            var actionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Submitted"] = "تقديم الطلب",
                ["SubmittedException"] = "تقديم طلب استثناء",
                ["ITReviewed"] = "مراجعة أمين العهدة",
                ["ResentForApproval"] = "إعادة إرسال للاعتماد بعد المراجعة",
                ["ApprovedByApproverOne"] = "اعتماد نائب رئيس الإدارة",
                ["ApprovedByApproverTwo"] = "اعتماد مدير الإدارة",
                ["RejectedByApproverOne"] = "رفض نائب رئيس الإدارة",
                ["RejectedByApproverTwo"] = "رفض مدير الإدارة",
                ["RejectedByIT"] = "رفض نهائي من أمين العهدة",
                ["Issued"] = "تم الصرف"
            };

            // NOTE: sp_GetRequestActivityLog returns LogID, EventType, Note, Quantity,
            // CreatedDate, PerformedByName, PerformedByRole — it does NOT return
            // Action / ActionDetails / ActionDate / IsException / SubmittedByDepartment.
            // Reading those non-existent column names threw an exception on every
            // single call, which is why the log never loaded (it either failed fast
            // with "تعذر تحميل السجل." or, under load, ran past the 10s abort
            // timeout and showed "انتهت مهلة التحميل — حاول مرة أخرى.").
            var items = dt.AsEnumerable().Select(row =>
            {
                var eventType = row["EventType"].ToString() ?? "";
                var note = row["Note"] != DBNull.Value ? row["Note"].ToString() : null;
                var quantity = row["Quantity"] != DBNull.Value ? (int?)Convert.ToInt32(row["Quantity"]) : null;
                var label = actionLabels.TryGetValue(eventType, out var l) ? l : eventType;

                return new
                {
                    label,
                    note,
                    quantity,
                    performedByName = row["PerformedByName"] != DBNull.Value ? row["PerformedByName"].ToString() : "النظام",
                    createdDate = Convert.ToDateTime(row["CreatedDate"]).ToString("yyyy/MM/dd HH:mm")
                };
            }).ToList();

            return Json(items);
        }


        // GET: /PaperManagement/Request/DashboardRequestDetails
        // Read-only request snapshot used by the dashboard quick lookup popup.
        [HttpGet]
        [Authorize(Roles = Roles.ITOrAdmin + "," + Roles.Approver)]
        public IActionResult DashboardRequestDetails(int requestId)
        {
            if (requestId <= 0)
            {
                return BadRequest(new { success = false, message = "رقم الطلب غير صحيح." });
            }

            try
            {
                var request = FetchAllFilteredRequestItems(new RequestFilterViewModel
                {
                    SearchKeyword = requestId.ToString()
                }).FirstOrDefault(r => r.RequestID == requestId);

                if (request == null)
                {
                    return NotFound(new { success = false, message = "لم يتم العثور على الطلب." });
                }

                var badge = StatusDisplayHelper.GetBadgeInfo(request.Status);

                string? FormatDate(DateTime? value) => value.HasValue
                    ? value.Value.ToEgyptTime().ToString("yyyy-MM-dd HH:mm")
                    : null;

                return Json(new
                {
                    success = true,
                    requestId = request.RequestID,
                    departmentId = request.DepartmentID,
                    departmentName = request.DepartmentName,
                    paperTypeId = request.PaperTypeID,
                    paperTypeName = request.PaperTypeName,
                    requestedQuantity = request.RequestedQuantity,
                    status = request.Status,
                    statusLabel = badge.Label,
                    statusClasses = badge.CssClasses,
                    isException = request.IsException,
                    needsReReview = request.Status == RequestStatus.PendingITReReview,
                    notes = request.Notes,
                    itReviewNotes = request.ITReviewNotes,
                    approverOneNote = request.ApproverOneNote,
                    approverTwoNote = request.ApproverTwoNote,
                    rejectionNote = request.RejectionNote,
                    issueNotes = request.IssueNotes,
                    requestDate = FormatDate(request.RequestDate),
                    itReviewedDate = FormatDate(request.ITReviewedDate),
                    itReviewedByName = request.ITReviewedByName,
                    approverOneApprovedDate = FormatDate(request.ApproverOneApprovedDate),
                    approverTwoApprovedDate = FormatDate(request.ApproverTwoApprovedDate),
                    approvedDate = FormatDate(request.ApprovedDate),
                    approvedByName = request.ApprovedByName,
                    rejectedDate = FormatDate(request.RejectedDate),
                    rejectedByName = request.RejectedByName,
                    issuedDate = FormatDate(request.IssuedDate),
                    issuedByName = request.IssuedByName
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "تعذر تحميل تفاصيل الطلب حالياً." });
            }
        }

        #region Helper Methods

        /// <summary>
        /// Reads every row that matches a request filter by paging through
        /// sp_GetFilteredRequests. The stored procedure caps a single page at
        /// 200 rows, so asking it for 5,000 rows actually falls back to 25.
        /// Keeping this paging logic here makes dashboard totals and department
        /// histories accurate even after years of accumulated requests.
        /// </summary>
        private List<RequestItemViewModel> FetchAllFilteredRequestItems(RequestFilterViewModel filter)
        {
            const int pageSize = 200;
            var firstPage = FetchFilteredRequests(filter, pageNumber: 1, pageSize: pageSize);
            var items = MapDataTableToRequests(firstPage);

            if (firstPage.Rows.Count == 0)
            {
                return items;
            }

            int totalCount = firstPage.Columns.Contains("TotalCount")
                ? GetInt(firstPage.Rows[0], "TotalCount", firstPage.Rows.Count)
                : firstPage.Rows.Count;

            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            for (int page = 2; page <= totalPages; page++)
            {
                var pageDt = FetchFilteredRequests(filter, pageNumber: page, pageSize: pageSize);
                if (pageDt.Rows.Count == 0) break;
                items.AddRange(MapDataTableToRequests(pageDt));
            }

            return items;
        }

        private DataTable FetchFilteredRequests(
            RequestFilterViewModel filter,
            int pageNumber = 1,
            int pageSize = 25,
            string? prioritizeStatus1 = null,
            string? prioritizeStatus2 = null)
        {
            int? deptId = filter.DepartmentID;
            if (!deptId.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
            {
                deptId = filter.SelectedDepartmentIDs[0];
            }

            object toDateParam = (filter.ToDate.HasValue && filter.ToDate.Value.TimeOfDay == TimeSpan.Zero)
                ? (object)filter.ToDate.Value.Date.AddDays(1)
                : ((object?)filter.ToDate ?? DBNull.Value);

            string? departmentIds = filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count > 1
                ? string.Join(",", filter.SelectedDepartmentIDs.Distinct().OrderBy(id => id))
                : null;

            return _db.ExecuteQuery("sp_GetFilteredRequests",
                new SqlParameter("@DepartmentID", (object?)deptId ?? DBNull.Value),
                new SqlParameter("@DepartmentIDs", (object?)departmentIds ?? DBNull.Value),
                new SqlParameter("@PaperTypeID", (object?)filter.PaperTypeID ?? DBNull.Value),
                new SqlParameter("@Status", (object?)filter.Status ?? DBNull.Value),
                new SqlParameter("@PendingStages", (object?)filter.PendingStages ?? DBNull.Value),
                new SqlParameter("@FromDate", (object?)filter.FromDate ?? DBNull.Value),
                new SqlParameter("@ToDate", toDateParam),
                new SqlParameter("@SearchKeyword", (object?)filter.SearchKeyword ?? DBNull.Value),
                new SqlParameter("@PageNumber", pageNumber < 1 ? 1 : pageNumber),
                new SqlParameter("@PageSize", pageSize),
                new SqlParameter("@PrioritizeStatus1", (object?)prioritizeStatus1 ?? DBNull.Value),
                new SqlParameter("@PrioritizeStatus2", (object?)prioritizeStatus2 ?? DBNull.Value)
            );
        }

        private DataTable FetchApproverRequests(
            int approverId,
            RequestFilterViewModel filter,
            int pageNumber = 1,
            int pageSize = 25)
        {
            int? deptId = filter.DepartmentID;
            if (!deptId.HasValue && filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count == 1)
            {
                deptId = filter.SelectedDepartmentIDs[0];
            }

            object toDateParam = (filter.ToDate.HasValue && filter.ToDate.Value.TimeOfDay == TimeSpan.Zero)
                ? (object)filter.ToDate.Value.Date.AddDays(1)
                : ((object?)filter.ToDate ?? DBNull.Value);

            string? departmentIds = filter.SelectedDepartmentIDs != null && filter.SelectedDepartmentIDs.Count > 1
                ? string.Join(",", filter.SelectedDepartmentIDs.Distinct().OrderBy(id => id))
                : null;

            return _db.ExecuteQuery("sp_GetApproverRequestView",
                new SqlParameter("@ApproverID", approverId),
                new SqlParameter("@DepartmentID", (object?)deptId ?? DBNull.Value),
                new SqlParameter("@DepartmentIDs", (object?)departmentIds ?? DBNull.Value),
                new SqlParameter("@PaperTypeID", (object?)filter.PaperTypeID ?? DBNull.Value),
                new SqlParameter("@Status", (object?)filter.Status ?? DBNull.Value),
                new SqlParameter("@PendingStages", (object?)filter.PendingStages ?? DBNull.Value),
                new SqlParameter("@FromDate", (object?)filter.FromDate ?? DBNull.Value),
                new SqlParameter("@ToDate", toDateParam),
                new SqlParameter("@SearchKeyword", (object?)filter.SearchKeyword ?? DBNull.Value),
                new SqlParameter("@PageNumber", pageNumber < 1 ? 1 : pageNumber),
                new SqlParameter("@PageSize", pageSize)
            );
        }

        private void PopulatePendingStageCounts(RequestFilterViewModel filter)
        {
            // Count each sub-stage independently, while preserving every other active
            // filter (department(s), paper type, dates and search keyword).
            int CountForStage(string stage)
            {
                var countFilter = new RequestFilterViewModel
                {
                    DepartmentID = filter.DepartmentID,
                    PaperTypeID = filter.PaperTypeID,
                    Status = "Pending",
                    PendingStages = stage,
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    SearchKeyword = filter.SearchKeyword,
                    DepartmentGroup = filter.DepartmentGroup,
                    SelectedDepartmentIDs = filter.SelectedDepartmentIDs?.ToList() ?? new List<int>()
                };

                var dt = FetchFilteredRequests(countFilter, pageNumber: 1, pageSize: 1);
                return dt.Rows.Count > 0 && dt.Columns.Contains("TotalCount")
                    ? Convert.ToInt32(dt.Rows[0]["TotalCount"])
                    : 0;
            }

            filter.PendingITReviewCount = CountForStage("ITReview");
            filter.PendingApproverOneCount = CountForStage("DeputyIT");
            filter.PendingApproverTwoCount = CountForStage("ITDepartment");
        }

        private void PopulateFilterLists(RequestFilterViewModel filter, bool includeStatusFilter = true)
        {
            if (includeStatusFilter)
            {
                filter.Statuses = new List<SelectListItem>
                {
                    new SelectListItem
                    {
                        Value = "Pending",
                        Text = "قيد الانتظار"
                    },
                    new SelectListItem
                    {
                        Value = RequestStatus.Approved,
                        Text = "تمت الموافقة"
                    },
                    new SelectListItem
                    {
                        Value = RequestStatus.Issued,
                        Text = "تم الصرف"
                    },
                    new SelectListItem
                    {
                        Value = "Rejected",
                        Text = "مرفوض"
                    }
                };
            }

            filter.Departments = GetDepartmentsSelectList();
            filter.PaperTypes = GetPaperTypesSelectList();
        }

        private IEnumerable<SelectListItem> GetDepartmentsSelectList()
        {
            var dt = _db.ExecuteQuery("sp_GetDepartments");
            var list = new List<SelectListItem>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new SelectListItem
                {
                    Value = row["DepartmentID"].ToString(),
                    Text = row["DepartmentName"].ToString()
                });
            }
            return list;
        }

        private List<InventoryMovementRow> GetInventoryMovementRows(
            int? contractId = null,
            int? paperTypeId = null,
            string? movementType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var dt = _db.ExecuteQuery("sp_GetInventoryMovementLedger",
                new SqlParameter("@ContractID", (object?)contractId ?? DBNull.Value),
                new SqlParameter("@PaperTypeID", (object?)paperTypeId ?? DBNull.Value),
                new SqlParameter("@MovementType", string.IsNullOrWhiteSpace(movementType) ? DBNull.Value : movementType),
                new SqlParameter("@FromDate", (object?)fromDate?.Date ?? DBNull.Value),
                new SqlParameter("@ToDate", (object?)toDate?.Date ?? DBNull.Value));

            return dt.Rows.Cast<DataRow>()
                .Select(r => new InventoryMovementRow
                {
                    EventDate = Convert.ToDateTime(r["EventDate"]),
                    MovementType = r["MovementType"].ToString() ?? string.Empty,
                    PaperTypeID = r["PaperTypeID"] != DBNull.Value ? Convert.ToInt32(r["PaperTypeID"]) : null,
                    PaperTypeName = r["PaperTypeName"] != DBNull.Value ? r["PaperTypeName"].ToString() : null,
                    QuantityChange = r["QuantityChange"] != DBNull.Value ? Convert.ToInt64(r["QuantityChange"]) : null,
                    ContractID = r["ContractID"] != DBNull.Value ? Convert.ToInt32(r["ContractID"]) : null,
                    ContractNumber = r["ContractNumber"] != DBNull.Value ? r["ContractNumber"].ToString() : null,
                    ContractName = r["ContractName"] != DBNull.Value ? r["ContractName"].ToString() : null,
                    ReferenceType = r["ReferenceType"] != DBNull.Value ? r["ReferenceType"].ToString() : null,
                    ReferenceID = r["ReferenceID"] != DBNull.Value ? r["ReferenceID"].ToString() : null,
                    PerformedByName = r["PerformedByName"] != DBNull.Value ? r["PerformedByName"].ToString() ?? string.Empty : string.Empty,
                    Details = r["Details"] != DBNull.Value ? r["Details"].ToString() : null
                })
                .ToList();
        }

        private void EnrichInventoryContractsWithTypeSummaries(List<InventoryContractViewModel> contracts)
        {
            foreach (var contract in contracts)
            {
                var balancesDt = _db.ExecuteQuery("sp_GetInventoryContractBalances",
                    new SqlParameter("@ContractID", contract.ContractID));

                var balances = balancesDt.Rows.Cast<DataRow>()
                    .Select(r => new InventoryContractBalanceViewModel
                    {
                        PaperTypeID = Convert.ToInt32(r["PaperTypeID"]),
                        PaperTypeName = r["PaperTypeName"].ToString() ?? string.Empty,
                        OpeningStock = Convert.ToInt32(r["OpeningStock"]),
                        ReceivedQuantity = Convert.ToInt32(r["ReceivedQuantity"]),
                        IssuedQuantity = Convert.ToInt32(r["IssuedQuantity"]),
                        ClosingStock = r["ClosingStock"] != DBNull.Value ? Convert.ToInt32(r["ClosingStock"]) : null,
                        CurrentPhysicalStock = Convert.ToInt32(r["CurrentPhysicalStock"]),
                        CalculatedContractBalance = Convert.ToInt32(r["CalculatedContractBalance"])
                    })
                    .ToList();

                contract.A4Summary = BuildInventoryContractTypeSummary(balances, IsA4Paper, "A4");
                contract.A3Summary = BuildInventoryContractTypeSummary(balances, IsA3Paper, "A3");
            }
        }

        private static InventoryContractTypeSummaryViewModel? BuildInventoryContractTypeSummary(
            IEnumerable<InventoryContractBalanceViewModel> balances,
            Func<string?, bool> matcher,
            string label)
        {
            var row = balances.FirstOrDefault(b => matcher(b.PaperTypeName));
            if (row == null)
            {
                return null;
            }

            return new InventoryContractTypeSummaryViewModel
            {
                Label = label,
                OpeningStock = row.OpeningStock,
                ReceivedQuantity = row.ReceivedQuantity,
                IssuedQuantity = row.IssuedQuantity,
                ClosingStock = row.ClosingStock,
                CurrentPhysicalStock = row.CurrentPhysicalStock
            };
        }

        public static bool IsA4Paper(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            return string.Equals(trimmed, "A4", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("A4 ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("A4-", StringComparison.OrdinalIgnoreCase)
                || trimmed.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsA3Paper(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            return string.Equals(trimmed, "A3", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("A3 ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("A3-", StringComparison.OrdinalIgnoreCase)
                || trimmed.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<SelectListItem> GetPaperTypesSelectList(bool excludeA3 = false)
        {
            var dt = _db.ExecuteQuery("sp_GetPaperTypes");
            var list = new List<SelectListItem>();
            foreach (DataRow row in dt.Rows)
            {
                var text = row["PaperTypeName"].ToString() ?? string.Empty;
                if (excludeA3 && IsA3Paper(text))
                {
                    continue;
                }

                list.Add(new SelectListItem
                {
                    Value = row["PaperTypeID"].ToString(),
                    Text = text
                });
            }
            return list;
        }

        private int GetCurrentUserId()
        {
            string? val = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(val, out int userId) ? userId : 1;
        }

        private List<RequestItemViewModel> MapDataTableToRequests(DataTable dt)
        {
            var list = new List<RequestItemViewModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new RequestItemViewModel
                {
                    RequestID = Convert.ToInt32(row["RequestID"]),
                    DepartmentID = Convert.ToInt32(row["DepartmentID"]),
                    DepartmentName = dt.Columns.Contains("DepartmentName") ? row["DepartmentName"].ToString() ?? "" : "",
                    QuotaLimit = GetInt(row, "DepartmentQuota", 0),
                    DepartmentUsedQuota = GetInt(row, "DepartmentUsedQuota", 0),
                    PaperTypeID = Convert.ToInt32(row["PaperTypeID"]),
                    PaperTypeName = dt.Columns.Contains("PaperTypeName") ? row["PaperTypeName"].ToString() ?? "" : "",
                    RequestedQuantity = Convert.ToInt32(row["RequestedQuantity"]),
                    Status = row["Status"].ToString() ?? "",
                    Notes = row["Notes"] != DBNull.Value ? row["Notes"].ToString() : null,
                    ITReviewNotes = dt.Columns.Contains("ITReviewNotes") && row["ITReviewNotes"] != DBNull.Value ? row["ITReviewNotes"].ToString() : null,
                    RejectionNote = dt.Columns.Contains("RejectionNote") && row["RejectionNote"] != DBNull.Value ? row["RejectionNote"].ToString() : null,
                    IssueNotes = dt.Columns.Contains("IssueNotes") && row["IssueNotes"] != DBNull.Value ? row["IssueNotes"].ToString() : null,
                    IsException = dt.Columns.Contains("IsException") && row["IsException"] != DBNull.Value && Convert.ToBoolean(row["IsException"]),
                    RequestDate = Convert.ToDateTime(row["RequestDate"]),
                    ITReviewedDate = dt.Columns.Contains("ITReviewedDate") && row["ITReviewedDate"] != DBNull.Value ? Convert.ToDateTime(row["ITReviewedDate"]) : null,
                    ITReviewedByName = dt.Columns.Contains("ITReviewedByName") && row["ITReviewedByName"] != DBNull.Value ? row["ITReviewedByName"].ToString() : null,
                    ApprovedDate = row["ApprovedDate"] != DBNull.Value ? Convert.ToDateTime(row["ApprovedDate"]) : null,
                    ApprovedByName = dt.Columns.Contains("ApprovedByName") && row["ApprovedByName"] != DBNull.Value ? row["ApprovedByName"].ToString() : null,
                    RejectedDate = dt.Columns.Contains("RejectedDate") && row["RejectedDate"] != DBNull.Value ? Convert.ToDateTime(row["RejectedDate"]) : null,
                    RejectedByName = dt.Columns.Contains("RejectedByName") && row["RejectedByName"] != DBNull.Value ? row["RejectedByName"].ToString() : null,
                    IssuedDate = row["IssuedDate"] != DBNull.Value ? Convert.ToDateTime(row["IssuedDate"]) : null,
                    IssuedByName = dt.Columns.Contains("IssuedByName") && row["IssuedByName"] != DBNull.Value ? row["IssuedByName"].ToString() : null,
                    ApproverOneNote = dt.Columns.Contains("ApproverOneNote") && row["ApproverOneNote"] != DBNull.Value ? row["ApproverOneNote"].ToString() : null,
                    ApproverTwoNote = dt.Columns.Contains("ApproverTwoNote") && row["ApproverTwoNote"] != DBNull.Value ? row["ApproverTwoNote"].ToString() : null,
                    ApproverOneApprovedDate = dt.Columns.Contains("ApproverOneApprovedDate") && row["ApproverOneApprovedDate"] != DBNull.Value ? Convert.ToDateTime(row["ApproverOneApprovedDate"]) : null,
                    ApproverTwoApprovedDate = dt.Columns.Contains("ApproverTwoApprovedDate") && row["ApproverTwoApprovedDate"] != DBNull.Value ? Convert.ToDateTime(row["ApproverTwoApprovedDate"]) : null
                });
            }
            return list;
        }

        private void ActivateDueInventoryContractIfPossible()
        {
            // Fast no-op when there is no contract waiting for its start date.
            // Activates only after the official start date and only if no other contract is active.
            _db.ExecuteNonQuery("sp_ActivateDueInventoryContract");
        }

        private void MarkScheduledContracts(List<InventoryContractViewModel> contracts)
        {
            var pending = _db.ExecuteQuery("sp_GetScheduledInventoryContracts");
            var pendingIds = pending.Rows.Cast<DataRow>()
                .Select(r => Convert.ToInt32(r["ContractID"]))
                .ToHashSet();
            foreach (var contract in contracts)
                contract.IsScheduled = pendingIds.Contains(contract.ContractID);
        }

        private static InventoryContractViewModel MapInventoryContractRow(DataRow row)
        {
            return new InventoryContractViewModel
            {
                ContractID = Convert.ToInt32(row["ContractID"]),
                ContractNumber = row.Table.Columns.Contains("ContractNumber") && row["ContractNumber"] != DBNull.Value ? row["ContractNumber"].ToString() : null,
                ContractName = row["ContractName"].ToString() ?? string.Empty,
                StartDate = Convert.ToDateTime(row["StartDate"]),
                EndDate = row.Table.Columns.Contains("EndDate") && row["EndDate"] != DBNull.Value ? Convert.ToDateTime(row["EndDate"]) : null,
                QuantityA4 = row.Table.Columns.Contains("QuantityA4") && row["QuantityA4"] != DBNull.Value ? Convert.ToInt32(row["QuantityA4"]) : null,
                QuantityA3 = row.Table.Columns.Contains("QuantityA3") && row["QuantityA3"] != DBNull.Value ? Convert.ToInt32(row["QuantityA3"]) : null,
                IsActive = row.Table.Columns.Contains("IsActive") && row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]),
                Notes = row.Table.Columns.Contains("Notes") && row["Notes"] != DBNull.Value ? row["Notes"].ToString() : null,
                CreatedUtc = row.Table.Columns.Contains("CreatedUtc") && row["CreatedUtc"] != DBNull.Value ? Convert.ToDateTime(row["CreatedUtc"]) : DateTime.MinValue,
                ClosedUtc = row.Table.Columns.Contains("ClosedUtc") && row["ClosedUtc"] != DBNull.Value ? Convert.ToDateTime(row["ClosedUtc"]) : null,
                CreatedByName = row.Table.Columns.Contains("CreatedByName") && row["CreatedByName"] != DBNull.Value ? row["CreatedByName"].ToString() ?? string.Empty : string.Empty,
                ClosedByName = row.Table.Columns.Contains("ClosedByName") && row["ClosedByName"] != DBNull.Value ? row["ClosedByName"].ToString() : null,
                CloseNote = row.Table.Columns.Contains("CloseNote") && row["CloseNote"] != DBNull.Value ? row["CloseNote"].ToString() : null,
                OpeningStock = row.Table.Columns.Contains("OpeningStock") && row["OpeningStock"] != DBNull.Value ? Convert.ToInt64(row["OpeningStock"]) : 0,
                ReceivedQuantity = row.Table.Columns.Contains("ReceivedQuantity") && row["ReceivedQuantity"] != DBNull.Value ? Convert.ToInt64(row["ReceivedQuantity"]) : 0,
                IssuedQuantity = row.Table.Columns.Contains("IssuedQuantity") && row["IssuedQuantity"] != DBNull.Value ? Convert.ToInt64(row["IssuedQuantity"]) : 0,
                ClosingStock = row.Table.Columns.Contains("ClosingStock") && row["ClosingStock"] != DBNull.Value ? Convert.ToInt64(row["ClosingStock"]) : null,
                CurrentPhysicalStock = row.Table.Columns.Contains("CurrentPhysicalStock") && row["CurrentPhysicalStock"] != DBNull.Value ? Convert.ToInt64(row["CurrentPhysicalStock"]) : 0
            };
        }

        private static int GetInt(DataRow row, string columnName, int fallback)
            => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToInt32(row[columnName])
                : fallback;

        #endregion
    }
}
