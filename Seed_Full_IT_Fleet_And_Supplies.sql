-- ==============================================================================
-- Full IT Inventory Setup Based on Official October 2026 Fleet Document
-- ==============================================================================

USE [Paper Managment];
GO

-- 1. Ensure All Required Categories Exist
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'ماكينات تصوير ومستلزماتها')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'ماكينات تصوير ومستلزماتها', 1, 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'أجهزة فاكس ومستلزماتها')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'أجهزة فاكس ومستلزماتها', 1, 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'وحدات تخزين وهاردات (Storage)')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'وحدات تخزين وهاردات (Storage)', 0, 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyCategories WHERE CategoryName = N'كابلات ومحولات وشبكات (Networking & Cables)')
BEGIN
    INSERT INTO dbo.SupplyCategories (CategoryName, TrackBySerial, RequiresPrinterModel, IsActive)
    VALUES (N'كابلات ومحولات وشبكات (Networking & Cables)', 0, 0, 1);
END
GO

-- Resolve Category IDs
DECLARE @CatInks INT       = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'أحبار وتونر');
DECLARE @CatDrums INT      = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'درامات طابعات');
DECLARE @CatPrinters INT   = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName = N'طابعات');
DECLARE @CatCopiers INT    = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%ماكينات تصوير%');
DECLARE @CatFaxes INT      = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%فاكس%');
DECLARE @CatStorage INT    = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%تخزين%');
DECLARE @CatCables INT     = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%كابلات%');
DECLARE @CatParts INT      = (SELECT TOP 1 CategoryID FROM dbo.SupplyCategories WHERE CategoryName LIKE N'%مستلزمات وقطع غيار%');

-- ==============================================================================
-- 2. Insert/Update Fleet Hardware from Document (Page 1)
-- ==============================================================================

-- A. Printers (Hardware - TrackType = 1)
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-446')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-446', N'طابعة HP LaserJet 446', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-402')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-402', N'طابعة HP LaserJet Pro M402', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-1102')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-1102', N'طابعة HP LaserJet Pro P1102', 'HP', N'جهاز', 1, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-7000')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-7000', N'طابعة HP OfficeJet 7000 Wide Format', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-106-107')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-106-107', N'طابعة HP Laser 106 / 107', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-SAM-X337M')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-SAM-X337M', N'طابعة سامسونج Samsung MultiXpress X337M', 'Samsung', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-SAM-X371')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-SAM-X371', N'طابعة سامسونج Samsung ProXpress X371', 'Samsung', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-M128')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-M128', N'طابعة متعددة الاستخدام HP LaserJet Pro MFP M128', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-HP-2015')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-HP-2015', N'طابعة HP LaserJet P2015', 'HP', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRN-XRX-6605')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatPrinters, 'PRN-XRX-6605', N'طابعة زيروكس ألوان Xerox WorkCentre 6605 Color', 'Xerox', N'جهاز', 1, 0, 1, 1);

-- B. Photocopiers (Hardware - TrackType = 1)
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-XRX-5855')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-XRX-5855', N'ماكينة تصوير زيروكس Xerox WorkCentre 5855', 'Xerox', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-XRX-5955')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-XRX-5955', N'ماكينة تصوير زيروكس Xerox WorkCentre 5955', 'Xerox', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-CANON')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-CANON', N'ماكينة تصوير كانون Canon Copier', 'Canon', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-CANON-RICOH-301')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-CANON-RICOH-301', N'ماكينة تصوير كانون / رايكو Ricoh Aficio 301', 'Ricoh', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-TOSHIBA')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-TOSHIBA', N'ماكينة تصوير توشيبا Toshiba e-Studio', 'Toshiba', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-XRX-7535')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-XRX-7535', N'ماكينة تصوير زيروكس ألوان Xerox WorkCentre 7535 Color', 'Xerox', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'COP-HP-9000')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCopiers, 'COP-HP-9000', N'ماكينة تصوير وتعدد HP LaserJet 9000 / 9025', 'HP', N'جهاز', 1, 0, 1, 1);

-- C. Faxes (Hardware - TrackType = 1)
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'FAX-BRT-2840')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatFaxes, 'FAX-BRT-2840', N'فاكس برازر Brother FAX-2840', 'Brother', N'جهاز', 1, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'FAX-XRX-215')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatFaxes, 'FAX-XRX-215', N'فاكس زيروكس Xerox WorkCentre 215', 'Xerox', N'جهاز', 1, 0, 1, 1);

-- ==============================================================================
-- 3. Insert Matching Fleet Consumables, Drums & Heaters (Page 1)
-- ==============================================================================

