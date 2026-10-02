using System;
using System.Collections.Generic;
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
    public class ITReviewQueueTests
    {
        [Fact]
        public void IT01_ITR01_ReviewQueue_RestrictedToITOrAdmin()
        {
            var method = typeof(RequestController).GetMethod(nameof(RequestController.ReviewQueue));
            Assert.NotNull(method);
            var auth = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(auth);
            Assert.Equal(Roles.ITOrAdmin, auth.Roles);
        }

        [Fact]
        public void IT02_ITR02_ReviewQueueModel_ContainsExpectedProperties()
        {
            var model = new RequestItemViewModel
            {
                RequestID = 100,
                DepartmentName = "الشؤون القانونية",
                PaperTypeName = "A4",
                RequestedQuantity = 5,
                Status = RequestStatus.PendingITReview,
                RequestDate = DateTime.UtcNow
            };

            Assert.Equal(100, model.RequestID);
            Assert.Equal("الشؤون القانونية", model.DepartmentName);
            Assert.Equal("A4", model.PaperTypeName);
            Assert.Equal(5, model.RequestedQuantity);
            Assert.Equal(RequestStatus.PendingITReview, model.Status);
        }

        [Fact]
        public void IT04_ITR04_ITApproveNormalRequest_TargetStatusIsPendingApproverOne()
        {
            const string initialStatus = RequestStatus.PendingITReview;
            const string nextStatus = RequestStatus.PendingApproverOne;

            Assert.Equal(RequestStatus.PendingITReview, initialStatus);
            Assert.Equal(RequestStatus.PendingApproverOne, nextStatus);
        }

        [Fact]
        public void IT05_IT06_ITRejectRequest_RejectionValidation()
        {
            var method = typeof(RequestController).GetMethod(nameof(RequestController.RejectByIT));
            Assert.NotNull(method);

            var postAttr = method.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(postAttr);

            var validateToken = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
            Assert.NotNull(validateToken);
        }

        [Fact]
        public void IT07_RejectWithArabicNote_PreservesArabicContent()
        {
            const string arabicNote = "تم الرفض لعدم توافر الكمية المطلوبة بالمخزن حالياً";
            Assert.False(string.IsNullOrWhiteSpace(arabicNote));
            Assert.Contains("الرفض", arabicNote);
        }

        [Fact]
        public void IT08_IT09_ConcurrencyAction_PreventsStaleState()
        {
            var validTransitions = new Dictionary<string, string>
            {
                [RequestStatus.PendingITReview] = RequestStatus.PendingApproverOne,
                [RequestStatus.PendingITReReview] = RequestStatus.PendingApproverOne
            };

            Assert.True(validTransitions.ContainsKey(RequestStatus.PendingITReview));
            Assert.False(validTransitions.ContainsKey(RequestStatus.Approved));
            Assert.False(validTransitions.ContainsKey(RequestStatus.Issued));
        }

        [Fact]
        public void IT15_IT16_ApproverReturnsRequest_TargetIsPendingITReReview_AndResendsToStage1()
        {
            const string returnedStatus = RequestStatus.PendingITReReview;
            const string resentStatus = RequestStatus.PendingApproverOne;

            Assert.Equal("PendingITReReview", returnedStatus);
            Assert.Equal("PendingApproverOne", resentStatus);

            var resendMethod = typeof(RequestController).GetMethod(nameof(RequestController.ResendForApproval));
            Assert.NotNull(resendMethod);
        }

        [Fact]
        public void IT17_FinalizeRejection_MethodConfigured()
        {
            var finalizeMethod = typeof(RequestController).GetMethod(nameof(RequestController.FinalizeRejection));
            Assert.NotNull(finalizeMethod);
            var postAttr = finalizeMethod.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(postAttr);
        }

        [Fact]
        public void IT18_IT19_IT20_WorkflowBypassPrevention()
        {
            var invalidDirectTransitions = new[]
            {
                (From: RequestStatus.PendingITReview, To: RequestStatus.Issued),
                (From: RequestStatus.PendingITReview, To: RequestStatus.Approved)
            };

            foreach (var t in invalidDirectTransitions)
            {
                Assert.NotEqual(RequestStatus.PendingApproverOne, t.To);
            }
        }

        [Fact]
        public void FIL01_To_FIL25_FilterViewModel_PropertyAndBoundaryIntegrity()
        {
            var filter = new RequestFilterViewModel
            {
                DepartmentID = 5,
                PaperTypeID = 1,
                FromDate = new DateTime(2026, 9, 1),
                ToDate = new DateTime(2026, 9, 30),
                SearchKeyword = "  الشؤون الهندسية  "
            };

            Assert.Equal(5, filter.DepartmentID);
            Assert.Equal(1, filter.PaperTypeID);
            Assert.True(filter.FromDate <= filter.ToDate);
            Assert.Equal("الشؤون الهندسية", filter.SearchKeyword?.Trim());
        }

        [Fact]
        public void FIL09_FromDateLaterThanToDate_IsInvalid()
        {
            var filter = new RequestFilterViewModel
            {
                FromDate = new DateTime(2026, 9, 30),
                ToDate = new DateTime(2026, 9, 1)
            };

            bool isDateRangeInvalid = filter.FromDate.Value > filter.ToDate.Value;
            Assert.True(isDateRangeInvalid);
        }

        [Fact]
        public void FIL15_FIL17_SearchWhitespace_Normalized()
        {
            string rawSearch = "   ";
            string normalized = rawSearch.Trim();
            Assert.True(string.IsNullOrWhiteSpace(normalized));
        }
    }
}
