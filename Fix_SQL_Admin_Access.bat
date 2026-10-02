@echo off
echo ========================================================
echo Fixing SQL Server Permissions for TARGET\H.Fathy...
echo ========================================================
echo.

echo [1/5] Closing any background SSMS connections...
taskkill /F /IM Ssms.exe 2>nul

echo.
echo [2/5] Stopping SQL Server Service...
net stop SQLSERVERAGENT 2>nul
net stop MSSQLFDLauncher 2>nul
net stop MSSQLSERVER /y

echo.
echo [3/5] Starting SQL Server in Single-User Recovery Mode...
net start MSSQLSERVER /m"SQLCMD"

echo.
echo [4/5] Granting SysAdmin and Database Owner permissions...
sqlcmd -S localhost -E -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'TARGET\H.Fathy') CREATE LOGIN [TARGET\H.Fathy] FROM WINDOWS; EXEC sp_addsrvrolemember 'TARGET\H.Fathy', 'sysadmin'; ALTER SERVER ROLE [sysadmin] ADD MEMBER [TARGET\H.Fathy]; ALTER AUTHORIZATION ON DATABASE::[Paper Managment] TO [TARGET\H.Fathy]; USE [Paper Managment]; IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'TARGET\H.Fathy') CREATE USER [TARGET\H.Fathy] FOR LOGIN [TARGET\H.Fathy]; ALTER ROLE [db_owner] ADD MEMBER [TARGET\H.Fathy];"

echo.
echo [5/5] Restarting SQL Server in Normal Mode...
net stop MSSQLSERVER /y
net start MSSQLSERVER

echo.
echo ========================================================
echo SUCCESS! Full SysAdmin and Database permissions granted.
echo ========================================================
pause
