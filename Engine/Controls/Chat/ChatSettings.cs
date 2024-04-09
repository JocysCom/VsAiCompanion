using System;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public class ChatSettings
	{
		/// <summary>
		/// By default, "null" indicates that the scroll is at the bottom.
		/// </summary>
		[DefaultValue(null)]
		public int? ScrollPosition { get; set; }

		public bool ShouldSerializeScrollPosition() => ScrollPosition != null;

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
