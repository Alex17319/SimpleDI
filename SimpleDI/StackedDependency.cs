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
	internal struct StackedDependency
	{
		// TODO: see if stackLevel is irrelevant to SafeDependencyLayer. If so, maybe
		// split this struct into two, one that stores the dependency & state, and the other
		// that wraps that struct alongside the stack level

		public readonly int StackLevel;
		public readonly StateWrappedDependency SDependency;

		public bool IsNull => SDependency == null;
		public static readonly StackedDependency Null = default;

		public StackedDependency(int stackLevel, StateWrappedDependency sDependency)
		{
			if (stackLevel < 1) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Must be at least 1");
			if (sDependency == null) throw new ArgumentNullException(nameof(sDependency));

			this.StackLevel = stackLevel;
			this.SDependency = sDependency;
		}

		private StackedDependency(int stackLevel, object dependency, IStateWrapper[] injectState, bool isSnapshot)
		{
			if (stackLevel < 1) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Must be at least 1");
			if (dependency == null) throw new ArgumentNullException(nameof(dependency));
			// injectState can be null or contain nulls

			this.StackLevel = stackLevel;
			this.dependency = dependency;
			this.injectState = injectState;
			this.isSnapshot = isSnapshot;
		}


		internal void RunOnInject() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnInject)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			this.SDependency.
		}
		
		internal void RunOnFetch() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetch)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			
		}
		
		internal StackedDependency RunOnSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnSnapshot)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			
		}

		internal void RunOnFetchFromSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetchFromSnapshot)}() cannot be used on a null {nameof(StackedDependency)}."
			);
			

		}
	}

	internal abstract class StateWrappedDependency
	{
		public readonly object Dependency;

		internal abstract void RunOnInject();
		internal abstract void RunOnFetch();
		internal abstract void RunOnSnapshot();
		internal abstract void RunOnFetchFromSnapshot();
	}
	
	internal class StateWrappedDependency<TInj, TSnap> : StateWrappedDependency
	{
		private readonly TInj[] InjectState;
		private readonly object SnapshotState;

		private static readonly object NON_SNAPSHOT_FLAG = new object();
		private bool IsSnapshot => !ReferenceEquals(SnapshotState, NON_SNAPSHOT_FLAG);

		internal override void RunOnInject() {
			if (InjectState == null) return;

			TInj[] injectState
			for (int i = 0; i < this.InjectState.Length; i++) {
				((IStatefulDependency<TInj, TSnap>)this.Dependency).OnInject();
				this.InjectState[i]?.RunOnInject();
			}
		}
		
		internal override void RunOnFetch() {
			if (InjectState == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.injectState[i]?.RunOnFetch();
			}
		}
		
		internal override StackedDependency RunOnSnapshot() {
			if (injectState == null) return new StackedDependency(this.stackLevel, this.dependency, null, isSnapshot: true);

			ISnapshotStateWrapper[] snapshotState = new ISnapshotStateWrapper[this.injectState.Length];
			for (int i = 0; i < this.injectState.Length; i++) {
				snapshotState[i] = this.injectState[i]?.RunOnSnapshot();
			}
			return new StackedDependency(this.stackLevel, this.dependency, snapshotState, isSnapshot: true);
		}

		internal override void RunOnFetchFromSnapshot() {
			if (!this.isSnapshot) throw new InvalidOperationException(
				$"{nameof(RunOnFetchFromSnapshot)}() cannot be used on a {nameof(StackedDependency)} that stores a dependency which has not been snapshotted."
			);

			if (this.injectState == null) return;

			ISnapshotStateWrapper[] snapshotState = (ISnapshotStateWrapper[])this.injectState;

			for (int i = 0; i < snapshotState.Length; i++) {
				snapshotState[i]?.RunOnFetchFromSnapshot();
			}
		}
	}
}

//*/