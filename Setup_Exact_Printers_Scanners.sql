-- ==============================================================================
-- Reset & Update Catalog with Exact Printers & Scanners Requested
-- Printers: HP LaserJet 408, HP LaserJet P2055, HP LaserJet M435, HP LaserJet MFP 443, HP LaserJet Pro M404
-- Scanners: HP ScanJet Pro 2500, HP ScanJet Pro 2600
-- ==============================================================================

USE [Paper Managment];
GO

-- 1. Ensure 'ماسحات ضوئية (Scanners)' Category exists
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'ماسحات ضوئية (Scanners)' OR CategoryName LIKE N'%ماسح%')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'ماسحات ضوئية (Scanners)', 1, 0, 1);
END
GO

-- 2. Clear existing entries from tables safely
DELETE FROM dbo.SupplyTransactionUnits;
DELETE FROM dbo.SupplyTransactions;
DELETE FROM dbo.SupplyDeviceUnits;
DELETE FROM dbo.SupplyItems;
GO

-- 3. Get Category IDs
DECLARE @PrinterCatID INT = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'طابعات');
DECLARE @ScannerCatID INT = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%ماسح%');
DECLARE @InkCatID INT = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'أحبار وتونر');
DECLARE @DrumCatID INT = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'درامات طابعات');

-- 4. Insert Exact Printers (TrackType = 1 Serialized)
INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
VALUES 
(@PrinterCatID, 'PRN-HP-408',   N'طابعة HP Laser 408dn',                'HP', NULL, N'جهاز', 1, 0, 2, 1),
(@PrinterCatID, 'PRN-HP-2055',  N'طابعة HP LaserJet P2055dn',           'HP', NULL, N'جهاز', 1, 0, 2, 1),
(@PrinterCatID, 'PRN-HP-M435',  N'طابعة متعددة الاستخدام HP LaserJet Pro MFP M435nw', 'HP', NULL, N'جهاز', 1, 0, 2, 1),
(@PrinterCatID, 'PRN-HP-443',   N'طابعة متعددة الاستخدام HP LaserJet MFP 443nda',     'HP', NULL, N'جهاز', 1, 0, 2, 1),
(@PrinterCatID, 'PRN-HP-404',   N'طابعة HP LaserJet Pro M404dn',        'HP', NULL, N'جهاز', 1, 0, 2, 1);

-- 5. Insert Exact Scanners (TrackType = 1 Serialized)
INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
VALUES
(@ScannerCatID, 'SCN-HP-2500',  N'ماسح ضوئي HP ScanJet Pro 2500 f1',    'HP', NULL, N'جهاز', 1, 0, 1, 1),
(@ScannerCatID, 'SCN-HP-2600',  N'ماسح ضوئي HP ScanJet Pro 2600 f1',    'HP', NULL, N'جهاز', 1, 0, 1, 1);

-- 6. Insert Matching Original Inks & Drums for these specific printer models
INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
VALUES
-- For HP 408: W1331A / W1331X toner & W1332A drum
(@InkCatID,  'TON-HP-W1331A', N'حبر HP 331A Black Toner (W1331A)',     'HP', 'HP Laser 408dn, MFP 432fdn', N'عبوة', 0, 10, 3, 1),
(@DrumCatID, 'DRM-HP-W1332A', N'درام طابعة HP 332A Imaging Drum (W1332A)', 'HP', 'HP Laser 408dn, MFP 432fdn', N'قطعة', 0, 4, 2, 1),

-- For HP P2055: CE505A / 05A toner
(@InkCatID,  'TON-HP-05A',    N'حبر HP 05A Black Toner (CE505A)',       'HP', 'HP LaserJet P2055dn, P2055d, P2035', N'عبوة', 0, 12, 4, 1),

-- For HP M435: CZ192A / 93A toner
(@InkCatID,  'TON-HP-93A',    N'حبر HP 93A Black Toner (CZ192A)',       'HP', 'HP LaserJet Pro MFP M435nw, M701, M706', N'عبوة', 0, 6, 2, 1),

-- For HP 443: W1335A / 335A toner & CF257A drum
(@InkCatID,  'TON-HP-335A',   N'حبر HP 335A Black Toner (W1335A)',      'HP', 'HP LaserJet MFP 443nda, 438n, 440dn', N'عبوة', 0, 8, 3, 1),
(@DrumCatID, 'DRM-HP-CF257A', N'درام طابعة HP 57A Imaging Drum (CF257A)', 'HP', 'HP LaserJet MFP 443nda, 438n, 440dn', N'قطعة', 0, 3, 2, 1),

-- For HP M404: CF259A / 59A toner
(@InkCatID,  'TON-HP-59A',    N'حبر HP 59A Black Toner (CF259A)',       'HP', 'HP LaserJet Pro M404dn, M404dw, MFP M428fdw', N'عبوة', 0, 15, 5, 1);
GO
