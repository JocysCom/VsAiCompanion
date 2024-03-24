CREATE TABLE [Embedding].[File] (
    [Id]        BIGINT         IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (256) CONSTRAINT [DF_File_Name] DEFAULT ('') NOT NULL,
    [Url]       NVARCHAR (MAX) CONSTRAINT [DF_File_Url] DEFAULT ('') NOT NULL,
    [Size]      BIGINT         CONSTRAINT [DF_File_Size] DEFAULT ((0)) NOT NULL,
    [HashType]  VARCHAR (20)   CONSTRAINT [DF_File_HashType] DEFAULT ('') NOT NULL,
    [Hash]      BINARY (64)    NULL,
    [GroupName] NVARCHAR (64)  CONSTRAINT [DF_File_GroupName] DEFAULT ('') NOT NULL,
    [State]     INT            CONSTRAINT [DF_File_State] DEFAULT ((0)) NOT NULL,
    [TextSize]  BIGINT         CONSTRAINT [DF_File_TextSize] DEFAULT ((0)) NOT NULL,
    [IsEnabled] BIT            CONSTRAINT [DF_File_IsEnabled] DEFAULT ((1)) NOT NULL,
    [Modified]  DATETIME       CONSTRAINT [DF_File_Modified] DEFAULT (getdate()) NOT NULL,
    [Created]   DATETIME       CONSTRAINT [DF_File_Created] DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_File] PRIMARY KEY CLUSTERED ([Id] ASC)
);




GO
CREATE NONCLUSTERED INDEX [IX_File_HashType_Hash]
    ON [Embedding].[File]([HashType] ASC, [Hash] ASC);


GO



GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'UTC date and time when the file was created.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Created';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'UTC date and time when the file was last modified.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Modified';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Indicates if the record is active and included in searches.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'IsEnabled';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Size in bytes of the text extracted for embedding.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'TextSize';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Processing state of the record.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'State';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the group to which the file belongs.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'GroupName';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The SHA-256 hash of the file''s bytes.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Hash';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Specifies the hash algorithm used to generate the hash value: MD2, MD4, MD5, SHA, SHA1, SHA2_256, and SHA2_512.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'HashType';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File size in bytes.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Size';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'URL specifying the file''s location.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Url';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File name.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Name';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique file id.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Id';

