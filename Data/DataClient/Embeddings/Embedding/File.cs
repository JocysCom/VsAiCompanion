using System;
using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Embeddings.Model
{

	/// <summary>File</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	[Table("File", Schema = "Embedding")]
	public partial class File
	{

		/// <summary>Unique file id</summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		/// <summary>File name</summary>
		[StringLength(512)]
		public string Name { get; set; }

		/// <summary>File location</summary>
		public string Url { get; set; }

		/// <summary>File size</summary>
		public long Size { get; set; }

		/// <summary>Size of extracted text for embedding</summary>
		public long TextSize { get; set; }

		/// <summary>File modify date</summary>
		public DateTime Modified { get; set; }

		/// <summary>File created date</summary>
		public DateTime Created { get; set; }

		#region Clone and Copy Methods

		/// <summary>Clone to new object.</summary>
		public File Clone(bool copyKey = false)
			=> Copy(this, new File(), copyKey);

		/// <summary>Copy to existing object.</summary>
		public File Copy(File target, bool copyKey = false)
			=> Copy(this, target, copyKey);

		/// <summary>Copy to existing object.</summary>
		public static File Copy(File source, File target, bool copyKey = false)
		{
			if (copyKey)
				target.Id = source.Id;
			target.Name = source.Name;
			target.Url = source.Url;
			target.Size = source.Size;
			target.TextSize = source.TextSize;
			target.Modified = source.Modified;
			target.Created = source.Created;
			return target;
		}

		#endregion

	}
}
