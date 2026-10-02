namespace Resource_Management_System.Models
{
    public class ReportsCenterModel
    {
        public string Tab { get; set; } = "contracts";
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public int? ContractId { get; set; }
        public string Detail { get; set; } = "summary";
        public List<int> DepartmentIds { get; set; } = new();
        public List<int> YearOptions { get; set; } = new();
        public int FirstYear { get; set; }
        public string Period { get; set; } = "year";
        public int? Year { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public string? Error { get; set; }
        public List<ReportContract> Contracts { get; set; } = new();
        public List<ReportContract> VisibleContracts { get; set; } = new();
        public ReportContract? SelectedContract { get; set; }
        public List<string> PaperTypes { get; set; } = new();
        public List<ReportPaperTotal> PaperTotals { get; set; } = new();
        public List<ReportDepartment> Departments { get; set; } = new();
        public List<ReportDepartment> DepartmentIssues { get; set; } = new();
        // Only show paper columns for which there is an actual issue in this report scope.
        public List<string> VisibleIssueTypes => PaperTypes
            .Where(type => DepartmentIssues.Any(dept => dept.Quantity(type) > 0)).ToList();
        public long Group1Issued { get; set; }
        public long Group2Issued { get; set; }
    }

    public class ReportContract
    {
        public int ContractID { get; set; }
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public bool Scheduled { get; set; }
        public long? ContractedA4 { get; set; }
        public long? ContractedA3 { get; set; }
        public long Opening { get; set; }
        public long Received { get; set; }
        public long Issued { get; set; }
        public long? Closing { get; set; }
        public long Balance => Closing ?? Opening + Received - Issued;
        public string Status => IsActive ? "نشط" : (Scheduled || StartDate.Date > DateTime.UtcNow.Date) ? "لم يبدأ بعد" : "مغلق";
    }

    public class ReportPaperTotal
    {
        public string Name { get; set; } = "";
        public long Opening { get; set; }
        public long Received { get; set; }
        public long Issued { get; set; }
        public long? Balance { get; set; }
    }

    public class ReportDepartment
    {
        public int ID { get; set; }
        public string Name { get; set; } = "";
        public string GroupName => ID <= 24 ? "إدارات وأفرع الشركة" : "القطاعات والوحدات التابعة";
        public Dictionary<string, long> ByPaper { get; set; } = new();
        public long Total => ByPaper.Values.Sum();
        public long Quantity(string paperType) => ByPaper.TryGetValue(paperType, out var quantity) ? quantity : 0;
    }
}
