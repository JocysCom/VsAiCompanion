using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class RisenHelper
	{
		public static string ConstructPrompt(string role, string instructions, string steps, string endGoal, string narrowing)
		{
			role = (role ?? "").Trim();
			instructions = (instructions ?? "").Trim();
			steps = (steps ?? "").Trim();
			endGoal = (endGoal ?? "").Trim();
			narrowing = (narrowing ?? "").Trim();
			// If all empty then return empty.
			var values = new string[] { role, instructions, steps, endGoal, narrowing };
			if (values.All(x => string.IsNullOrWhiteSpace(x)))
				return "";
			var promptTemplate = Resources.MainResources.main_RISEN_Prompt_Template;
			// Create a dictionary for placeholders and values
			var placeholders = new Dictionary<string, string>
			{
				{ "{Role}", !string.IsNullOrWhiteSpace(role) ? role : "Assistant" },
				{ "{Instructions}", instructions },
				{ "{Steps}", steps },
				{ "{EndGoal}", endGoal },
				{ "{Narrowing}", narrowing }
			};
			var prompt = promptTemplate;
			foreach (var placeholder in placeholders)
				prompt = prompt.Replace(placeholder.Key, placeholder.Value);
			return prompt;
		}

		public static (string Role, string Instructions, string Steps, string EndGoal, string Narrowing)? ExtractProperties(string message)
		{
			// Split the message into lines
			var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			// Regular expression to match lines starting with optional spaces followed by ###
			var headerRegex = new Regex(@"^\s*###");
			// Find indices of lines that are section headers
			var sectionIndices = new List<int>();
			for (var i = 0; i < lines.Length; i++)
			{
				if (headerRegex.IsMatch(lines[i]))
					sectionIndices.Add(i);
			}
			// Ensure there are at least 5 sections
			if (sectionIndices.Count < 5)
			{
				System.Diagnostics.Debug.WriteLine("Message does not contain enough sections.");
				return null;
			}
			// Extract content for each section
			var sections = new List<string>();
			for (var i = 0; i < sectionIndices.Count; i++)
			{
				var startLine = sectionIndices[i] + 1; // Content starts after header line
				var endLine = (i + 1 < sectionIndices.Count) ? sectionIndices[i + 1] : lines.Length;
				var sectionLines = lines.Skip(startLine).Take(endLine - startLine);
				var sectionContent = string.Join(Environment.NewLine, sectionLines).Trim();
				sections.Add(sectionContent);
			}
			// Map the sections to properties based on their order
			var role = sections[0];
			var instructions = sections[1];
			var steps = sections[2];
			var endGoal = sections[3];
			var narrowing = sections[4];
			return (role, instructions, steps, endGoal, narrowing);
		}
	}
}
