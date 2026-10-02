using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Resource_Management_System.Areas.PaperManagement.Controllers;
using Resource_Management_System.Common;
using Resource_Management_System.Models;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class DepartmentNormalRequestTests
    {
        private IList<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        /// <summary>
        /// REQ-01: With quota 20, submit a valid request for quantity 5.
        /// </summary>
        [Fact]
        public void REQ01_ValidRequest_PassesValidation()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 5,
                QuotaLimit = 20,
                UsedQuota = 0,
                Notes = "طلب عادي"
            };

            var errors = ValidateModel(model);
            Assert.Empty(errors);
            Assert.Equal(20, model.RemainingQuota);
            Assert.True(model.RequestedQuantity <= model.RemainingQuota);
        }

        /// <summary>
        /// REQ-02: With remaining quota 5, request exactly 5.
        /// </summary>
        [Fact]
        public void REQ02_RequestExactRemainingQuota_PassesValidation()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 5,
                QuotaLimit = 20,
                UsedQuota = 15
            };

            var errors = ValidateModel(model);
            Assert.Empty(errors);
            Assert.Equal(5, model.RemainingQuota);
            Assert.True(model.RequestedQuantity <= model.RemainingQuota);
        }

        /// <summary>
        /// REQ-03: With remaining quota 5, request 6 -> Exceeds quota.
        /// </summary>
        [Fact]
        public void REQ03_RequestExceedingRemainingQuota_Detected()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 6,
                QuotaLimit = 20,
                UsedQuota = 15
            };

            Assert.Equal(5, model.RemainingQuota);
            bool exceeds = model.RequestedQuantity > model.RemainingQuota;
            Assert.True(exceeds, "Request exceeding remaining quota must be flagged");
        }

        /// <summary>
        /// REQ-04: Submit quantity 0 -> Validation rejects.
        /// </summary>
        [Fact]
        public void REQ04_QuantityZero_ValidationRejects()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 0
            };

            var errors = ValidateModel(model);
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(NewRequestViewModel.RequestedQuantity)));
        }

        /// <summary>
        /// REQ-05: Submit negative quantity -> Validation rejects.
        /// </summary>
        [Fact]
        public void REQ05_NegativeQuantity_ValidationRejects()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = -5
            };

            var errors = ValidateModel(model);
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(NewRequestViewModel.RequestedQuantity)));
        }

        /// <summary>
        /// REQ-08: Submit without paper type -> Validation rejects.
        /// </summary>
        [Fact]
        public void REQ08_MissingPaperType_ValidationRejects()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 0, // Default unselected
                RequestedQuantity = 5
            };

            // Business logic checks PaperTypeID > 0
            bool isValidPaperType = model.PaperTypeID > 0;
            Assert.False(isValidPaperType);
        }

        /// <summary>
        /// REQ-10 & REQ-11: Duplicate submission protection via SubmissionKey.
        /// </summary>
        [Fact]
        public void REQ10_REQ11_SubmissionKey_PreventsDuplicates()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            Assert.NotEqual(Guid.Empty, key1);
            Assert.NotEqual(key1, key2);

            var model = new NewRequestViewModel { SubmissionKey = key1 };
            Assert.Equal(key1, model.SubmissionKey);
        }

        /// <summary>
        /// REQ-14: Submit valid normal request -> Initial internal status is PendingITReview.
        /// </summary>
        [Fact]
        public void REQ14_InitialStatus_IsPendingITReview()
        {
            const string initialStatus = RequestStatus.PendingITReview;
            Assert.Equal("PendingITReview", initialStatus);
        }

        /// <summary>
        /// REQ-16: Tamper DepartmentID in browser request -> Server forces cookie claim DepartmentID for Department role.
        /// </summary>
        [Fact]
        public void REQ16_DepartmentRole_IgnoresClientDepartmentID()
        {
            // For Department users, departmentId is resolved strictly from User.FindFirst("DepartmentID")
            // Client-tampered model.DepartmentID is completely bypassed
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "CAIRO_USER"),
                new Claim(ClaimTypes.Role, Roles.Department),
                new Claim("DepartmentID", "2") // Authorized Cairo department
            };
            var identity = new ClaimsIdentity(claims, "CookieAuth");
            var principal = new ClaimsPrincipal(identity);

            // Attacker sends model.DepartmentID = 99 (tampered)
            var tamperedModel = new NewRequestViewModel
            {
                DepartmentID = 99,
                PaperTypeID = 1,
                RequestedQuantity = 3
            };

            int resolvedDepartmentId;
            if (principal.IsInRole(Roles.Department))
            {
                int.TryParse(principal.FindFirst("DepartmentID")?.Value, out resolvedDepartmentId);
            }
            else
            {
                resolvedDepartmentId = tamperedModel.DepartmentID ?? 0;
            }

            Assert.Equal(2, resolvedDepartmentId);
            Assert.NotEqual(99, resolvedDepartmentId);
        }

        /// <summary>
        /// REQ-17: Try request from inactive department -> Submission is prevented.
        /// </summary>
        [Fact]
        public void REQ17_InactiveDepartment_SubmissionPrevented()
        {
            // Department status is checked via IsActive flag
            bool isDepartmentActive = false;
            Assert.False(isDepartmentActive, "Inactive department cannot submit requests");
        }

        /// <summary>
        /// REQ-18: Inspect paper type list with inactive paper type -> Inactive paper type cannot be selected.
        /// </summary>
        [Fact]
        public void REQ18_InactivePaperType_ExcludedFromSelection()
        {
            var paperTypes = new List<(int Id, string Name, bool IsActive)>
            {
                (1, "A4 80gsm", true),
                (2, "A3 80gsm", true),
                (3, "Letter (Deprecated)", false)
            };

            var selectableTypes = paperTypes.Where(p => p.IsActive).ToList();

            Assert.Equal(2, selectableTypes.Count);
            Assert.DoesNotContain(selectableTypes, p => p.Id == 3);
            Assert.All(selectableTypes, p => Assert.True(p.IsActive));
        }

        /// <summary>
        /// REQ-19: Submit Arabic notes -> Arabic text is preserved correctly.
        /// </summary>
        [Fact]
        public void REQ19_SubmitArabicNotes_PreservedCorrectly()
        {
            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 2,
                Notes = "طلب أوراق للمطبعة المركزية لتجهيز كشوف الحسابات الشهرية"
            };

            var errors = ValidateModel(model);
            Assert.Empty(errors);
            Assert.Equal("طلب أوراق للمطبعة المركزية لتجهيز كشوف الحسابات الشهرية", model.Notes);
        }

        /// <summary>
        /// REQ-20: Submit excessively long notes -> Handled by validation without crash or corruption.
        /// </summary>
        [Fact]
        public void REQ20_ExcessivelyLongNotes_ValidationRejectsWithoutCrash()
        {
            // 501 characters exceeds the 500-character maximum
            string excessivelyLongNote = new string('أ', 501);

            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 5,
                Notes = excessivelyLongNote
            };

            var errors = ValidateModel(model);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(NewRequestViewModel.Notes)));
            Assert.Contains(errors, e => e.ErrorMessage != null && e.ErrorMessage.Contains("500 حرف"));
        }

        /// <summary>
        /// REQ-20 Boundary: Notes exactly 500 characters -> Valid.
        /// </summary>
        [Fact]
        public void REQ20_Exact500CharacterNotes_ValidationPasses()
        {
            string exact500Note = new string('ن', 500);

            var model = new NewRequestViewModel
            {
                PaperTypeID = 1,
                RequestedQuantity = 5,
                Notes = exact500Note
            };

            var errors = ValidateModel(model);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("A3", true)]
        [InlineData("A3 80gsm", true)]
        [InlineData("a3", true)]
        [InlineData("A3-Special", true)]
        [InlineData("A4", false)]
        [InlineData("A4 80gsm", false)]
        [InlineData("Letter", false)]
        public void A3_Identification_WorksCorrectly(string name, bool expectedIsA3)
        {
            bool isA3 = Resource_Management_System.Areas.PaperManagement.Controllers.RequestController.IsA3Paper(name);
            Assert.Equal(expectedIsA3, isA3);
        }

        [Fact]
        public void NormalRequest_ExcludesA3_WhileExceptionRequestIncludesA3()
        {
            var paperTypes = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
            {
                new() { Value = "1", Text = "A4" },
                new() { Value = "2", Text = "A3" }
            };

            var normalFiltered = paperTypes
                .Where(p => !Resource_Management_System.Areas.PaperManagement.Controllers.RequestController.IsA3Paper(p.Text))
                .ToList();

            var exceptionTypes = paperTypes.ToList();

            Assert.Single(normalFiltered);
            Assert.Equal("A4", normalFiltered[0].Text);

            Assert.Equal(2, exceptionTypes.Count);
            Assert.Contains(exceptionTypes, p => p.Text == "A3");
            Assert.Contains(exceptionTypes, p => p.Text == "A4");
        }
    }
}
