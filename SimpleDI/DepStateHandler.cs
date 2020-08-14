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
	internal abstract class DepStateHandler {
		public abstract object OnInject(object dep);
		public abstract void OnFetch(object dep, object injectState);
		public abstract object OnSnapshot(object dep, object injectState);
		public abstract void OnFetchFromSnapshot(object dep, object injectState, object snapshotState);

		private DepStateHandler() { } // Disable external inheritance (while inherited by nested class)

		// TODO: Test that this doesn't lock when reading, & so won't slow down other threads too much (except when still
		// loading stuff up)
		// Otherwise may need to use something threadstatic but that'll add duplicate data & keep getting
		// cleared if someone makes & kills lots of threads
		private static readonly ConcurrentDictionary<Type, DepStateHandler[]> wrapperBuilders = new ConcurrentDictionary<Type, DepStateHandler[]>();

		internal static DepStateHandler[] LookupHandlersFor(object dependency)
		{
			if (dependency == null || !(dependency is IStatefulDependency statefulDep)) return null;

			Type dType = dependency.GetType();
			if (wrapperBuilders.TryGetValue(dType, out DepStateHandler[] handlers)) {
				return handlers;
			}

			// Fine to do separate calls (in regard to multithreading) - if another thread adds an entry
			// for the same type in-between, then the second call will just have no effect, which is fine.
			handlers = MakeHandlers(statefulDep);
			wrapperBuilders.TryAdd(dType, handlers);
			return handlers;
		}

		private static DepStateHandler[] MakeHandlers(IStatefulDependency dependency)
		{
			if (!typeof(IStatefulDependency<,>).IsAssignableFrom(dependency.GetType())) return null;

			Type[] stateInterfaces = dependency.GetType().FindInterfaces(
				(t, _) => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IStatefulDependency<,>),
				null
			);

			DepStateHandler[] builders = new DepStateHandler[stateInterfaces.Length];

			for (int i = 0; i < stateInterfaces.Length; i++) {
				Type[] genericArgs = stateInterfaces[i].GenericTypeArguments;
				builders[i] = (DepStateHandler)(
					typeof(StateHandlerInternal<,>)
					.MakeGenericType(genericArgs)
					.GetConstructor(Type.EmptyTypes)
					.Invoke(null)
				);
			}

			return builders;
		}

		private class StateHandlerInternal<TInj, TSnap> : DepStateHandler {
			//Note: Accessed via reflection
			public StateHandlerInternal() { }

			public override object OnInject(object dep)
				=> ((IStatefulDependency<TInj, TSnap>)dep).OnInject();

			public override void OnFetch(object dep, object injectState)
				=> ((IStatefulDependency<TInj, TSnap>)dep).OnFetch((TInj)injectState);

			public override object OnSnapshot(object dep, object injectState)
				=> ((IStatefulDependency<TInj, TSnap>)dep).OnSnapshot((TInj)injectState);

			public override void OnFetchFromSnapshot(object dep, object injectState, object snapshotState)
				=> ((IStatefulDependency<TInj, TSnap>)dep).OnFetchFromSnapshot((TInj)injectState, (TSnap)snapshotState);
		}
	}
}

//*/