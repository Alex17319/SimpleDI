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



		private static void addToStack(object dependency, Type toMatchAgainst)
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

		private static IEnumerable<Type> addWildcardToStack(object dependency, Type toMatchAgainst)
		{
			// Add each successive base class
			Type t = dependency.GetType();
			while (t != null) {
				addToStack(dependency, t);
				yield return t;
				t = t.BaseType;
			}

			// Add all interfaces (directly or indirectly implemented)
			foreach (Type iType in toMatchAgainst.GetInterfaces())
			{
				addToStack(dependency, toMatchAgainst);
				yield return iType;
			}
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static InjectFrame Inject<T>(T dependency)
			//	where T : class
		{
			addToStack(dependency, typeof(T));

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
			//	RequireDependencyReferenceType(toMatchAgainst);

			addToStack(dependency, toMatchAgainst);

			return new InjectFrame(stackLevel++, toMatchAgainst);
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static MultiInjectFrame InjectWild<T>(T dependency)
			//	where T : class
		{
			var result = new MultiInjectFrame(
				stackLevel,
				addWildcardToStack(dependency, typeof(T)).ToList()
			);

			stackLevel++;
			return result;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static MultiInjectFrame InjectWild(object dependency)
		{
			ThrowIfArgNull(dependency, nameof(dependency));
			//	RequireDependencyReferenceType(dependency.GetType());

			var result = new MultiInjectFrame(
				stackLevel,
				addWildcardToStack(dependency, dependency.GetType()).ToList()
			);

			stackLevel++;
			return result;
		}

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
		public static MultiInjectFrame InjectWild(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			RequireDependencySubtypeOf(dependency, toMatchAgainst);
			//	RequireDependencyReferenceType(toMatchAgainst);

			var result = new MultiInjectFrame(
				stackLevel,
				addWildcardToStack(dependency, toMatchAgainst).ToList()
			);

			stackLevel++;
			return result;
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependencies"></param>
		/// <returns></returns>
		public static MultiInjectFrame InjectSimultaneous(IEnumerable<KeyValuePair<Type, object>> dependencies)
			=> InjectSimultaneous(dependencies.Select(d => (dependency: d.Key, toMatchAgainst: d.Value, isWildCard: false)));

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependencies"></param>
		/// <returns></returns>
		public static MultiInjectFrame InjectSimultaneousWild(IEnumerable<KeyValuePair<Type, object>> dependencies)
			=> InjectSimultaneous(dependencies.Select(d => (dependency: d.Key, toMatchAgainst: d.Value, isWildCard: true)));

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependencies"></param>
		/// <returns></returns>
		public static MultiInjectFrame InjectSimultaneous(
			IEnumerable<(Type toMatchAgainst, object dependency, bool isWildcard)> dependencies
		) {
			if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

			int i = 0;
			foreach (var d in dependencies)
			{
				if (d.toMatchAgainst == null) throw new ArgumentException(
					$"Dependency at index '{i}' in array has null type."
				);
				RequireDependencySubtypeOf(
					d.dependency,
					d.toMatchAgainst,
					$"dependency at index {i}"
				);
				//	RequireDependencyReferenceType(
				//		d.toMatchAgainst,
				//		$"dependency at index {i}"
				//	);

				i++;
			}

			var result = new MultiInjectFrame(stackLevel, addAllToStack().ToList());
			stackLevel++;
			return result;

			IEnumerable<Type> addAllToStack()
			{
				foreach (var d in dependencies)
				{
					if (d.isWildcard) {
						foreach (Type t in addWildcardToStack(d.dependency, d.toMatchAgainst)) yield return t;
					} else {
						addToStack(d.dependency, d.toMatchAgainst);
						yield return d.toMatchAgainst;
					}
				}
			}
		}



		private static InjectionBuilder injectFirst(object dependency, Type toMatchAgainst, bool isWildcard)
			=> new InjectionBuilder(
				(toMatchAgainst, dependency, isWildcard: false),
				prev: null
			);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static InjectionBuilder InjectFirst<T>(T dependency) // where T : class
			=> injectFirst(dependency, typeof(T), isWildcard: false);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static InjectionBuilder InjectFirstWild<T>(T dependency) // where T : class
			=> injectFirst(dependency, typeof(T), isWildcard: true);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public static InjectionBuilder InjectFirst(object dependency, Type toMatchAgainst) {
			RequireDependencySubtypeOf(dependency, toMatchAgainst);
			//	RequireDependencyReferenceType(toMatchAgainst);

			return injectFirst(dependency, toMatchAgainst, isWildcard: false);
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public static InjectionBuilder InjectFirstWild(object dependency, Type toMatchAgainst) {
			RequireDependencySubtypeOf(dependency, toMatchAgainst);
			//	RequireDependencyReferenceType(toMatchAgainst);

			return injectFirst(dependency, toMatchAgainst, isWildcard: true);
		}


		/// <summary>
		///	<see langword="[Call inside using()]"></see>
		///	
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public static FetchFrame Get<T>(out T dependency)
			// where T : class
		{
			if (!_dependencyStacks.TryGetValue(typeof(T), out var stack) || stack.Count == 0) {
				throw new DependencyNotFoundException(typeof(T));
			}

			var dInfo = stack.Peek();

			dependency = (T)dInfo.dependency;

			if (typeof(T).IsValueType) return default;

			// If the dependency is a reference-type, then need to add to _fetchRecord so that the
			// dependency can look up what dependencies were available when it was originally injected
			// If this dependency object instance has been fetched before, need to copy out the current
			// entry in _fetchRecord so that it can be restored later.
			bool alreadyFetched = _fetchRecord.TryGetValue(dInfo.dependency, out int prevFetchStackLevel);

			_fetchRecord[dInfo.dependency] = dInfo.stackLevel;
			return new FetchFrame(
				dInfo.dependency,
				alreadyFetched ? prevFetchStackLevel : FetchFrame.NoPrevious
			);
		}



		/// <summary>
		///	<see langword="[Call inside using()]"></see>
		/// Once a dependency has been retrieved, it may call this method to find
		/// dependencies that were in place when it was originally injected.
		/// </summary>
		/// <remarks>
		/// If the same depencency object has been injected multiple times, the most recent injection will be used.
		/// <para/>
		/// Only reference-type dependencies may make use of this method, as reference-equality is used
		/// to perform the lookup.
		/// </remarks>
		/// <typeparam name="TOuter">The type of outer dependency to fetch now</typeparam>
		/// <param name="self"></param>
		/// <param name="outer"></param>
		/// <returns></returns>
		public static FetchFrame GetOuterDependency<TOuter>(object self, out TOuter outerDependency)
		{
			if (self == null) throw new ArgumentNullException(nameof(self));

			if (!_fetchRecord.TryGetValue(self, out int originalStackLevel)) throw new ArgumentException(
				$"No record is available of a dependency fetch having been performed for type '{self.GetType().FullName}'. " +
				"Depending on how this occurred (incorrect call or invalid state), continued oepration may be undefined."
			);

			if (!_dependencyStacks.TryGetValue(typeof(TOuter), out var stack) || stack.Count == 0) {
				throw new DependencyNotFoundException(typeof(TOuter));
			}

			int pos = stack.BinarySearch(new StackedDependency(originalStackLevel, null), new StackSearchComparer());

			StackedDependency found;
			if (pos >= 0) found = stack[pos];
			else {
				int posOfLater = ~pos; // position of dependency added for TOuter with the next higher stack level
				int posOfEarlier = posOfLater - 1; // position of dependency added for TOuter with the next lower stack level

				if (posOfEarlier < 0) throw new DependencyNotFoundException(typeof(TOuter));
				else found = stack[posOfEarlier];
			}

			outerDependency = (TOuter)found.dependency;

			return new FetchFrame(found.dependency, found.stackLevel);
		}



		internal static void CloseFrame(InjectFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'."
			);

			uninjectDependency(frame.type, frame.stackLevel);
		}

		internal static void CloseFrame(MultiInjectFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'."
			);

			foreach (Type t in frame.types)
			{
				uninjectDependency(t, frame.stackLevel);
			}
		}

		private static void uninjectDependency(Type type, int frameStackLevel)
		{
			if (!_dependencyStacks.TryGetValue(type, out var stack)) throw new InjectFrameCloseException(
				$"No dependency stack for type '{type}' is available."
			);

			if (stack.Count == 0) throw new InjectFrameCloseException(
				$"No dependency stack frames for type '{type}' are available."
			);

			StackedDependency toRemove = stack.Peek();
			if (toRemove.stackLevel != frameStackLevel) throw new InjectFrameCloseException(
				$"Top element of stack for type '{type}' has stack level '{toRemove.stackLevel}' " +
				$"but frame to be closed has a different stack level: '{frameStackLevel}'."
			);

			stack.Pop();
		}



		internal static void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			closeFetchedDependency(frame.dependency, frame.prevFetchStackLevel);
		}

		internal static void CloseFetchFrame(MultiFetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			foreach ((object dependency, int prevFetchStackLevel) in frame.dependencies)
			{
				closeFetchedDependency(dependency, prevFetchStackLevel);
			}
		}

		private static void closeFetchedDependency(object dependency, int prevFetchStackLevel)
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
				$"(with reference hashcode '{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dependency)}')."
			);
		}



		private static T ThrowIfArgNull<T>(T arg, string argName)
			=> arg == null ? throw new ArgumentNullException(argName) : arg;

		private static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyMoniker = "dependency")
		{
			if (dependency != null && !type.IsInstanceOfType(dependency)) throw new ArgumentException(
				$"Cannot add {dependencyMoniker} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}

		//	private static void RequireDependencyReferenceType(Type type, string dependencyMoniker = "dependency")
		//	{
		//		if (type.IsValueType) throw new ArgumentException(
		//			$"Cannot add {dependencyMoniker} of type '{type.FullName}' as type is not a reference-type."
		//		);
		//	}

		public class InjectionBuilder : IEnumerable<(Type, object, bool isWildcard)>
		{
			private (Type, object, bool isWildcard) toInject;
			private InjectionBuilder prev;

			internal InjectionBuilder((Type, object, bool isWildcard) toInject, InjectionBuilder prev = null) {
				this.prev = prev;
				this.toInject = toInject;
			}

			private InjectionBuilder then(object dependency, Type toMatchAgainst, bool isWildcard) => new InjectionBuilder(
				(toMatchAgainst, dependency, isWildcard),
				prev: this
			);

			/// <summary>
			/// <see langword="[Call inside using()]"></see>
			/// 
			/// </summary>
			/// <typeparam name="T"></typeparam>
			/// <param name="dependency"></param>
			/// <returns></returns>
			public InjectionBuilder Then<T>(T dependency) // where T : class
				=> then(dependency, typeof(T), isWildcard: false);

			/// <summary>
			/// <see langword="[Call inside using()]"></see>
			/// 
			/// </summary>
			/// <typeparam name="T"></typeparam>
			/// <param name="dependency"></param>
			/// <returns></returns>
			public InjectionBuilder ThenWild<T>(T dependency) // where T : class
				=> then(dependency, typeof(T), isWildcard: true);

			/// <summary>
			/// <see langword="[Call inside using()]"></see>
			/// 
			/// </summary>
			/// <param name="dependency"></param>
			/// <param name="toMatchAgainst"></param>
			/// <returns></returns>
			public InjectionBuilder Then(object dependency, Type toMatchAgainst)
			{
				RequireDependencySubtypeOf(dependency, toMatchAgainst);
				//	RequireDependencyReferenceType(toMatchAgainst);

				return then(dependency, toMatchAgainst, isWildcard: false);
			}

			/// <summary>
			/// <see langword="[Call inside using()]"></see>
			/// 
			/// </summary>
			/// <param name="dependency"></param>
			/// <param name="toMatchAgainst"></param>
			/// <returns></returns>
			public InjectionBuilder ThenWild(object dependency, Type toMatchAgainst)
			{
				RequireDependencySubtypeOf(dependency, toMatchAgainst);
				//	RequireDependencyReferenceType(toMatchAgainst);

				return then(dependency, toMatchAgainst, isWildcard: true);
			}

			/// <summary>
			/// <see langword="[Call inside using()]"></see>
			/// 
			/// </summary>
			/// <returns></returns>
			public MultiInjectFrame Inject() => InjectSimultaneous(this);

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			public IEnumerator<(Type, object, bool isWildcard)> GetEnumerator()
			{
				InjectionBuilder ib = this;
				while (ib != null)
				{
					yield return ib.toInject;
					ib = ib.prev;
				}
			}
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
