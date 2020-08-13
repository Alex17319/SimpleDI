using System;
using System.Collections;
using System.Collections.Concurrent;
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
	internal interface IStateWrapper
	{
		void RunOnInject();
		void RunOnFetch();
		ISnapshotStateWrapper RunOnSnapshot();
	}

	public abstract class StateWrapper : IStateWrapper
	{
		// TODO: Test that this doesn't lock when reading, & so won't slow down other threads too much (except when still
		// loading stuff up)
		// Otherwise may need to use something threadstatic but that'll add duplicate data & keep getting
		// cleared if someone makes & kills lots of threads
		private static readonly ConcurrentDictionary<Type, IWrapperBuilder[]> wrapperBuilders = new ConcurrentDictionary<Type, IWrapperBuilder[]>();

		internal StateWrapper() { }

		internal abstract void RunOnInject();
		internal abstract void RunOnFetch();
		internal abstract ISnapshotStateWrapper RunOnSnapshot();

		void IStateWrapper.RunOnInject() => RunOnInject();
		void IStateWrapper.RunOnFetch() => RunOnFetch();
		ISnapshotStateWrapper IStateWrapper.RunOnSnapshot() => RunOnSnapshot();

		internal static IStateWrapper[] WrapDependencyState(object dependency)
		{
			if (dependency == null || !(dependency is IStatefulDependency statefulDep)) return null;

			Type dType = dependency.GetType();
			if (!wrapperBuilders.TryGetValue(dType, out IWrapperBuilder[] builders)) {
				// Fine to do separate calls (in regard to multithreading) - if another thread adds an entry
				// for the same type in-between, then the second call will just have no effect, which is fine.
				wrapperBuilders.TryAdd(dType, MakeWrapperBuilders(statefulDep));
			}

			var wrapped = new IStateWrapper[builders.Length];
			for (int i = 0; i < builders.Length; i++) {
				wrapped[i] = builders[i].Wrap(statefulDep);
			}

			// TODO: See if we should remove any nulls here & copy to a smaller array to save space,
			// or if that's worse anyway with reallocation. Maybe if there's more nulls than some threshold?

			return wrapped;
		}

		private static IWrapperBuilder[] MakeWrapperBuilders(IStatefulDependency dependency)
		{
			if (!typeof(IStatefulDependency<,>).IsAssignableFrom(dependency.GetType())) return null;

			Type[] stateInterfaces = dependency.GetType().FindInterfaces(
				(t, _) => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IStatefulDependency<,>),
				null
			);

			IWrapperBuilder[] builders = new IWrapperBuilder[stateInterfaces.Length];

			for (int i = 0; i < stateInterfaces.Length; i++) {
				Type[] genericArgs = stateInterfaces[i].GenericTypeArguments;
				builders[i] = (IWrapperBuilder)(
					typeof(WrapperBuilder<,>)
					.MakeGenericType(genericArgs)
					.GetConstructor(Type.EmptyTypes)
					.Invoke(null)
				);
			}

			return builders;
		}

		private interface IWrapperBuilder {
			//Note: Accessed via reflection
			IStateWrapper Wrap(IStatefulDependency dep);
		}

		private class WrapperBuilder<TInj, TSnap> : IWrapperBuilder {
			//Note: Accessed via reflection
			public IStateWrapper Wrap(IStatefulDependency dep)
				=> new StateWrapper<TInj, TSnap>((IStatefulDependency<TInj, TSnap>)dep);
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