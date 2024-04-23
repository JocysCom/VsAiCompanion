CREATE PROCEDURE [Embedding].[sp_getSimilarFiles]
    @groupName nvarchar(64),
    @groupFlag bigint,
    @vectors varbinary(max),
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH RankedFiles AS
    (
        SELECT
            f.*,
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].CosineSimilarity(@vectors, fp.[Embedding]) DESC)
        FROM
            [FilePart] AS fp WITH (NOLOCK)
        INNER JOIN
            [Embedding].[File] AS f WITH (NOLOCK) ON f.Id = fp.FileId
        WHERE
            (@groupName = '' OR @groupName = fp.GroupName) AND
            (@groupFlag = 0 OR (@groupFlag & fp.GroupFlag) > 0)
    )
    SELECT *
    FROM RankedFiles
    WHERE RowNum > @skip AND RowNum <= @skip + @take
    ORDER BY RowNum
END
