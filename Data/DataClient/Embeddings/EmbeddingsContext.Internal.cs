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
	public partial class EmbeddingsContext
	{


	}
}
