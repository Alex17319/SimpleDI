using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	class CountedImmutableStack<T> : IReadOnlyCollection<T>
	{
		public ImmutableStack<T> Stack { get; private set; }
		public int Count { get; private set; }

		public CountedImmutableStack() {
			this.Stack = ImmutableStack.Create<T>();
		}

		public CountedImmutableStack(ImmutableStack<T> stack) {
			this.Stack = stack;
			this.Count = stack.Count();
		}

		public CountedImmutableStack(T item) {
			this.Stack = ImmutableStack.Create(item);
			this.Count = 1;
		}

		public CountedImmutableStack(IEnumerable<T> items) {
			// The idea here is to iterate only once
			// However if CreateRange tries to iterate twice (it shouldn't), we give up on that strategy

			int count = 0;
			this.Stack = ImmutableStack.CreateRange(counter());
			this.Count = count;

			IEnumerable<T> counter()
			{
				if (count != 0) {
					// CreateRange is iterating multiple times for some reason
					// It shouldn't do this (what if the Enumerable is a LINQ-to-SQL query), but if it is,
					// give up on the whole count-as-we-go strategy
					count = items.Count();
					foreach (var item in items) yield return item;
				}

				foreach (var item in items) {
					count++;
					yield return item;
				}
			}
		}

		public ImmutableStack<T>.Enumerator GetEnumerator() => Stack.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Stack).GetEnumerator();
		IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Stack).GetEnumerator();

		public void Push(T value)
		{
			this.Stack = this.Stack.Push(value);
			this.Count++;
		}
	}
}

//*/