
CREATE PROCEDURE [Embedding].[sp_getMostSimilarFiles]
    @promptEmbedding varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
	SELECT TOP 10
		f1.[Url] AS Url1, f2.[Url] AS Url2,
		[Embedding].CosineSimilarity(fe1.[Embedding], fe2.[Embedding]) AS Similarity
	FROM
		FileEmbedding AS fe1
	CROSS JOIN
		FileEmbedding AS fe2
	INNER JOIN [File] f1 ON f1.Id = fe1.FileId
	INNER JOIN [File] f2 ON f2.Id = fe2.FileId
	WHERE
		fe1.Id != fe2.Id
	ORDER BY Similarity DESC
END
