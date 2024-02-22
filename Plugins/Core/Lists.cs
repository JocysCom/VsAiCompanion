﻿using System.Collections.Generic;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Manages various lists.
	/// </summary>
	public partial class Lists
	{
		/// <summary>
		/// Property set from the external class.
		/// </summary>
		private static IList<ListInfo> AllLists { get; set; }

		#region List Manipulation

		/// <summary>
		/// Retrieves all lists.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		public static IList<ListInfo> GetLists()
		{
			return AllLists;
		}

		/// <summary>
		/// Creates a new list.
		/// </summary>
		/// <returns>True if the list is created successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool CreateList(string listName, string description)
		{
			if (AllLists == null) AllLists = new List<ListInfo>();
			if (AllLists.Any(l => l.Name == listName)) return false; // List already exists
			AllLists.Add(new ListInfo { Name = listName, Description = description, Items = new List<ListItem>() });
			return true;
		}

		/// <summary>
		/// Updates an existing list.
		/// </summary>
		/// <returns>True if the list is updated successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool UpdateList(string listName, string description)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			if (list != null)
			{
				list.Description = description;
				return true;
			}
			return CreateList(listName, description); // Create if not exists and return result
		}

		/// <summary>
		/// Deletes an existing list.
		/// </summary>
		/// <returns>True if the list is deleted successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool DeleteList(string listName)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			if (list != null)
			{
				AllLists.Remove(list);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears all items from a list.
		/// </summary>
		/// <returns>True if the list items are cleared successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool ClearList(string listName)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			if (list != null)
			{
				list.Items.Clear();
				return true;
			}
			return false;
		}

		#endregion

		#region Item Manipulation

		/// <summary>
		/// Sets or adds an item to a list.
		/// </summary>
		/// <returns>True if the item is set or added successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool SetListItem(string listName, string key, string value, string comment = "")
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			if (list == null)
			{
				CreateList(listName, ""); // Create list if not exists
				list = AllLists.First(l => l.Name == listName);
			}
			var item = list.Items.FirstOrDefault(i => i.Key == key);
			if (item != null)
			{
				item.Value = value;
				item.Comment = comment;
			}
			else list.Items.Add(new ListItem { Key = key, Value = value, Comment = comment });
			return true;
		}

		/// <summary>
		/// Retrieves an item from a list.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		public static ListItem GetListItem(string listName, string key)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			return list?.Items.FirstOrDefault(i => i.Key == key);
		}

		/// <summary>
		/// Deletes an item from a list.
		/// </summary>
		/// <returns>True if the item is deleted successfully.</returns>
		[RiskLevel(RiskLevel.Low)]
		public static bool DeleteListItem(string listName, string key)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			var item = list?.Items.FirstOrDefault(i => i.Key == key);
			if (item != null)
			{
				list.Items.Remove(item);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Retrieves all items from a list.
		/// </summary>
		[RiskLevel(RiskLevel.Low)]
		public static IList<ListItem> GetListItems(string listName)
		{
			var list = AllLists.FirstOrDefault(l => l.Name == listName);
			return list?.Items ?? new List<ListItem>();
		}

		#endregion

	}
}