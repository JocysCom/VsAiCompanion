-- Returns: 1
SELECT Embedding.RegexIsMatch('The quick brown fox jumps over the lazy dog', 'f[a-z]x')
	
-- Returns: The quick brown cat jumps over the lazy cat.
SELECT Embedding.RegexReplace('The quick brown fox jumps over the lazy dog.', '(?<who>fox|dog)', 'cat')

-- Returns: fox
SELECT Embedding.RegexReplace('The quick brown fox jumps over the lazy dog.', '(.*brown\s)(?<who>\w+)(.*)', '${who}')
