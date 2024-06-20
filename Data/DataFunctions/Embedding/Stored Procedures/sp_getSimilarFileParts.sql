CREATE PROCEDURE [Embedding].[sp_getSimilarFileParts]
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
            fp.*,
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].fn_CosineSimilarity(@vectors, fp.[Embedding]) DESC)
        FROM
            [FilePart] AS fp WITH (NOLOCK)
        INNER JOIN
            [File] AS f WITH (NOLOCK) ON f.Id = fp.FileId
        WHERE
            (@groupName = '' OR @groupName = fp.GroupName) AND
            (@groupFlag = 0 OR (@groupFlag & fp.GroupFlag) > 0)
    )
    SELECT *
    FROM RankedFiles
    WHERE RowNum > @skip AND RowNum <= @skip + @take
	ORDER BY RowNum
END
