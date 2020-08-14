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

		public readonly int stackLevel;
		public readonly object dependency;
		public readonly object[] injectState;
		public readonly object[] snapshotState;
		public readonly DepStateHandler[] stateHandlers;
		
		private static readonly object[] STATELESS_SNAPSHOT_FLAG = new object[0];
		public bool IsSnapshot => snapshotState != null;

		public bool IsNull => dependency == null;
		public static readonly StackedDependency Null = default;

		public StackedDependency(int stackLevel, object dependency)
		{
			if (stackLevel < 1) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Must be at least 1");
			if (dependency == null) throw new ArgumentNullException(nameof(dependency));

			this.stackLevel = stackLevel;
			this.dependency = dependency;
			this.stateHandlers = DepStateHandler.LookupHandlersFor(dependency);
			this.injectState = stateHandlers == null ? null : new object[stateHandlers.Length];
			this.snapshotState = null;
		}

		private StackedDependency(int stackLevel, object dependency, object[] injectState, object[] snapshotState, DepStateHandler[] stateHandlers)
		{
			this.stackLevel = stackLevel;
			this.dependency = dependency;
			this.injectState = injectState;
			this.snapshotState = snapshotState;
			this.stateHandlers = stateHandlers;
		}


		internal void RunOnInject() {
			// TODO: Consider throwing an exception if this is run twice (need a way to detect it though)

			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnInject)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (stateHandlers == null) return;

			for (int i = 0; i < this.stateHandlers.Length; i++) {
				this.injectState[i] = this.stateHandlers[i]?.OnInject(dependency);
			}
		}
		
		internal void RunOnFetch() {
			// TODO: Consider throwing an exception if RunOnInject() hasn't been run yet (need a way to detect it though)

			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetch)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (stateHandlers == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.stateHandlers[i]?.OnFetch(dependency, this.injectState[i]);
			}
		}
		
		internal StackedDependency RunOnSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnSnapshot)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (stateHandlers == null) return new StackedDependency(
				this.stackLevel,
				this.dependency,
				injectState: null,
				snapshotState: STATELESS_SNAPSHOT_FLAG,
				stateHandlers: null
			);

			object[] snapshotState = new object[this.stateHandlers.Length];
			for (int i = 0; i < this.stateHandlers.Length; i++) {
				snapshotState[i] = this.stateHandlers[i]?.OnSnapshot(dependency, this.injectState[i]);
			}
			return new StackedDependency(this.stackLevel, this.dependency, this.injectState, snapshotState, stateHandlers);
		}

		internal void RunOnFetchFromSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetchFromSnapshot)}() cannot be used on a null {nameof(StackedDependency)}."
			);
			if (!this.IsSnapshot) throw new InvalidOperationException(
				$"{nameof(RunOnFetchFromSnapshot)}() cannot be used on a {nameof(StackedDependency)} that stores a dependency which has not been snapshotted."
			);

			if (this.stateHandlers == null) return;

			for (int i = 0; i < this.stateHandlers.Length; i++) {
				this.stateHandlers[i]?.OnFetchFromSnapshot(dependency, this.injectState[i], this.snapshotState[i]);
			}
		}
	}
}

//*/