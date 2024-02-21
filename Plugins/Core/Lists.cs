using JocysCom.VS.AiCompanion.Plugins.Core.Common;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{
	/// <summary>
	/// Manage various lists.
	/// </summary>
	public partial class Lists
	{

		/// <summary>
		/// Curent lists. Key is list name, Value is description.
		/// </summary>
		public static List<ListInfo> AllLists { get; set; }

		/// <summary>
		/// Get all available lists.
		/// </summary>
		public static List<ListInfo> GetAllLists()
		{
			return AllLists;
		}

		///// <summary>
		///// Retrieves all items from the specified list.
		///// </summary>
		///// <param name="listName"></param>
		//ReadList(string listName)
		//{
		//}

		/*

		public static CreateItem(string listName, string item) : Adds an item to a specified list.If the list doesn't exist, it creates it.


•ReadList(string listName) : Retrieves all items from the specified list.


•UpdateItem(string listName, int itemId, string newItemValue) : Replaces an item in a list with a new value.


•DeleteItem(string listName, int itemId) : Removes an item from a specified list.


•DeleteList(string listName) : Removes an entire list.

*/

	}
}
