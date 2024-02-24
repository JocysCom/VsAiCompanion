using System.Collections.Generic;


namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{

	public static class TokenHelper
	{

		public static IEnumerable<Token> ParseTokens(string text)
		{
			var keywords = new HashSet<string> { "AI", "WPF", "ListView" };
			var words = text.Split(' ');

			foreach (var word in words)
			{
				if (keywords.Contains(word))
				{
					yield return new Token { Type = TokenType.Keyword, Text = word };
				}
				else
				{
					yield return new Token { Type = TokenType.Normal, Text = word };
				}
			}
		}
	}

}
