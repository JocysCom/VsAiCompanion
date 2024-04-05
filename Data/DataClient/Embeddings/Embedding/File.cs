using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Embeddings.Embedding
{

	/// <summary>File</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	[Table("File", Schema = "Embedding")]
	public partial class File
	{

		/// <summary>Unique file id.</summary>
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		/// <summary>Name of the group to which the file belongs.</summary>
		[Required]
		[StringLength(128)]
		public string GroupName { get; set; }

		/// <summary>A bitwise operation will be used to include groups by their group flag.</summary>
		public long GroupFlag { get; set; }

		/// <summary>File name.</summary>
		[Required]
		[StringLength(512)]
		public string Name { get; set; }

		/// <summary>URL specifying the file's location.</summary>
		[Required]
		[StringLength(4096)]
		public string Url { get; set; }

		/// <summary>File size in bytes.</summary>
		public long Size { get; set; }

		/// <summary>Specifies the hash algorithm used to generate the hash value: MD2, MD4, MD5, SHA, SHA1, SHA2_256, and SHA2_512.</summary>
		[Required]
		[StringLength(20)]
		public string HashType { get; set; }

		/// <summary>The SHA-256 hash of the file's bytes.</summary>
		public byte[] Hash { get; set; }

		/// <summary>Processing state of the record.</summary>
		public int State { get; set; }

		/// <summary>Indicates if the record is active and included in searches.</summary>
		public bool IsEnabled { get; set; }

		/// <summary>UTC date and time when the file was last modified.</summary>
		public DateTime Modified { get; set; }

		/// <summary>UTC date and time when the file was created.</summary>
		public DateTime Created { get; set; }

		#region Foreign Key Lists

		/// <summary>Collection of File Parts. Foreign key relationship.</summary>
		[InverseProperty(nameof(FilePart.File))]
		public virtual ICollection<FilePart> FileParts { get; }

		#endregion

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
			target.GroupName = source.GroupName;
			target.GroupFlag = source.GroupFlag;
			target.Name = source.Name;
			target.Url = source.Url;
			target.Size = source.Size;
			target.HashType = source.HashType;
			target.Hash = source.Hash;
			target.State = source.State;
			target.IsEnabled = source.IsEnabled;
			target.Modified = source.Modified;
			target.Created = source.Created;
			return target;
		}

		#endregion

	}
}