-- HP Toners & Drums
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-56A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-56A', N'حبارة HP 56A Black Toner (CF256A)', 'HP', 'HP LaserJet 446', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-26A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-26A', N'حبارة HP 26A Black Toner (CF226A)', 'HP', 'HP LaserJet Pro M402, M426', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-85A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-85A', N'حبارة HP 85A Black Toner (CE285A)', 'HP', 'HP LaserJet P1102, M1132, M1212', N'عبوة', 0, 0, 3, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-14A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-14A', N'حبارة HP 14A Black Toner (CF214A)', 'HP', 'HP OfficeJet 7000 / LaserJet 700', N'عبوة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-106A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-106A', N'حبارة HP 106A / 107A Black Toner (W1106A)', 'HP', 'HP Laser 106 / 107', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-44A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-44A', N'حبارة HP 44A Black Toner (CF244A)', 'HP', 'HP LaserJet Pro M128 / M15 / M28', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-53A')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-53A', N'حبارة HP 53A Black Toner (Q7553A)', 'HP', 'HP LaserJet P2015, M2727', N'عبوة', 0, 0, 1, 1);

-- Samsung Toners
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-SAM-203')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-SAM-203', N'حبارة سامسونج 203 Samsung Toner (MLT-D203L)', 'Samsung', 'Samsung MultiXpress X337M', N'عبوة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-SAM-205')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-SAM-205', N'حبارة سامسونج 205 Samsung Toner (MLT-D205L)', 'Samsung', 'Samsung ProXpress X371', N'عبوة', 0, 0, 1, 1);

-- Xerox & Copier Toners, Drums & Heaters
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-XRX-6605-SET')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-XRX-6605-SET', N'مجموعة أحبار ألوان زيروكس Xerox 6605 (CMYK)', 'Xerox', 'Xerox WorkCentre 6605', N'طقم', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-XRX-5855')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-XRX-5855', N'حبارة تصوير زيروكس Xerox 5855 Toner', 'Xerox', 'Xerox WorkCentre 5855', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'DRM-XRX-5855')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatDrums, 'DRM-XRX-5855', N'درام زيروكس Xerox 5855 Drum Cartridge', 'Xerox', 'Xerox WorkCentre 5855', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'FUS-XRX-5855')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'FUS-XRX-5855', N'سخان زيروكس Xerox 5855 Fuser Unit', 'Xerox', 'Xerox WorkCentre 5855', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-XRX-5955')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-XRX-5955', N'حبارة تصوير زيروكس Xerox 5955 Toner', 'Xerox', 'Xerox WorkCentre 5955', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'DRM-XRX-5955')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatDrums, 'DRM-XRX-5955', N'درام زيروكس Xerox 5955 Drum Cartridge', 'Xerox', 'Xerox WorkCentre 5955', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'FUS-XRX-5955')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'FUS-XRX-5955', N'سخان زيروكس Xerox 5955 Fuser Unit', 'Xerox', 'Xerox WorkCentre 5955', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-CAN-COP')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-CAN-COP', N'حبارة ماكينة تصوير كانون Canon Copier Toner', 'Canon', 'Canon Copier', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'DRM-CAN-COP')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatDrums, 'DRM-CAN-COP', N'درام ماكينة تصوير كانون Canon Drum Unit', 'Canon', 'Canon Copier', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'FUS-CAN-COP')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'FUS-CAN-COP', N'سخان ماكينة تصوير كانون Canon Fuser Unit', 'Canon', 'Canon Copier', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-RICOH-301')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-RICOH-301', N'حبارة كانون / رايكو Ricoh MP 301 Toner', 'Ricoh', 'Ricoh Aficio MP 301', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-TOSHIBA')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-TOSHIBA', N'حبارة ماكينة تصوير توشيبا Toshiba Toner', 'Toshiba', 'Toshiba e-Studio', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-XRX-7535-SET')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-XRX-7535-SET', N'مجموعة أحبار ألوان زيروكس Xerox 7535 (CMYK)', 'Xerox', 'Xerox WorkCentre 7535', N'طقم', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-9000')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-9000', N'حبارة ماكينة تصوير HP 9000 (C8543X)', 'HP', 'HP LaserJet 9000', N'عبوة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-HP-9025')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-HP-9025', N'حبارة ماكينة تصوير HP 9025', 'HP', 'HP LaserJet 9025', N'عبوة', 0, 0, 1, 1);

-- Fax Toners & Drums
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-BRT-2840')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-BRT-2840', N'حبارة فاكس برازر Brother TN-2280 / TN-450', 'Brother', 'Brother FAX-2840', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'DRM-BRT-2840')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatDrums, 'DRM-BRT-2840', N'درام فاكس برازر Brother DR-2255 Drum', 'Brother', 'Brother FAX-2840', N'قطعة', 0, 0, 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-XRX-215')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-XRX-215', N'حبارة فاكس زيروكس Xerox 215 Toner', 'Xerox', 'Xerox WorkCentre 215', N'عبوة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'DRM-XRX-215')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, CompatiblePrinters, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatDrums, 'DRM-XRX-215', N'درام فاكس زيروكس Xerox 215 Drum', 'Xerox', 'Xerox WorkCentre 215', N'قطعة', 0, 0, 1, 1);

