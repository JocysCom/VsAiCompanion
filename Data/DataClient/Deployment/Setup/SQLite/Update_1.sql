-- Attempt to add 'Timestamp' column to 'File'
ALTER TABLE [File] ADD COLUMN [Timestamp] INTEGER DEFAULT 0;

GO
-- Attempt to add 'Timestamp' column to 'FilePart'
ALTER TABLE [FilePart] ADD COLUMN [Timestamp] INTEGER DEFAULT 0;

GO
-- Attempt to add 'Timestamp' column to 'Group'
ALTER TABLE [Group] ADD COLUMN [Timestamp] INTEGER DEFAULT 0;
