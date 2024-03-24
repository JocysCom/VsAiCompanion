
CREATE PROCEDURE [Embedding].[sp_getMostSimilarFiles]
    @promptEmbedding varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
	SELECT TOP 10
		f1.[Url] AS Url1, f2.[Url] AS Url2,
		[Embedding].CosineSimilarity(fp1.[Embedding], fp2.[Embedding]) AS Similarity
	FROM
		FilePart AS fp1
	CROSS JOIN
		FilePart AS fp2
	INNER JOIN [File] f1 ON f1.Id = fp1.FileId
	INNER JOIN [File] f2 ON f2.Id = fp2.FileId
	WHERE
		fp1.Id != fp2.Id
	ORDER BY Similarity DESC
END
