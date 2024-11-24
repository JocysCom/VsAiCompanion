using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JocysCom.ClassLibrary.Data {

	/* Example: How to hook attribute up to your EF context.

	  Source: https://itecnote.com/tecnote/c-entity-framework-datetime-and-utc/

	  public class MyContext : DbContext
	  {
		  public DbSet<Foo> Foos { get; set; }

		  public MyContext()
		  {
			  ((IObjectContextAdapter)this).ObjectContext.ObjectMaterialized +=
			  (sender, e) => DateTimeKindAttribute.Apply(e.Entity);
		  }
	  }

   */

	[AttributeUsage(AttributeTargets.Property)]
	public class DateTimeKindAttribute : Attribute {
		private readonly DateTimeKind _kind;

		public DateTimeKindAttribute(DateTimeKind kind) {
			_kind = kind;
		}

		public DateTimeKind Kind {
			get { return _kind; }
		}

		public static void Apply(object entity, bool cache = true) {
			if (entity is null)
				return;
			var properties = cache
				? Properties.GetOrAdd(entity.GetType(), x => GetProperties(x))
				: GetProperties(entity.GetType());
			// Process datetime properties.
			foreach (var property in properties) {
				var attr = property.GetCustomAttribute<DateTimeKindAttribute>();
				if (attr is null)
					continue;
				var value = property.GetValue(entity);
				if (value is null)
					continue;
				var date = property.PropertyType == typeof(DateTime)
					? (DateTime)value
					: (DateTime?)value;
				property.SetValue(entity, DateTime.SpecifyKind(date.Value, attr.Kind));
			}
		}

		/// <summary>Cache data for speed.</summary>
		/// <remarks>Cache allows for this class to work 20 times faster.</remarks>
		private static ConcurrentDictionary<Type, PropertyInfo[]> Properties { get; } = new ConcurrentDictionary<Type, PropertyInfo[]>();

		private static PropertyInfo[] GetProperties(Type t) {
			var list = new List<PropertyInfo>();
			var infos = t.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
				.Where(x => x.PropertyType == typeof(DateTime) || x.PropertyType == typeof(DateTime?));
			foreach (PropertyInfo pi in infos) {
				var attribute = pi.GetCustomAttribute(typeof(DateTimeKindAttribute), false);
				if (attribute != null)
					list.Add(pi);
			}
			return list.ToArray();
		}

	}
}
