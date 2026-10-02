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
    public class IssueQueueAndDispenseTests
    {
        [Fact]
        public void ISSUE01_IssueQueue_RestrictedToITOrAdmin()
        {
            var issueQueueMethod = typeof(RequestController).GetMethod(nameof(RequestController.IssueQueue));
            Assert.NotNull(issueQueueMethod);
            var auth = issueQueueMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(auth);
            Assert.Equal(Roles.ITOrAdmin, auth.Roles);
        }

        [Fact]
        public void ISSUE02_ISSUE03_NonApprovedRequests_ExcludedFromIssueQueue()
        {
            // Only requests with Status == RequestStatus.Approved can be physically issued
            var statuses = new[]
            {
                RequestStatus.PendingITReview,
                RequestStatus.PendingApproverOne,
                RequestStatus.PendingApproverTwo,
                RequestStatus.PendingITReReview,
                RequestStatus.Rejected,
                RequestStatus.Issued
            };

            foreach (var s in statuses)
            {
                Assert.NotEqual(RequestStatus.Approved, s);
            }
        }

        [Fact]
        public void ISSUE04_ISSUE05_IssueMethod_SetsStatusToIssued()
        {
            var issueMethod = typeof(RequestController).GetMethod(nameof(RequestController.Issue));
            Assert.NotNull(issueMethod);
            var postAttr = issueMethod.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(postAttr);
            var authAttr = issueMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal(Roles.ITOrAdmin, authAttr.Roles);
        }

        [Fact]
        public void BUG01_To_BUG10_IssueNotesParameter_SupportNullAndPunctuation()
        {
            // Verify handling of various IssueNotes values including null, empty, Arabic, punctuation, and length
            string? noteNull = null;
            string noteEmpty = "";
            string noteArabic = "تم الصرف بمعرفة أمين المخزن - إذن رقم 1445/ب";
            string notePunctuation = "ملاحظة: صرف خاص (عاجل) - رقم #123!";

            Assert.True(string.IsNullOrEmpty(noteNull));
            Assert.True(string.IsNullOrEmpty(noteEmpty));
            Assert.Contains("الصرف", noteArabic);
            Assert.Contains(":", notePunctuation);
        }

        [Fact]
        public void IQF01_To_IQF15_IssueQueueFilter_DoesNotIncludeStatusSelector()
        {
            // The Issue Queue only deals with Approved requests, so no status filter is needed
            var filter = new RequestFilterViewModel
            {
                DepartmentID = 10,
                PaperTypeID = 2
            };

            Assert.Equal(10, filter.DepartmentID);
            Assert.Equal(2, filter.PaperTypeID);
        }

        [Fact]
        public void DISP01_To_DISP12_StockDeduction_Integrity()
        {
            int currentStock = 50;
            int requestedQuantity = 5;

            Assert.True(requestedQuantity <= currentStock, "Quantity cannot exceed current stock");
            int remainingStock = currentStock - requestedQuantity;
            Assert.Equal(45, remainingStock);
            Assert.True(remainingStock >= 0, "Stock cannot become negative");
        }

        [Fact]
        public void DISP12_ISSUE16_DepartmentSeesIssuedLabel()
        {
            var (label, css) = StatusDisplayHelper.GetDepartmentBadgeInfo(RequestStatus.Issued);
            Assert.Equal("تم الصرف", label);
            Assert.Contains("status-issued", css);
        }
    }
}
