using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

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
				.Where(x => x.Name.Equals(listName, System.StringComparison.OrdinalIgnoreCase))
				.FirstOrDefault();
		}

		/// <summary>
		/// Get list information
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="includeItems">`true` to include items, `false` to get information only.</param>
		[RiskLevel(RiskLevel.None)]
		public ListInfo GetList(string listName, bool includeItems)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return null;
			var newLi = new ListInfo();
			RuntimeHelper.CopyProperties(li, newLi, true);
			// Remove unecessary data.
			newLi.IconData = null;
			newLi.IconType = null;
			if (includeItems)
				newLi.Items = li.Items;
			return newLi;
		}

		/// <summary>
		/// Get List names filtered by an optional regular expression pattern.
		/// </summary>
		/// <param name="pattern">The regex pattern to filter list names. If null, all names are returned.</param>
		/// <returns>A List of filtered list names.</returns>
		[RiskLevel(RiskLevel.None)]
		public List<string> GetListNames(string pattern = null)
		{
			var enabledLists = GetEnabledLists()
				.Select(x => x.Name);
			if (pattern != null)
			{
				var regex = new Regex(pattern, RegexOptions.IgnoreCase);
				enabledLists = enabledLists.Where(x => regex.IsMatch(x));
			}
			return enabledLists.ToList();
		}

		/// <summary>
		/// Get Lists filtered by regular expression pattern.
		/// </summary>
		/// <param name="pattern">The regex pattern to filter by list name. If null, all lists are returned.</param>
		/// <returns>Filtered lists.</returns>
		[RiskLevel(RiskLevel.None)]
		public List<ListInfo> GetLists(string pattern)
		{
			var names = GetListNames(pattern)
				.ToArray();
			var enabledLists = GetEnabledLists()
				.Where(x => names.Contains(x.Name));
			return enabledLists.ToList();
		}

		/// <summary>
		/// Load items into existing list from the CSV file.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="path">Path to the CSV file</param>
		/// <param name="keyColumn">Use csv column for the Key property.</param>
		/// <param name="statusColumn">Optional. Use csv column for the Status property.</param>
		/// <param name="valueColumn">Optional. Use csv column for the Value property.</param>
		/// <param name="commentColumn">Optional. Use csv column for the Comment property.</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.Medium)]
		public OperationResult<string> LoadListFromCsv(
			string listName,
			string path,
			string keyColumn = null, string statusColumn = null,
			string valueColumn = null, string commentColumn = null)
		{
			try
			{
				var li = GetFilteredListInfo(listName);
				if (li == null)
					return GetStatus(ListErrorCode.ListNotFound);
				if (li.IsReadOnly)
					return GetStatus(ListErrorCode.ListReadOnly);
				var table = JocysCom.ClassLibrary.Files.CsvHelper.Read(path, true, true);
				foreach (DataRow row in table.Rows)
				{
					var item = new ListItem();
					if (!string.IsNullOrWhiteSpace(keyColumn))
						item.Key = (string)row[keyColumn];
					ProgressStatus status;
					if (!string.IsNullOrWhiteSpace(statusColumn) && Enum.TryParse((string)row[statusColumn], out status))
						item.Status = status;
					if (!string.IsNullOrWhiteSpace(valueColumn))
						item.Value = (string)row[valueColumn];
					if (!string.IsNullOrWhiteSpace(commentColumn))
						item.Comment = (string)row[commentColumn];
					li.Items.Add(item);
				}
				return GetStatus(ListErrorCode.Success, $"CSV loaded into list '{listName}' successfully.");
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Filter list by parent, to make sure that only Global and child lists are selected.
		/// </summary>
		public string FilterPath;

		/// <summary>
		/// Creates a new list.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="description">Description provides user-facing details of the list.</param>
		/// <param name="instructions"> Instructions, outline how AI should operate based on the list's content.</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> CreateList(string listName, string description, string instructions)
		{
			var li = GetFilteredListInfo(listName);
			// List already exists.
			if (li != null)
				return GetStatus(ListErrorCode.ListAlreadyExists);
			li = new ListInfo();
			li.Path = FilterPath;
			li.Name = listName;
			li.Description = description;
			li.Instructions = instructions;
			li.Items = new BindingList<ListItem>();
			_AllLists.Add(li);
			return GetStatus(ListErrorCode.Success, $"List '{li.Name}' was successfully created");
		}

		/// <summary>
		/// Updates an existing list.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="description">Optional. Description provides user-facing details of the list.</param>
		/// <param name="instructions">Optional. Instructions, outline how AI should operate based on the list's content.</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> UpdateList(string listName, string description = null, string instructions = null)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			if (description != null)
				li.Description = description;
			if (instructions != null)
				li.Instructions = instructions;
			return GetStatus(ListErrorCode.Success, $"List '{listName}' updated successfully.");
		}

		/// <summary>
		/// Deletes an existing list.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> DeleteList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			bool removed = _AllLists.Remove(li);
			if (removed)
				return GetStatus(ListErrorCode.Success, $"List '{listName}' deleted successfully.");
			else
				return GetStatus(ListErrorCode.ListNotFound, $"List '{listName}' could not be deleted.");
		}

		/// <summary>
		/// Clears all items from a list.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> ClearList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			li.Items.Clear();
			return GetStatus(ListErrorCode.Success, $"List '{listName}' cleared successfully.");
		}

		/// <summary>
		/// Sort items by key.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> SortList(string listName)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			var allVersion = li.Items.All(x => System.Version.TryParse(x.Key, out _));
			List<ListItem> sortedList;
			if (allVersion)
				sortedList = li.Items.OrderBy(x => System.Version.Parse(x.Key)).ToList();
			else
				sortedList = li.Items.OrderBy(x => x.Key).ToList();
			CollectionsHelper.Synchronize(sortedList, li.Items);
			return GetStatus(ListErrorCode.Success, $"List '{listName}' sorted successfully.");
		}

		#endregion

		#region Item Manipulation

		/// <summary>
		/// Sets or adds an item to a list.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="key">Item key.</param>
		/// <param name="status">Item progress status.</param>
		/// <param name="value">Item value.</param>
		/// <param name="comment">Item comment.</param>
		/// <param name="index">Optional. The zero-based index at which item should be inserted.</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> UpdateListItem(string listName, string key, string value, ProgressStatus? status = null, string comment = "", int? index = null)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			var item = li.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
			{
				item = new ListItem
				{
					Key = key,
					Status = status,
					Value = value,
					Comment = comment,
				};
				if (index.HasValue)
					li.Items.Insert(index.Value, item);
				else
					li.Items.Add(item);
			}
			else
			{
				item.Value = value;
				item.Comment = comment;
				item.Status = status;
			}
			return GetStatus(ListErrorCode.Success, $"Item '{key}' updated successfully in list '{listName}'.");
		}

		/// <summary>
		/// Sets list item progress status.
		/// </summary>
		/// <param name="listName">Name of the list</param>
		/// <param name="key">Item key.</param>
		/// <param name="status">Item progress status.</param>
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		public OperationResult<string> SetListItemStatus(string listName, string key, ProgressStatus? status = null)
		{
			var li = GetFilteredListInfo(listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			var item = li.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
				return GetStatus(ListErrorCode.ListItemNotFound);
			item.Status = status;
			return GetStatus(ListErrorCode.Success, $"Status for item '{key}' updated successfully in list '{listName}'.");
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
		/// <returns>OperationResult with Success or a corresponding error code.</returns>
		[RiskLevel(RiskLevel.None)]
		public OperationResult<string> DeleteListItem(string listName, string key)
		{
			var li = GetFilteredListInfos().FirstOrDefault(l => l.Name == listName);
			if (li == null)
				return GetStatus(ListErrorCode.ListNotFound);
			var item = li.Items.FirstOrDefault(i => i.Key == key);
			if (item == null)
				return GetStatus(ListErrorCode.ListItemNotFound);
			if (li.IsReadOnly)
				return GetStatus(ListErrorCode.ListReadOnly);
			li.Items.Remove(item);
			return GetStatus(ListErrorCode.Success, $"Item '{key}' deleted successfully from list '{listName}'.");
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

		private OperationResult<string> GetStatus(ListErrorCode statusCode, string message = null)
		{
			var statusText = Attributes.GetDescription(statusCode);
			if (!string.IsNullOrWhiteSpace(message))
				statusText += $". {message}";
			return new OperationResult<string>(null, (int)statusCode, statusText);
		}

		#endregion

	}
}
