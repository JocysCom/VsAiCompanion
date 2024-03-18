
CREATE PROCEDURE sp_getMostSimilar
    @promptEmbedding varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
    /*
	SELECT TOP 10
        f.[Name] AS FileName,
        CLR_NAMESPACE.CosineSimilarity(
            CLR_NAMESPACE.BinaryToVector(@promptEmbedding),
            CLR_NAMESPACE.BinaryToVector(fe.[Embedding])
        ) AS Similarity
    FROM
        FileEmbedding AS fe
    INNER JOIN
        File AS f ON f.Id = fe.FileId
    ORDER BY Similarity DESC
	*/
END