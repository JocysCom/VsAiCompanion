IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Embedding].[sp_getSimilarFileParts]') AND type in (N'P', N'PC'))
BEGIN
    PRINT 'Creating stored procedure [Embedding].[sp_getSimilarFileParts]'

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
            RowNum = ROW_NUMBER() OVER (ORDER BY [Embedding].CosineSimilarity(@vectors, fp.[Embedding]) DESC)
        FROM
            [FilePart] AS fp WITH (NOLOCK)
        INNER JOIN
            [File] AS f WITH (NOLOCK) ON f.Id = fp.FileId
        WHERE
            fp.GroupName = @groupName AND
            (@groupFlag = 0 OR (fp.GroupFlag & @groupFlag) > 0)
    )
    SELECT *
    FROM RankedFiles
    WHERE RowNum > @skip AND RowNum <= @skip + @take
	ORDER BY RowNum
END


END
