using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public class ChatSettings
	{
		[DefaultValue(0)]
		public int ScrollPosition { get; set; }

		// Override the Equals method.
		public override bool Equals(object o)
		{
			if (o == null || GetType() != o.GetType())
				return false;
			var other = (ChatSettings)o;
			// Return true if all properties equal.
			return ScrollPosition == other.ScrollPosition;
		}

		// Override GetHashCode method.
		public override int GetHashCode()
		{
			return HashCode.Combine(ScrollPosition);
		}

	}
}
