# Bug Analysis & Resolution Report: Missing `@SubmissionKey` Parameter

**Project Name:** Paper Bundle Distribution Management System (v12)  
**Error Message:** `Procedure or function 'sp_SubmitRequest' expects parameter '@SubmissionKey', which was not supplied.`  
**Status:** ✅ RESOLVED & VERIFIED  
**Date:** August 30, 2026  

---

## 1. Problem Overview & Symptom

When users attempted to submit a new paper request via the web application, the request failed with the following SQL exception:

> `System.Data.SqlClient.SqlException / Microsoft.Data.SqlClient.SqlException:`  
> **Procedure or function 'sp_SubmitRequest' expects parameter '@SubmissionKey', which was not supplied.**

This error blocked all users (Department Users, IT, and Admins) from creating new paper requests or submitting exception requests.

---

## 2. Technical Root Cause Analysis

### A. Database Stored Procedure Contract (`ScriptV12.sql`)
In Version 12 of the database schema script (`ScriptV12.sql`), an **Idempotency Control Mechanism** was added to `sp_SubmitRequest` and `sp_SubmitExceptionRequest`. This requires a unique GUID parameter `@SubmissionKey` to ensure that if a user double-clicks the submit button or experiences a network retry, SQL Server will return the existing `RequestID` rather than inserting duplicate records.

The SQL procedure signatures are defined as follows:

```sql
CREATE PROCEDURE [dbo].[sp_SubmitRequest]
    @DepartmentID int,
    @PaperTypeID int,
    @RequestedQuantity int,
    @Notes nvarchar(max) = NULL,
    @NewRequestID int OUTPUT,
    @SubmittedByID int = NULL,
    @SubmissionKey uniqueidentifier -- Mandatory parameter! No default value.
AS
```

```sql
CREATE PROCEDURE [dbo].[sp_SubmitExceptionRequest]
    @DepartmentID int,
    @PaperTypeID int,
    @RequestedQuantity int,
    @Notes nvarchar(max),
    @NewRequestID int OUTPUT,
    @SubmittedByID int = NULL,
    @SubmissionKey uniqueidentifier -- Mandatory parameter! No default value.
AS
```

Because `@SubmissionKey` was defined **without** a default value (`= NULL`), SQL Server strictly requires `@SubmissionKey` to be present in `SqlCommand.Parameters`.

### B. Controller Implementation Deficiency (`RequestController.cs`)
In `RequestController.cs`, the ADO.NET calls to `_db.ExecuteNonQuery` for `sp_SubmitRequest` and `sp_SubmitExceptionRequest` omitted the `@SubmissionKey` parameter:

#### Original Code (Faulty):
```csharp
// In RequestController.cs -> Create (POST)
_db.ExecuteNonQuery("sp_SubmitRequest",
    new SqlParameter("@DepartmentID", departmentId),
    new SqlParameter("@PaperTypeID", model.PaperTypeID),
    new SqlParameter("@RequestedQuantity", model.RequestedQuantity),
    new SqlParameter("@Notes", (object?)model.Notes ?? DBNull.Value),
    newIdParam,
    new SqlParameter("@SubmittedByID", GetCurrentUserId())
    // ❌ MISSING: new SqlParameter("@SubmissionKey", ...)
);
```

#### Secondary Discovered Mismatch (`sp_RejectRequestByIT`):
The procedure `sp_RejectRequestByIT` also requires a mandatory parameter `@Note nvarchar(500)`. In `RequestController.cs`, `RejectByIT` omitted `@Note`, which would have caused a secondary runtime exception upon IT rejection.

---

## 3. Implemented Fix & Code Changes

### Step 1: Updated ViewModel (`NewRequestViewModel.cs`)
Added `SubmissionKey` property to `NewRequestViewModel` to hold the idempotency GUID.

```csharp
public class NewRequestViewModel
{
    // ... existing properties ...

    public Guid SubmissionKey { get; set; } = Guid.NewGuid();
}
```

### Step 2: Updated Controller Logic (`RequestController.cs`)
Updated `Create(POST)` and `ExceptionRequest(POST)` actions to automatically generate or pass the `SubmissionKey` parameter to SQL Server. Also fixed `RejectByIT` to pass `@Note`.

#### Fixed `sp_SubmitRequest` Call:
```csharp
var submissionKey = model.SubmissionKey == Guid.Empty ? Guid.NewGuid() : model.SubmissionKey;

_db.ExecuteNonQuery("sp_SubmitRequest",
    new SqlParameter("@DepartmentID", departmentId),
    new SqlParameter("@PaperTypeID", model.PaperTypeID),
    new SqlParameter("@RequestedQuantity", model.RequestedQuantity),
    new SqlParameter("@Notes", (object?)model.Notes ?? DBNull.Value),
    newIdParam,
    new SqlParameter("@SubmittedByID", GetCurrentUserId()),
    new SqlParameter("@SubmissionKey", submissionKey) // ✅ ADDED
);
```

#### Fixed `sp_SubmitExceptionRequest` Call:
```csharp
var submissionKey = model.SubmissionKey == Guid.Empty ? Guid.NewGuid() : model.SubmissionKey;

_db.ExecuteNonQuery("sp_SubmitExceptionRequest",
    new SqlParameter("@DepartmentID", departmentId),
    new SqlParameter("@PaperTypeID", model.PaperTypeID),
    new SqlParameter("@RequestedQuantity", model.RequestedQuantity),
    new SqlParameter("@Notes", model.Notes),
    newIdParam,
    new SqlParameter("@SubmittedByID", GetCurrentUserId()),
    new SqlParameter("@SubmissionKey", submissionKey) // ✅ ADDED
);
```

#### Fixed `sp_RejectRequestByIT` Call:
```csharp
[HttpPost]
[Authorize(Roles = Roles.ITOrAdmin)]
[ValidateAntiForgeryToken]
public IActionResult RejectByIT(int requestId, string? note)
{
    try
    {
        _db.ExecuteNonQuery("sp_RejectRequestByIT",
            new SqlParameter("@RequestID", requestId),
            new SqlParameter("@RejectorID", GetCurrentUserId()),
            new SqlParameter("@Note", (object?)note ?? DBNull.Value)); // ✅ ADDED
```

### Step 3: Updated Razor Views (`Create.cshtml` & `ExceptionRequest.cshtml`)
Added hidden form fields for `SubmissionKey` to preserve idempotency across validation retries.

```razor
<form asp-action="Create" method="post">
    @Html.AntiForgeryToken()
    <input type="hidden" asp-for="SubmissionKey" />
    <!-- ... form fields ... -->
</form>
```

---

## 4. Verification & Summary

| File | Change Description | Result |
| :--- | :--- | :--- |
| **`NewRequestViewModel.cs`** | Added `public Guid SubmissionKey { get; set; } = Guid.NewGuid();` | ✅ Built Cleanly |
| **`RequestController.cs`** | Passed `@SubmissionKey` to `sp_SubmitRequest` and `sp_SubmitExceptionRequest`. Passed `@Note` to `sp_RejectRequestByIT`. | ✅ Built Cleanly |
| **`Create.cshtml`** | Added `<input type="hidden" asp-for="SubmissionKey" />` | ✅ Validated |
| **`ExceptionRequest.cshtml`** | Added `<input type="hidden" asp-for="SubmissionKey" />` | ✅ Validated |

---
*Report prepared by Database & Software Specialist AI Agent.*
