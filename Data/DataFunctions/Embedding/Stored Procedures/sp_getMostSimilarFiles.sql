
CREATE PROCEDURE [Embedding].[sp_getMostSimilarFiles]
    @promptEmbedding varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
/*
	SELECT TOP 10
		f1.[Name] AS File1,
		f2.[Name] AS File2,
		CLR_NAMESPACE.CosineSimilarity(
			CLR_NAMESPACE.BinaryToVector(f1.[Embedding]),
			CLR_NAMESPACE.BinaryToVector(f2.[Embedding])
		) AS Similarity
	FROM
		FileEmbedding AS f1
	CROSS JOIN
		FileEmbedding AS f2
	WHERE
		f1.Id != f2.Id
	ORDER BY Similarity DESC
	*/
END
