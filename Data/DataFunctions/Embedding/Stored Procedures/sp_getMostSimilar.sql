CREATE PROCEDURE [Embedding].[sp_getMostSimilar]
    @promptEmbedding varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
  /*
    SELECT TOP 10
        f.[Name],
        EmbeddingBase.CosineSimilarity(@promptEmbedding, fe.[Embedding]) AS Similarity
    FROM
        FileEmbedding AS fe
    INNER JOIN
        Embedding.[File] AS f ON f.Id = fe.FileId
    ORDER BY Similarity DESC
    */
	
END
