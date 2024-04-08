/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
*/

-- Declare properties.
DECLARE
	@clr_hash binary(64),
	@clr_name nvarchar(4000) = 'DataFunctions',
	@clr_full_name nvarchar(4000)

SELECT
	@clr_full_name = a.clr_name,
	@clr_hash = HASHBYTES('SHA2_512', af.content)
FROM sys.assemblies a
JOIN sys.assembly_files af ON a.assembly_id = af.assembly_id
WHERE a.is_user_defined = 1 AND a.[name] = @clr_name AND af.[file_id] = 1

SELECT @clr_full_name [name], @clr_hash as [hash]

---------------------------------------------------------------
-- Requires SQL 2017+
---------------------------------------------------------------

DECLARE @old_hash binary(64)

-- Remove assembly from trust.
SELECT @old_hash = [hash] FROM sys.trusted_assemblies WHERE [description] = @clr_name
IF @old_hash IS NOT NULL
	EXEC sys.sp_drop_trusted_assembly @hash = @old_hash;

SELECT * FROM sys.assemblies WHERE [name] = @clr_name
SELECT * FROM sys.assembly_files WHERE [name] LIKE '%' + @clr_name + '.dll'
SELECT * FROM sys.trusted_assemblies

-- Add assembly to trust.
EXEC sys.sp_add_trusted_assembly @hash = @clr_hash, @description = @clr_name

SELECT * FROM sys.trusted_assemblies

---------------------------------------------------------------
GO
---------------------------------------------------------------

sp_configure 'show advanced options', 1
RECONFIGURE;
GO

-- Disable strict security.	
sp_configure 'clr strict security', 1;
RECONFIGURE;
GO

sp_configure 'show advanced options', 0
RECONFIGURE;
GO
