---------------------------------------------------------------
-- DROP DataFunctions.RegexBase Functions
---------------------------------------------------------------

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[RegexIsMatch]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[RegexIsMatch]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[RegexReplace]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[RegexReplace]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[GetTitleKey]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[GetTitleKey]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[ConvertToASCII]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[ConvertToASCII]
GO

---------------------------------------------------------------
-- DROP DataFunctions.StringBase Functions
---------------------------------------------------------------

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[HtmlDecode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[HtmlDecode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[HtmlEncode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[HtmlEncode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[UrlDecode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[UrlDecode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[UrlEncode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[UrlEncode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[dbo].[UrlEncodeKeyValue]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [dbo].[UrlEncodeKeyValue]
GO


---------------------------------------------------------------
-- DROP Assembly
---------------------------------------------------------------

IF EXISTS (SELECT * FROM sys.assemblies a WHERE a.name = N'DataFunctions')
DROP ASSEMBLY [DataFunctions]
GO

---------------------------------------------------------------
-- DROP Login and Key
---------------------------------------------------------------

EXEC('
USE [master]

IF EXISTS (SELECT * FROM master.sys.server_principals WHERE [name] = ''DataFunctionsKeyLogin'')
DROP LOGIN DataFunctionsKeyLogin

IF EXISTS (SELECT * FROM master.sys.asymmetric_keys WHERE [name] = ''DataFunctionsKey'')
DROP ASYMMETRIC KEY DataFunctionsKey
')
