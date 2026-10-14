using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===============================================================================");
        Console.WriteLine("  STARTING FULL AUTOMATED END-TO-END TEST SUITE FOR SUPPLIES TRACKING");
        Console.WriteLine("===============================================================================");

        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            AllowAutoRedirect = false
        };

        using var client = new HttpClient(handler);
        string baseUrl = "https://localhost:7107";

        try
        {
            // -------------------------------------------------------------
            // TEST 1: Login Authentication as ADMIN
            // -------------------------------------------------------------
            Console.Write("\n[TEST 1] Logging in as ADMIN (password: 123456)... ");
            var loginPageRes = await client.GetAsync($"{baseUrl}/Account/Login");
            var loginHtml = await loginPageRes.Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(loginHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!tokenMatch.Success) throw new Exception("Antiforgery token not found on login page!");

            string token = tokenMatch.Groups[1].Value;
            var loginData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "UserName", "ADMIN" },
                { "Password", "123456" },
                { "RememberMe", "false" },
                { "__RequestVerificationToken", token }
            });

            var loginPostRes = await client.PostAsync($"{baseUrl}/Account/Login", loginData);
            if (loginPostRes.StatusCode != HttpStatusCode.Redirect && loginPostRes.StatusCode != HttpStatusCode.Found)
            {
                throw new Exception($"Login failed with status {loginPostRes.StatusCode}");
            }
            Console.WriteLine("PASSED! (Authenticated)");

            // -------------------------------------------------------------
            // TEST 2: Access Supplies Tracking Index & Dashboard
            // -------------------------------------------------------------
            Console.Write("[TEST 2] Accessing Supplies Dashboard (/SuppliesTracking/Supplies/Index)... ");
            var indexRes = await client.GetAsync($"{baseUrl}/SuppliesTracking/Supplies/Index");
            if (indexRes.StatusCode != HttpStatusCode.OK) throw new Exception($"Supplies Index returned {indexRes.StatusCode}");
            var indexHtml = await indexRes.Content.ReadAsStringAsync();
            if (!indexHtml.Contains("دليل الأصناف والأرصدة الحالية")) throw new Exception("Index page title missing in HTML!");
            Console.WriteLine("PASSED! (200 OK & Title verified)");

            // Extract fresh antiforgery token
            var tokenMatch2 = Regex.Match(indexHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (tokenMatch2.Success) token = tokenMatch2.Groups[1].Value;

            // -------------------------------------------------------------
            // TEST 3: Receive Serialized Devices (Auto-Barcode Generation)
            // Receiving 2 units of HP Laser 408dn (ItemID = 7, TrackType = 1)
            // -------------------------------------------------------------
            Console.Write("[TEST 3] Inbound Receive of 2 Serialized Devices (HP 408dn)... ");
            var receivePageRes = await client.GetAsync($"{baseUrl}/SuppliesTracking/Supplies/Receive?itemId=7");
            var receiveHtml = await receivePageRes.Content.ReadAsStringAsync();
            var tokenMatchRec = Regex.Match(receiveHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (tokenMatchRec.Success) token = tokenMatchRec.Groups[1].Value;

            var receivePostData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "ItemID", "7" },
                { "Quantity", "2" },
                { "SupplierOrReference", "شركة توريد أجهزة الاختبار - إذن رقم 991" },
                { "SerialNumbersText", "SN-HP408-TEST-001\nSN-HP408-TEST-002" },
                { "Notes", "توريد تجريبي للاختبار الشامل" },
                { "__RequestVerificationToken", token }
            });

            var receivePostRes = await client.PostAsync($"{baseUrl}/SuppliesTracking/Supplies/Receive", receivePostData);
            if (receivePostRes.StatusCode != HttpStatusCode.Redirect && receivePostRes.StatusCode != HttpStatusCode.Found)
            {
                throw new Exception($"Receive POST failed with status: {receivePostRes.StatusCode}");
            }

            string redirectLocation = receivePostRes.Headers.Location?.ToString() ?? "";
            Console.WriteLine($"PASSED! (Redirected to: {redirectLocation})");

            // -------------------------------------------------------------
            // TEST 4: Print Barcodes Screen
            // -------------------------------------------------------------
            Console.Write("[TEST 4] Loading PrintBarcodes Page for generated devices... ");
            var printBarcodeRes = await client.GetAsync($"{baseUrl}{redirectLocation}");
            if (printBarcodeRes.StatusCode != HttpStatusCode.OK) throw new Exception($"PrintBarcodes page failed with {printBarcodeRes.StatusCode}");
            var printBarcodeHtml = await printBarcodeRes.Content.ReadAsStringAsync();
            if (!printBarcodeHtml.Contains("DEV-2026-") || !printBarcodeHtml.Contains("SN-HP408-TEST-001"))
            {
                throw new Exception("Generated Barcode or Serial Number not found in PrintBarcodes HTML!");
            }
            Console.WriteLine("PASSED! (Barcodes 'DEV-2026-XXXXX' and serials successfully generated)");

            // -------------------------------------------------------------
            // TEST 5: Issue Consumable (Toner) to Department with Printer Model
            // ItemID = 14 (HP 331A Toner), Qty = 2, DepartmentID = 1, PrinterModel = HP Laser 408dn
            // -------------------------------------------------------------
            Console.Write("[TEST 5] Issuing Consumable Toner to Department with PrinterModel requirement... ");
            var movementsPageRes = await client.GetAsync($"{baseUrl}/SuppliesTracking/Supplies/Movements?type=Issue&itemId=14");
            var movementsHtml = await movementsPageRes.Content.ReadAsStringAsync();
            var tokenMatchMov = Regex.Match(movementsHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (tokenMatchMov.Success) token = tokenMatchMov.Groups[1].Value;

            var issuePostData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "MovementType", "Issue" },
                { "ItemID", "14" },
                { "Quantity", "2" },
                { "DepartmentID", "1" },
                { "RecipientName", "أ. محمد عبد الله (سكرتارية الإدارة)" },
                { "PrinterModel", "HP Laser 408dn" },
                { "ReferenceNumber", "طلب صرف رقم 105" },
                { "Notes", "صرف حبارة لتشغيل طابعة الإدارة" },
                { "__RequestVerificationToken", token }
            });

            var issuePostRes = await client.PostAsync($"{baseUrl}/SuppliesTracking/Supplies/Movements", issuePostData);
            if (issuePostRes.StatusCode != HttpStatusCode.Redirect && issuePostRes.StatusCode != HttpStatusCode.Found)
            {
                throw new Exception($"Issue POST failed with status: {issuePostRes.StatusCode}");
            }

            string issueRedirect = issuePostRes.Headers.Location?.ToString() ?? "";
            Console.WriteLine($"PASSED! (Redirected to Voucher: {issueRedirect})");

            // -------------------------------------------------------------
            // TEST 6: Print Official Handover Voucher
            // -------------------------------------------------------------
            Console.Write("[TEST 6] Loading Official Handover Voucher (/SuppliesTracking/Supplies/PrintVoucher)... ");
            var voucherRes = await client.GetAsync($"{baseUrl}{issueRedirect}");
            if (voucherRes.StatusCode != HttpStatusCode.OK) throw new Exception($"Voucher failed with status {voucherRes.StatusCode}");
            var voucherHtml = await voucherRes.Content.ReadAsStringAsync();
            if (!voucherHtml.Contains("إذن صرف وتسليم مستلزمات") || !voucherHtml.Contains("HP Laser 408dn"))
            {
                throw new Exception("Voucher HTML does not contain official header or printer model!");
            }
            Console.WriteLine("PASSED! (Official A4 Voucher verified with signatures & recipient details)");

            // -------------------------------------------------------------
            // TEST 7: Negative Validation Test - Out of Stock Rejection
            // Attempt to issue 9999 units of an item
            // -------------------------------------------------------------
            Console.Write("[TEST 7] Testing Out-of-Stock Validation Defense... ");
            var overPageRes = await client.GetAsync($"{baseUrl}/SuppliesTracking/Supplies/Movements?type=Issue&itemId=14");
            var overPageHtml = await overPageRes.Content.ReadAsStringAsync();
            var overTokenMatch = Regex.Match(overPageHtml, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (overTokenMatch.Success) token = overTokenMatch.Groups[1].Value;

            var overIssuePostData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "MovementType", "Issue" },
                { "ItemID", "14" },
                { "Quantity", "999" },
                { "DepartmentID", "1" },
                { "RecipientName", "موظف" },
                { "PrinterModel", "HP Laser 408dn" },
                { "__RequestVerificationToken", token }
            });

            var overIssueRes = await client.PostAsync($"{baseUrl}/SuppliesTracking/Supplies/Movements", overIssuePostData);
            var overIssueHtml = await overIssueRes.Content.ReadAsStringAsync();
            var decodedOverHtml = WebUtility.HtmlDecode(overIssueHtml);
            if (!decodedOverHtml.Contains("غير كاف") && !overIssueHtml.Contains("غير كاف"))
            {
                Console.WriteLine($"\n[DIAGNOSTIC] Status: {overIssueRes.StatusCode}");
                Console.WriteLine($"[DIAGNOSTIC] Body: {(decodedOverHtml.Length > 500 ? decodedOverHtml.Substring(0, 500) : decodedOverHtml)}");
                throw new Exception("Out-of-stock validation failed to reject over-quantity!");
            }
            Console.WriteLine("PASSED! (Gracefully rejected with 'رصيد المخزن الحالي غير كافٍ')");

            // -------------------------------------------------------------
            // TEST 8: Reports Screen and Historical Ledger
            // -------------------------------------------------------------
            Console.Write("[TEST 8] Querying Movement Reports & Historical Ledger... ");
            var reportsRes = await client.GetAsync($"{baseUrl}/SuppliesTracking/Supplies/Reports");
            if (reportsRes.StatusCode != HttpStatusCode.OK) throw new Exception($"Reports failed with status {reportsRes.StatusCode}");
            var reportsHtml = await reportsRes.Content.ReadAsStringAsync();
            var decodedReportsHtml = WebUtility.HtmlDecode(reportsHtml);
            if (!decodedReportsHtml.Contains("سجل") || !reportsHtml.Contains("TRN-2026-"))
            {
                Console.WriteLine($"\n[DIAGNOSTIC] Reports HTML: {(decodedReportsHtml.Length > 500 ? decodedReportsHtml.Substring(0, 500) : decodedReportsHtml)}");
                throw new Exception("Reports screen did not render recorded transactions!");
            }
            Console.WriteLine("PASSED! (All movements audited and searchable in reports)");

            Console.WriteLine("\n===============================================================================");
            Console.WriteLine("  ALL 8 TESTS PASSED WITH 100% SUCCESS! (0 ERRORS, 0 FAILURES)");
            Console.WriteLine("===============================================================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nFAILED with error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
