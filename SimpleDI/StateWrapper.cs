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
	public abstract class StateWrapper
	{
		internal StateWrapper() { }

		internal abstract void RunOnInject();
		internal abstract void RunOnFetch();
		internal abstract ISnapshotStateWrapper RunOnSnapshot();

		public static StateWrapper<TInj, TSnap> Wrap<TInj, TSnap>(IStatefulDependency<TInj, TSnap> dependency)
			=> new StateWrapper<TInj, TSnap>(dependency);

		public static SelfWrapper[] PrepareWrap(IStatefulDependency dependency)
		{
			if (!typeof(IStatefulDependency<,>).IsAssignableFrom(dependency.GetType())) return null;

			Type[] stateInterfaces = dependency.GetType().FindInterfaces(
				(t, _) => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IStatefulDependency<,>),
				null
			);

			SelfWrapper[] delegates = new SelfWrapper[stateInterfaces.Length];

			for (int i = 0; i < stateInterfaces.Length; i++) {
				delegates[i] = (SelfWrapper)(
					stateInterfaces[i]
					.GetMethod(nameof(IStatefulDependency.WrapSelf))
					.CreateDelegate(typeof(SelfWrapper), dependency)
				);
			}

			return delegates;
		}

		/// <summary>
		/// Used to implement <see cref="IStatefulDependency"/> - see remarks for example.
		/// </summary>
		/// <remarks>
		/// Example usage:
		/// <code>
		/// public abstract class StatefulDependency : IStatefulDependency
		/// {
		/// 	private SelfWrapper[] wrapperDelegates = null;
		/// 
		/// 	StateWrapper[] IStatefulDependency.WrapSelf()
		/// 		=> StateWrapper.HelpWrapSelf(this, ref this.wrapperDelegates);
		/// }
		/// </code>
		/// </remarks>
		public static StateWrapper[] HelpWrapSelf(IStatefulDependency self, ref SelfWrapper[] wrapperDelegates)
		{
			wrapperDelegates = wrapperDelegates ?? PrepareWrap(self);

			var wrapped = new StateWrapper[wrapperDelegates.Length];
			for (int i = 0; i < wrapperDelegates.Length; i++) {
				wrapped[i] = wrapperDelegates[i]();
			}
			return wrapped;
		}
	}
	
	public class StateWrapper<TInj, TSnap> : StateWrapper
	{
		private protected readonly IStatefulDependency<TInj, TSnap> dependency;
		private protected TInj injectState;

		private protected StateWrapper(IStatefulDependency<TInj, TSnap> dependency, TInj injectState) {
			this.dependency = dependency;
			this.injectState = injectState;
		}

		internal StateWrapper(IStatefulDependency<TInj, TSnap> dependency)
			: this(dependency, default) { }

		internal override void RunOnInject()
		{
			if (injectState != null) throw new InvalidDIStateException(
				"Stored inject state is already non-null - cannot run OnInject again."
			);

			injectState = dependency.OnInject();
		}

		internal override void RunOnFetch()
			=> dependency.OnFetch(injectState);

		internal override ISnapshotStateWrapper RunOnSnapshot()
			=> new SnapshotStateWrapper<TInj, TSnap>(dependency, injectState, dependency.OnSnapshot(injectState));
	}
}

//*/