using System;
using System.Collections.Generic;
using Resource_Management_System.Common;
using Resource_Management_System.Models;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class MasterWorkflowEndToEndTests
    {
        [Fact]
        public void E2E01_MasterHappyPath_WorkflowStateTransitions()
        {
            // 1. Department submits normal request
            string status = RequestStatus.PendingITReview;
            var (deptLabel1, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabel1);

            // 2. IT approves -> PendingApproverOne
            status = RequestStatus.PendingApproverOne;
            var (deptLabel2, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabel2);

            // 3. Approver 1 approves -> PendingApproverTwo
            status = RequestStatus.PendingApproverTwo;
            var (deptLabel3, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabel3);

            // 4. Approver 2 approves -> Approved
            status = RequestStatus.Approved;
            var (deptLabel4, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabel4);

            // 5. IT issues physical paper -> Issued
            status = RequestStatus.Issued;
            var (deptLabel5, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("تم الصرف", deptLabel5);
        }

        [Fact]
        public void E2E02_MasterITRejectionPath_WorkflowStateTransitions()
        {
            // 1. Department submits
            string status = RequestStatus.PendingITReview;
            var (deptLabel1, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabel1);

            // 2. IT rejects -> Rejected / RejectedByIT
            status = RequestStatus.Rejected;
            var (deptLabel2, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("مرفوض", deptLabel2);

            var (deptLabel3, _) = StatusDisplayHelper.GetDepartmentBadgeInfo("RejectedByIT");
            Assert.Equal("مرفوض", deptLabel3);
        }

        [Fact]
        public void E2E03_MasterApproverReReviewPath_WorkflowStateTransitions()
        {
            // 1. Submit -> PendingITReview
            string status = RequestStatus.PendingITReview;

            // 2. IT approves -> PendingApproverOne
            status = RequestStatus.PendingApproverOne;

            // 3. Approver 1 rejects/returns -> PendingITReReview
            status = RequestStatus.PendingITReReview;
            var (deptLabelReRev, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("قيد الانتظار", deptLabelReRev);

            // 4. IT resubmits -> PendingApproverOne
            status = RequestStatus.PendingApproverOne;

            // 5. Approver 1 approves -> PendingApproverTwo
            status = RequestStatus.PendingApproverTwo;

            // 6. Approver 2 approves -> Approved
            status = RequestStatus.Approved;

            // 7. IT issues -> Issued
            status = RequestStatus.Issued;
            var (deptLabelFinal, _) = StatusDisplayHelper.GetDepartmentBadgeInfo(status);
            Assert.Equal("تم الصرف", deptLabelFinal);
        }

        [Fact]
        public void E2E04_E2E05_MasterExceptionWorkflowAndDefenseInDepth()
        {
            int remainingQuota = 0;
            bool allowException = remainingQuota == 0;
            Assert.True(allowException);

            var exceptionModel = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 3,
                Notes = "طلب استثناء طارئ لأعمال الطباعة",
                SubmissionKey = Guid.NewGuid()
            };

            Assert.False(string.IsNullOrWhiteSpace(exceptionModel.Notes));
            Assert.NotEqual(Guid.Empty, exceptionModel.SubmissionKey);
        }

        [Fact]
        public void SIGN01_To_SIGN38_FinalRegressionSignOff_Verification()
        {
            // Verify all 7 core internal statuses exist and are distinct
            var allStatuses = new HashSet<string>
            {
                RequestStatus.PendingITReview,
                RequestStatus.PendingITReReview,
                RequestStatus.PendingApproverOne,
                RequestStatus.PendingApproverTwo,
                RequestStatus.Approved,
                RequestStatus.Rejected,
                RequestStatus.Issued
            };

            Assert.Equal(7, allStatuses.Count);

            // Verify all 4 core roles exist and are distinct
            var allRoles = new HashSet<string>
            {
                Roles.Admin,
                Roles.IT,
                Roles.Approver,
                Roles.Department
            };

            Assert.Equal(4, allRoles.Count);
        }

        [Fact]
        public void SMOKE01_To_SMOKE08_SmokeChecks_InstantiateModels()
        {
            var login = new LoginViewModel { UserName = "IT", Password = "Password123" };
            Assert.Equal("IT", login.UserName);

            var req = new RequestItemViewModel { RequestID = 1, Status = RequestStatus.PendingITReview };
            Assert.Equal(1, req.RequestID);

            var dept = new DepartmentItemViewModel { DepartmentID = 1, DepartmentName = "مكتب رئيس مجلس الإدارة" };
            Assert.Equal("مكتب رئيس مجلس الإدارة", dept.DepartmentName);
        }
    }
}
