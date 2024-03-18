CREATE TABLE [Embedding].[File] (
    [Id]       BIGINT         IDENTITY (1, 1) NOT NULL,
    [Name]     NVARCHAR (256) NOT NULL,
    [Url]      NVARCHAR (MAX) NOT NULL,
    [Size]     BIGINT         NOT NULL,
    [TextSize] BIGINT         NOT NULL,
    [Modified] DATETIME       NOT NULL,
    [Created]  DATETIME       NOT NULL,
    CONSTRAINT [PK_File] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File created date', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Created';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File modify date', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Modified';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Size of extracted text for embedding', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'TextSize';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File size', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Size';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File location', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Url';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'File name', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Name';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Unique file id', @level0type = N'SCHEMA', @level0name = N'Embedding', @level1type = N'TABLE', @level1name = N'File', @level2type = N'COLUMN', @level2name = N'Id';

