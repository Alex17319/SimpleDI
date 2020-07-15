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
	// Snapshotable == mutable wrapper holding an immutable type
	// For stacks that means we can cache the count rather than having it need to be recalculated
	public class SnapshotableStack<T> : IReadOnlyCollection<T>
	{
		public ImmutableStack<T> Stack { get; private set; }
		public int Count { get; private set; }

		public bool IsEmpty => Count == 0;

		public SnapshotableStack() {
			this.Stack = ImmutableStack.Create<T>();
		}

		public SnapshotableStack(ImmutableStack<T> stack) {
			this.Stack = stack;
			this.Count = stack.Count();
		}

		public SnapshotableStack(T item) {
			this.Stack = ImmutableStack.Create(item);
			this.Count = 1;
		}

		public SnapshotableStack(IEnumerable<T> items) {
			// The idea here is to iterate only once
			// However if CreateRange tries to iterate twice (it shouldn't), we give up on that strategy

			int count = 0;
			this.Stack = ImmutableStack.CreateRange(enumerateAndCount());
			this.Count = count;

			IEnumerable<T> enumerateAndCount()
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

		public T Peek() => this.Stack.Peek();
		public ref readonly T PeekRef() => ref this.Stack.PeekRef();

		public T Pop()
		{
			this.Stack = this.Stack.Pop(out T value);
			this.Count--;
			return value;
		}

		public void Clear()
		{
			this.Stack = this.Stack.Clear();
			this.Count = 0;
		}
	}
}

//*/