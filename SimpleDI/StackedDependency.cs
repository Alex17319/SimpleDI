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
		public readonly int stackLevel;
		public readonly object dependency;
		public readonly StateWrapper[] injectState;

		public bool IsNull => dependency == null;
		public static readonly StackedDependency Null = default;

		public StackedDependency(int stackLevel, object dependency, StateWrapper[] injectState)
		{
			if (stackLevel < 1) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Must be at least 1");
			if (dependency == null) throw new ArgumentNullException(nameof(dependency));
			// injectState can be null or contain nulls

			this.stackLevel = stackLevel;
			this.dependency = dependency;
			this.injectState = injectState;
		}

		internal void RunOnInject() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnInject)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (injectState == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.injectState[i]?.RunOnInject();
			}
		}
		
		internal void RunOnFetch() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetch)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (injectState == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.injectState[i]?.RunOnFetch();
			}
		}
		
		internal ISnapshotStateWrapper[] RunOnSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnSnapshot)}() cannot be used on a null {nameof(StackedDependency)}."
			);

			if (injectState == null) return null;

			ISnapshotStateWrapper[] snapshotState = new ISnapshotStateWrapper[this.injectState.Length];
			for (int i = 0; i < this.injectState.Length; i++) {
				snapshotState[i] = this.injectState[i]?.RunOnSnapshot();
			}
			return snapshotState;
		}
	}
}

//*/