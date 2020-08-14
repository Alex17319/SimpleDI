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
	public sealed partial class MutatingDependencyLayer : DependencyLayer
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

		private int currentStackLevel = 0;

		protected override int CurrentStackLevel => currentStackLevel;

		private DependencySnapshot _snapshot = DependencySnapshot.Null;
		private void markSnapshotDirty() => _snapshot = DependencySnapshot.Null;
		public override bool SnapshotReady => !_snapshot.IsNull;

		internal MutatingDependencyLayer() : base() { }
		internal MutatingDependencyLayer(DependencyLayer fallback) : base(fallback) { }



		public override DependencySnapshot Snapshot(bool useFallbacks) {
			if (SnapshotReady) return _snapshot;

			
			var builder = ImmutableDictionary.CreateBuilder<Type, StackedDependency>();
			builder.AddRange(enumerateDependencies());

			IEnumerable<KeyValuePair<Type, StackedDependency>> enumerateDependencies()
			{
				foreach (var kvp in _dependencyStacks)
				{
					if (kvp.Value.Count != 0) yield return new KeyValuePair<Type, StackedDependency>(
						kvp.Key,
						kvp.Value.Peek().RunOnSnapshot()
					);
				}
			}

			var fb = this.Fallback;
			while (useFallbacks && fb != null) {
				DependencyLayer.AddAsFallbackToSnapshot(fb, builder);
				fb = fb.Fallback;
			}

			return _snapshot = new DependencySnapshot(builder.ToImmutable());
		}

		private protected override void AddAsFallbackToSnapshot(
			ImmutableDictionary<Type, StackedDependency>.Builder snapshotBuilder
		) {
			snapshotBuilder.AddRange(enumerateAddableDependencies());
			
			IEnumerable<KeyValuePair<Type, StackedDependency>> enumerateAddableDependencies()
			{
				foreach (var kvp in _dependencyStacks)
				{
					if (kvp.Value.Count != 0 && !snapshotBuilder.ContainsKey(kvp.Key)) {
						yield return new KeyValuePair<Type, StackedDependency>(
							kvp.Key,
							kvp.Value.Peek().RunOnSnapshot()
						);
					}
				}
			}
		}


		/// <summary>
		/// Compares by stack level only
		/// </summary>
		private class StackSearchComparer : IComparer<StackedDependency>
		{
			public StackSearchComparer() { }

			public int Compare(StackedDependency x, StackedDependency y)
				=> x.StackLevel.CompareTo(y.StackLevel);
		}
	}
}

//*/