CREATE FUNCTION [Embedding].[CosineSimilarity]
(@bytes1 VARBINARY (8000) NULL, @bytes2 VARBINARY (8000) NULL)
RETURNS REAL
AS
 EXTERNAL NAME [DataFunctions].[JocysCom.VS.AiCompanion.DataFunctions.EmbeddingBase].[CosineSimilarity]
