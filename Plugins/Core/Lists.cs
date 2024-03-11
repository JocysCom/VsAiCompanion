using JocysCom.ClassLibrary.Runtime;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Allows AI to manage various lists, such as task lists, to-do lists, task progress, and completion lists. It can also be used to store environment variables or settings.
	/// </summary>
	public partial class Lists
	{
		/// <summary>
		/// Property set from the external class.
		/// </summary>
		public static IList<ListInfo> AllLists
		{
			get
			{
				lock (AllListsLock)
					return _AllLists = _AllLists ?? new List<ListInfo>();
			}
			set => _AllLists = value;
		}
		static IList<ListInfo> _AllLists;
		static object AllListsLock = new object();

		#region List Manipulation


		private IEnumerable<ListInfo> GetEnabledLists()
		{
			return AllLists
				.Where(x => x.IsEnabled)
				.OrderBy(x => $"{x.Path}/{x.Name}")
				.Where(x => string.IsNullOrEmpty(x.Path) || x.Path == FilterPath);
		}

		/// <summary>
		/// Retrieves all lists.
		/// </summary>
		private IList<ListInfo> GetFilteredListInfos()
		{
			return GetEnabledLists()
				.ToList();
		}

		/// <summary>
		/// Get list by name.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		public ListInfo GetFilteredListInfo(string listName)
		{
			return GetEnabledLists()
				.Where(x => x.Name == listName)
				.FirstOrDefault();
		}


		/// <summary>
		/// Get list information
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="includeItems">`true` to include items, `false` to get information only.</param>
		[RiskLevel(RiskLevel.None)]
		public ListInfo GetList(string listName, bool includeItems = false)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return null;
			if (includeItems)
				return li;
			var newLi = new ListInfo();
			RuntimeHelper.CopyProperties(li, newLi, true);
			newLi.IconData = null;
			newLi.IconType = null;
			return newLi;
		}

		/// <summary>
		/// Get List names.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public List<string> GetListNames()
		{
			return GetEnabledLists()
				.Select(x => x.Name)
				.ToList();
		}

		/// <summary>
		/// Load items into existing list from the CSV file.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="path">Path to the CSV file</param>
		/// <param name="keyColumn">Use csv column for the Key property.</param>
		/// <param name="valueColumn">Use csv column for the Value property.</param>
		/// <param name="commentColumn">Use csv column for the Comment property.</param>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public int LoadListFromCsv(
			string listName,
			string path,
			string keyColumn = null, string valueColumn = null, string commentColumn = null)
		{
			var li = GetFilteredListInfo(listName);
			// List don't exists
			if (li == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			var table = JocysCom.ClassLibrary.Files.CsvHelper.Read(path, true, true);
			foreach (DataRow row in table.Rows)
			{
				var item = new ListItem();
				if (string.IsNullOrWhiteSpace(keyColumn))
					item.Key = (string)row[keyColumn];
				if (string.IsNullOrWhiteSpace(valueColumn))
					item.Value = (string)row[valueColumn];
				if (string.IsNullOrWhiteSpace(commentColumn))
					item.Comment = (string)row[commentColumn];
				li.Items.Add(item);
			}
			return 0;
		}

		/// <summary>
		/// Filter list by parent, to make sure that only Global and child lists are selected.
		/// </summary>
		public string FilterPath;

		/// <summary>
		/// Creates a new list.
		/// </summary>
		/// <returns>0 operation successfull, -2 list is readonly, -3 list already exists.</returns>
		[RiskLevel(RiskLevel.None)]
		public int CreateList(string listName, string description)
		{
			var li = GetFilteredListInfo(listName);
			// List already exists.
			if (li != null)
				return -3;
			if (li.IsReadOnly)
				return -2;
			li = new ListInfo();
			li.Path = FilterPath;
			li.Name = listName;
			li.Description = description;
			li.Items = new BindingList<ListItem>();
			_AllLists.Add(li);
			return 0;
		}

		/// <summary>
		/// Updates an existing list.
		/// </summary>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.None)]
		public int UpdateList(string listName, string description)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			// Create if not exists and return result
			return CreateList(listName, description);
		}

		/// <summary>
		/// Deletes an existing list.
		/// </summary>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.None)]
		public int DeleteList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			return _AllLists.Remove(li) ? 0 : -1;
		}

		/// <summary>
		/// Clears all items from a list.
		/// </summary>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.None)]
		public int ClearList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			li.Items.Clear();
			return 0;
		}

		#endregion

		#region Item Manipulation

		/// <summary>
		/// Sets or adds an item to a list.
		/// </summary>
		/// <returns>True if the item is set or added successfully.</returns>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.None)]
		public int UpdateListItem(string listName, string key, string value, string comment = "")
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			var item = li.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
			{
				item = new ListItem
				{
					Key = key,
					Value = value,
					Comment = comment,
				};
				li.Items.Add(item);
			}
			else
			{
				item.Value = value;
				item.Comment = comment;
			}
			return 0;
		}

		/// <summary>
		/// Retrieves an item from a list.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public ListItem GetListItem(string listName, string key)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			return li?.Items.FirstOrDefault(i => i.Key == key);
		}

		/// <summary>
		/// Deletes an item from a list.
		/// </summary>
		/// <returns>0 operation successfull, -1 list not found, -2 list is readonly.</returns>
		[RiskLevel(RiskLevel.None)]
		public int DeleteListItem(string listName, string key)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			var item = li?.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
				return -1;
			if (li.IsReadOnly)
				return -2;
			li.Items.Remove(item);
			return 0;
		}

		/// <summary>
		/// Retrieves all items from a list.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public IList<ListItem> GetListItems(string listName)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			return li?.Items.ToList() ?? new List<ListItem>();
		}

		#endregion

	}
}
