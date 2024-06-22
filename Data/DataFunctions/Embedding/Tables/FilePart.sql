CREATE TABLE [Embedding].[FilePart] (
    [Id]             BIGINT           IDENTITY (1, 1) NOT NULL,
    [GroupName]      NVARCHAR (64)    CONSTRAINT [DF_FilePart_GroupName] DEFAULT ('') NOT NULL,
    [GroupFlag]      BIGINT           CONSTRAINT [DF_FilePart_GroupFlag] DEFAULT ((0)) NOT NULL,
    [FileId]         BIGINT           NOT NULL,
    [Index]          INT              CONSTRAINT [DF_FilePart_Index] DEFAULT ((0)) NOT NULL,
    [Count]          INT              CONSTRAINT [DF_FilePart_Count] DEFAULT ((1)) NOT NULL,
    [HashType]       VARCHAR (20)     CONSTRAINT [DF_FilePart_HashType] DEFAULT ('') NOT NULL,
    [Hash]           BINARY (64)      NULL,
    [State]          INT              CONSTRAINT [DF_Part_State] DEFAULT ((0)) NOT NULL,
    [Text]           NVARCHAR (MAX)   CONSTRAINT [DF_FilePart_Text] DEFAULT ('') NOT NULL,
    [TextTokens]     BIGINT           CONSTRAINT [DF_FilePart_Tokens] DEFAULT ((0)) NOT NULL,
    [EmbeddingModel] NVARCHAR (50)    CONSTRAINT [DF_FilePart_EmbeddingModel] DEFAULT ('') NOT NULL,
    [EmbeddingSize]  INT              CONSTRAINT [DF_FilePart_EmbeddingSize] DEFAULT ((0)) NOT NULL,
    [Embedding]      VARBINARY (MAX)  NULL,
    [IsEnabled]      BIT              CONSTRAINT [DF_FilePart_IsEnabled] DEFAULT ((1)) NOT NULL,
    [Created]        DATETIME         CONSTRAINT [DF_FilePart_Created] DEFAULT (getdate()) NOT NULL,
    [Modified]       DATETIME         CONSTRAINT [DF_FilePart_Modified] DEFAULT (getdate()) NOT NULL,
    [Timestamp]      BIGINT           CONSTRAINT [DF_FilePart_Timestamp] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_FilePart] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FilePart_File] FOREIGN KEY ([FileId]) REFERENCES [Embedding].[File] ([Id])
);



GO
CREATE NONCLUSTERED INDEX [IX_FilePart_HashType_Hash]
    ON [Embedding].[FilePart]([HashType] ASC, [Hash] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_FilePart_GroupName_GroupFlag_FileId_Index_IsEnabled]
    ON [Embedding].[FilePart]([GroupName] ASC, [GroupFlag] ASC, [FileId] ASC, [Index] ASC, [IsEnabled] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_FilePart_Filed_Index]
    ON [Embedding].[FilePart]([FileId] ASC, [Index] ASC);


GO



GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'UTC date and time when the part was last modified.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Modified';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'UTC date and time when the part was created.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Created';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Indicates if the record is active and considered in searches.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'IsEnabled';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Binary representation of embedding vectors generated for this file part.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Embedding';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The number of vectors contained within the embedding, e.g., 256, 512, 1024, 2048.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'EmbeddingSize';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AI Model used for embedding.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'EmbeddingModel';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Processing state of the record.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'State';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The SHA-256 hash of the ''PartText'' bytes, represented as a string in UTF-16 encoding.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Hash';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Specifies the hash algorithm used to generate the hash value: MD2, MD4, MD5, SHA, SHA1, SHA2_256, and SHA2_512.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'HashType';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'A bitwise operation will be used to include groups by their group flag.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'GroupFlag';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Total number of parts into which the file is divided.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Count';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Index of this part relative to other parts of the same file.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Index';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File text part used for embedding', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Text';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique identifier of the associated file.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'FileId';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique identifier of the file part.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Id';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Name of the group to which the file belongs.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'GroupName';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File Part size in tokens.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'TextTokens';

GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Time stamp in UTC as 100-nanosecond intervals from 0001-01-01 00:00:00Z.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FilePart', @level2type = N'COLUMN', @level2name = N'Timestamp';