-- ==============================================================================
-- 4. Insert IT Operating Supplies, Storage & Accessories (Page 2)
-- ==============================================================================

-- Storage
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-SSD-256')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-SSD-256', N'هارد SSD 256GB SATA', 'Kingston/Crucial', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-SSD-128')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-SSD-128', N'هارد SSD 128GB SATA', 'Kingston/Crucial', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-M2-128')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-M2-128', N'هارد M.2 NVMe 128GB', 'Crucial/Lexar', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-M2-256')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-M2-256', N'هارد M.2 NVMe 256GB', 'Crucial/Lexar', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-HDD-EXT')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-HDD-EXT', N'هارد ديسك خارجي HD External Drive', 'WD/Seagate', N'قطعة', 0, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'STO-HDD-ENC')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatStorage, 'STO-HDD-ENC', N'هوزنج هارد ديسك HDD Enclosure USB 3.0', 'Orico', N'قطعة', 0, 0, 3, 1);

-- Networking & Cables
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'NET-SWITCH')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'NET-SWITCH', N'سويتش شبكة Network Switch', 'TP-Link/D-Link', N'جهاز', 1, 0, 2, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CAB-POWER')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CAB-POWER', N'كابل باور أجهزة / شاشات Power Cable', 'Standard', N'قطعة', 0, 0, 10, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CAB-HDMI-10M')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CAB-HDMI-10M', N'كابل شاشة HDMI طول 10 متر', 'HighSpeed', N'قطعة', 0, 0, 3, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CAB-HDMI-STD')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CAB-HDMI-STD', N'كابل شاشة HDMI قياسي (1.5 - 3 متر)', 'Standard', N'قطعة', 0, 0, 10, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CAB-SATA-3M')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CAB-SATA-3M', N'كابل داتا ساتا SATA Data Cable', 'Standard', N'قطعة', 0, 0, 10, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CNV-VGA-BS')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CNV-VGA-BS', N'كونفرتر شاشة Converter BS / VGA', 'Standard', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'CNV-HDMI')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatCables, 'CNV-HDMI', N'كونفرتر شاشة HDMI Converter', 'Standard', N'قطعة', 0, 0, 5, 1);

-- Parts & Peripherals
IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRT-CMOS-BAT')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PRT-CMOS-BAT', N'بطارية جهاز ماذربورد CMOS CR2032', 'Maxell/Sony', N'قطعة', 0, 0, 20, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRT-CPU-COOLER')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PRT-CPU-COOLER', N'مروحة ومعجون بروسيسور CPU Cooler / Paste', 'Standard', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PER-KB-MOUSE-WL')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PER-KB-MOUSE-WL', N'طقم كيبورد وماوس ويرلس Wireless Keyboard & Mouse', 'Logitech/Dell', N'طقم', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PER-MOUSE-USB')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PER-MOUSE-USB', N'ماوس أوريجينال USB Optical Mouse', 'Dell/HP', N'قطعة', 0, 0, 10, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PER-KB-USB')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PER-KB-USB', N'كيبورد أوريجينال USB Keyboard', 'Dell/HP', N'قطعة', 0, 0, 10, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'PRT-RAM-8GB')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'PRT-RAM-8GB', N'رام كمبيوتر 8GB DDR3 / DDR4', 'Samsung/Crucial', N'قطعة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'MSC-CLEAN-FOAM')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'MSC-CLEAN-FOAM', N'فوم تنظيف شاشات وأجهزة Cleaning Foam', 'Standard', N'عبوة', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-BULK-900G')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-BULK-900G', N'كيس حبر بودرة خام 900 جرام (Bulk Toner)', 'Universal', N'كيس', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'TON-BULK-BRT')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatInks, 'TON-BULK-BRT', N'كيس حبر برازر بودرة خام', 'Brother', N'كيس', 0, 0, 5, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SupplyItems WHERE ItemCode = 'MSC-POWER-STRIP-4')
    INSERT INTO dbo.SupplyItems (CategoryID, ItemCode, ItemName, BrandModel, UnitName, TrackType, CurrentStock, LowStockThreshold, IsActive)
    VALUES (@CatParts, 'MSC-POWER-STRIP-4', N'مشترك كهرباء 4 عين أصلي Power Strip 4-Way', 'Standard', N'قطعة', 0, 0, 5, 1);
GO
