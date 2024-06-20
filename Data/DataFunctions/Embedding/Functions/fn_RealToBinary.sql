CREATE FUNCTION [Embedding].[fn_RealToBinary]
(
    @value REAL,
    @littleEndian BIT  -- 0 - SQL Server big-endian byte order, 1 - reverse byte order (C# little-endian).
)
RETURNS TABLE
AS
RETURN
(

	/*
	DECLARE @b1 binary(4), @b2 binary(4), @b3 binary(4)

	SELECT @b1 = BinaryValue FROM [Embedding].[fn_RealToBinary](1.23, 1) AS fn1;
	SELECT @b2 = BinaryValue FROM [Embedding].[fn_RealToBinary](4.56, 1) AS fn2;
	SELECT @b3 = BinaryValue FROM [Embedding].[fn_RealToBinary](7.89, 1) AS fn3;
	SELECT  @b1 as b1, @b2 AS b2, @b3 as b3

	DECLARE
		@r1 real = [Embedding].[fn_BinaryToReal](@b1, 1),
		@r2 real = [Embedding].[fn_BinaryToReal](@b2, 1),
		@r3 real = [Embedding].[fn_BinaryToReal](@b3, 1)
	SELECT  @r1 as r1, @r2 AS r2, @r3 as r3
	*/

    SELECT
        CASE
            WHEN @littleEndian = 1 THEN
                -- Reverse byte order (C# little-endian).
                CAST(
                    SUBSTRING(CAST(@value AS BINARY(4)), 4, 1) + 
                    SUBSTRING(CAST(@value AS BINARY(4)), 3, 1) + 
                    SUBSTRING(CAST(@value AS BINARY(4)), 2, 1) + 
                    SUBSTRING(CAST(@value AS BINARY(4)), 1, 1) AS BINARY(4)
                )
            ELSE
                CAST(@value AS BINARY(4))
        END AS [Value]
);