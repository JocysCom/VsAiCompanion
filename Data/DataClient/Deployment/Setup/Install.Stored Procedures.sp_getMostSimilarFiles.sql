IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Embedding].[sp_getMostSimilarFiles]') AND type in (N'P', N'PC'))
BEGIN
    PRINT 'Creating stored procedure [Embedding].[sp_getMostSimilarFiles]'

CREATE PROCEDURE [Embedding].[sp_getMostSimilarFiles]
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


END
