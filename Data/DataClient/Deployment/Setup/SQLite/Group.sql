﻿CREATE TABLE Group (
    [Id]        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    [Name]      TEXT DEFAULT '' NOT NULL,
	[Flag]      INTEGER DEFAULT 0 NOT NULL,
	[FlagName]  TEXT DEFAULT '' NOT NULL,
);