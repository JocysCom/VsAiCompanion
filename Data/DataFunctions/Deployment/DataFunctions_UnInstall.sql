---------------------------------------------------------------
-- DROP DataFunctions.RegexBase Functions
---------------------------------------------------------------

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[RegexIsMatch]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[RegexIsMatch]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[RegexReplace]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[RegexReplace]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[GetTitleKey]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[GetTitleKey]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[ConvertToASCII]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[ConvertToASCII]
GO

---------------------------------------------------------------
-- DROP DataFunctions.StringBase Functions
---------------------------------------------------------------

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[HtmlDecode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[HtmlDecode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[HtmlEncode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[HtmlEncode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[UrlDecode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[UrlDecode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[UrlEncode]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[UrlEncode]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE [object_id] = OBJECT_ID(N'[Embedding].[UrlEncodeKeyValue]') AND [type] in (N'FN', N'IF', N'TF', N'FS', N'FT'))
DROP FUNCTION [Embedding].[UrlEncodeKeyValue]
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
