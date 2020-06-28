using System;

namespace SimpleDI
{
	public abstract class DependencyLayer
	{
		// TODO: Idea: ImmutableDict type layer will throw an exception that has a method
		// to allow recovery to the current stack level
		// TODO: Should Dependencies overall also do this?

		/// <summary>
		/// Fallbacks are used to increase the search space, but will not be modified in any way.
		/// </summary>
		public DependencyLayer Fallback { get; }

		protected bool Disposed { get; private set; } = false;

		protected int StackLevel { get; }

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
		///	<see langword="[Call inside using()]"></see>
		///	
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		/// <exception cref="DependencyNotFoundException">
		/// No dependency against type <typeparamref name="T"/> is available.
		/// </exception>
		public FetchFrame Get<T>(out T dependency, bool useFallbacks)
		{
			FetchFrame result = TryGet(out dependency, out bool found, useFallbacks);
			if (!found) throw new DependencyNotFoundException(typeof(T));
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public FetchFrame GetOrNull<T>(out T dependency, bool useFallbacks)
			where T : class
		{
			FetchFrame result = TryGet(out dependency, out bool found, useFallbacks);
			if (!found) dependency = null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T (not nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryGet{T}(out T, out bool, bool)"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public FetchFrame GetOrNull<T>(out T? dependency, bool useFallbacks)
			where T : struct
		{
			FetchFrame result = TryGet(out T dep, out bool found, useFallbacks);
			dependency = found ? dep : (T?)null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T? (nullable), and returns it (or else null) via a nullable T? parameter
		/// <para/>
		/// See <see cref="TryGet{T}(out T, out bool, bool)"/>
		/// </summary>
		/// <remarks>
		/// Note that null nullable instances (eg new int?()) are boxed to true null pointers (and then treated
		/// as blocking the visibility of dependencies further out) - so searching for TOuter? doesn't introduce
		/// two different types of null values or anything.
		/// </remarks>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public FetchFrame GetNullableOrNull<T>(out T? dependency, bool useFallbacks)
			 where T : struct
		{
			FetchFrame result = TryGet(out dependency, out bool found, useFallbacks);
			if (!found) dependency = null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <param name="found"></param>
		/// <returns></returns>
		public FetchFrame TryGet<T>(out T dependency, out bool found, bool useFallbacks)
		{
			if (!this.StealthTryGet(
				out dependency,
				out int stackLevel,
				useFallbacks,
				out var layerFoundIn)
			) {
				found = false;
				return FetchFrame.CleanupFree;
			}

			found = true;

			if (typeof(T).IsValueType) return FetchFrame.CleanupFree;

			// If the dependency is a reference-type (and was found), then need to add to _fetchRecords so that the
			// dependency can look up what dependencies were available when it was originally injected.
			// Must only add to/modify the current layer's records (as we must only modify the current layer in general).
			this.AddToFetchRecord(dependency, layerFoundIn, stackLevel, out var prevFetch);

			return new FetchFrame(layerSearchingFrom: this, dependency, prevFetch);
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or throws a <see cref="DependencyNotFoundException"/> if it could not be found.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
		/// </summary>
		/// <typeparam name="TOuter"></typeparam>
		/// <param name="self"></param>
		/// <param name="outerDependency"></param>
		/// <returns></returns>
		/// <exception cref="DependencyNotFoundException">
		/// No dependency against type <typeparamref name="TOuter"/> was available when <paramref name="self"/> was injected
		/// </exception>
		/// <exception cref="ArgumentNullException"><paramref name="self"/> is null</exception>
		/// <exception cref="ArgumentTypeException"><paramref name="self"/> is an instance of a value-type</exception>
		/// <exception cref="ArgumentException">
		/// There is no record of <paramref name="self"/> having been fetched previously
		/// </exception>
		public FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
		{
			FetchFrame result = TryGetOuter(self, out outerDependency, out bool found, useFallbacks);
			if (!found) throw new DependencyNotFoundException(typeof(TOuter));
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or returns null if it could not be found.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
		/// </summary>
		/// <typeparam name="TOuter"></typeparam>
		/// <param name="self"></param>
		/// <param name="outerDependency"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"><paramref name="self"/> is null</exception>
		/// <exception cref="ArgumentTypeException"><paramref name="self"/> is an instance of a value-type</exception>
		/// <exception cref="ArgumentException">
		/// There is no record of <paramref name="self"/> having been fetched previously
		/// </exception>
		public FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
			where TOuter : class
		{
			FetchFrame result = TryGetOuter(self, out outerDependency, out bool found, useFallbacks);
			if (!found) outerDependency = null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency of type T (not nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
		/// </summary>
		/// <typeparam name="TOuter"></typeparam>
		/// <param name="self"></param>
		/// <param name="outerDependency"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"><paramref name="self"/> is null</exception>
		/// <exception cref="ArgumentTypeException"><paramref name="self"/> is an instance of a value-type</exception>
		/// <exception cref="ArgumentException">
		/// There is no record of <paramref name="self"/> having been fetched previously
		/// </exception>
		public FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
			where TOuter : struct
		{
			FetchFrame result = TryGetOuter(self, out TOuter oDep, out bool found, useFallbacks);
			outerDependency = found ? oDep : (TOuter?)null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency of type T? (nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
		/// </summary>
		/// <remarks>
		/// Note that null nullable instances (eg new int?()) are boxed to true null pointers (and then treated
		/// as blocking the visibility of dependencies further out) - so searching for TOuter? doesn't introduce
		/// two different types of null values or anything.
		/// </remarks>
		/// <typeparam name="TOuter"></typeparam>
		/// <param name="self"></param>
		/// <param name="outerDependency"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"><paramref name="self"/> is null</exception>
		/// <exception cref="ArgumentTypeException"><paramref name="self"/> is an instance of a value-type</exception>
		/// <exception cref="ArgumentException">
		/// There is no record of <paramref name="self"/> having been fetched previously
		/// </exception>
		public FetchFrame GetOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
			 where TOuter : struct
		{
			FetchFrame result = TryGetOuter(self, out outerDependency, out bool found, useFallbacks);
			if (!found) outerDependency = null;
			return result;
		}

		/// <summary>
		///	<see langword="[Call inside using()]"></see>
		/// Once a dependency has been retrieved, it may call this method to find
		/// dependencies that were in place when it was originally injected.
		/// </summary>
		/// <remarks>
		/// If the same depencency object has been injected multiple times,
		/// the most recently fetched injection will be used.
		/// <para/>
		/// Only reference-type dependencies may make use of this method, as reference-equality is used
		/// to perform the lookup.
		/// </remarks>
		/// <typeparam name="TOuter">The type of outer dependency to fetch</typeparam>
		/// <param name="self"></param>
		/// <param name="outerDependency"></param>
		/// <param name="found"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"><paramref name="self"/> is null</exception>
		/// <exception cref="ArgumentTypeException"><paramref name="self"/> is an instance of a value-type</exception>
		/// <exception cref="ArgumentException">
		/// There is no record of <paramref name="self"/> having been fetched previously
		/// </exception>
		public FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found, bool useFallbacks)
		{
			if (!this.StealthTryGetOuter(
				self,
				out outerDependency,
				out int outerStackLevel,
				useFallbacks,
				out var layerOuterFoundIn
			)) {
				outerDependency = default;
				found = false;
				return FetchFrame.CleanupFree;
			}

			found = true;

			// Now add to the current layer's _fetchRecords that the outer dependency was just fetched
			this.AddToFetchRecord(outerDependency, layerOuterFoundIn, outerStackLevel, out FetchRecord prevOuterFetch);

			return new FetchFrame(layerSearchingFrom: this, outerDependency, prevOuterFetch);
		}



		internal void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			if (frame.layerSearchingFrom != this) throw new InjectFrameCloseException(
				$"Cannot close fetch frame as it does not belong to the current dependency layer " +
				$"(current layer = '{this}', " +
				$"{nameof(frame)}.{nameof(FetchFrame.layerSearchingFrom)} = '{frame.layerSearchingFrom}')"
			);

			CloseFetchedDependency(frame.dependency, frame.prevFetch);
		}

		internal void CloseFetchFrame(MultiFetchFrame multiFrame)
		{
			if (multiFrame.IsCleanupFree) return;

			if (multiFrame.layerSearchingFrom != this) throw new InjectFrameCloseException(
				$"Cannot close fetch frame as it does not belong to the current dependency layer " +
				$"(current layer = '{this}', " +
				$"{nameof(multiFrame)}.{nameof(FetchFrame.layerSearchingFrom)} = '{multiFrame.layerSearchingFrom}')"
			);

			foreach (FetchFrame f in multiFrame.frames)
			{
				CloseFetchedDependency(f.dependency, f.prevFetch);
			}
		}



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

		protected abstract InjectFrame InjectInternal(object dependency, Type toMatchAgainst);

		public abstract SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst);
		public abstract SimultaneousInjectFrame InjectWild<T>(T dependency);

		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously<T>(SimultaneousInjectFrame soFar, T dependency, bool isWildcard);
		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously(SimultaneousInjectFrame soFar, object dependency, Type toMatchAgainst, bool isWildcard);

		private protected abstract bool StealthTryGet<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);
		private protected abstract bool StealthTryGetOuter<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);

		protected static bool StealthTryGet<T>(DependencyLayer @this, out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryGet(out dependency, out stackLevel, useFallbacks, out layerFoundIn);

		protected static bool StealthTryGetOuter<TOuter>(DependencyLayer @this, object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryGetOuter(self, out dependency, out stackLevel, useFallbacks, out layerFoundIn);

		private protected abstract void AddToFetchRecord(object dependency, DependencyLayer layerFoundIn, int stackLevelFoundAt, out FetchRecord prevFetch);

		internal abstract void CloseInjectFrame(InjectFrame frame);
		internal abstract void CloseInjectFrame(SimultaneousInjectFrame frame);

		private protected abstract void CloseFetchedDependency(object dependency, FetchRecord prevFetch);

		protected static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentTypeException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}
	}
}