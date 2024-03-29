CREATE TABLE FilePart (
    Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    FileId INTEGER NOT NULL,
    [Text] TEXT DEFAULT '' NOT NULL,
    [Index] INT DEFAULT 0 NOT NULL,
    [Count] INT DEFAULT 1 NOT NULL,
    GroupFlag INTEGER DEFAULT 0 NOT NULL,
    HashType TEXT DEFAULT '' NOT NULL,
    [Hash] BLOB,
    [State] INTEGER DEFAULT 0 NOT NULL,
    EmbeddingModel TEXT DEFAULT '' NOT NULL,
    EmbeddingSize INT DEFAULT 0 NOT NULL,
    Embedding BLOB,
    IsEnabled INTEGER DEFAULT 1 NOT NULL,
    Created TEXT DEFAULT (datetime('now')) NOT NULL,
    Modified TEXT DEFAULT (datetime('now')) NOT NULL,
    FOREIGN KEY (FileId) REFERENCES [File] (Id)
);

CREATE INDEX IX_FilePart_HashType_Hash ON FilePart(HashType, Hash);

CREATE INDEX IX_FilePart_GroupFlag_FileId_Index_IsEnabled ON FilePart(GroupFlag, FileId, [Index], IsEnabled);

CREATE INDEX IX_FilePart_Filed_Index ON FilePart(FileId, [Index]);
