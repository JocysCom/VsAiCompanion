CREATE FUNCTION [Embedding].[fn_RealToBinary]
(
    @value REAL,
	@littleEndian BIT -- 0 - SQL Server big-endian byte order, 1 - reverse byte order (C# little-endian).
)
RETURNS VARBINARY(4)
AS
BEGIN

	/*
	DECLARE
		@b1 binary(4) = [Embedding].[fn_RealToBinary](1.23, 1),
		@b2 binary(4) = [Embedding].[fn_RealToBinary](4.56, 1),
		@b3 binary(4) = [Embedding].[fn_RealToBinary](7.89, 1)
	 SELECT  @b1 as b1, @b2 AS b2, @b3 as b3

	DECLARE
		@r1 real = [Embedding].[fn_BinaryToReal](@b1, 1),
		@r2 real = [Embedding].[fn_BinaryToReal](@b2, 1),
		@r3 real = [Embedding].[fn_BinaryToReal](@b3, 1)
	SELECT  @r1 as r1, @r2 AS r2, @r3 as r3
	*/

    IF @littleEndian = 0
	    RETURN CAST(@value AS BINARY(4))

    DECLARE	@Int32 INT = CAST(CAST(@value AS BINARY(4)) AS INT)
	
	SET @Int32 = 
		 (((@Int32 & 0x7F000000) / 0x1000000)
		+ ((@Int32 & 0x00FF0000) / 0x100)
		+ ((@Int32 & 0x0000FF00) * 0x100)
		+ ((@Int32 & 0x0000007F) * 0x1000000))
		-- Restore first bit of first and last byte.
		+ (0x80000000 * ((@Int32 & 0x80) / 0x80))
		+ (0x80 * ((@Int32 & 0x80000000) / 0x80000000))
	
	RETURN CAST(@Int32 AS BINARY(4))

END
