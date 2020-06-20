using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	/// <summary>Allows both stack operations and index-based operations such as binary search</summary>
	/// <remarks>Used by <see cref="Dependencies"/>.</remarks>
	/// <typeparam name="T"></typeparam>
	internal class SearchableStack<T> : List<T>
	{
		public SearchableStack() : base() { }
		public SearchableStack(int capacity) : base(capacity) { }
		public SearchableStack(IEnumerable<T> collection) : base(collection) { }

		public void Push(T item) => this.Add(item);

		public T Peek() => this[this.Count - 1];

		public T Pop() {
			T res = Peek();
			this.RemoveAt(this.Count - 1);
			return res;
		}
	}
}

//*/