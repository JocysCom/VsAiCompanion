CREATE FUNCTION [Embedding].[fn_CosineSimilarity]
(
    @binaryVector1 VARBINARY(MAX),
    @binaryVector2 VARBINARY(MAX)
)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        CASE 
            WHEN (SQRT(SUM(v1.[Value] * v1.[Value])) * SQRT(SUM(v2.[Value] * v2.[Value]))) = 0 
            THEN 0
            ELSE SUM(v1.[Value] * v2.[Value]) / (SQRT(SUM(v1.[Value] * v1.[Value])) * SQRT(SUM(v2.[Value] * v2.[Value])))
        END AS CosineSimilarity
    FROM [Embedding].[fn_BinaryToVectors](@binaryVector1) v1
    INNER JOIN [Embedding].[fn_BinaryToVectors](@binaryVector2) v2 ON v1.Id = v2.Id
);