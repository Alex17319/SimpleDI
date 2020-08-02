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
		
		public StackedDependency(int stackLevel, object dependency, StateWrapper[] injectState)
		{
			this.stackLevel = stackLevel;
			this.dependency = dependency;
			this.injectState = injectState;
		}

		internal void RunOnInject() {
			if (injectState == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.injectState[i].RunOnInject();
			}
		}
		
		internal void RunOnFetch() {
			if (injectState == null) return;

			for (int i = 0; i < this.injectState.Length; i++) {
				this.injectState[i].RunOnFetch();
			}
		}
		
		internal ISnapshotStateWrapper[] RunOnSnapshot() {
			if (injectState == null) return null;

			ISnapshotStateWrapper[] snapshotState = new ISnapshotStateWrapper[this.injectState.Length];
			for (int i = 0; i < this.injectState.Length; i++) {
				snapshotState[i] = this.injectState[i].RunOnSnapshot();
			}
			return snapshotState;
		}
	}
}

//*/