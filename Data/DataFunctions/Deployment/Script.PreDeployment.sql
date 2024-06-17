/*
 Pre-Deployment Script Template							
--------------------------------------------------------------------------------------
*/

DECLARE @sql NVARCHAR(MAX);
SET @sql = 'ALTER DATABASE [' + DB_NAME() + '] SET TRUSTWORTHY ON;';
EXEC sp_executesql @sql;

GO

sp_configure 'show advanced options', 1
RECONFIGURE;
GO

-- Enable .NET execution on SQL.
sp_configure 'clr enabled', 1
RECONFIGURE;
GO

-- Disable strict security.	
sp_configure 'clr strict security', 0;
RECONFIGURE;
GO
