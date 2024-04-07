#if NETFRAMEWORK
using JocysCom.VS.AiCompanion.DataClient;
using System;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SQLite.EF6;
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
