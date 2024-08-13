namespace Embeddings.Embedding
{

	public partial class DataInfo
	{

		/// <summary>Name of the group to which the file belongs.</summary>
		public string GroupName { get; set; }

		/// <summary>A bitwise operation will be used to include groups by their group flag.</summary>
		public long GroupFlag { get; set; }

		/// <summary>Name of the flag</summary>
		public string GroupFlagName { get; set; }

		public long Count { get; set; }
	}
}
