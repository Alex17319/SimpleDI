using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDI
{
	interface IStatefulDependency<TInj, TSnap>
	{
		/// <summary>
		/// Returns an object containing any needed state which will be passed to the
		/// <see cref="OnFetch(TInj)"/>, <see cref="OnSnapshot(TInj)"/>,
		/// and <see cref="OnFetchFromSnapshot(TInj, TSnap)"/> methods as 'injectState'.
		/// </summary>
		TInj OnInject();

		void OnFetch(TInj injectState);

		/// <summary>
		/// Returns an object containing any needed state which will be passed to the
		/// <see cref="OnFetchFromSnapshot(TSnap)"/> method as 'snapshotState'.
		/// </summary>
		TSnap OnSnapshot(TInj injectState);

		void OnFetchFromSnapshot(TInj injectState, TSnap snapshotState);
	}
}

//*/