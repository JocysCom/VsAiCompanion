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
		}

		public event EventHandler Synchronized;

		/// <summary>
		/// Synchronize source collection to destination.
		/// </summary>
		public void Synchronize(IList<T> source, IList<T> target)
		{
			// Convert to array to avoid modification of collection during processing.
			var sList = source.ToArray();
			var t = 0;
			for (var s = 0; s < sList.Length; s++)
			{
				var item = sList[s];
				// If item exists in destination and is in the correct position then continue
				if (t < target.Count && target[t].Equals(item))
				{
					t++;
					continue;
				}
				// If item is in destination but not at the correct position, remove it.
				var indexInDestination = target.IndexOf(item);
				if (indexInDestination != -1)
					target.RemoveAt(indexInDestination);
				// Insert item at the correct position.
				target.Insert(s, item);
				t = s + 1;
			}
			// Remove extra items.
			while (target.Count > sList.Length)
				target.RemoveAt(target.Count - 1);
			Synchronized?.Invoke(this, EventArgs.Empty);
		}
	}

}
