using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.ClassLibrary.Controls
{
	public class SearchHelper<T>
	{
		private BindingList<T> SourceList;
		private Func<T, bool> Predicate;
		private Func<ListChangedEventArgs, bool> FilterPredicate;

		public BindingList<T> FilteredList { get; private set; }

		public SearchHelper(
			Func<T, bool> predicate,
			Func<ListChangedEventArgs, bool> filterPredicate = null,
			BindingList<T> filteredList = null
		)
		{
			Predicate = predicate;
			FilteredList = filteredList ?? new BindingList<T>();
			FilterPredicate = filterPredicate;
		}

		public void SetSource(BindingList<T> source)
		{
			if (SourceList != null)
				SourceList.ListChanged -= SourceList_ListChanged;
			SourceList = source;
			SourceList.ListChanged += SourceList_ListChanged;
			Filter();
		}

		private void SourceList_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (FilterPredicate is null || FilterPredicate.Invoke(e))
			{
				// refresh results
				var refresh =
					e.ListChangedType == ListChangedType.ItemDeleted ||
					e.ListChangedType == ListChangedType.ItemAdded ||
					e.ListChangedType == ListChangedType.ItemMoved;
				if (refresh)
					Filter();
			}
		}

		private CancellationTokenSource cts = new CancellationTokenSource();

		public async void Filter()
		{
			// Cancel any previous filter operation.
			cts.Cancel();
			cts = new CancellationTokenSource();
			await Task.Delay(500);
			// If new filter operation was started then return.
			if (cts.Token.IsCancellationRequested)
				return;
			var filteredSourceList = new BindingList<T>(SourceList.Where(item => Predicate(item)).ToList());
			Synchronize(filteredSourceList, FilteredList);
			Synchronized?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler Synchronized;

		/// <summary>
		/// Synchronize source collection to destination.
		/// </summary>
		/// <remarks>
		/// Same Code:
		/// JocysCom\Controls\SearchHelper.cs
		/// JocysCom\Configuration\SettingsHelper.cs
		/// </remarks>
		static void Synchronize(IList<T> source, IList<T> target)
		{
			// Create a dictionary for fast lookup in source list
			var sourceSet = new Dictionary<T, int>();
			for (int i = 0; i < source.Count; i++)
				sourceSet[source[i]] = i;
			// Iterate over the target, remove items not in source
			for (int i = target.Count - 1; i >= 0; i--)
				if (!sourceSet.ContainsKey(target[i]))
					target.RemoveAt(i);
			// Iterate over source
			for (int s = 0; s < source.Count; s++)
			{
				// If item is not present in target, insert it.
				if (!target.Contains(source[s]))
				{
					target.Insert(s, source[s]);
					continue;
				}
				// If item is present in target but not at the right position, move it.
				int t = target.IndexOf(source[s]);
				if (t != s)
				{
					T temp = target[s];
					target[s] = target[t];
					target[t] = temp;
				}
			}
			// Remove items at the end of target that exceed source's length
			while (target.Count > source.Count)
				target.RemoveAt(target.Count - 1);
		}

	}

}
