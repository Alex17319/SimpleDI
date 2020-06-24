using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	public class DependencyLayer
	{
		// For future improvement: Maybe try to implement wildcard dependencies more efficiently (wildcard = returned
		// when any parent class/interface is requested, rather than just when exactly the correct type is requested).
		// Most of the time I'm guessing wildcards won't be used though, so shouldn't sacrifice efficiency elsewhere.

		// Maps from a type to a stack of all dependencies matching that type that are available.
		// Depending on the use of strict (default) or wildcard insertion, dependencies of sub-types may or may not
		// also be returned
		// Each stack is always sorted in descending order by stackLevel (often with gaps), and may be
		// searched through with a binary search (the need for both binary search and stack operations resulted
		// in the SearchableStack<T> class)
		[ThreadStatic]
		private readonly Dictionary<Type, SearchableStack<StackedDependency>> _dependencyStacks
			= new Dictionary<Type, SearchableStack<StackedDependency>>();

		// Maps from a dependency that has been fetched to the stack level that it was originally injected at
		[ThreadStatic]
		private readonly Dictionary<object, FetchRecord> _fetchRecords
			= new Dictionary<object, FetchRecord>(new RefEqualityComparer());

		[ThreadStatic]
		private int stackLevel;

		/// <summary>
		/// Fallbacks are used to increase the search space, but will not be modified in any way.
		/// </summary>
		public DependencyLayer Fallback { get; }



		internal DependencyLayer(DependencyLayer fallback)
		{
			this.Fallback = fallback;
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
			addToStack_internal(dependency, typeof(T));

			return new InjectFrame(stackLevel++, typeof(T));
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

			addToStack_internal(dependency, toMatchAgainst);

			return new InjectFrame(stackLevel++, toMatchAgainst);
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame InjectWild<T>(T dependency)
			=> injectSimul_internal(ImmutableStack.Create<Type>(), dependency, typeof(T), isWildcard: true);

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
		public SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			RequireDependencySubtypeOf(dependency, toMatchAgainst);

			return injectSimul_internal(ImmutableStack.Create<Type>(), dependency, toMatchAgainst, isWildcard: true);
		}



		public SimultaneousInjectFrame BeginSimultaneousInject()
		{
			return new SimultaneousInjectFrame();
		}



		internal SimultaneousInjectFrame AndInjectSimultaneously<T>(
			SimultaneousInjectFrame soFar,
			T dependency,
			bool isWildcard
		) {
			return andInjectMoreSimultaneously_internal(soFar, dependency, typeof(T), isWildcard);
		}

		internal SimultaneousInjectFrame AndInjectSimultaneously(
			SimultaneousInjectFrame soFar,
			object dependency,
			Type toMatchAgainst,
			bool isWildcard
		) {
			if (toMatchAgainst == null) throw new ArgumentNullException(
				nameof(toMatchAgainst),
				$"Cannot inject dependency with a null Type to match against " +
				$"(dependency object = '{dependency}', isWildcard = {isWildcard})."
			);
			RequireDependencySubtypeOf(dependency, toMatchAgainst);

			return andInjectMoreSimultaneously_internal(soFar, dependency, toMatchAgainst, isWildcard);
		}



		private SimultaneousInjectFrame andInjectMoreSimultaneously_internal(
			SimultaneousInjectFrame soFar,
			object dependency,
			Type toMatchAgainst,
			bool isWildcard
		) {
			if (stackLevel != soFar.stackLevel + 1) throw new InvalidDIStateException(
				$"Cannot inject another dependency simultaneously as stack level has changed " +
				$"(object dependency = '{dependency}', Type toMatchAgainst = '{toMatchAgainst}', "+
				$"current stack level = '{stackLevel}', " +
				$"required stack level (soFar.stackLevel + 1) = '{soFar.stackLevel + 1}'"
			);

			stackLevel--;
			SimultaneousInjectFrame result;

			try {
				result = injectSimul_internal(
					soFar.IsEmpty ? ImmutableStack.Create<Type>() : soFar.types,
					dependency,
					toMatchAgainst,
					isWildcard
				);
			} finally {
				stackLevel++;
			}

			return result;
		}

		private SimultaneousInjectFrame injectSimul_internal(
			ImmutableStack<Type> soFar,
			object dependency,
			Type toMatchAgainst,
			bool isWildcard
		) {
			SimultaneousInjectFrame result;

			if (isWildcard)
			{
				ImmutableStack<Type> resStack = soFar;
				foreach (Type t in addWildcardToStack_internal(dependency, toMatchAgainst)) {
					resStack = resStack.Push(t);
				}

				result = new SimultaneousInjectFrame(stackLevel, resStack);
			}
			else
			{
				addToStack_internal(dependency, toMatchAgainst);

				result = new SimultaneousInjectFrame(stackLevel, soFar.Push(toMatchAgainst));
			}

			stackLevel++;
			return result;
		}

		private IEnumerable<Type> addWildcardToStack_internal(object dependency, Type toMatchAgainst)
		{
			// Add each successive base class
			Type t = dependency.GetType();
			while (t != null) {
				addToStack_internal(dependency, t);
				yield return t;
				t = t.BaseType;
			}

			// Add all interfaces (directly or indirectly implemented)
			foreach (Type iType in toMatchAgainst.GetInterfaces())
			{
				addToStack_internal(dependency, toMatchAgainst);
				yield return iType;
			}
		}

		private void addToStack_internal(object dependency, Type toMatchAgainst)
		{
			var toPush = new StackedDependency(stackLevel, dependency);

			if (_dependencyStacks.TryGetValue(toMatchAgainst, out var stack))
			{
				if (stack.Peek().stackLevel == stackLevel) throw new InvalidOperationException(
					$"Cannot inject dependency against type '{toMatchAgainst.FullName}' " +
					$"as there is already a dependency present against the same type at the current stack level " +
					$"(stack level = '{stackLevel}'). Most likely cause: calling a method to inject multiple " +
					$"dependencies at the same time (i.e. at the same stack level), but requesting to add two or " +
					$"more dependencies against the same type, or more than one wildcard dependency. This would " +
					$"result in an ambiguity for what object should be returned when the dependencies are fetched " +
					$"(in the case of wildcards, attempting to fetch a dependency against type 'object' or any other " +
					$"common parent type would cause this ambiguity). Instead, this is disallowed. Consider " +
					$"injecting the dependencies one at a time so that they have a defined priority order. " +
					$"Otherwise, if you do need multiple of the same dependency type T to be fetched as a group, " +
					$"consider using Inject() and Get() with a T[], List<T>, or some other collection. If you need " +
					$"inner code to both Get() the group and Get() just the first element (for example) then inject" +
					$"e.g. both the List<T> and the instance of T."
				);
				stack.Push(toPush);
			}
			else
			{
				_dependencyStacks.Add(toMatchAgainst, new SearchableStack<StackedDependency> { toPush });
			}
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
			if (!this.tryGetFromDependencyStacks(typeof(T), out var stack, useFallbacks, out var layerFoundIn)) {
				// No stack found, or all found stacks were empty
				return fail(out dependency, out found);
			}

			var dInfo = stack.Peek();
			dependency = (T)dInfo.dependency;

			// Fail if a null has been added to hide earlier dependencies
			if (dependency == null) return fail(out dependency, out found);
			found = true;

			if (typeof(T).IsValueType) return FetchFrame.CleanupFree;

			// If the dependency is a reference-type (and was found), then need to add to _fetchRecords so that the
			// dependency can look up what dependencies were available when it was originally injected.
			// Must only add to/modify the current layer's records (as we must only modify the current layer in general).
			this.addToFetchRecord(dInfo.dependency, layerFoundIn, dInfo.stackLevel, out var prevFetch);

			return new FetchFrame(dInfo.dependency, prevFetch);

			FetchFrame fail(out T d, out bool f) {
				f = false;
				d = default;
				return FetchFrame.CleanupFree;
			}
		}

		private void addToFetchRecord(
			object dependency,
			DependencyLayer layerFoundIn,
			int stackLevelFoundAt,
			out FetchRecord prevFetch
		) {
			// If this dependency object instance has been fetched before, need to copy out the current
			// entry in _fetchRecords (passed out via prevFetch) so that it can be restored later.
			// We must only modify _fetchRecords of the current layer, just as we must only modify the current
			// layer in general - so only need to look in the current layer for a previous fetch to replace.

			bool alreadyFetched = _fetchRecords.TryGetValue(dependency, out prevFetch);

			_fetchRecords[dependency] = new FetchRecord(layerFoundIn, stackLevelFoundAt);
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
			if (self == null) throw new ArgumentNullException(nameof(self));

			if (self.GetType().IsValueType) throw new ArgumentTypeException(
				$"Only reference-type dependencies may fetch outer dependencies from when they were injected. " +
				$"Object '{self}' is of type '{self.GetType().FullName}', which is a value-type.",
				nameof(self)
			);

			if (!tryGetFromFetchRecord(self, out FetchRecord mostRecentFetch, useFallbacks, out var layerFetchFoundIn)) {
				throw new ArgumentException(
					$"No record is available of a dependency fetch having been performed for object '{self}' " +
					$"(of type '{self.GetType().FullName}'). " +
					$"Depending on how this occurred (incorrect call or invalid state), continued operation may be undefined."
				);
			}

			if (!tryFindMostRecentBeforeFetch(out StackedDependency outerInfo, out DependencyLayer layerOuterFoundIn)) {
				outerDependency = default;
				found = false;
				return FetchFrame.CleanupFree;
			}

			outerDependency = (TOuter)outerInfo.dependency;

			if (outerDependency == null) {
				outerDependency = default;
				found = false;
				return FetchFrame.CleanupFree;
			}

			found = true;

			// Now add to the current layer's _fetchRecords that the outer dependency was just fetched
			this.addToFetchRecord(outerDependency, layerOuterFoundIn, outerInfo.stackLevel, out FetchRecord prevOuterFetch);

			return new FetchFrame(outerInfo.dependency, prevOuterFetch);

			bool tryFindMostRecentBeforeFetch(out StackedDependency mostRecent, out DependencyLayer layerFoundIn)
			{
				if (!layerFetchFoundIn.tryGetFromDependencyStacks(
					typeof(TOuter),
					out var stack,
					useFallbacks,
					out var layerStackFoundIn
				)) {
					mostRecent = default;
					layerFoundIn = null;
					return false;
				}

				// We previously found the previous time 'self' was fetched, and got the layer it was in.
				// Now we've just looked for a stack of dependencies against type TOuter, in that layer or
				// a fallback layer (if useFallbacks = true).
				// If the stack was in a different layer, we can just get the top of the stack
				// (which is also guaranteed to be non-empty).
				// However, if the stack was in the same layer, then some dependencies might've
				// been added to it since 'self' was fetched. In that case, we need to do a binary
				// search through the stack, to find the most recent entry before 'self' was fetched.
				// If there is no such entry in the stack, then (if useFallbacks = true) we need to
				// fallback to previous layers (and then just get the top of whatever stack we find).

				// If the layers are different, just get the top of the stack and return

				if (!ReferenceEquals(layerStackFoundIn, layerFetchFoundIn))
				{
					mostRecent = stack.Peek();
					layerFoundIn = layerStackFoundIn;
					return true;
				}

				// Otherwise, layers are the same; do a binary search

				int pos = stack.BinarySearch(
					new StackedDependency(mostRecentFetch.stackLevelFoundAt, null),
					new StackSearchComparer()
				);

				// That returns either the position of an exact match, or the bitwise complement
				// of the position of the next greater element. We need the position of the previous
				// element (even if it was an exact match - we shouldn't return dependencies that
				// were injected simultaneously, only ones injected previously). So:

				int prevPos;
				if (pos >= 0) {
					prevPos = pos - 1;
				} else {
					int nextPos = ~pos;
					prevPos = nextPos - 1;
				}

				// Now, if prevPos >= 0, we've sucessfully found the dependency

				if (pos >= 0) {
					mostRecent = stack[prevPos];
					layerFoundIn = layerStackFoundIn;
					return true;
				}

				// But otherwise, we need to fall back to previous layers (if we can)
				
				if (!useFallbacks || layerStackFoundIn.Fallback == null) {
					// Not allowed to fall back, or nothing to fall back to
					mostRecent = default;
					layerFoundIn = null;
					return false;
				}

				if (layerStackFoundIn.Fallback.tryGetFromDependencyStacks(
					typeof(TOuter),
					out var fallbackStack,
					useFallbacks,
					out var fallbackLayerStackFoundIn
				)) {
					mostRecent = fallbackStack.Peek(); // guaranteed non-empty
					layerFoundIn = fallbackLayerStackFoundIn;
					return true;
				} else {
					mostRecent = default;
					layerFoundIn = null;
					return false;
				}
			}
		}

		private bool tryGetFromFetchRecord(object key, out FetchRecord value, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			var layer = this;

			while (layer != null) {
				if (layer._fetchRecords.TryGetValue(key, out value)) {
					layerFoundIn = layer;
					return true;
				}

				if (useFallbacks) layer = layer.Fallback;
				else break;
			}

			value = default;
			layerFoundIn = null;
			return false;
		}

		private bool tryGetFromDependencyStacks(Type key, out SearchableStack<StackedDependency> value, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			var layer = this;

			while (layer != null) {
				if (layer._dependencyStacks.TryGetValue(key, out value) && value.Count > 0) {
					layerFoundIn = layer;
					return true;
				}

				if (useFallbacks) layer = layer.Fallback;
				else break;
			}

			value = default;
			layerFoundIn = null;
			return false;
		}



		internal void CloseFrame(InjectFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			uninjectDependency_internal(frame.type, frame.stackLevel);
		}

		internal void CloseFrame(SimultaneousInjectFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			foreach (Type t in frame.types)
			{
				uninjectDependency_internal(t, frame.stackLevel);
			}
		}

		private void uninjectDependency_internal(Type type, int frameStackLevel)
		{
			if (!_dependencyStacks.TryGetValue(type, out var stack)) throw new InjectFrameCloseException(
				$"No dependency stack for type '{type}' is available.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			if (stack.Count == 0) throw new InjectFrameCloseException(
				$"No dependency stack frames for type '{type}' are available.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			StackedDependency toRemove = stack.Peek();
			if (toRemove.stackLevel != frameStackLevel) throw new InjectFrameCloseException(
				$"Top element of stack for type '{type}' has stack level '{toRemove.stackLevel}' " +
				$"but frame to be closed has a different stack level: '{frameStackLevel}'.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			stack.Pop();
		}



		internal void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			closeFetchedDependency_internal(frame.dependency, frame.prevFetchStackLevel);
		}

		internal void CloseFetchFrame(MultiFetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			foreach ((object dependency, int prevFetchStackLevel) in frame.dependencies)
			{
				closeFetchedDependency_internal(dependency, prevFetchStackLevel);
			}
		}

		private void closeFetchedDependency_internal(object dependency, int prevFetchStackLevel)
		{
			if (prevFetchStackLevel == FetchFrame.NoPrevious)
			{
				if (!_fetchRecords.Remove(dependency)) throw noEntryPresentException();
			}
			else
			{
				if (!_fetchRecords.ContainsKey(dependency)) throw noEntryPresentException();
				_fetchRecords[dependency] = prevFetchStackLevel;
			}

			FetchFrameCloseException noEntryPresentException() => new FetchFrameCloseException(
				$"No entry in fetch record available to remove for object '{dependency}' " +
				$"(with reference hashcode '{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dependency)}').",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);
		}


		public SafeDisposeExceptionsRegion SafeDisposeExceptions()
			=> DisposeExceptionsManager.SafeDisposeExceptions();



		//	private static T ThrowIfArgNull<T>(T arg, string argName)
		//		=> arg == null ? throw new ArgumentNullException(argName) : arg;

		private void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentTypeException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}


		private class RefEqualityComparer : IEqualityComparer<object>
		{
			public RefEqualityComparer() { }

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);

			public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}

		/// <summary>
		/// Compares by stack level only
		/// </summary>
		private class StackSearchComparer : IComparer<StackedDependency>
		{
			public StackSearchComparer() { }

			public int Compare(StackedDependency x, StackedDependency y)
				=> x.stackLevel.CompareTo(y.stackLevel);
		}
	}
}

//*/