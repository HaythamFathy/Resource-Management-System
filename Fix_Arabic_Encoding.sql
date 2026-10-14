-- Fix Arabic Encoding for SupplyCategories and SupplyItems
USE [Paper Managment];
GO

-- 1. SupplyCategories
UPDATE dbo.SupplyCategories SET CategoryName = N'أحبار وتونر' WHERE CategoryID = 1;
UPDATE dbo.SupplyCategories SET CategoryName = N'درامات طابعات' WHERE CategoryID = 2;
UPDATE dbo.SupplyCategories SET CategoryName = N'طابعات' WHERE CategoryID = 3;
UPDATE dbo.SupplyCategories SET CategoryName = N'أجهزة حاسب ولابتوب' WHERE CategoryID = 4;
UPDATE dbo.SupplyCategories SET CategoryName = N'مستلزمات وقطع غيار أخرى' WHERE CategoryID = 5;
GO

-- 2. SupplyItems
UPDATE dbo.SupplyItems SET ItemName = N'حبر أسود HP LaserJet 85A (CE285A)', UnitName = N'عبوة' WHERE ItemID = 1;
UPDATE dbo.SupplyItems SET ItemName = N'حبر كانون Canon 725 أسود', UnitName = N'عبوة' WHERE ItemID = 2;
UPDATE dbo.SupplyItems SET ItemName = N'درام طابعة HP 32A Original Drum (CF232A)', UnitName = N'قطعة' WHERE ItemID = 3;
UPDATE dbo.SupplyItems SET ItemName = N'طابعة HP LaserJet Pro M404dn', UnitName = N'جهاز' WHERE ItemID = 4;
UPDATE dbo.SupplyItems SET ItemName = N'حاسب مكتبي Dell OptiPlex 7090 Core i7', UnitName = N'جهاز' WHERE ItemID = 5;
UPDATE dbo.SupplyItems SET ItemName = N'لابتوب Dell Latitude 5420 Core i5', UnitName = N'جهاز' WHERE ItemID = 6;
GO
