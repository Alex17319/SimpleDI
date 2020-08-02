using SimpleDI.TryGet;
using System;

namespace SimpleDI
{
	public abstract class DependencyLayer
	{
		// ---- TODO ----
		// Work out how useful fetching outer dependencies is in practise if dependencies are to be stored
		// for later, & how it might work with snapshots - is it worth including for snapshots, given it wouldn't
		// be possible when they're stored normally? Should there be some other single-dependency-with-fetch-record
		// storage mechanism?
		// The two ideas, snapshots and outer dependencies, seem to be pretty incompatible atm - even just storing a
		// dependency for later won't work for fetching outer dependencies.
		
		
		// TODO: Idea: ImmutableDict type layer will throw an exception that has a method
		// to allow recovery to the current stack level
		// TODO: Should Dependencies overall also do this?
		// TODO: MutatingDependencyLayer is also possible to recover, by enumerating through
		// the dictionary and for each stack removing all entries with too high a stack level.
		// Nope, only the dependencies can be recovered, not the fetch record.

		// TODO: Plan for snapshots
		// Safe dependency layers can easily be snapshotted, while mutating ones will use a lazy initialised/dirtied
		// snapshot - no cost if no snapshots occur, no extra cost if multiple snapshots taken with no mutation.
		// Snapshot should be an immutable dict (or a type that wraps & exposes an immutable dict) so that
		// new safe layers can check if a snapshot is available, and then use it as a starting point.

		/// <summary>
		/// Fallbacks are used to increase the search space, but will not be modified in any way.
		/// </summary>
		public DependencyLayer Fallback { get; }

		//public abstract bool SnapshotReady { get; }

		//public abstract DependencySnapshot Snapshot { get; }

		protected bool Disposed { get; private set; } = false;

		protected abstract int CurrentStackLevel { get; }

		internal DependencyLayer()
		{
			this.Fallback = null;
		}

		internal DependencyLayer(DependencyLayer fallback)
		{
			this.Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
		}

		protected virtual void Dispose()
		{
			if (Disposed) return;
			Disposed = true;

			Dependencies.CloseLayer(this);
		}

		internal void MarkDisposed()
		{
			Disposed = true;
		}

		// Used to prevent people from randomly calling Dependencies.CurrentLayer.Dispose().
		// Instead of DependencyLayer implementing IDisposable, these structs implement it,
		// and pass on the calls to DependencyLayer. External code can't make a proper
		// instance of these structs in order to call Dispose() (except via reflection)
		public struct Disposer : IDisposable
		{
			public DependencyLayer Layer { get; }

			public bool IsNull => Layer == null;
			public static readonly Disposer Null = default;

			internal Disposer(DependencyLayer layer) => this.Layer = layer;

			//	public static Disposer FromGeneric(Disposer<DependencyLayer> disposer) => new Disposer(disposer.Layer);
			//	public static Disposer<DependencyLayer> ToGeneric(Disposer disposer) => new Disposer<DependencyLayer>(disposer.Layer);

			public void Dispose() => this.Layer.Dispose();
		}

		//	public struct Disposer<T> : IDisposable
		//		where T : DependencyLayer
		//	{
		//		public T Layer { get; }
		//	
		//		public bool IsNull => Layer == null;
		//		public static readonly Disposer<T> Null = default;
		//	
		//		internal Disposer(T layer) => this.Layer = layer;
		//	
		//		public void Dispose() => this.Layer.Dispose();
		//	}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Injects an object that will be returned for all dependency searches
		/// for any supertype of <paramref name="toMatchAgainst"/>
		/// </summary>
		/// <remarks>
		/// Currently not that efficient - effectively just calls Inject() for every supertype of toMatchAgainst,
		/// and returns a DependencyFrame that holds all of these types;
		/// </remarks>
		/// <param name="dependency">The depencency to add. May be null (to block existing dependencies from being accessed)</param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public abstract SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public abstract SimultaneousInjectFrame InjectWild<T>(T dependency);

		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously<T>(SimultaneousInjectFrame soFar, T dependency, bool isWildcard);
		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously(SimultaneousInjectFrame soFar, object dependency, Type toMatchAgainst, bool isWildcard);

		protected abstract InjectFrame InjectInternal(object dependency, Type toMatchAgainst);


		private protected abstract bool StealthTryFetch<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);

