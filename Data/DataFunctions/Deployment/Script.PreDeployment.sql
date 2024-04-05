/*
 Pre-Deployment Script Template							
--------------------------------------------------------------------------------------
*/

ALTER DATABASE Embeddings SET TRUSTWORTHY ON;
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

sp_configure 'show advanced options', 0
RECONFIGURE;
GO
