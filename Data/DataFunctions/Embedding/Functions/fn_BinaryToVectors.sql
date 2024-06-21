CREATE FUNCTION [Embedding].[fn_BinaryToVectors]
(
    @binary VARBINARY(MAX)
)
RETURNS @vectors TABLE
(
	Id INT PRIMARY KEY IDENTITY(1,1),
    [Value] REAL
)
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

    DECLARE @index INT = 1

    WHILE @index <= DATALENGTH(@binary)
    BEGIN
        INSERT INTO @vectors ([Value])
        VALUES (Embedding.fn_BinaryToReal(SUBSTRING(@binary, @index, 4), 1))
        SET @index = @index + 4
    END

    RETURN
END