		protected static bool StealthTryFetch<T>(DependencyLayer @this, out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryFetch(out dependency, out stackLevel, useFallbacks, out layerFoundIn);



		internal abstract void CloseInjectFrame(InjectFrame frame);
		internal abstract void CloseInjectFrame(SimultaneousInjectFrame frame);

		//	private protected abstract void CloseFetchedDependency(FetchFrame frame);


		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public InjectFrame Inject<T>(T dependency)
		{
			return InjectInternal(dependency, typeof(T));
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public InjectFrame Inject(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			RequireDependencySubtypeOf(dependency, toMatchAgainst);

			return InjectInternal(dependency, toMatchAgainst);
		}


		public SimultaneousInjectFrame BeginSimultaneousInject()
		{
			return new SimultaneousInjectFrame(layer: this);
		}



		/// <summary>
		///	<see langword="[Call inside using()]"></see>
		///	
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		/// <exception cref="DependencyNotFoundException">
		/// No dependency against type <typeparamref name="T"/> is available.
		/// </exception>
		public void Fetch<T>(out T dependency, bool useFallbacks)
		{
			TryFetch(out dependency, out bool found, useFallbacks);
			if (!found) throw new DependencyNotFoundException(typeof(T));
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public void FetchOrNull<T>(out T dependency, bool useFallbacks)
			where T : class
		{
			TryFetch(out dependency, out bool found, useFallbacks);
			if (!found) dependency = null;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T (not nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryFetch{T}(out T, out bool, bool)"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public void FetchOrNull<T>(out T? dependency, bool useFallbacks)
			where T : struct
		{
			TryFetch(out T dep, out bool found, useFallbacks);
			dependency = found ? dep : (T?)null;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T? (nullable), and returns it (or else null) via a nullable T? parameter
		/// <para/>
		/// See <see cref="TryFetch{T}(out T, out bool, bool)"/>
		/// </summary>
		/// <remarks>
		/// Note that null nullable instances (eg new int?()) are boxed to true null pointers (and then treated
		/// as blocking the visibility of dependencies further out) - so searching for TOuter? doesn't introduce
		/// two different types of null values or anything.
		/// </remarks>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public void FetchNullableOrNull<T>(out T? dependency, bool useFallbacks)
			 where T : struct
		{
			TryFetch(out dependency, out bool found, useFallbacks);
			if (!found) dependency = null;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <param name="found"></param>
		/// <returns></returns>
		public void TryFetch<T>(out T dependency, out bool found, bool useFallbacks)
		{
			int stackLevelBeforeFetch = this.CurrentStackLevel;

			if (!this.StealthTryFetch(
				out dependency,
				out int stackLevel,
				useFallbacks,
				out var layerFoundIn)
			) {
				found = false;
				return;
			}

			// Fail if a null has been added to hide earlier dependencies
			// TODO: Should it be possible to hide non-nullable value type dependencies?? Currently isn't
			if (dependency == null) {
				found = false;
				return;
			}

			found = true;
		}



		protected static object[] RunOnInject(object dependency)
		{
			Type[] stateInterfaces = GetStatefulDependencyInterfaces(dependency.GetType());
			if (stateInterfaces == null) return null;

			object[] injectState = new object[stateInterfaces.Length];
			for (int i = 0; i < stateInterfaces.Length; i++) {
				injectState[i] = stateInterfaces[i].GetMethod("OnInject").Invoke(dependency, null);
				// Note: Fine to just search for "OnInject" without specifying parameters etc, as we're searching
				// in the interface, which we have full control over.
			}
			return injectState;
		}

		protected static void RunOnFetch(object dependency, object[] injectState)
		{
			if (injectState == null) return;

			Type[] stateInterfaces = GetStatefulDependencyInterfaces(dependency.GetType());

			VerifyArrayLengths(stateInterfaces?.Length ?? 0, injectState.Length, -1);

			for (int i = 0; i < stateInterfaces.Length; i++)
			{
				stateInterfaces[i].GetMethod("OnFetch").Invoke(dependency, new object[] { injectState[i] });
				// Note: Fine to just search for "OnFetch" without specifying parameters etc, as we're searching
				// in the interface, which we have full control over.
			}
		}

		protected static object[] RunOnSnapshot(object dependency, object[] injectState)
		{
			if (injectState == null) return null;

			Type[] stateInterfaces = GetStatefulDependencyInterfaces(dependency.GetType());

			VerifyArrayLengths(stateInterfaces?.Length ?? 0, injectState.Length, -1);

			object[] snapshotState = new object[stateInterfaces.Length];
			for (int i = 0; i < stateInterfaces.Length; i++) {
				snapshotState[i] = stateInterfaces[i].GetMethod("OnSnapshot").Invoke(dependency, new object[] { injectState[i] });
				// Note: Fine to just search for "OnSnapshot" without specifying parameters etc, as we're searching
				// in the interface, which we have full control over.
			}
			return snapshotState;
		}

		protected static void RunOnFetchFromSnapshot(object dependency, object[] injectState, object[] snapshotState)
		{
			if (injectState == null && snapshotState == null) return;

			Type[] stateInterfaces = GetStatefulDependencyInterfaces(dependency.GetType());

			VerifyArrayLengths(stateInterfaces?.Length ?? 0, injectState?.Length ?? 0, snapshotState?.Length ?? 0);

			for (int i = 0; i < stateInterfaces.Length; i++)
			{
				stateInterfaces[i].GetMethod("OnFetchFromSnapshot").Invoke(dependency, new object[] { injectState[i] });
				// Note: Fine to just search for "OnFetchFromSnapshot" without specifying parameters etc, as we're searching
				// in the interface, which we have full control over.
			}
		}

		/// <remarks>
		/// Note: use dependency.GetType(), not the type toMatchAgainst or anything,
		/// in order to allow dependency classes to make use of this without the code
		/// that injects them needing to do anything different.
		/// </remarks>
		private static Type[] GetStatefulDependencyInterfaces(Type dependencyType)
		{
			if (!typeof(IStatefulDependency<,>).IsAssignableFrom(dependencyType)) return null;

			return dependencyType.FindInterfaces(
				(t, _) => t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(IStatefulDependency<,>),
				null
			);
		}

		/// <remarks>
		/// Note: Use -1 for any parameter to indicate 'irrelevant'
		/// </remarks>
		private static void VerifyArrayLengths(int interfacesLen, int injectStateLen, int snapshotStateLen)
		{
			if (interfacesLen == 0 && injectStateLen > 0) throw new InvalidDIStateException(
				$"Some inject-state data is stored but the dependency object does not " +
				$"implement any variants of IStatefulDependency."
			);

			if (interfacesLen == 0 && snapshotStateLen > 0) throw new InvalidDIStateException(
				$"Some snapshot-state data is stored but the dependency object does not " +
				$"implement any variants of IStatefulDependency."
			);

			if (interfacesLen > 0 && injectStateLen == 0) throw new InvalidDIStateException(
				$"The dependency object implements some variants of IStatefulDependency " +
				$"but no inject-state data is stored."
			);

			if (interfacesLen > 0 && snapshotStateLen == 0) throw new InvalidDIStateException(
				$"The dependency object implements some variants of IStatefulDependency " +
				$"but no snapshot-state data is stored."
			);

			if (interfacesLen >= 0 && injectStateLen >= 0 && interfacesLen != injectStateLen) throw new InvalidDIStateException(
				$"Dependency object has different number of IStatefulDependency " +
				$"inteface variants than the number of stored pieces of inject-state data " +
				$"({interfacesLen} interface varaiants, {injectStateLen} pieces of state data)."
			);

			if (interfacesLen >= 0 && snapshotStateLen >= 0 && interfacesLen != snapshotStateLen) throw new InvalidDIStateException(
				$"Dependency object has different number of IStatefulDependency " +
				$"inteface variants than the number of stored pieces of snapshot-state data " +
				$"({interfacesLen} interface varaiants, {snapshotStateLen} pieces of state data)."
			);
		}


		//	internal void CloseFetchFrame(FetchFrame frame)
		//	{
		//		if (frame.IsCleanupFree) return;
		//	
		//		if (frame.layerSearchingFrom != this) throw new InjectFrameCloseException(
		//			$"Cannot close fetch frame as it does not belong to the current dependency layer " +
		//			$"(current layer = '{this}', " +
		//			$"{nameof(frame)}.{nameof(FetchFrame.layerSearchingFrom)} = '{frame.layerSearchingFrom}')"
		//		);
		//	
		//		CloseFetchedDependency(frame);
		//	}

		//	internal void CloseFetchFrame(MultiFetchFrame multiFrame)
		//	{
		//		if (multiFrame.IsCleanupFree) return;
		//	
		//		if (multiFrame.layerSearchingFrom != this) throw new InjectFrameCloseException(
		//			$"Cannot close fetch frame as it does not belong to the current dependency layer " +
		//			$"(current layer = '{this}', " +
		//			$"{nameof(multiFrame)}.{nameof(FetchFrame.layerSearchingFrom)} = '{multiFrame.layerSearchingFrom}')"
		//		);
		//	
		//		foreach (FetchFrame f in multiFrame.frames)
		//		{
		//			CloseFetchedDependency(f);
		//		}
		//	}



		protected static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentTypeException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}
	}
}