CREATE FUNCTION [Embedding].[fn_BinaryToFloat]
(
    @value BINARY(8)
)
RETURNS FLOAT
AS
BEGIN

	/*
	DECLARE
		@b1 binary(8) = [Embedding].[fn_FloatToBinary](1.23),
		@b2 binary(8) = [Embedding].[fn_FloatToBinary](4.56),
		@b3 binary(8) = [Embedding].[fn_FloatToBinary](7.89)
	 SELECT  @b1 as b1, @b2 AS b2, @b3 as b3

	DECLARE
		@f1 real = [Embedding].[fn_BinaryToFloat](@b1),
		@f2 real = [Embedding].[fn_BinaryToFloat](@b2),
		@f3 real = [Embedding].[fn_BinaryToFloat](@b3)
	SELECT  @f1 as f1, @f2 AS f2, @f3 as f3
	*/

    -- IEEE 754 Double Precision binary to float
    -- Layout:
    --   64 bits total
    --   1 bit sign
    --   11 bits exponent (excess 1023)
    --   52 bits fractional mantissa  (with implicit leading 1) = 1.xxxxx
    -- Does not support IEEE NaN, +Inf, or -Inf values. 

    IF (@value IS NULL OR DATALENGTH(@value) <> 8) RETURN NULL
    IF (@value = 0x0000000000000000) RETURN 0
    IF (@value = 0x8000000000000000) RETURN -0e0 -- IEEE Negative 0

    DECLARE @Int64 BIGINT = CAST(@value AS BIGINT)
    DECLARE @One FLOAT = 1
    DECLARE @Two FLOAT = 2
    DECLARE @Mantissa FLOAT = @One + (@Int64 & 0x000FFFFFFFFFFFFF) * POWER(@Two, -52)
    DECLARE @Exponent INT = (@Int64 & 0x7ff0000000000000) / 0x0010000000000000 - 1023

    IF (@Exponent = 1024) RETURN NULL -- Unsupported special: Inf, -Inf, NaN
  
    RETURN SIGN(@Int64) * @Mantissa * POWER(@Two, @Exponent)
END