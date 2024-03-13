using System.Collections.Generic;

namespace JocysCom.Controls.UpdateControl.GitHub
{
	public class @release
	{
		public string url { get; set; }
		public string assets_url { get; set; }
		public string upload_url { get; set; }
		public string html_url { get; set; }
		public long id { get; set; }
		public author author { get; set; }
		public string node_id { get; set; }
		public string tag_name { get; set; }
		public string target_commitish { get; set; }
		public string name { get; set; }
		public bool draft { get; set; }
		public bool prerelease { get; set; }
		public string created_at { get; set; }
		public string published_at { get; set; }
		public List<asset> assets { get; set; }
		public string tarball_url { get; set; }
		public string zipball_url { get; set; }
		public string body { get; set; }
	}
}
