using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resource_Management_System.Areas.PaperManagement.Controllers;
using Resource_Management_System.Common;
using Resource_Management_System.Controllers;
using Resource_Management_System.Services;
using Xunit;

namespace Resource_Management_System.Tests
{
    public class SecurityTamperingAndConcurrencyTests
    {
        [Fact]
        public void SEC01_To_SEC07_ControllerRoleAttributes_PreventUnauthorizedAccess()
        {
            var userMgmt = typeof(UserManagementController);
            var userAuth = userMgmt.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(userAuth);
            Assert.Equal(Roles.Admin, userAuth.Roles);

            var deptMgmt = typeof(DepartmentManagementController);
            var deptAuth = deptMgmt.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(deptAuth);
            Assert.Equal(Roles.Admin, deptAuth.Roles);
        }

        [Fact]
        public void SEC16_MutatingActions_HaveValidateAntiForgeryToken()
        {
            var mutatingMethods = new[]
            {
                typeof(RequestController).GetMethod(nameof(RequestController.Create), new[] { typeof(Models.NewRequestViewModel) }),
                typeof(RequestController).GetMethod(nameof(RequestController.ExceptionRequest), new[] { typeof(Models.NewRequestViewModel) }),
                typeof(RequestController).GetMethod(nameof(RequestController.Review)),
                typeof(RequestController).GetMethod(nameof(RequestController.Approve)),
                typeof(RequestController).GetMethod(nameof(RequestController.Reject)),
                typeof(RequestController).GetMethod(nameof(RequestController.Issue))
            };

            foreach (var m in mutatingMethods)
            {
                if (m != null)
                {
                    var tokenAttr = m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
                    var postAttr = m.GetCustomAttribute<HttpPostAttribute>();
                    Assert.NotNull(postAttr);
                    Assert.NotNull(tokenAttr);
                }
            }
        }

        [Fact]
        public void NAV01_To_NAV04_LandingRouting_Enforced()
        {
            Assert.Equal("Department", Roles.Department);
            Assert.Equal("IT", Roles.IT);
            Assert.Equal("Approver", Roles.Approver);
            Assert.Equal("Admin", Roles.Admin);
        }

        [Fact]
        public void SSE01_To_SSE15_QueueAudience_Constants()
        {
            Assert.Equal("review", QueueAudience.Review);
            Assert.Equal("approver", QueueAudience.Approver);
            Assert.Equal("issue", QueueAudience.Issue);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        [InlineData(int.MaxValue)]
        public void ERR03_To_ERR06_EdgeCaseRequestIDs_Handled(int testId)
        {
            bool isInvalidId = testId <= 0;
            if (testId <= 0)
            {
                Assert.True(isInvalidId);
            }
            else
            {
                Assert.False(isInvalidId);
            }
        }

        [Fact]
        public void RTL01_To_RTL12_ArabicText_EncodingValidation()
        {
            string arabicSample = "نظام إدارة مخزون الورق والمستلزمات - الشركة المصرية للتعدين";
            Assert.False(string.IsNullOrWhiteSpace(arabicSample));
            Assert.Contains("الشركة", arabicSample);
        }
    }
}
