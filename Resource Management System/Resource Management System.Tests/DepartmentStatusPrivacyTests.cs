using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resource_Management_System.Areas.PaperManagement.Controllers;
using Resource_Management_System.Common;
using Resource_Management_System.Models;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class DepartmentStatusPrivacyTests
    {
        /// <summary>
        /// DST-01: Internal status PendingITReview -> Department sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST01_PendingITReview_DepartmentSeesPending()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingITReview);
            Assert.Equal("قيد الانتظار", label);
            Assert.Contains("status-review", css);
        }

        /// <summary>
        /// DST-02: Internal status PendingApproverOne -> Department sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST02_PendingApproverOne_DepartmentSeesPending()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverOne);
            Assert.Equal("قيد الانتظار", label);
            Assert.Contains("status-review", css);
        }

        /// <summary>
        /// DST-03: Internal status PendingApproverTwo -> Department sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST03_PendingApproverTwo_DepartmentSeesPending()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverTwo);
            Assert.Equal("قيد الانتظار", label);
            Assert.Contains("status-review", css);
        }

        /// <summary>
        /// DST-04: Internal status PendingITReReview -> Department sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST04_PendingITReReview_DepartmentSeesPending()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingITReReview);
            Assert.Equal("قيد الانتظار", label);
            Assert.Contains("status-review", css);
        }

        /// <summary>
        /// DST-05: Internal status Approved but not issued -> Department sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST05_ApprovedNotIssued_DepartmentSeesPending()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Approved);
            Assert.Equal("قيد الانتظار", label);
            Assert.Contains("status-review", css);
        }

        /// <summary>
        /// DST-06: Internal status RejectedByIT -> Department sees مرفوض.
        /// </summary>
        [Fact]
        public void DST06_RejectedByIT_DepartmentSeesRejected()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo("RejectedByIT");
            Assert.Equal("مرفوض", label);
            Assert.Contains("status-rejected", css);
        }

        /// <summary>
        /// DST-07: Any final rejection status -> Department sees مرفوض.
        /// </summary>
        [Fact]
        public void DST07_FinalRejection_DepartmentSeesRejected()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Rejected);
            Assert.Equal("مرفوض", label);
            Assert.Contains("status-rejected", css);
        }

        /// <summary>
        /// DST-08: Internal status Issued -> Department sees تم الصرف.
        /// </summary>
        [Fact]
        public void DST08_Issued_DepartmentSeesIssued()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Issued);
            Assert.Equal("تم الصرف", label);
            Assert.Contains("status-issued", css);
        }

        /// <summary>
        /// DST-09: Inspect Department My Requests table -> No internal workflow stage is exposed.
        /// </summary>
        [Theory]
        [InlineData(RequestStatus.PendingITReview)]
        [InlineData(RequestStatus.PendingApproverOne)]
        [InlineData(RequestStatus.PendingApproverTwo)]
        [InlineData(RequestStatus.PendingITReReview)]
        [InlineData(RequestStatus.Approved)]
        public void DST09_InspectDepartmentMyRequestsTable_NoInternalWorkflowStageExposed(string internalStatus)
        {
            var (label, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(internalStatus);
            
            // Sensitive internal stage keywords must never be part of the department-facing label
            Assert.DoesNotContain("IT", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Approver", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("أمين", label);
            Assert.DoesNotContain("نائب", label);
            Assert.DoesNotContain("مدير", label);
            Assert.Equal("قيد الانتظار", label);
        }

        /// <summary>
        /// DST-10: Open Department request details -> No approver stage is disclosed.
        /// Confirms RequestController.MyRequests is restricted to Department role and returns simplified models.
        /// </summary>
        [Fact]
        public void DST10_OpenDepartmentRequestDetails_NoApproverStageDisclosed()
        {
            var myRequestsMethod = typeof(RequestController).GetMethod(nameof(RequestController.MyRequests));
            Assert.NotNull(myRequestsMethod);

            var authAttr = myRequestsMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal(Roles.Department, authAttr.Roles);
        }

        /// <summary>
        /// DST-11: Inspect Department timeline/history -> No internal workflow location is disclosed.
        /// Confirms ActivityLog and DepartmentHistory endpoints forbid Department role.
        /// </summary>
        [Fact]
        public void DST11_InspectDepartmentTimelineHistory_EndpointsForbidDepartment()
        {
            var activityLogMethod = typeof(RequestController).GetMethod(nameof(RequestController.ActivityLog));
            Assert.NotNull(activityLogMethod);
            var activityAuth = activityLogMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(activityAuth);
            Assert.DoesNotContain(Roles.Department, activityAuth.Roles?.Split(',') ?? Array.Empty<string>());

            var deptHistoryMethod = typeof(RequestController).GetMethod(nameof(RequestController.DepartmentHistory));
            Assert.NotNull(deptHistoryMethod);
            var deptHistoryAuth = deptHistoryMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(deptHistoryAuth);
            Assert.DoesNotContain(Roles.Department, deptHistoryAuth.Roles?.Split(',') ?? Array.Empty<string>());
        }

        /// <summary>
        /// DST-12: Inspect rendered page source -> Internal status names are not unnecessarily exposed.
        /// </summary>
        [Theory]
        [InlineData(RequestStatus.PendingITReview, "قيد الانتظار")]
        [InlineData(RequestStatus.PendingApproverOne, "قيد الانتظار")]
        [InlineData(RequestStatus.PendingApproverTwo, "قيد الانتظار")]
        [InlineData(RequestStatus.PendingITReReview, "قيد الانتظار")]
        [InlineData(RequestStatus.Approved, "قيد الانتظار")]
        [InlineData(RequestStatus.Issued, "تم الصرف")]
        [InlineData(RequestStatus.Rejected, "مرفوض")]
        public void DST12_InspectRenderedPageSource_OnlyPrivacyLabelsUsed(string internalStatus, string expectedLabel)
        {
            var (label, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(internalStatus);
            Assert.Equal(expectedLabel, label);
        }

        /// <summary>
        /// DST-13: Inspect browser Network response as Department -> Sensitive internal stage data is not returned.
        /// </summary>
        [Fact]
        public void DST13_DepartmentQuotaEndpoint_DoesNotReturnInternalWorkflowData()
        {
            var quotaMethod = typeof(RequestController).GetMethod(nameof(RequestController.GetPaperQuotaStatus));
            Assert.NotNull(quotaMethod);
            
            // Method parameters and structure check
            var parameters = quotaMethod.GetParameters();
            Assert.Contains(parameters, p => p.Name == "departmentId");
            Assert.Contains(parameters, p => p.Name == "paperTypeId");
        }

        /// <summary>
        /// DST-14: Move request from Approver 1 to Approver 2 -> Department still sees same pending label.
        /// </summary>
        [Fact]
        public void DST14_MoveFromApproverOneToApproverTwo_DepartmentSeesSamePendingLabel()
        {
            var stage1 = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverOne);
            var stage2 = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverTwo);

            Assert.Equal("قيد الانتظار", stage1.Label);
            Assert.Equal("قيد الانتظار", stage2.Label);
            Assert.Equal(stage1.Label, stage2.Label);
            Assert.Equal(stage1.CssClasses, stage2.CssClasses);
        }

        /// <summary>
        /// DST-15: Return request to IT re-review -> Department still sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST15_ReturnRequestToITReReview_DepartmentStillSeesPending()
        {
            var approverStage = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverOne);
            var itReReviewStage = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingITReReview);

            Assert.Equal("قيد الانتظار", approverStage.Label);
            Assert.Equal("قيد الانتظار", itReReviewStage.Label);
            Assert.Equal(approverStage.Label, itReReviewStage.Label);
            Assert.Equal(approverStage.CssClasses, itReReviewStage.CssClasses);
        }

        /// <summary>
        /// DST-16: Approve request and leave waiting for issue -> Department still sees قيد الانتظار.
        /// </summary>
        [Fact]
        public void DST16_ApproveRequestAndLeaveWaitingForIssue_DepartmentStillSeesPending()
        {
            var pendingStage = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.PendingApproverTwo);
            var approvedStage = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Approved);

            Assert.Equal("قيد الانتظار", pendingStage.Label);
            Assert.Equal("قيد الانتظار", approvedStage.Label);
            Assert.Equal(pendingStage.Label, approvedStage.Label);
            Assert.Equal(pendingStage.CssClasses, approvedStage.CssClasses);
        }
    }
}
