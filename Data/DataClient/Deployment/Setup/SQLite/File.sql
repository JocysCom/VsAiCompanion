CREATE TABLE [File] (
    [Id]        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    [GroupName] TEXT DEFAULT '' NOT NULL,
    [GroupFlag] INTEGER DEFAULT 0 NOT NULL,
    [Url]       TEXT DEFAULT '' NOT NULL,
    [Name]      TEXT DEFAULT '' NOT NULL,
    [Size]      INTEGER DEFAULT 0 NOT NULL,
    [HashType]  TEXT DEFAULT '' NOT NULL,
    [Hash]      BLOB,
    [State]     INT DEFAULT 0 NOT NULL,
    [IsEnabled] INT DEFAULT 1 NOT NULL,
    [Modified]  TEXT DEFAULT (datetime('now')) NOT NULL,
    [Created]   TEXT DEFAULT (datetime('now')) NOT NULL,
    [Timestamp] INTEGER DEFAULT 0 NOT NULL
);

CREATE INDEX IX_File_HashType_Hash ON [File]([HashType], [Hash]);

CREATE INDEX IX_File_GroupFlag_FileId_Index_IsEnabled ON [File]([GroupName], [GroupFlag], [Url]);

