CREATE PROCEDURE [Embedding].[sp_getSimilarFileParts]
    @groupName NVARCHAR(64),
    @groupFlag BIGINT,
    @vectors VARBINARY(MAX),
    @skip INT,
    @take INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Use a table variable for ConvertedVectors
    DECLARE @ConvertedVectors TABLE (
        RowNum INT PRIMARY KEY,
        Value1 REAL
    );

    -- Pre-convert @vectors
    INSERT INTO @ConvertedVectors (RowNum, Value1)
    SELECT 
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS RowNum,
        CAST(
			-- IEEE 754 Single Precision binary to float
			-- Layout:
			--   32 bits total
			--   1 bit sign
			--   8 bits exponent (excess 127)
			--   23 bits fractional mantissa  (with implicit leading 1) = 1.xxxxx
			SIGN(Int32) * 
			(POWER(CAST(2.0 AS REAL), -23) * (Int32 & 0x007FFFFF) + CAST(1.0 AS REAL)) * 
			(POWER(CAST(2.0 AS REAL), ((Int32 & 0x7F800000) / 0x00800000 - 127)))
			AS REAL
        ) AS Value
    FROM (
        SELECT CAST(
			-- Big-endian byte order (SQL Server)
			-- SUBSTRING(@vectors, (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 1, 4)
			-- Little-endian byte order (C#)
            SUBSTRING(@vectors, (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 4, 1) +
            SUBSTRING(@vectors, (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 3, 1) +
            SUBSTRING(@vectors, (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 2, 1) +
            SUBSTRING(@vectors, (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 1, 1)
			AS INT
        ) AS Int32
        FROM sys.objects
        WHERE object_id <= DATALENGTH(@vectors) / 4
    ) v;

    DECLARE @VectorLength REAL;
    SELECT @VectorLength = SQRT(SUM(CAST(Value1 AS FLOAT) * CAST(Value1 AS FLOAT)))
    FROM @ConvertedVectors;

    -- Main query with optimized joins and filtering
    WITH RankedFiles AS
    (
        SELECT
            fp.Id,
            fp.GroupName,
            fp.GroupFlag,
            fp.[Embedding],
            ROW_NUMBER() OVER (ORDER BY 
                CASE 
                    WHEN (@VectorLength * SQRT(SUM(CAST(v.Value2 AS FLOAT) * CAST(v.Value2 AS FLOAT)))) = 0 
                    THEN 0
                    ELSE SUM(cv.Value1 * v.Value2) / (@VectorLength * SQRT(SUM(CAST(v.Value2 AS FLOAT) * CAST(v.Value2 AS FLOAT))))
                END DESC
            ) AS RowNum
        FROM
            [FilePart] AS fp WITH (NOLOCK)
        CROSS APPLY (
            SELECT
                ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS RowNum,
                CAST(
					-- IEEE 754 Single Precision binary to float
					-- Layout:
					--   32 bits total
					--   1 bit sign
					--   8 bits exponent (excess 127)
					--   23 bits fractional mantissa  (with implicit leading 1) = 1.xxxxx
					SIGN(Int32) *
					(POWER(CAST(2.0 AS REAL), -23) * (Int32 & 0x007FFFFF) + CAST(1.0 AS REAL)) * 
					(POWER(CAST(2.0 AS REAL), ((Int32 & 0x7F800000) / 0x00800000 - 127)))
					AS REAL
                ) AS Value2
            FROM (
                SELECT 
                    CAST(
						-- Big-endian byte order (SQL Server)
						--SUBSTRING(fp.[Embedding], (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 1, 4)
						-- Little-endian byte order (C#)
						SUBSTRING(fp.[Embedding], (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 4, 1) +
                        SUBSTRING(fp.[Embedding], (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 3, 1) +
                        SUBSTRING(fp.[Embedding], (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 2, 1) +
                        SUBSTRING(fp.[Embedding], (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 4 + 1, 1)
						AS INT
                    ) AS Int32
                FROM sys.objects
                WHERE object_id <= DATALENGTH(fp.[Embedding]) / 4
            ) v2
        ) v
        JOIN @ConvertedVectors cv ON cv.RowNum = v.RowNum
        WHERE
            (@groupName = '' OR @groupName = fp.GroupName) AND
            (@groupFlag = 0 OR (@groupFlag & fp.GroupFlag) > 0)
        GROUP BY fp.Id, fp.GroupName, fp.GroupFlag, fp.[Embedding]
    )
    SELECT fp2.*
    FROM RankedFiles rf
	-- Join full table results at the end.
    JOIN [FilePart] AS fp2 WITH (NOLOCK) ON fp2.Id = rf.Id
    WHERE rf.RowNum > @skip AND rf.RowNum <= @skip + @take
    ORDER BY rf.RowNum;
END
