CREATE PROCEDURE [Embedding].[sp_getSimilarFileParts]
    @groupName NVARCHAR(64),
    @groupFlag BIGINT,
    @vectors VARBINARY(MAX),
    @skip INT,
    @take INT,
	@useAssembly bit = 0
AS
BEGIN
    SET NOCOUNT ON;

	IF @useAssembly = 1
	BEGIN

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
				(@groupName = '' OR @groupName = fp.GroupName) AND
				(@groupFlag = 0 OR (@groupFlag & fp.GroupFlag) > 0)
		)
		SELECT *
		FROM RankedFiles
		WHERE RowNum > @skip AND RowNum <= @skip + @take
		ORDER BY RowNum
	END
	ELSE
	BEGIN

		;WITH RankedFiles AS
		(
			SELECT
				fp.*,
				RowNum = ROW_NUMBER() OVER (ORDER BY cs.CosineSimilarity DESC)
			FROM
				[FilePart] AS fp WITH (NOLOCK)
			INNER JOIN
				[File] AS f WITH (NOLOCK) ON f.Id = fp.FileId
			CROSS APPLY 
				(SELECT CosineSimilarity FROM [Embedding].[fn_CosineSimilarity](@vectors, fp.[Embedding])) cs
			WHERE
				(@groupName = '' OR @groupName = fp.GroupName) AND
				(@groupFlag = 0 OR (@groupFlag & fp.GroupFlag) > 0)
		)
		SELECT *
		FROM RankedFiles
		WHERE RowNum > @skip AND RowNum <= @skip + @take
		ORDER BY RowNum;

	END
END
