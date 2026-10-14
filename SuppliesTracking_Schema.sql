-- ==============================================================================
-- Schema Script: Supplies Tracking Subsystem (متابعة المستلزمات)
-- Target DB: Paper Managment (SQL Server 2014 Compatible)
-- ==============================================================================

USE [Paper Managment];
GO

-- 1. SupplyCategories
IF OBJECT_ID('dbo.SupplyTransactionUnits', 'U') IS NOT NULL DROP TABLE dbo.SupplyTransactionUnits;
IF OBJECT_ID('dbo.SupplyTransactions', 'U') IS NOT NULL DROP TABLE dbo.SupplyTransactions;
IF OBJECT_ID('dbo.SupplyDeviceUnits', 'U') IS NOT NULL DROP TABLE dbo.SupplyDeviceUnits;
IF OBJECT_ID('dbo.SupplyItems', 'U') IS NOT NULL DROP TABLE dbo.SupplyItems;
IF OBJECT_ID('dbo.SupplyCategories', 'U') IS NOT NULL DROP TABLE dbo.SupplyCategories;
GO

CREATE TABLE dbo.SupplyCategories (
    CategoryID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    TrackBySerial BIT NOT NULL DEFAULT 0, -- 0: Consumables, 1: Devices
    RequiresPrinterModel BIT NOT NULL DEFAULT 0, -- 1 for Ink / Toner / Drums
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- 2. SupplyItems
CREATE TABLE dbo.SupplyItems (
    ItemID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CategoryID INT NOT NULL CONSTRAINT FK_SupplyItems_Category REFERENCES dbo.SupplyCategories(CategoryID),
    ItemCode NVARCHAR(50) NOT NULL UNIQUE,
    ItemName NVARCHAR(150) NOT NULL,
    BrandModel NVARCHAR(100) NULL,
    CompatiblePrinters NVARCHAR(250) NULL,
    UnitName NVARCHAR(50) NOT NULL DEFAULT N'قطعة',
    TrackType BIT NOT NULL DEFAULT 0, -- 0: Consumable (by quantity), 1: Serialized/Device (by barcode/unit)
    CurrentStock INT NOT NULL DEFAULT 0,
    TotalReceived INT NOT NULL DEFAULT 0,
    TotalIssued INT NOT NULL DEFAULT 0,
    LowStockThreshold INT NOT NULL DEFAULT 5,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- 3. SupplyDeviceUnits
CREATE TABLE dbo.SupplyDeviceUnits (
    UnitID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ItemID INT NOT NULL CONSTRAINT FK_SupplyDeviceUnits_Item REFERENCES dbo.SupplyItems(ItemID),
    Barcode NVARCHAR(50) NOT NULL UNIQUE,
    SerialNumber NVARCHAR(100) NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'InStock', -- InStock, Issued, UnderMaintenance, Scrapped
    CurrentDepartmentID INT NULL CONSTRAINT FK_SupplyDeviceUnits_Department REFERENCES dbo.Departments(DepartmentID),
    AssignedPrinterOrUser NVARCHAR(150) NULL,
    ReceivedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastMovementDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes NVARCHAR(MAX) NULL
);
GO

-- 4. SupplyTransactions
CREATE TABLE dbo.SupplyTransactions (
    TransactionID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TransactionNumber NVARCHAR(50) NOT NULL UNIQUE,
    TransactionType NVARCHAR(30) NOT NULL, -- Receive, Issue, Return, Maintenance, Scrap
    ItemID INT NOT NULL CONSTRAINT FK_SupplyTransactions_Item REFERENCES dbo.SupplyItems(ItemID),
    Quantity INT NOT NULL DEFAULT 1,
    DepartmentID INT NULL CONSTRAINT FK_SupplyTransactions_Department REFERENCES dbo.Departments(DepartmentID),
    RecipientName NVARCHAR(150) NULL,
    PrinterModel NVARCHAR(100) NULL,
    SupplierOrReference NVARCHAR(150) NULL,
    Notes NVARCHAR(MAX) NULL,
    PerformedBy INT NULL CONSTRAINT FK_SupplyTransactions_User REFERENCES dbo.Users(UserID),
    TransactionDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- 5. SupplyTransactionUnits
CREATE TABLE dbo.SupplyTransactionUnits (
    TransactionID INT NOT NULL CONSTRAINT FK_SupplyTxnUnits_Txn REFERENCES dbo.SupplyTransactions(TransactionID),
    UnitID INT NOT NULL CONSTRAINT FK_SupplyTxnUnits_Unit REFERENCES dbo.SupplyDeviceUnits(UnitID),
    CONSTRAINT PK_SupplyTransactionUnits PRIMARY KEY (TransactionID, UnitID)
);
GO

-- Seed Default Categories
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'أحبار وتونر')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'أحبار وتونر', 0, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'درامات طابعات')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'درامات طابعات', 0, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'طابعات')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'طابعات', 1, 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'أجهزة حاسب ولابتوب')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'أجهزة حاسب ولابتوب', 1, 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'مستلزمات وقطع غيار أخرى')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'مستلزمات وقطع غيار أخرى', 0, 0, 1);
END
GO
