﻿CREATE PROCEDURE [Embedding].[sp_getSimilarFiles]
    @promptEmbedding varbinary(max),
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH RankedFiles AS
    (
        SELECT
            f.*,
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].CosineSimilarity(@promptEmbedding, fp.[Embedding]) DESC)
        FROM
            [FilePart] AS fp
        INNER JOIN
            [Embedding].[File] AS f ON f.Id = fp.FileId
    )
    SELECT
        *
    FROM
        RankedFiles
    WHERE
        RowNum > @skip AND RowNum <= @skip + @take
END
