using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#if NETFRAMEWORK
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
#else
using Microsoft.EntityFrameworkCore;
#endif

namespace Embeddings
{
	/// <summary>Embeddings Context</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
	public partial class EmbeddingsContext : DbContext
	{


		/// <summary>Initialize model</summary>
		public EmbeddingsContext() : base() { }

#if NETFRAMEWORK
#else
		/*

		// Set connection string for EmbeddingsContext before calling "var app = builder.Build(); " in Program.cs
		// Requires: using Microsoft.EntityFrameworkCore;
		builder.Services.AddDbContext<EmbeddingsContext>(x =>
		{
			var connectionString = builder.Configuration.GetConnectionString(nameof(EmbeddingsContext) + "Connection");
			x.UseSqlServer(connectionString);
		});
		var db = EmbeddingsContext.Create(connectionString);

		// How to initialize context and retrieve options.
		var options = app.Services.GetService<DbContextOptions<EmbeddingsContext>>();
		var db = new EmbeddingsContext(options)();

		var db = app.Services.GetService<EmbeddingsContext>();

		*/

		/// <summary>Initialize model with options.</summary>
		public EmbeddingsContext(DbContextOptions<EmbeddingsContext> options) : base(options) { }

		/// <summary>Create context with connection string.</summary>
		public static EmbeddingsContext Create(string connectionString) {
			var optionsBuilder = new DbContextOptionsBuilder<EmbeddingsContext>();
			optionsBuilder.UseSqlServer(connectionString);
			return new EmbeddingsContext(optionsBuilder.Options);
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			modelBuilder.Ignore<Type>();
			modelBuilder.Ignore<System.Reflection.CustomAttributeData>();
			base.OnModelCreating(modelBuilder);
		}

#endif


		/// <summary>File</summary>
		public virtual DbSet<Embedding.File> Files { get; set; }

		/// <summary>File Part</summary>
		public virtual DbSet<Embedding.FilePart> FileParts { get; set; }

	}
}
