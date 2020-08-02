using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDI
{
	//	interface IStatefulDependency
	//	{
	//		/// <summary>
	//		/// Returns an object containing any needed state which will be passed to the
	//		/// <see cref="OnFetch(object)"/>, <see cref="OnSnapshot(object)"/>,
	//		/// and <see cref="OnFetchFromSnapshot(object, object)"/> methods as 'injectState'.
	//		/// </summary>
	//		object OnInject();
	//	
	//		void OnFetch(object injectState);
	//	
	//		/// <summary>
	//		/// Returns an object containing any needed state which will be passed to the
	//		/// <see cref="OnFetchFromSnapshot(object)"/> method as 'snapshotState'.
	//		/// </summary>
	//		object OnSnapshot(object injectState);
	//	
	//		void OnFetchFromSnapshot(object injectState, object snapshotState);
	//	}

	interface IStatefulDependency<TInj, TSnap> // : IStatefulDependency
	{
		// Note: Must not add overloads (reflection has been used with only the method names searched for)

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