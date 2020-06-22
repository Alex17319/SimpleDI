using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	public static class Dependencies
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
		private static readonly Dictionary<Type, SearchableStack<StackedDependency>> _dependencyStacks
			= new Dictionary<Type, SearchableStack<StackedDependency>>();

		// Maps from a dependency that has been fetched to the stack level that it was originally injected at
		[ThreadStatic]
		private static readonly Dictionary<object, int> _fetchRecord
			= new Dictionary<object, int>(new RefEqualityComparer());

		[ThreadStatic]
		private static int stackLevel;



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static InjectFrame Inject<T>(T dependency)
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
		public static InjectFrame Inject(object dependency, Type toMatchAgainst)
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
		public static SimultaneousInjectFrame InjectWild<T>(T dependency)
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
		public static SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			RequireDependencySubtypeOf(dependency, toMatchAgainst);

			return injectSimul_internal(ImmutableStack.Create<Type>(), dependency, toMatchAgainst, isWildcard: true);
		}



		public static SimultaneousInjectFrame BeginSimultaneousInject()
		{
			return new SimultaneousInjectFrame();
		}



		internal static SimultaneousInjectFrame AndInjectSimultaneously<T>(
			SimultaneousInjectFrame soFar,
			T dependency,
			bool isWildcard
		) {
			return andInjectMoreSimultaneously_internal(soFar, dependency, typeof(T), isWildcard);
		}

		internal static SimultaneousInjectFrame AndInjectSimultaneously(
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



		private static SimultaneousInjectFrame andInjectMoreSimultaneously_internal(
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

		private static SimultaneousInjectFrame injectSimul_internal(
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

		private static IEnumerable<Type> addWildcardToStack_internal(object dependency, Type toMatchAgainst)
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

		private static void addToStack_internal(object dependency, Type toMatchAgainst)
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
		public static FetchFrame Get<T>(out T dependency)
		{
			FetchFrame result = TryGet(out dependency, out bool found);
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
		public static FetchFrame GetOrNull<T>(out T dependency)
			where T : class
		{
			FetchFrame result = TryGet(out dependency, out bool found);
			if (!found) dependency = null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T (not nullable), and returns it (or else null) via a nullable T? parameter.
		/// <para/>
		/// See <see cref="TryGet{T}(out T, out bool)"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static FetchFrame GetOrNull<T>(out T? dependency)
			where T : struct
		{
			FetchFrame result = TryGet(out T dep, out bool found);
			dependency = found ? dep : (T?)null;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches a dependency of type T? (nullable), and returns it (or else null) via a nullable T? parameter
		/// <para/>
		/// See <see cref="TryGet{T}(out T, out bool)"/>
		/// </summary>
		/// <remarks>
		/// Note that null nullable instances (eg new int?()) are boxed to true null pointers (and then treated
		/// as blocking the visibility of dependencies further out) - so searching for TOuter? doesn't introduce
		/// two different types of null values or anything.
		/// </remarks>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static FetchFrame GetNullableOrNull<T>(out T? dependency)
			where T : struct
		{
			FetchFrame result = TryGet(out dependency, out bool found);
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
		public static FetchFrame TryGet<T>(out T dependency, out bool found)
		{
			if (!_dependencyStacks.TryGetValue(typeof(T), out var stack)) return fail(out dependency, out found);

			if (stack.Count == 0) return fail(out dependency, out found);

			var dInfo = stack.Peek();
			dependency = (T)dInfo.dependency;

			// Fail if a null has been added to hide earlier dependencies
			if (dependency == null) return fail(out dependency, out found);
			found = true;

			if (typeof(T).IsValueType) return FetchFrame.CleanupFree;

			// If the dependency is a reference-type (and was found), then need to add to _fetchRecord so
			// that the dependency can look up what dependencies were available when it was originally injected
			// If this dependency object instance has been fetched before, need to copy out the current
			// entry in _fetchRecord so that it can be restored later.
			bool alreadyFetched = _fetchRecord.TryGetValue(dInfo.dependency, out int prevFetchStackLevel);

			_fetchRecord[dInfo.dependency] = dInfo.stackLevel;
			return new FetchFrame(
				dInfo.dependency,
				alreadyFetched ? prevFetchStackLevel : FetchFrame.NoPrevious
			);

			FetchFrame fail(out T d, out bool f) {
				f = false;
				d = default;
				return FetchFrame.CleanupFree;
			}
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or throws a <see cref="DependencyNotFoundException"/> if it could not be found.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool)"/>.
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
		public static FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency)
		{
			FetchFrame result = TryGetOuter(self, out outerDependency, out bool found);
			if (!found) throw new DependencyNotFoundException(typeof(TOuter));
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fetches an outer dependency, or returns null if it could not be found.
		/// <para/>
		/// See <see cref="TryGetOuter{TOuter}(object, out TOuter, out bool)"/>.
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
		public static FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency)
			where TOuter : class
		{
			FetchFrame result = TryGetOuter(self, out outerDependency, out bool found);
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
		public static FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));

			if (self.GetType().IsValueType) throw new ArgumentTypeException(
				$"Only reference-type dependencies may fetch outer dependencies from when they were injected. " +
				$"Object '{self}' is of type '{self.GetType().FullName}', which is a value-type.",
				nameof(self)
			);

			if (!_fetchRecord.TryGetValue(self, out int originalStackLevel)) throw new ArgumentException(
				$"No record is available of a dependency fetch having been performed for object '{self}' " +
				$"(of type '{self.GetType().FullName}'). " +
				$"Depending on how this occurred (incorrect call or invalid state), continued operation may be undefined."
			);

			if (!_dependencyStacks.TryGetValue(typeof(TOuter), out var stack) || stack.Count == 0) {
				return fail(out outerDependency, out found);
			}

			int pos = stack.BinarySearch(new StackedDependency(originalStackLevel, null), new StackSearchComparer());

			StackedDependency outerInfo;
			if (pos >= 0) outerInfo = stack[pos];
			else {
				int posOfLater = ~pos; // position of dependency added for TOuter with the next higher stack level
				int posOfEarlier = posOfLater - 1; // position of dependency added for TOuter with the next lower stack level

				if (posOfEarlier < 0) return fail(out outerDependency, out found);
				else outerInfo = stack[posOfEarlier];
			}

			outerDependency = (TOuter)outerInfo.dependency;

			if (outerDependency == null) return fail(out outerDependency, out found);
			found = true;

			return new FetchFrame(outerInfo.dependency, outerInfo.stackLevel);

			FetchFrame fail(out TOuter od, out bool f) {
				f = false;
				od = default;
				return FetchFrame.CleanupFree;
			}
		}



		internal static void CloseFrame(InjectFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			uninjectDependency_internal(frame.type, frame.stackLevel);
		}

		internal static void CloseFrame(SimultaneousInjectFrame frame)
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

		private static void uninjectDependency_internal(Type type, int frameStackLevel)
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



		internal static void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			closeFetchedDependency_internal(frame.dependency, frame.prevFetchStackLevel);
		}

		internal static void CloseFetchFrame(MultiFetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			foreach ((object dependency, int prevFetchStackLevel) in frame.dependencies)
			{
				closeFetchedDependency_internal(dependency, prevFetchStackLevel);
			}
		}

		private static void closeFetchedDependency_internal(object dependency, int prevFetchStackLevel)
		{
			if (prevFetchStackLevel == FetchFrame.NoPrevious)
			{
				if (!_fetchRecord.Remove(dependency)) throw noEntryPresentException();
			}
			else
			{
				if (!_fetchRecord.ContainsKey(dependency)) throw noEntryPresentException();
				_fetchRecord[dependency] = prevFetchStackLevel;
			}

			FetchFrameCloseException noEntryPresentException() => new FetchFrameCloseException(
				$"No entry in fetch record available to remove for object '{dependency}' " +
				$"(with reference hashcode '{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dependency)}').",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);
		}


		public static SafeDisposeExceptionsRegion SafeDisposeExceptions()
			=> DisposeExceptionsManager.SafeDisposeExceptions();



		//	private static T ThrowIfArgNull<T>(T arg, string argName)
		//		=> arg == null ? throw new ArgumentNullException(argName) : arg;

		private static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
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

		private class StackSearchComparer : IComparer<StackedDependency>
		{
			public StackSearchComparer() { }

			public int Compare(StackedDependency x, StackedDependency y)
				=> x.stackLevel.CompareTo(y.stackLevel);
		}
	}
}
