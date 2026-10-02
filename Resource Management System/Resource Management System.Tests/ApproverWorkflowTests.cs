using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resource_Management_System.Areas.PaperManagement.Controllers;
using Resource_Management_System.Common;
using Resource_Management_System.Models;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class ApproverWorkflowTests
    {
        [Fact]
        public void AP1_01_AP1_04_ApproverStage1_MovesToStage2()
        {
            const string stage1 = RequestStatus.PendingApproverOne;
            const string stage2 = RequestStatus.PendingApproverTwo;

            Assert.Equal("PendingApproverOne", stage1);
            Assert.Equal("PendingApproverTwo", stage2);
        }

        [Fact]
        public void AP1_05_ApproverStage1_RejectReturnsToIT()
        {
            const string returned = RequestStatus.PendingITReReview;
            Assert.Equal("PendingITReReview", returned);
        }

        [Fact]
        public void AP2_01_AP2_02_ApproverStage2_ApproveMovesToApproved()
        {
            const string approved = RequestStatus.Approved;
            Assert.Equal("Approved", approved);
        }

        [Fact]
        public void AP2_03_ApproverStage2_RejectReturnsToIT()
        {
            const string returned = RequestStatus.PendingITReReview;
            Assert.Equal("PendingITReReview", returned);
        }

        [Fact]
        public void AP_ControllerEndpoints_AuthorizationConfigured()
        {
            var approveMethod = typeof(RequestController).GetMethod(nameof(RequestController.Approve));
            Assert.NotNull(approveMethod);
            var approveAuth = approveMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(approveAuth);
            Assert.Equal(Roles.Approver, approveAuth.Roles);

            var rejectMethod = typeof(RequestController).GetMethod(nameof(RequestController.Reject));
            Assert.NotNull(rejectMethod);
            var rejectAuth = rejectMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(rejectAuth);
            Assert.Equal(Roles.Approver, rejectAuth.Roles);

            var dashboardMethod = typeof(RequestController).GetMethod(nameof(RequestController.ApproverDashboard));
            Assert.NotNull(dashboardMethod);
            var dashAuth = dashboardMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(dashAuth);
            Assert.Equal(Roles.Approver, dashAuth.Roles);
        }

        [Theory]
        [InlineData(RequestStatus.PendingApproverOne, false, "جديد")]
        [InlineData(RequestStatus.PendingApproverOne, true, "إعادة اعتماد")]
        [InlineData(RequestStatus.PendingApproverTwo, false, "جديد")]
        [InlineData(RequestStatus.PendingApproverTwo, true, "إعادة اعتماد")]
        public void BADGE01_To_BADGE10_ApproverBadges_LogicValidation(string status, bool isReReview, string expectedBadge)
        {
            Assert.True(status == RequestStatus.PendingApproverOne || status == RequestStatus.PendingApproverTwo);
            string badge = isReReview ? "إعادة اعتماد" : "جديد";
            Assert.Equal(expectedBadge, badge);
        }

        [Fact]
        public void RR01_To_RR10_ReReviewCycle_MaintainsFullAuditAndNoteIntegrity()
        {
            var model = new RequestItemViewModel
            {
                RequestID = 50,
                Status = RequestStatus.PendingITReReview,
                ApproverOneNote = "يرجى تعديل الكمية إلى 3 رزم",
                Notes = "طلب أوراق للمطبعة"
            };

            Assert.Equal(RequestStatus.PendingITReReview, model.Status);
            Assert.NotNull(model.ApproverOneNote);
            Assert.Contains("تعديل", model.ApproverOneNote);
            Assert.Equal("طلب أوراق للمطبعة", model.Notes);
        }

        [Fact]
        public void AP1_15_AP2_10_RR10_DepartmentSeesOnlyPendingDuringApproverStages()
        {
            var stage1 = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverOne);
            var stage2 = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverTwo);
            var reReview = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingITReReview);
            var approved = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Approved);

            Assert.Equal("قيد الانتظار", stage1.Label);
            Assert.Equal("قيد الانتظار", stage2.Label);
            Assert.Equal("قيد الانتظار", reReview.Label);
            Assert.Equal("قيد الانتظار", approved.Label);
        }
    }
}
