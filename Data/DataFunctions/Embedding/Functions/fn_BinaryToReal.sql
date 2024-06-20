CREATE FUNCTION [Embedding].[fn_BinaryToReal]
(
    @value BINARY(4),
	@littleEndian BIT -- 0 - SQL Server big-endian byte order, 1 - reverse byte order (C# little-endian).
)
RETURNS REAL
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

    DECLARE @Int32 INT = CAST(@value AS INT)
	
    -- Reverse byte order (C# little-endian).
	IF @littleEndian = 1
	BEGIN
		SET @Int32 = 
			 (((@Int32 & 0x7F000000) / 0x1000000)
			+ ((@Int32 & 0x00FF0000) / 0x100)
			+ ((@Int32 & 0x0000FF00) * 0x100)
			+ ((@Int32 & 0x0000007F) * 0x1000000))
			-- Restore first bit of first and last byte.
			+ (0x80000000 * ((@Int32 & 0x80) / 0x80))
			+ (0x80 * ((@Int32 & 0x80000000) / 0x80000000))	
		SET @value = CAST(@Int32 AS BINARY(4))
	END

    -- IEEE 754 Single Precision binary to float
    -- Layout:
    --   32 bits total
    --   1 bit sign
    --   8 bits exponent (excess 127)
    --   23 bits fractional mantissa  (with implicit leading 1) = 1.xxxxx
    -- Does not support IEEE NaN, +Inf, or -Inf values. 

    IF (@value IS NULL OR DATALENGTH(@value) <> 4) RETURN NULL
    IF (@value = 0x00000000) RETURN 0
    IF (@value = 0x80000000) RETURN -0e0 -- IEEE Negative 0

    DECLARE @One REAL = 1
    DECLARE @Two REAL = 2
    DECLARE @Mantissa REAL = @One + (@Int32 & 0x007FFFFF) * POWER(@Two, -23)
    DECLARE @Exponent INT = (@Int32 & 0x7f800000) / 0x00800000 - 127

    IF (@Exponent = 128) RETURN NULL -- Unsupported special: Inf, -Inf, NaN

    RETURN SIGN(@Int32) * @Mantissa * POWER(@Two, @Exponent)

END