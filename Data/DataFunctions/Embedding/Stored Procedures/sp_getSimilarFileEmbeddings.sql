CREATE PROCEDURE [Embedding].[sp_getSimilarFileEmbeddings]
    @promptEmbedding varbinary(max),
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH RankedFiles AS
    (
        SELECT
            fe.*,
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].CosineSimilarity(@promptEmbedding, fe.[Embedding]) DESC)
        FROM
            FileEmbedding AS fe
        INNER JOIN
            [Embedding].[File] AS f ON f.Id = fe.FileId
    )
    SELECT
        *
    FROM
        RankedFiles
    WHERE
        RowNum > @skip AND RowNum <= @skip + @take
END
