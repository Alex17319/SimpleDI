using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleDI.DisposeExceptions;

namespace SimpleDI
{
	public partial class MutatingDependencyLayer : _DependencyLayerInternal, IDependencyLayer
	{
		// For future improvement: Maybe try to implement wildcard dependencies more efficiently (wildcard = returned
		// when any parent class/interface is requested, rather than just when exactly the correct type is requested).
		// Most of the time I'm guessing wildcards won't be used though, so shouldn't sacrifice efficiency elsewhere.

		// Maps from a type to a stack of all dependencies matching that type that are available.
		// Depending on the use of strict (default) or wildcard insertion, dependencies of sub-types may or may not
		// also be returned
		// Each stack is always sorted in descending order by stackLevel (often with gaps), and may be
		// searched through with a binary search (the need for both binary search and stack operations resulted
		// in the SearchableStack<T> class)
		private readonly Dictionary<Type, SearchableStack<StackedDependency>> _dependencyStacks
			= new Dictionary<Type, SearchableStack<StackedDependency>>();

		// Maps from a dependency that has been fetched to the stack level that it was originally injected at
		private readonly Dictionary<object, FetchRecord> _fetchRecords
			= new Dictionary<object, FetchRecord>(new RefEqualityComparer());

		private int stackLevel;

		private bool _disposed = false;

		/// <summary>
		/// Fallbacks are used to increase the search space, but will not be modified in any way.
		/// </summary>
		public IDependencyLayer Fallback { get; }


		internal MutatingDependencyLayer()
		{
			this.Fallback = null;
		}

		internal MutatingDependencyLayer(IDependencyLayer fallback)
		{
			this.Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
		}



		private static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentTypeException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}

		private void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			Dependencies.CloseLayer(this);
		}

		void _DependencyLayerInternal.MarkDisposed()
		{
			_disposed = true;
		}

		IDisposableLayer _DependencyLayerInternal.AsDisposable()
		{
			return new Disposer(this);
		}

		// Used to prevent people from randomly calling Dependencies.CurrentLayer.Dispose()
		// Instead of DependencyLayer implementing IDisposable, this class implements it,
		// and passes on the call to DependencyLayer. External code can't make a proper
		// instance of this class in order to call Dispose() (except via reflection)
		private class Disposer : IDisposableLayer
		{
			public MutatingDependencyLayer Layer { get; }

			public bool IsNull => Layer == null;
			public static readonly Disposer Null = default;

			internal Disposer(MutatingDependencyLayer layer) {
				this.Layer = layer;
			}

			public void Dispose() {
				this.Layer.Dispose();
			}
		}

		private class RefEqualityComparer : IEqualityComparer<object>
		{
			public RefEqualityComparer() { }

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);

			public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}

		/// <summary>
		/// Compares by stack level only
		/// </summary>
		private class StackSearchComparer : IComparer<StackedDependency>
		{
			public StackSearchComparer() { }

			public int Compare(StackedDependency x, StackedDependency y)
				=> x.stackLevel.CompareTo(y.stackLevel);
		}
	}
}

//*/