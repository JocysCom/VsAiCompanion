CREATE TABLE [Embedding].[FileEmbedding] (
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [PartText]       NVARCHAR (MAX)  CONSTRAINT [DF_FileEmbedding_PartText] DEFAULT ('') NOT NULL,
    [PartIndex]      INT             CONSTRAINT [DF_FileEmbedding_PartIndex] DEFAULT ((0)) NOT NULL,
    [PartCount]      INT             CONSTRAINT [DF_FileEmbedding_PartCount] DEFAULT ((1)) NOT NULL,
    [FileId]         BIGINT          NOT NULL,
    [Embedding]      VARBINARY (MAX) NOT NULL,
    [EmbeddingSize]  INT             CONSTRAINT [DF_FileEmbedding_EmbeddingSize] DEFAULT ((0)) NOT NULL,
    [EmbeddingModel] NVARCHAR (50)   CONSTRAINT [DF_FileEmbedding_EmbeddingModel] DEFAULT ('') NOT NULL,
    CONSTRAINT [PK_FileEmbedding] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FileEmbedding_File] FOREIGN KEY ([Id]) REFERENCES [Embedding].[File] ([Id])
);




GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AI Model used for embedding.', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'EmbeddingModel';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Number of vectors inside embedding: 256, 512, 1024, 2048...', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'EmbeddingSize';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Embedding vectors', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'Embedding';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Id to original file info', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'FileId';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Number of parts', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'PartCount';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Part index', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'PartIndex';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File text part used for embedding', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'FileEmbedding', @level2type = N'COLUMN', @level2name = N'PartText';

