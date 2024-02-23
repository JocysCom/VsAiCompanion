using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	public class VersionNode
	{
		[JsonPropertyName("@id")]
		public string Id { get; set; }

		[JsonPropertyName("count")]
		public int? Count { get; set; }

		[JsonPropertyName("items")]
		public List<VersionNode> Items { get; set; }

		[JsonPropertyName("version")]
		public string Version { get; set; }

		[JsonPropertyName("published")]
		public DateTime? Published { get; set; }

		[JsonPropertyName("packageContent")]
		public string PackageContent { get; set; }
	}
}
