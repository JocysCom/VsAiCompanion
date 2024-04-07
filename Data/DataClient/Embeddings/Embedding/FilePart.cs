using System;
using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Embeddings.Embedding
{

	/// <summary>File Part</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	[Table("FilePart", Schema = "Embedding")]
	public partial class FilePart
	{

		/// <summary>Unique identifier of the file part.</summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		/// <summary>Name of the group to which the file belongs.</summary>
		[Required]
		[StringLength(128)]
		public string GroupName { get; set; }

		/// <summary>A bitwise operation will be used to include groups by their group flag.</summary>
		public long GroupFlag { get; set; }

		/// <summary>Unique identifier of the associated file.</summary>
		public long FileId { get; set; }

		/// <summary>Index of this part relative to other parts of the same file.</summary>
		public int Index { get; set; }

		/// <summary>Total number of parts into which the file is divided.</summary>
		public int Count { get; set; }

		/// <summary>Specifies the hash algorithm used to generate the hash value: MD2, MD4, MD5, SHA, SHA1, SHA2_256, and SHA2_512.</summary>
		[Required]
		[StringLength(20)]
		public string HashType { get; set; }

		/// <summary>The SHA-256 hash of the 'PartText' bytes, represented as a string in UTF-16 encoding.</summary>
		public byte[] Hash { get; set; }

		/// <summary>Processing state of the record.</summary>
		public int State { get; set; }

		/// <summary>File text part used for embedding</summary>
		[Required]
		public string Text { get; set; }

		/// <summary>File Part size in tokens.</summary>
		public long TextTokens { get; set; }

		/// <summary>AI Model used for embedding.</summary>
		[Required]
		[StringLength(100)]
		public string EmbeddingModel { get; set; }

		/// <summary>The number of vectors contained within the embedding, e.g., 256, 512, 1024, 2048.</summary>
		public int EmbeddingSize { get; set; }

		/// <summary>Binary representation of embedding vectors generated for this file part.</summary>
		public byte[] Embedding { get; set; }

		/// <summary>Indicates if the record is active and considered in searches.</summary>
		public bool IsEnabled { get; set; }

		/// <summary>UTC date and time when the part was created.</summary>
		public DateTime Created { get; set; }

		/// <summary>UTC date and time when the part was last modified.</summary>
		public DateTime Modified { get; set; }

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
			target.GroupName = source.GroupName;
			target.GroupFlag = source.GroupFlag;
			target.FileId = source.FileId;
			target.Index = source.Index;
			target.Count = source.Count;
			target.HashType = source.HashType;
			target.Hash = source.Hash;
			target.State = source.State;
			target.Text = source.Text;
			target.TextTokens = source.TextTokens;
			target.EmbeddingModel = source.EmbeddingModel;
			target.EmbeddingSize = source.EmbeddingSize;
			target.Embedding = source.Embedding;
			target.IsEnabled = source.IsEnabled;
			target.Created = source.Created;
			target.Modified = source.Modified;
			return target;
		}

		#endregion

	}
}
