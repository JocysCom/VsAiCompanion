using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class AppHelper
	{

		public static void SetText(Label label, string name, int count, int updatable = 0)
		{
			var text = $"{count} {name}" + (count == 1 ? "" : "s");
			if (updatable > 0)
				text += $", {updatable} Updatable";
			ControlsHelper.SetText(label, text);
		}

		public static MacroValues GetMacroValues()
		{
			var mv = new MacroValues();
			if (Global.GetSelection != null)
				mv.Selection = Global.GetSelection();
			if (Global.GetActiveDocument != null)
				mv.Document = Global.GetActiveDocument() ?? new DocItem();
			return mv;
		}

		public static List<string> GetReplaceMacrosSelection()
			=> JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DocItem>(true, nameof(MacroValues.Selection));

		public static List<string> GetMacrosOfStartupProject()
			=> Global.GetMacrosOfStartupProject().Keys.ToList();

		public static List<string> GetReplaceMacrosDocument()
			=> JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DocItem>(true, nameof(MacroValues.Document));
		public static List<string> GetReplaceMacrosDate()
			=> JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DateTime>(true, nameof(MacroValues.Date));

		public static string ReplaceMacros(string s, MacroValues o)
		{
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Date, true, nameof(MacroValues.Date));
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Selection, true, nameof(MacroValues.Selection));
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Document, true, nameof(MacroValues.Document));
			return s;
		}

		public static string GetCodeFromReply(string replyText)
		{
			// Try to match code block pattern (triple backticks) with any language
			var codeBlockPattern = @"(?s)[\`]{3}(?<language>.*?)\r?\n(?<code>.*?)[\`]{3}";
			var codeBlockMatch = Regex.Match(replyText, codeBlockPattern, RegexOptions.Singleline);
			// If code block found, return the code inside it
			if (codeBlockMatch.Success)
				return codeBlockMatch.Groups["code"].Value.Trim();
			// Try to match inline code pattern (single backticks)
			var inlineCodePattern = @"`(?<code>.*?)`";
			var inlineCodeMatch = Regex.Match(replyText, inlineCodePattern);
			// If inline code found, return the code inside it
			if (inlineCodeMatch.Success)
				return inlineCodeMatch.Groups["code"].Value;
			// If no code block or inline code found, return the original reply text
			return replyText;
		}

		public static DocItem GetClipboard()
		{
			var text = Clipboard.GetText();
			var item = new DocItem(text);
			item.Name = nameof(Clipboard);
			item.Language = "CSharp";
			return item;
		}

		public static void SetClipboard(string text)
		{
			Clipboard.SetText(text);
		}

		/// <summary>
		/// Return lis of messages, but do not exceed availableTokens
		/// </summary>
		/// <param name="messages"></param>
		/// <param name="availableTokens"></param>
		public static List<MessageHistoryItem> GetMessages(Dictionary<MessageHistoryItem, int> messages, int availableTokens)
		{
			var orderedMessages = messages.OrderBy(x => x.Key.Date).ToList();
			var result = new List<MessageHistoryItem>();
			if (orderedMessages.Count == 0)
				return result;
			int currentTokens = 0;
			var firstMessage = orderedMessages.FirstOrDefault().Key;
			var firstTokens = orderedMessages.FirstOrDefault().Value;
			// Return if the first message can't be added.
			if (firstTokens > availableTokens)
				return result;
			// Add the first message.
			result.Add(orderedMessages[0].Key);
			currentTokens += orderedMessages[0].Value;
			// Iterate from the end
			for (int i = orderedMessages.Count - 1; i > 0; i--)
			{
				if (currentTokens + orderedMessages[i].Value > availableTokens)
					break;
				result.Add(orderedMessages[i].Key);
				currentTokens += orderedMessages[i].Value;
			}
			// Reverse the result to maintain the original order
			result.Reverse();
			return result;
		}

	}

}
