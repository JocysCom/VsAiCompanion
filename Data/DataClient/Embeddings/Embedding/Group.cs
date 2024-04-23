using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Embeddings.Embedding
{

	/// <summary>File</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	[Table("Group", Schema = "Embedding")]
	public partial class Group
	{

		/// <summary>Unique id.</summary>
		/// <summary>Unique identifier of the file part.</summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		/// <summary>GroupName.</summary>
		[Required]
		[StringLength(128)]
		public string Name { get; set; }

		/// <summary>FlagValue.</summary>
		[Required]
		public long Flag { get; set; }

		/// <summary>Flag name</summary>
		[Required]
		[StringLength(128)]
		public string FlagName { get; set; }

		/// <summary>Time stamp in UTC as 100-nanosecond intervals from 0001-01-01 00:00:00Z.</summary>
		public long Timestamp { get; set; }

	}
}
