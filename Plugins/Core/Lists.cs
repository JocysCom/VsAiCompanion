using System.Collections.Generic;
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

		/// <summary>
		/// Retrieves all lists.
		/// </summary>
		private IList<ListInfo> GetFilteredListInfos()
		{
			return AllLists
				.Where(x => string.IsNullOrEmpty(x.Path) || x.Path == FilterPath)
				.ToList();
		}

		/// <summary>
		/// Get List names.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public List<string> GetListNames()
		{
			return AllLists
				.Where(x => string.IsNullOrEmpty(x.Path) || x.Path == FilterPath)
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
		/// <returns></returns>
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
			return table.Rows.Count;
		}

		/// <summary>
		/// Get list by name.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		public ListInfo GetFilteredListInfo(string listName)
		{
			return AllLists
				.Where(x => string.IsNullOrEmpty(x.Path) || x.Path == FilterPath)
				.Where(x => x.Name == listName)
				.FirstOrDefault();
		}

		/// <summary>
		/// Filter list by parent, to make sure that only Global and child lists are selected.
		/// </summary>
		public string FilterPath;

		/// <summary>
		/// Creates a new list.
		/// </summary>
		/// <returns>True if the list is created successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool CreateList(string listName, string description)
		{
			var li = GetFilteredListInfo(listName);
			// List already exists.
			if (li != null)
				return false;
			li = new ListInfo();
			li.Path = FilterPath;
			li.Name = listName;
			li.Description = description;
			li.Items = new List<ListItem>();
			_AllLists.Add(li);
			return true;
		}

		/// <summary>
		/// Updates an existing list.
		/// </summary>
		/// <returns>True if the list is updated successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool UpdateList(string listName, string description)
		{
			var li = GetFilteredListInfo(listName);
			if (li != null)
			{
				li.Description = description;
				return true;
			}
			// Create if not exists and return result
			return CreateList(listName, description);
		}

		/// <summary>
		/// Deletes an existing list.
		/// </summary>
		/// <returns>True if the list is deleted successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool DeleteList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return true;
			return _AllLists.Remove(li);
		}

		/// <summary>
		/// Clears all items from a list.
		/// </summary>
		/// <returns>True if the list items are cleared successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool ClearList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return false;
			li.Items.Clear();
			return true;
		}

		#endregion

		#region Item Manipulation

		/// <summary>
		/// Sets or adds an item to a list.
		/// </summary>
		/// <returns>True if the item is set or added successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool SetListItem(string listName, string key, string value, string comment = "")
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return false;
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
			return true;
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
		/// <returns>True if the item is deleted successfully.</returns>
		[RiskLevel(RiskLevel.None)]
		public bool DeleteListItem(string listName, string key)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			var item = li?.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
				return false;
			li.Items.Remove(item);
			return true;
		}

		/// <summary>
		/// Retrieves all items from a list.
		/// </summary>
		[RiskLevel(RiskLevel.None)]
		public IList<ListItem> GetListItems(string listName)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			return li?.Items ?? new List<ListItem>();
		}

		#endregion

	}
}
