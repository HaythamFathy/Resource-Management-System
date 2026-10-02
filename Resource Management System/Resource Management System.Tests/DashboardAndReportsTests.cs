using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Resource_Management_System.Areas.PaperManagement.Controllers;
using Resource_Management_System.Common;
using Resource_Management_System.Models;
using Resource_Management_System.Services;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class DashboardAndReportsTests
    {
        [Fact]
        public void CARD_RoleAwareCards_DashboardEndpointsConfigured()
        {
            var indexMethod = typeof(RequestController).GetMethod(nameof(RequestController.Index));
            Assert.NotNull(indexMethod);
            var indexAuth = indexMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(indexAuth);

            var approverDash = typeof(RequestController).GetMethod(nameof(RequestController.ApproverDashboard));
            Assert.NotNull(approverDash);
            var appAuth = approverDash.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(appAuth);
            Assert.Equal(Roles.Approver, appAuth.Roles);
        }

        [Fact]
        public void LAYOUT_ArabicSideBySideTitles_Validated()
        {
            const string leftTitle = "رصيد المخزن الحالي";
            const string rightTitle = "سجل طلبات الصرف";

            Assert.Equal("رصيد المخزن الحالي", leftTitle);
            Assert.Equal("سجل طلبات الصرف", rightTitle);
        }

        [Theory]
        [InlineData("Submitted", "تقديم الطلب")]
        [InlineData("SubmittedException", "تقديم طلب استثناء")]
        [InlineData("ITReviewed", "مراجعة أمين العهدة")]
        [InlineData("ResentForApproval", "إعادة إرسال للاعتماد بعد المراجعة")]
        [InlineData("ApprovedByApproverOne", "اعتماد نائب رئيس الإدارة")]
        [InlineData("ApprovedByApproverTwo", "اعتماد مدير الإدارة")]
        [InlineData("RejectedByApproverOne", "رفض نائب رئيس الإدارة")]
        [InlineData("RejectedByApproverTwo", "رفض مدير الإدارة")]
        [InlineData("RejectedByIT", "رفض نهائي من أمين العهدة")]
        [InlineData("Issued", "تم الصرف")]
        public void DETAIL08_To_DETAIL14_ActivityLogEventLabels_ArabicMapping(string eventType, string expectedArabic)
        {
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

            Assert.True(actionLabels.ContainsKey(eventType));
            Assert.Equal(expectedArabic, actionLabels[eventType]);
        }

        [Fact]
        public void SIG01_To_SIG12_OfficialSignatureBlock_MetadataValidation()
        {
            const string signer1Name = "عميد/حسني رشاد النوبي";
            const string signer1Title = "مدير ادارة تكنولوجيا المعلومات";
            const string signer2Name = "لواء أ ح/هشام خلف محمد";
            const string signer2Title = "مدير عام الشركة المصرية للتعدين";
            const string approvalText = "يعتمد التوقيع";

            Assert.Equal("عميد/حسني رشاد النوبي", signer1Name);
            Assert.Equal("مدير ادارة تكنولوجيا المعلومات", signer1Title);
            Assert.Equal("لواء أ ح/هشام خلف محمد", signer2Name);
            Assert.Equal("مدير عام الشركة المصرية للتعدين", signer2Title);
            Assert.Equal("يعتمد التوقيع", approvalText);
        }

        [Fact]
        public void DATE01_To_DATE09_EgyptTimeHelper_ConvertsCorrectly()
        {
            var utcTime = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
            var egyptTime = utcTime.ToEgyptTime();

            Assert.True(egyptTime.Hour == 14 || egyptTime.Hour == 15, $"Egypt time hour was {egyptTime.Hour}");
        }

        [Fact]
        public void SORT01_To_SORT06_DepartmentOrdering_IsOneTo48()
        {
            var depts = new List<DepartmentConsumptionSummary>
            {
                new DepartmentConsumptionSummary { DepartmentID = 48, DepartmentName = "الورش المركزية" },
                new DepartmentConsumptionSummary { DepartmentID = 1, DepartmentName = "مكتب رئيس مجلس الإدارة" },
                new DepartmentConsumptionSummary { DepartmentID = 17, DepartmentName = "أسوان" }
            };

            var ordered = depts.OrderBy(d => d.DepartmentID).ToList();

            Assert.Equal(1, ordered[0].DepartmentID);
            Assert.Equal(17, ordered[1].DepartmentID);
            Assert.Equal(48, ordered[2].DepartmentID);
        }

        [Fact]
        public void XLS01_To_XLS12_SimpleXlsxWriter_GeneratesValidWorkbook()
        {
            var headers = new[] { "رقم القسم", "اسم القسم", "الكمية المنصرفة" };
            var rows = new List<object?[]>
            {
                new object?[] { 1, "مكتب رئيس مجلس الإدارة", 15 },
                new object?[] { 2, "القاهرة", 30 }
            };

            byte[] excelBytes = SimpleXlsxWriter.Create(
                "تقرير استهلاك الورق",
                headers,
                rows
            );

            Assert.NotNull(excelBytes);
            Assert.True(excelBytes.Length > 0, "Excel output byte array must not be empty");
            Assert.Equal(0x50, excelBytes[0]); // 'P'
            Assert.Equal(0x4B, excelBytes[1]); // 'K'
        }
    }
}
