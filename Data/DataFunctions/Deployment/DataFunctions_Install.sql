---------------------------------------------------------------
-- Enable Common Language Runtime (CLR) Integration
---------------------------------------------------------------

/*
GO
sp_configure 'show advanced options', 1
GO
RECONFIGURE;
GO

-- Enable .NET execution on SQL.
sp_configure 'clr enabled', 1
RECONFIGURE;
GO

-- Disable strict security.	
sp_configure 'clr strict security', 0;
RECONFIGURE;

RECONFIGURE WITH OVERRIDE
GO
*/
---------------------------------------------------------------
-- Fix Database Owner
---------------------------------------------------------------

--EXEC SP_CHANGEDBOWNER 'sa'

--DECLARE @admin sysname = DB_NAME() + 'Admin'
--EXEC SP_CHANGEDBOWNER @admin

---------------------------------------------------------------
-- ALLOW UNSAFE CODE
---------------------------------------------------------------
/*
EXEC('
USE [master];

-- Step 1: Create Asymmetric Key from Assembly File.
CREATE ASYMMETRIC KEY DataFunctionsKey FROM EXECUTABLE FILE = ''d:\Projects\Jocys.com GitHub\VsAiCompanion\Data\DataFunctions\JocysCom.VS.AiCompanion.DataFunctions.dll''

-- Step 2: Create SQL Server Login linked to the Asymmetric Key.
CREATE LOGIN DataFunctionsKeyLogin FROM ASYMMETRIC KEY DataFunctionsKey

-- Step 3: Grant UNSAFE assembly permission to the login created.
GRANT UNSAFE ASSEMBLY TO DataFunctionsKeyLogin;
')
*/

---------------------------------------------------------------
-- ALLOW UNSAFE CODE WITH CERTIFICTAE
---------------------------------------------------------------

USE [master]
CREATE CERTIFICATE [DataFunctionsCertificate]
FROM FILE = 'd:\Projects\Jocys.com GitHub\VsAiCompanion\Data\DataFunctions\Deployment\Evaldas_Jocys.cer';
GO

USE [master]
CREATE LOGIN [DataFunctionsLogin] FROM CERTIFICATE [DataFunctionsCertificate]
GO

USE [master]
GRANT UNSAFE ASSEMBLY TO [DataFunctionsLogin];
GO

---------------------------------------------------------------
-- CREATE Assembly
---------------------------------------------------------------

-- Important Note: Makse to to select target database first.
USE [Embeddings]
CREATE ASSEMBLY [DataFunctions]
FROM 'd:\Projects\Jocys.com GitHub\VsAiCompanion\Data\DataFunctions\bin\Release\DataFunctions.dll'
GO

-- Important Note: Makse to to select target database first.
USE [Embeddings]
ALTER ASSEMBLY [DataFunctions] ADD FILE
FROM 'd:\Projects\Jocys.com GitHub\VsAiCompanion\Data\DataFunctions\bin\Release\DataFunctions.dll'
GO

-- Set assembly permissions to allow network access.
ALTER ASSEMBLY [DataFunctions]
   WITH PERMISSION_SET = UNSAFE
GO

---------------------------------------------------------------
-- CREATE DataFunctions.RegexBase Functions
---------------------------------------------------------------

CREATE FUNCTION [Embedding].RegexReplace(@input nvarchar(max), @pattern nvarchar(max), @replacement nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.RegexBase.RegexReplace
GO

CREATE FUNCTION [Embedding].RegexIsMatch(@input nvarchar(max), @pattern nvarchar(max))
RETURNS bit WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.RegexBase.RegexIsMatch
GO

---------------------------------------------------------------
-- CREATE DataFunctions.StringBase Functions
---------------------------------------------------------------

CREATE FUNCTION [Embedding].HtmlDecode(@value nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.StringBase.HtmlDecode
GO

CREATE FUNCTION [Embedding].HtmlEncode(@value nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.StringBase.HtmlEncode
GO

CREATE FUNCTION [Embedding].UrlDecode(@value nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.StringBase.UrlDecode
GO

CREATE FUNCTION [Embedding].UrlEncode(@value nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.StringBase.UrlEncode
GO

CREATE FUNCTION [Embedding].UrlEncodeKeyValue(@key nvarchar(max), @value nvarchar(max))
RETURNS nvarchar(max) WITH EXECUTE AS CALLER
AS EXTERNAL NAME DataFunctions.StringBase.UrlEncodeKeyValue
GO
