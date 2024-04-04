CREATE PROCEDURE [Embedding].[sp_getSimilarFileParts]
    @promptEmbedding varbinary(max),
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH RankedFiles AS
    (
        SELECT
            fp.*,
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].CosineSimilarity(@promptEmbedding, fp.[Embedding]) DESC)
        FROM
            [FilePart] AS fp
        INNER JOIN
            [File] AS f ON f.Id = fp.FileId
    )
    SELECT
        *
    FROM
        RankedFiles
    WHERE
        RowNum > @skip AND RowNum <= @skip + @take
END
