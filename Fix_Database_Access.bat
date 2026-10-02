@echo off
echo Granting SQL Server permissions to TARGET\H.Fathy...
sqlcmd -S localhost -E -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'TARGET\H.Fathy') CREATE LOGIN [TARGET\H.Fathy] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [TARGET\H.Fathy]; USE [Paper Managment]; IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'TARGET\H.Fathy') CREATE USER [TARGET\H.Fathy] FOR LOGIN [TARGET\H.Fathy]; ALTER ROLE db_owner ADD MEMBER [TARGET\H.Fathy];"
sqlcmd -S DMZ-TARGET -E -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'TARGET\H.Fathy') CREATE LOGIN [TARGET\H.Fathy] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [TARGET\H.Fathy]; USE [Paper Managment]; IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'TARGET\H.Fathy') CREATE USER [TARGET\H.Fathy] FOR LOGIN [TARGET\H.Fathy]; ALTER ROLE db_owner ADD MEMBER [TARGET\H.Fathy];"
echo Done!
pause
