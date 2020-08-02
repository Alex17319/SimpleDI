using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDI
{
	public delegate StateWrapper SelfWrapper();

	public interface IStatefulDependency
	{
		// Note: Must not add overloads (reflection is used/may be used with only the method names searched for)

		/// <summary>
		/// Should be implemented using <see cref="StateWrapper.HelpWrapSelf(IStatefulDependency, ref SelfWrapper[])"/>.
		/// </summary>
		StateWrapper[] WrapSelf();
	}

	public interface IStatefulDependency<TInj, TSnap> // : IStatefulDependency
	{
		// Note: Must not add overloads (reflection is used/may be used with only the method names searched for)

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

	public abstract class StatefulDependency : IStatefulDependency
	{
		private SelfWrapper[] wrapperDelegates = null;

		StateWrapper[] IStatefulDependency.WrapSelf()
			=> StateWrapper.HelpWrapSelf(this, ref this.wrapperDelegates);
	}
}

//*/