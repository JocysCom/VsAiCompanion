CREATE FUNCTION [Embedding].[fn_VectorsToBinary]
(
    @vectors [Embedding].[RealTable] READONLY
)
RETURNS VARBINARY(MAX)
AS
BEGIN

	/*
	DECLARE @realTable AS [Embedding].[RealTable]

	INSERT INTO @realTable ([Value]) VALUES (1.23), (4.56), (7.89)

	DECLARE @binary VARBINARY(MAX) = [Embedding].[fn_VectorsToBinary](@realTable)

	SELECT @binary AS binaryData

	DECLARE @vectors AS TABLE ([Value] real)

	INSERT INTO @vectors ([Value])
	SELECT [Value] FROM [Embedding].[fn_BinaryToVectors](@binary)

	SELECT [Value] AS Vector FROM @vectors
	*/

    DECLARE @result VARBINARY(MAX) = 0x

    DECLARE @value FLOAT
    DECLARE vector_cursor CURSOR FOR SELECT [Value] FROM @vectors
    OPEN vector_cursor
    FETCH NEXT FROM vector_cursor INTO @value

    WHILE @@FETCH_STATUS = 0
    BEGIN

		SELECT @result = @result + fn.[Value]
		FROM Embedding.fn_RealToBinary(@value, 1) AS fn;

        FETCH NEXT FROM vector_cursor INTO @value
    END

    CLOSE vector_cursor
    DEALLOCATE vector_cursor

    RETURN @result
END