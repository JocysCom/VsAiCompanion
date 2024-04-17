using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// HTML Helper.
	/// </summary>
	public static class HtmlHelper
	{
		/// <summary>
		/// Read HTML as plain text.
		/// </summary>
		public static string ReadHtmlAsPlainText(string html)
		{
			var doc = new HtmlAgilityPack.HtmlDocument();
			doc.LoadHtml(html);
			var sb = new StringBuilder();
			// Select all text nodes that are not children of script or style elements
			var textNodes = doc.DocumentNode.SelectNodes("//text()[normalize-space(.) != '' and not(parent::script or parent::style)]");
			if (textNodes != null)
			{
				foreach (HtmlNode node in textNodes)
				{
					// Exclude text nodes that may not be visible
					if (!IsLikelyVisible(node))
						continue;
					string text = HtmlEntity.DeEntitize(node.InnerText);
					text = NormalizeWhitespace(text);
					sb.AppendLine(text);
				}
			}
			return sb.ToString().Trim();
		}

		private static bool IsLikelyVisible(HtmlNode node)
		{
			// Simple check to skip nodes that are likely not visible
			// This may include checking for class names or inline styles commonly used to hide elements
			// However, without rendering the page, we can't handle external CSS or JS-based hiding
			var parent = node.ParentNode;
			while (parent != null)
			{
				if (parent.Attributes["style"] != null)
				{
					var styleValue = parent.Attributes["style"].Value;
					if (styleValue.Contains("display: none") || styleValue.Contains("visibility: hidden"))
						return false;
				}
				if (parent.Attributes["class"] != null)
				{
					// Example: Skipping elements with a class indicating they might be hidden
					// This highly depends on the specific HTML/CSS in use
					var classValue = parent.Attributes["class"].Value;
					if (classValue.Contains("hidden") || classValue.Contains("d-none"))
						return false;
				}
				parent = parent.ParentNode;
			}
			return true;
		}

		//This will normalize whitespaces to a single space and remove leading/trailing spaces.
		private static string NormalizeWhitespace(string input)
		{
			return Regex.Replace(input, @"\s+", " ").Trim();
		}

	}
}
