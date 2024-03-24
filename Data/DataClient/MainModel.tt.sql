CREATE PROCEDURE [Security].[Tools_GetColumnInfo]
	@SchemaName nvarchar(256) = NULL,
	@TableName nvarchar(256) = NULL,
	@Name nvarchar(256) = NULL,
	@IsMsShipped bit = 0
AS

/*
-- Get Table Types and Tables
EXEC [Security].[Tools_GetColumnInfo] null, null, null, null
-- Get Table Types
EXEC [Security].[Tools_GetColumnInfo] null, null, null, 1
-- Get Tables
EXEC [Security].[Tools_GetColumnInfo] null, null, null, 0

EXEC [Security].[Tools_GetColumnInfo] null, 'Identity', null, 0
EXEC [Security].[Tools_GetColumnInfo] 'Trip', 'Booking', null, 0
EXEC [Security].[Tools_GetColumnInfo] 'Supplier', 'Vendor', null, 0

*/

--INSERT INTO [Security].[SchemaSettings]
--EXEC [Security].[Tools_GetColumnInfo] 'Identity'

----- TABLE INFO ----------------------------------------------

-- Create a temp table TO store the select results
DECLARE @TableInfo TABLE
(
	[Id] nvarchar(256) NOT NULL PRIMARY KEY,
	[SchemaName] nvarchar(128) NOT NULL,
    [TableName] nvarchar(128) NOT NULL,
	[Index] int NOT NULL,
	[Name] nvarchar(128) NOT NULL,
	[Type] nvarchar(128) NOT NULL,
	[Length] int NOT NULL,
	[Precision] int NOT NULL,
	[Scale] int NOT NULL,
	[Default] nvarchar(128) NOT NULL,
	[IsNullable] bit NOT NULL,
	[IsCustomLength] bit NOT NULL,
	[IsCustomPrecision] bit NOT NULL,
	[IsMsShipped] bit NOT NULL,
	[Collation] nvarchar(128) NOT NULL,
	[Description] nvarchar(MAX) NOT NULL,
	-- CustomOptions:
	-- Enabled = 1, Internal = 2,  External = 4, Approved = 8
	-- RecordCreated = 16, RecordUpdated = 32, RecordDeleted = 64
	[CustomOptions] int NOT NULL,
	[CustomType] nvarchar(128) NOT NULL,
	-- Primary Key info.
	[IsPrimaryKey]  bit NOT NULL,
	[IsIdentity]  bit NOT NULL,
	-- Foreign Primary Key info.
	[PrimaryKeySchema] nvarchar(128) NOT NULL,
	[PrimaryKeyTable] nvarchar(128) NOT NULL,
    [PrimaryKeyColumn] nvarchar(128) NOT NULL,
    [PrimaryKeyNumber] int NOT NULL
)

-- Fill table.
INSERT INTO @TableInfo
SELECT
	[Id] = CONCAT(SCHEMA_NAME(ISNULL(tt.[schema_id], t.[schema_id])), '.', ISNULL(tt.[name], OBJECT_NAME(c.[object_id])), '.', c.[name]),
	[SchemaName] = ISNULL(SCHEMA_NAME(ISNULL(tt.[schema_id], t.[schema_id])),''),
	[TableName] = ISNULL(tt.[name], OBJECT_NAME(c.[object_id])),
	[Index] = c.column_id,
	[Name] = c.[name],
	[Type] = ut.[name],
	[Length] = c.max_length,
	[Precision] = c.[precision],
	[Scale] = c.[Scale],
	[Default] = CASE WHEN (sc.[text] is null) THEN '' ELSE sc.[text] END,
	[IsNullable] = c.is_nullable,
	[IsCustomLength] =  CASE WHEN (ut.[length] = 8000 OR ut.prec = 38) THEN 1 ELSE 0 END,
	[IsCustomPrecision] =  CASE WHEN (ut.prec = 38) THEN 1 ELSE 0 END,
	[IsMsShipped] = CASE WHEN  OBJECTPROPERTY(c.[object_id], 'IsMsShipped') = 1 AND tt.is_user_defined = 0 THEN 1 ELSE 0 END,
	[Collation] = CASE WHEN (c.collation_name is null) THEN '' ELSE c.collation_name END,
	[Description] = CONVERT(nvarchar(MAX),CASE WHEN (ex.[value] is null) THEN '' ELSE ex.[value] END),
	[CustomOptions] = 0,
	[CustomType] = '',
	-- Primary Key Info.
	[IsPrimaryKey] = 0,
	[IsIdentity] = columnproperty(c.[object_id], c.[name], 'IsIdentity'),
	-- Foreign Primary Key info
	[PrimaryKeySchema] = '',
	[PrimaryKeyTable] = '',
    [PrimaryKeyColumn] = '',
    [PrimaryKeyNumber] = ''
