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

/*

namespace SimpleDI
{
	internal struct SnapshottedDependency
	{
		public readonly object dependency;
		public readonly ISnapshotStateWrapper[] snapshotState;

		public bool IsNull => dependency == null;
		public static readonly SnapshottedDependency Null = default;

		public SnapshottedDependency(object dependency, ISnapshotStateWrapper[] snapshotState)
		{
			if (dependency == null) throw new ArgumentNullException(nameof(dependency));
			// snapshotState can be null or contain nulls

			this.dependency = dependency;
			this.snapshotState = snapshotState;
		}

		internal void RunOnFetchFromSnapshot() {
			if (this.IsNull) throw new InvalidOperationException(
				$"{nameof(RunOnFetchFromSnapshot)}() cannot be used on a null {nameof(SnapshottedDependency)}."
			);

			if (snapshotState == null) return;

			for (int i = 0; i < this.snapshotState.Length; i++) {
				this.snapshotState[i]?.RunOnFetchFromSnapshot();
			}
		}
	}
}

//*/