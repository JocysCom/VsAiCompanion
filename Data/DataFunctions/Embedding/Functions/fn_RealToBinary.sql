CREATE FUNCTION [Embedding].[fn_RealToBinary]
(
    @value REAL
)
RETURNS VARBINARY(4)
AS
BEGIN

	/*
	DECLARE
		@b1 binary(4) = [Embedding].[fn_RealToBinary](1.23),
		@b2 binary(4) = [Embedding].[fn_RealToBinary](4.56),
		@b3 binary(4) = [Embedding].[fn_RealToBinary](7.89)
	 SELECT  @b1 as b1, @b2 AS b2, @b3 as b3

	DECLARE
		@r1 real = [Embedding].[fn_BinaryToReal](@b1),
		@r2 real = [Embedding].[fn_BinaryToReal](@b2),
		@r3 real = [Embedding].[fn_BinaryToReal](@b3)
	SELECT  @r1 as r1, @r2 AS r2, @r3 as r3
	*/

   RETURN convert(varbinary(4), @value)
END