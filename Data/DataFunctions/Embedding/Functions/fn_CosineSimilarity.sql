CREATE FUNCTION [Embedding].[fn_CosineSimilarity]
(
    @binaryVector1 VARBINARY(MAX),
    @binaryVector2 VARBINARY(MAX)
)
RETURNS REAL
AS
BEGIN
    DECLARE @vectors1 TABLE (RowIndex INT, Value REAL);
    DECLARE @vectors2 TABLE (RowIndex INT, Value REAL);
    
    -- Convert binary data to vector tables with row indices
    WITH Vector1CTE AS (
        SELECT 
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowIndex,
            [Value]
        FROM [Embedding].[fn_BinaryToVectors](@binaryVector1)
    )
    INSERT INTO @vectors1
    SELECT RowIndex, Value FROM Vector1CTE;

    WITH Vector2CTE AS (
        SELECT 
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowIndex,
            [Value]
        FROM [Embedding].[fn_BinaryToVectors](@binaryVector2)
    )
    INSERT INTO @vectors2
    SELECT RowIndex, [Value] FROM Vector2CTE;

    -- Dot product and norms
    DECLARE @dotProduct REAL = 0;
    DECLARE @norm1 REAL = 0;
    DECLARE @norm2 REAL = 0;

    -- Perform the calculations
    SELECT
        @dotProduct = @dotProduct + v1.[Value] * v2.[Value],
        @norm1 = @norm1 + v1.[Value] * v1.[Value],
        @norm2 = @norm2 + v2.[Value] * v2.[Value]
    FROM @vectors1 v1
    INNER JOIN @vectors2 v2 ON v1.RowIndex = v2.RowIndex;

    -- Return cosine similarity
    RETURN @dotProduct / (SQRT(@norm1) * SQRT(@norm2));
END;