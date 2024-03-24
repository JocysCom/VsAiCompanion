using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Embeddings.Model
{

	/// <summary>File Embedding</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	[Table("FileEmbedding", Schema = "Embedding")]
	public partial class FilePart
	{

		/// <summary>Id (unique primary key).</summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		/// <summary>File text part used for embedding</summary>
		public string PartText { get; set; }

		/// <summary>Part index</summary>
		public int PartIndex { get; set; }

		/// <summary>Number of parts</summary>
		public int PartCount { get; set; }

		/// <summary>Id to original file info</summary>
		public long FileId { get; set; }

		/// <summary>Embedding vectors</summary>
		public byte[] Embedding { get; set; }

		/// <summary>Number of vectors inside embedding: 256, 512, 1024, 2048...</summary>
		public int EmbeddingSize { get; set; }

		/// <summary>AI Model used for embedding.</summary>
		[StringLength(100)]
		public string EmbeddingModel { get; set; }

		#region Clone and Copy Methods

		/// <summary>Clone to new object.</summary>
		public FilePart Clone(bool copyKey = false)
			=> Copy(this, new FilePart(), copyKey);

		/// <summary>Copy to existing object.</summary>
		public FilePart Copy(FilePart target, bool copyKey = false)
			=> Copy(this, target, copyKey);

		/// <summary>Copy to existing object.</summary>
		public static FilePart Copy(FilePart source, FilePart target, bool copyKey = false)
		{
			if (copyKey)
				target.Id = source.Id;
			target.PartText = source.PartText;
			target.PartIndex = source.PartIndex;
			target.PartCount = source.PartCount;
			target.FileId = source.FileId;
			target.Embedding = source.Embedding;
			target.EmbeddingSize = source.EmbeddingSize;
			target.EmbeddingModel = source.EmbeddingModel;
			return target;
		}

		#endregion

	}
}
