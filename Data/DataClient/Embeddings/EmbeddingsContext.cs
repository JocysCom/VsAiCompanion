using System.CodeDom.Compiler;
using JocysCom.ClassLibrary.Data;

#if NETFRAMEWORK
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
#else
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
#endif

namespace Embeddings
{

	/// <summary>Embeddings Context</summary>
	[GeneratedCode("MainModel", "2023.1.19")]
#if NETFRAMEWORK
	// Setting EF6 configuration OPTION 1.
	//[DbConfigurationType(typeof(System.Data.Entity.SqlServer.MicrosoftSqlDbConfiguration))]
	//[DbConfigurationType(typeof(EntityFrameworkConfiguration))]
#endif
	public partial class EmbeddingsContext : DbContext
	{
		/// <summary>Initialize model</summary>
		public EmbeddingsContext()
			: base()
		{
			Init();
		}


#if NETFRAMEWORK


		public EmbeddingsContext(DbConnection existingConnection, bool contextOwnsConnection)
			: base(existingConnection, contextOwnsConnection)
		{
			Init();
		}


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
		public EmbeddingsContext(DbContextOptions<EmbeddingsContext> options) : base(options)
		{
			Init();
		}

		/// <summary>Create context with connection string.</summary>
		public static EmbeddingsContext Create(string connectionString)
		{
			var optionsBuilder = new DbContextOptionsBuilder<EmbeddingsContext>();
			optionsBuilder.UseSqlServer(connectionString);
			// Uncomment line to output SQL statements.
			//optionsBuilder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
			var context = new EmbeddingsContext(optionsBuilder.Options);
			return context;
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Ignore<Type>();
			modelBuilder.Ignore<System.Reflection.CustomAttributeData>();
			base.OnModelCreating(modelBuilder);
		}

#endif

		private void Init()
		{
#if NETFRAMEWORK
			// Uncomment line to output SQL statements.
			//Database.Log = value => Console.WriteLine(value);
			// The code will crash here if the code and database models do not match.
			var context = ((IObjectContextAdapter)this).ObjectContext;
			// Fix the kind of all DateTime properties when an entity is created.
			context.ObjectMaterialized +=
				(sender, e) => DateTimeKindAttribute.Apply(e.Entity);
#else
			var context = ChangeTracker;
			// Fix the kind of all DateTime properties when an entity is created.
			context.Tracked +=
				(sender, e) => DateTimeKindAttribute.Apply(e.Entry.Entity);
#endif
		}

		/// <summary>File</summary>
		public virtual DbSet<Embedding.File> Files { get; set; }

		/// <summary>File Part</summary>
		public virtual DbSet<Embedding.FilePart> FileParts { get; set; }

		/// <summary>Group Flag</summary>
		public virtual DbSet<Embedding.Group> Groups { get; set; }


	}
}
