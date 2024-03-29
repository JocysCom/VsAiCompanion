CREATE TABLE File (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT DEFAULT '' NOT NULL,
    Url TEXT DEFAULT '' NOT NULL,
    Size INTEGER DEFAULT 0 NOT NULL,
    HashType TEXT DEFAULT '' NOT NULL,
    Hash BLOB,
    GroupName TEXT DEFAULT '' NOT NULL,
    State INTEGER DEFAULT 0 NOT NULL,
    TextSize INTEGER DEFAULT 0 NOT NULL,
    IsEnabled INTEGER DEFAULT 1 NOT NULL,
    Modified TEXT DEFAULT (datetime('now')) NOT NULL,
    Created TEXT DEFAULT (datetime('now')) NOT NULL
);

CREATE INDEX IX_File_HashType_Hash ON File(HashType, Hash);
