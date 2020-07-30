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

		public abstract bool SnapshotReady { get; }

		public abstract DependencySnapshot Snapshot { get; }

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


		private protected abstract bool TryGetFromFetchRecords(object self, out FetchRecord mostRecentFetch);

		private protected abstract bool StealthTryFetch<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);

		protected static bool StealthTryFetch<T>(DependencyLayer @this, out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryFetch(out dependency, out stackLevel, useFallbacks, out layerFoundIn);

		/// <summary>
		/// Given that a previous fetch found some dependency in the current layer at a particular stack level,
		/// this method looks for dependencies in outer stack levels (stricly not equal), and optionally
		/// fallback layers as well.
		/// </summary>
		private protected abstract bool StealthTryFetchOuter<TOuter>(int prevFetchStackLevelFoundAt, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);

		private protected abstract void AddToFetchRecord(object dependency, DependencyLayer layerFoundIn, int stackLevelFoundAt, out FetchRecord prevFetch);



		internal abstract void CloseInjectFrame(InjectFrame frame);
		internal abstract void CloseInjectFrame(SimultaneousInjectFrame frame);

		private protected abstract void CloseFetchedDependency(FetchFrame frame);


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
		public FetchFrame Fetch<T>(out T dependency, bool useFallbacks)
		{
			FetchFrame result = TryFetch(out dependency, out bool found, useFallbacks);
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
		public FetchFrame FetchOrNull<T>(out T dependency, bool useFallbacks)
			where T : class
		{
			FetchFrame result = TryFetch(out dependency, out bool found, useFallbacks);
			if (!found) dependency = null;
			return result;
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
		public FetchFrame FetchOrNull<T>(out T? dependency, bool useFallbacks)
			where T : struct
		{
			FetchFrame result = TryFetch(out T dep, out bool found, useFallbacks);
			dependency = found ? dep : (T?)null;
			return result;
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
		public FetchFrame FetchNullableOrNull<T>(out T? dependency, bool useFallbacks)
			 where T : struct
		{
			FetchFrame result = TryFetch(out dependency, out bool found, useFallbacks);
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
		public FetchFrame TryFetch<T>(out T dependency, out bool found, bool useFallbacks)
		{
			int stackLevelBeforeFetch = this.CurrentStackLevel;

			if (!this.StealthTryFetch(
				out dependency,
				out int stackLevel,
				useFallbacks,
				out var layerFoundIn)
			) {
				found = false;
				return FetchFrame.CleanupFree;
			}

			// Fail if a null has been added to hide earlier dependencies
			// TODO: Should it be possible to hide non-nullable value type dependencies?? Currently isn't
			if (dependency == null) {
				found = false;
				return FetchFrame.CleanupFree;
			}

			found = true;

			if (typeof(T).IsValueType) return FetchFrame.CleanupFree;

			// If the dependency is a reference-type (and was found), then need to add to _fetchRecords so that the
			// dependency can look up what dependencies were available when it was originally injected.
			// Must only add to/modify the current layer's records (as we must only modify the current layer in general).
			this.AddToFetchRecord(dependency, layerFoundIn, stackLevel, out var prevFetch);

			return new FetchFrame(layerSearchingFrom: this, dependency, prevFetch, stackLevelBeforeFetch);
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or throws a <see cref="DependencyNotFoundException"/> if it could not be found.
		/// <para/>
		/// See <see cref="TryFetchOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
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
		public FetchFrame FetchOuter<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
		{
			FetchFrame result = TryFetchOuter(self, out outerDependency, out bool found, useFallbacks);
			if (!found) throw new DependencyNotFoundException(typeof(TOuter));
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or returns null if it could not be found.
		/// <para/>
		/// See <see cref="TryFetchOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
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
		public FetchFrame FetchOuterOrNull<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
			where TOuter : class
		{
			FetchFrame result = TryFetchOuter(self, out outerDependency, out bool found, useFallbacks);
			if (!found) outerDependency = null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency of type T (not nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryFetchOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
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
		public FetchFrame FetchOuterOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
			where TOuter : struct
		{
			FetchFrame result = TryFetchOuter(self, out TOuter oDep, out bool found, useFallbacks);
			outerDependency = found ? oDep : (TOuter?)null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency of type T? (nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryFetchOuter{TOuter}(object, out TOuter, out bool, bool)"/>.
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
		public FetchFrame FetchOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
			 where TOuter : struct
		{
			FetchFrame result = TryFetchOuter(self, out outerDependency, out bool found, useFallbacks);
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
		public FetchFrame TryFetchOuter<TOuter>(object self, out TOuter outerDependency, out bool found, bool useFallbacks)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));

			if (self.GetType().IsValueType) throw new ArgumentTypeException(
				$"Only reference-type dependencies may fetch outer dependencies from when they were injected. " +
				$"Object '{self}' is of type '{self.GetType().FullName}', which is a value-type.",
				nameof(self)
			);

			int stackLevelBeforeFetch = this.CurrentStackLevel;

			if (!this.tryFetchOuterInternal(
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

			// Fail if a null has been added to hide earlier dependencies
			// TODO: Should it be possible to hide non-nullable value type dependencies?? Currently isn't
			if (outerDependency == null) {
				found = false;
				return FetchFrame.CleanupFree;
			}

			found = true;

			// Now add to the current layer's _fetchRecords that the outer dependency was just fetched
			this.AddToFetchRecord(outerDependency, layerOuterFoundIn, outerStackLevel, out FetchRecord prevOuterFetch);

			return new FetchFrame(layerSearchingFrom: this, outerDependency, prevOuterFetch, stackLevelBeforeFetch);
		}

		private bool tryFetchOuterInternal<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			// Try to find a fetch record locally
			// If we can't, then try to use fallbacks if possible, calling the current method recursively, and return
			if (!this.TryGetFromFetchRecords(self, out FetchRecord mostRecentFetch))
			{
				if (!useFallbacks || this.Fallback == null) throw new ArgumentException(
					$"No record is available of a dependency fetch having been performed for object '{self}' " +
					$"(of type '{self.GetType().FullName}'). " +
					$"Depending on how this occurred (incorrect call or invalid state), continued operation may be undefined."
				);

				return Logic.SucceedIf(this.Fallback.tryFetchOuterInternal(
					self,
					out dependency,
					out stackLevel,
					useFallbacks,
					out layerFoundIn
				));
			}
			
			// The fetch record must have been found locally if this point is reached
			// If it was in a fallback, then that was found using this method recursively, and we've already returned.
			
			// However, that isn't the same thing as it representing a local dependency having been fetched,
			// just that the record is stored locally. Either way, go to the layer from which the dependency
			// was fetched, and from there search for outer dependencies
			return Logic.SucceedIf(mostRecentFetch.layerFoundAt.StealthTryFetchOuter(
				mostRecentFetch.stackLevelFoundAt,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			));
		}



		internal void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			if (frame.layerSearchingFrom != this) throw new InjectFrameCloseException(
				$"Cannot close fetch frame as it does not belong to the current dependency layer " +
				$"(current layer = '{this}', " +
				$"{nameof(frame)}.{nameof(FetchFrame.layerSearchingFrom)} = '{frame.layerSearchingFrom}')"
			);

			CloseFetchedDependency(frame);
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
				CloseFetchedDependency(f);
			}
		}



		protected static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentTypeException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}
	}
}