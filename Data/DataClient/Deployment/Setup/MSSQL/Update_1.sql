IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'Timestamp' AND Object_ID = Object_ID(N'Embedding.File'))
BEGIN
    ALTER TABLE [Embedding].[File]     ADD [Timestamp] BIGINT CONSTRAINT [DF_File_Timestamp] DEFAULT (0) NOT NULL;
END

GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'Timestamp' AND Object_ID = Object_ID(N'Embedding.FilePart'))
BEGIN
    ALTER TABLE [Embedding].[FilePart] ADD [Timestamp] BIGINT CONSTRAINT [DF_FilePart_Timestamp] DEFAULT (0) NOT NULL;
END

GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'Timestamp' AND Object_ID = Object_ID(N'Embedding.Group'))
BEGIN
    ALTER TABLE [Embedding].[Group]    ADD [Timestamp] BIGINT CONSTRAINT [DF_Group_Timestamp] DEFAULT (0) NOT NULL;
END
