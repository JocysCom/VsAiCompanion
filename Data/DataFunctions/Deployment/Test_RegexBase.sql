-- Returns: 1
SELECT dbo.RegexIsMatch('The quick brown fox jumps over the lazy dog', 'f[a-z]x')
	
-- Returns: The quick brown cat jumps over the lazy cat.
SELECT dbo.RegexReplace('The quick brown fox jumps over the lazy dog.', '(?<who>fox|dog)', 'cat')

-- Returns: fox
SELECT dbo.RegexReplace('The quick brown fox jumps over the lazy dog.', '(.*brown\s)(?<who>\w+)(.*)', '${who}')