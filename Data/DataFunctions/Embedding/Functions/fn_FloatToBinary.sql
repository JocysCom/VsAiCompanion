CREATE FUNCTION [Embedding].[fn_FloatToBinary]
(
    @value FLOAT
)
RETURNS BINARY(8)
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

   RETURN convert(varbinary(8), @value)
END