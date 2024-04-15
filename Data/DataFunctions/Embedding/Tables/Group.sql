CREATE TABLE [Embedding].[Group](
	[Id]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [Name]     NVARCHAR (128)   CONSTRAINT [DF_Group_Name] DEFAULT ('') NOT NULL,
    [Flag]     BIGINT           CONSTRAINT [DF_Group_Flag] DEFAULT (0) NOT NULL,
	[FlagName] NVARCHAR (128)   CONSTRAINT [DF_Group_FlagName] DEFAULT ('') NOT NULL,
 CONSTRAINT [PK_Group] PRIMARY KEY CLUSTERED ([Id] ASC)
)
GO
