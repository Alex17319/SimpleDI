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
	internal interface ISnapshotStateWrapper : IStateWrapper
	{
		void RunOnFetchFromSnapshot();
	}

	// Needs to be a separate class/object from StateWrapper as multiple snapshots
	// may be taken of a single injected dependency, i.e. RunOnSnapshot may be called many times, &
	// all resulting state needs to be handled separately.
	// This is made a subtype of StateWrapper for performance benefits - if a new dependency layer is created
	// based on a snapshot (eg. when starting a new thread), then OnInject shouldn't be run (the layer needs
	// to keep the inject state from before, not record a new state), and so we can reuse the same instance
	// of the SnapshotStateWrapper as a StateWrapper in the new layer. It's then just as mostly-immutable as
	// a normal instance of StateWrapper, while the snapshot state part is definitely immutable and ignored
	// anyway.
	public sealed class SnapshotStateWrapper<TInj, TSnap> : StateWrapper<TInj, TSnap>, ISnapshotStateWrapper
	{
		private readonly TSnap snapshotState;

		internal SnapshotStateWrapper(IStatefulDependency<TInj, TSnap> dependency, TInj injectState, TSnap snapshotState)
			: base(dependency, injectState)
			=> this.snapshotState = snapshotState;

		void ISnapshotStateWrapper.RunOnFetchFromSnapshot()
			=> dependency.OnFetchFromSnapshot(injectState, snapshotState);

		void IStateWrapper.RunOnInject() => RunOnInject();
		void IStateWrapper.RunOnFetch() => RunOnFetch();
		ISnapshotStateWrapper IStateWrapper.RunOnSnapshot() => RunOnSnapshot();
	}
}

//*/