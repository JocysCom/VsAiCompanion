#if NETFRAMEWORK
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
#if NETFRAMEWORK
	[DbConfigurationType(typeof(MyDbConfiguration))]
#endif
	public partial class EmbeddingsContext
	{


	}

#if NETFRAMEWORK

	public class MyDbConfiguration : DbConfiguration
	{

		public MyDbConfiguration() : base()
		{
		}

		public MyDbConfiguration(string connectionString) : base()
		{
			if (connectionString.Contains(".db"))
			{
				var instance = SQLiteProviderFactory.Instance;
				var service = (System.Data.Entity.Core.Common.DbProviderServices)instance.GetService(typeof(System.Data.Entity.Core.Common.DbProviderServices));
				SetProviderFactory("System.Data.SQLite.EF6", instance);
				SetProviderServices("System.Data.SQLite.EF6", service);
			}
			else
			{
				//SetProviderServices("System.Data.SqlClient", SqlProviderServices.inInstance);
			}
		}
	}

#endif
}
