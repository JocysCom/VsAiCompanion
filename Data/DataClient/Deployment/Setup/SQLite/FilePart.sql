CREATE TABLE FilePart (
    [Id]             INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    [GroupName]      TEXT DEFAULT '' NOT NULL,
    [GroupFlag]      INTEGER DEFAULT 0 NOT NULL,
    [FileId]         INTEGER NOT NULL,
    [Index]          INT DEFAULT 0 NOT NULL,
    [Count]          INT DEFAULT 1 NOT NULL,
    [HashType]       TEXT DEFAULT '' NOT NULL,
    [Hash]           BLOB,
    [State]          INT DEFAULT 0 NOT NULL,
    [Text]           TEXT DEFAULT '' NOT NULL,
    [TextTokens]         INTEGER DEFAULT 0 NOT NULL,
    [EmbeddingModel] TEXT DEFAULT '' NOT NULL,
    [EmbeddingSize]  INT DEFAULT 0 NOT NULL,
    [Embedding]      BLOB,
    [IsEnabled]      INT DEFAULT 1 NOT NULL,
    [Created]        TEXT DEFAULT (datetime('now')) NOT NULL,
    [Modified]       TEXT DEFAULT (datetime('now')) NOT NULL,
    [Timestamp]      INTEGER DEFAULT 0 NOT NULL,
    FOREIGN KEY (FileId) REFERENCES [File] (Id)
);

CREATE INDEX IX_FilePart_HashType_Hash ON [FilePart]([HashType], [Hash]);

CREATE INDEX IX_FilePart_GroupFlag_FileId_Index_IsEnabled ON [FilePart]([GroupName], [GroupFlag], [FileId], [Index], [IsEnabled]);

CREATE INDEX IX_FilePart_Filed_Index ON [FilePart]([FileId], [Index]);