FROM sys.columns c
-- Select user defined table types (IsMsShipped = 1)
LEFT OUTER JOIN sys.table_types tt ON tt.type_table_object_id = c.[object_id]
-- Select user defined tables (IsMsShipped = 0)
LEFT OUTER JOIN sys.tables t ON t.[object_id] = c.[object_id]
LEFT OUTER JOIN sys.extended_properties ex ON ex.major_id = c.[object_id] AND ex.minor_id = c.column_id AND ex.name = 'MS_Description'
LEFT OUTER JOIN sys.syscomments sc ON c.default_object_id = sc.id
LEFT OUTER JOIN sys.systypes ut ON ut.xusertype = c.user_type_id
--LEFT OUTER JOIN sys.systypes ut ON ut.xtype = c.system_type_id
WHERE
	((@IsMsShipped IS NULL) OR (CASE WHEN  OBJECTPROPERTY(c.[object_id], 'IsMsShipped') = 1 AND tt.is_user_defined = 0 THEN 1 ELSE 0 END) = @IsMsShipped) AND
	((@SchemaName IS NULL) OR SCHEMA_NAME(ISNULL(tt.[schema_id], t.[schema_id])) LIKE @SchemaName) AND
	((@TableName IS NULL) OR ISNULL(tt.[name], OBJECT_NAME(c.[object_id])) LIKE @TableName) AND
	((@Name IS NULL) OR OBJECT_NAME(c.[object_id]) LIKE @Name) AND
	ISNULL(tt.[name], SCHEMA_NAME(t.[schema_id])) IS NOT NULL
ORDER BY [SchemaName], [TableName], [Index]

/*

IF @TableName = 'Identity'
BEGIN 
	SELECT *
	INTO [Security].[ColumnInfo]
	FROM @TableInfo
	ORDER BY [SchemaName], [TableName], [Index]
END
ELSE IF @TableName = 'Booking'
BEGIN
	INSERT INTO [Security].[ColumnInfo]
	SELECT * FROM @TableInfo
	ORDER BY [SchemaName], [TableName], [Index]
END 

*/

----- UPDATE PRIMARY KEY INFO ---------------------------------

DECLARE @PrimaryKeys TABLE (
	[SchemaName] nvarchar(128) NOT NULL,
	[TableName] nvarchar(128) NOT NULL,
    [ColumnName] nvarchar(128) NOT NULL
)

INSERT INTO @PrimaryKeys
SELECT
	K.TABLE_SCHEMA,
	K.TABLE_NAME,
    K.COLUMN_NAME
    -- K.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON
	C.TABLE_NAME = K.TABLE_NAME
	AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG
    AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA
    AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
WHERE
	C.CONSTRAINT_TYPE = 'PRIMARY KEY'

UPDATE ti SET
	ti.IsPrimaryKey = 1
FROM @TableInfo ti
INNER JOIN @PrimaryKeys pk ON
	pk.SchemaName = ti.SchemaName AND
	pk.TableName = ti.TableName AND
	pk.ColumnName = ti.[Name]

----- UPDATE FOREIGN KEY INFO ---------------------------------

DECLARE @ForeignKeys TABLE
(
	[ForeignSchema] nvarchar(128) NOT NULL,
	[ForeignTable] nvarchar(128) NOT NULL,
    [ForeignColumn] nvarchar(128) NOT NULL,
	[PrimarySchema] nvarchar(128) NOT NULL,
	[PrimaryTable] nvarchar(128) NOT NULL,
    [PrimaryColumn] nvarchar(128) NOT NULL,
    [PrimaryNumber] int NOT NULL,
    [ConstraintName] nvarchar(256) NOT NULL
)

INSERT INTO @ForeignKeys
SELECT
	SCHEMA_NAME(tab.[schema_id]) AS ForeignSchema,
	tab.[name] AS ForeignTable,
    col.[name] AS ForeignColumn,
	SCHEMA_NAME(pk_tab.[schema_id]) AS PrimarySchema,
    pk_tab.[name] AS PrimaryTable,
    pk_col.[name] AS PrimaryColumn,
    fk_cols.constraint_column_id AS [Number],
    fk.[name] AS ConstraintName
FROM sys.tables tab
    INNER JOIN sys.columns col ON col.[object_id] = tab.[object_id]
    LEFT OUTER JOIN  sys.foreign_key_columns fk_cols ON fk_cols.parent_object_id = tab.[object_id] AND fk_cols.parent_column_id = col.column_id
    LEFT OUTER JOIN  sys.foreign_keys fk ON fk.[object_id] = fk_cols.constraint_object_id
    LEFT OUTER JOIN  sys.tables pk_tab ON pk_tab.[object_id] = fk_cols.referenced_object_id
    LEFT OUTER JOIN  sys.columns pk_col ON pk_col.column_id = fk_cols.referenced_column_id AND pk_col.[object_id] = fk_cols.referenced_object_id
WHERE fk.[object_id] IS NOT NULL
ORDER BY ForeignSchema, ForeignTable, ForeignColumn

UPDATE ti SET
	ti.PrimaryKeySchema = fk.PrimarySchema,
	ti.PrimaryKeyTable = fk.PrimaryTable,
	ti.PrimaryKeyColumn = fk.PrimaryColumn,
	ti.PrimaryKeyNumber = fk.PrimaryNumber
FROM @TableInfo ti
INNER JOIN @ForeignKeys fk ON
	fk.ForeignSchema = ti.SchemaName AND
	fk.ForeignTable = ti.TableName AND
	fk.ForeignColumn = ti.[Name]

----- RETURN RESULTS ------------------------------------------

SELECT * FROM @TableInfo
ORDER BY [SchemaName], [TableName], [Index]