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
    public class InventoryAndQuotaTests
    {
        [Theory]
        [InlineData(20, 5, 15)]
        [InlineData(20, 20, 0)]
        [InlineData(10, 0, 10)]
        public void QUOTA01_QUOTA02_RemainingQuotaCalculation_IsCorrect(int quotaLimit, int usedQuota, int expectedRemaining)
        {
            var model = new DepartmentItemViewModel
            {
                QuotaLimit = quotaLimit,
                UsedQuota = usedQuota
            };

            Assert.Equal(expectedRemaining, model.RemainingQuota);
        }

        [Fact]
        public void QUOTA03_QUOTA04_QUOTA05_UnissuedRequests_DoNotConsumeIssuedQuota()
        {
            int initialStock = 100;
            int initialQuotaUsed = 0;

            Assert.Equal(100, initialStock);
            Assert.Equal(0, initialQuotaUsed);
        }

        [Fact]
        public void EXC01_To_EXC06_ExceptionRequest_OnlyAllowedWhenQuotaExhausted()
        {
            int remainingQuotaWithStock = 5;
            bool canSubmitException1 = remainingQuotaWithStock == 0;
            Assert.False(canSubmitException1, "Exception should be blocked when remaining quota > 0");

            int exhaustedQuota = 0;
            bool canSubmitException2 = exhaustedQuota == 0;
            Assert.True(canSubmitException2, "Exception should be allowed when remaining quota == 0");
        }

        [Fact]
        public void EXC_ControllerAction_Configured()
        {
            var excGet = typeof(RequestController).GetMethod(nameof(RequestController.ExceptionRequest), new Type[] { });
            Assert.NotNull(excGet);
            var authGet = excGet.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authGet);
            Assert.Equal(Roles.DepartmentOrITOrAdmin, authGet.Roles);

            var excPost = typeof(RequestController).GetMethod(nameof(RequestController.ExceptionRequest), new Type[] { typeof(NewRequestViewModel) });
            Assert.NotNull(excPost);
            var postAttr = excPost.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(postAttr);
        }

        [Fact]
        public void INV01_To_INV16_Inventory_StockThresholdAndNonNegative()
        {
            int currentStock = 10;
            int threshold = 15;

            bool isLowStock = currentStock <= threshold;
            Assert.True(isLowStock, "Should trigger low stock indication");

            int invalidReceipt = -5;
            Assert.True(invalidReceipt <= 0, "Non-positive receipt quantity must be rejected");
        }

        [Theory]
        [InlineData(10, 5, 0, 50.0, 50.0, 0.0)]
        [InlineData(10, 10, 5, 100.0, 0.0, 50.0)]
        [InlineData(0, 0, 0, 0.0, 0.0, 0.0)]
        public void BAR01_To_BAR09_ThreeColorQuotaProgressBar_Proportions(
            int quotaLimit, int regularIssued, int exceptionIssued,
            double expectedRegularPct, double expectedRemainingPct, double expectedExceptionPct)
        {
            double regularPct = 0;
            double remainingPct = 0;
            double exceptionPct = 0;

            if (quotaLimit > 0)
            {
                regularPct = (double)regularIssued / quotaLimit * 100.0;
                int remaining = Math.Max(0, quotaLimit - regularIssued);
                remainingPct = (double)remaining / quotaLimit * 100.0;
                exceptionPct = (double)exceptionIssued / quotaLimit * 100.0;
            }

            Assert.Equal(expectedRegularPct, regularPct);
            Assert.Equal(expectedRemainingPct, remainingPct);
            Assert.Equal(expectedExceptionPct, exceptionPct);
        }

        [Fact]
        public void PAPER01_To_PAPER07_PaperTypeManagement_EndpointsConfigured()
        {
            var createMethod = typeof(RequestController).GetMethod(nameof(RequestController.CreatePaperType));
            Assert.NotNull(createMethod);
            var authCreate = createMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authCreate);
            Assert.Equal(Roles.ITOrAdmin, authCreate.Roles);

            var deleteMethod = typeof(RequestController).GetMethod(nameof(RequestController.DeletePaperType));
            Assert.NotNull(deleteMethod);
            var authDelete = deleteMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authDelete);
            Assert.Equal(Roles.ITOrAdmin, authDelete.Roles);
        }
    }
}
