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
		// For future improvement: Implement wildcard dependencies more efficiently (wildcard = returned when
		// any parent class/interface is requested, rather than just when exactly the correct type is requested).
		// Most of the time I'm guessing wildcards won't be used though, so shouldn't sacrifice efficiency elsewhere.

		[ThreadStatic]
		private static readonly Dictionary<Type, Stack<StackedDependency>> _dependencyStacks
			= new Dictionary<Type, Stack<StackedDependency>>();

		[ThreadStatic]
		private static int stackLevel;

		private static void addToStack(object dependency, Type toMatchAgainst)
		{
			var toPush = new StackedDependency(stackLevel, dependency);
			if (_dependencyStacks.TryGetValue(toMatchAgainst, out var stack)) stack.Push(toPush);
			else {
				var newStack = new Stack<StackedDependency>();
				newStack.Push(toPush);
				_dependencyStacks.Add(toMatchAgainst, newStack);
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

		public static DependencyFrame Inject<T>(T dependency)
		{
			addToStack(dependency, typeof(T));

			return new DependencyFrame(stackLevel++, new[] { typeof(T) });
		}

		public static DependencyFrame Inject(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			if (dependency != null)
				RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");

			addToStack(dependency, toMatchAgainst);

			return new DependencyFrame(stackLevel++, new[] { toMatchAgainst });
		}



		public static DependencyFrame InjectWild<T>(T dependency) {
			var result = new DependencyFrame(
				stackLevel,
				addWildcardToStack(dependency, typeof(T)).ToList()
			);

			stackLevel++;
			return result;
		}

		public static DependencyFrame InjectWild(object dependency)
		{
			ThrowIfArgNull(dependency, nameof(dependency));

			var result = new DependencyFrame(
				stackLevel,
				addWildcardToStack(dependency, dependency.GetType()).ToList()
			);

			stackLevel++;
			return result;
		}

		/// <summary>
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
		public static DependencyFrame InjectWild(object dependency, Type toMatchAgainst)
		{
			if (toMatchAgainst == null) throw new ArgumentNullException(nameof(toMatchAgainst));
			if (dependency != null)
				RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");

			var result = new DependencyFrame(
				stackLevel,
				addWildcardToStack(dependency, toMatchAgainst).ToList()
			);

			stackLevel++;
			return result;
		}



		public static DependencyFrame InjectAll(IEnumerable<KeyValuePair<Type, object>> dependencies)
			=> InjectAll(dependencies.Select(d => (dependency: d.Key, toMatchAgainst: d.Value, isWildCard: false)));

		public static DependencyFrame InjectAllWild(IEnumerable<KeyValuePair<Type, object>> dependencies)
			=> InjectAll(dependencies.Select(d => (dependency: d.Key, toMatchAgainst: d.Value, isWildCard: true)));

		public static DependencyFrame InjectAll(
			IEnumerable<(Type toMatchAgainst, object dependency, bool isWildcard)> dependencies
		) {
			if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));

			int i = 0;
			foreach (var d in dependencies) {
				if (d.toMatchAgainst == null) throw new ArgumentException(
					$"Dependency at index '{i}' in array has null type."
				);
				if (d.dependency != null) RequireDependencySubtypeOf(
					d.dependency,
					d.toMatchAgainst,
					$"dependency at index {i}"
				);
			}

			var result = new DependencyFrame(stackLevel, addAllToStack().ToList());
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

		public static InjectionBuilder InjectFirst<T>(T dependency)
			=> injectFirst(dependency, typeof(T), isWildcard: false);
		public static InjectionBuilder InjectFirstWild<T>(T dependency)
			=> injectFirst(dependency, typeof(T), isWildcard: true);

		public static InjectionBuilder InjectFirst(object dependency, Type toMatchAgainst) {
			RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");
			return injectFirst(dependency, toMatchAgainst, isWildcard: false);
		}
		public static InjectionBuilder InjectFirstWild(object dependency, Type toMatchAgainst) {
			RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");
			return injectFirst(dependency, toMatchAgainst, isWildcard: true);
		}



		public static T Get<T>()
		{
			if (!_dependencyStacks.TryGetValue(typeof(T), out var stack) || stack.Count == 0) {
				throw new DependencyNotFoundException(
					$"No dependency of type '{typeof(T).FullName}' could be found."
				);
			}

			return (T)stack.Peek().dependency;
		}



		internal static void CloseFrame(DependencyFrame frame)
		{
			if (frame.stackLevel != stackLevel) throw new DependencyFrameCloseException(
				$"Cannot close frame with stack level '{frame.stackLevel}' different to the current stack level '{stackLevel}'"
			);

			foreach (Type t in frame.types)
			{
				if (!_dependencyStacks.TryGetValue(t, out var stack)) throw new DependencyFrameCloseException(
					$"No dependency stack for type '{t}' is available."
				);

				if (stack.Count == 0) throw new DependencyFrameCloseException(
					$"No dependency stack frames for type '{t}' are available."
				);

				StackedDependency toRemove = stack.Peek();
				if (toRemove.stackLevel != frame.stackLevel) throw new DependencyFrameCloseException(
					$"Top element of stack for type '{t} ' has stack level '{toRemove.stackLevel}' " +
					$"but frame to be closed has a different stack level: '{frame.stackLevel}'."
				);

				stack.Pop();
			}
		}
		
		private static T ThrowIfArgNull<T>(T arg, string argName)
		{
			if (arg == null) throw new ArgumentNullException(argName);
			return arg;
		}

		private static void RequireDependencySubtypeOf(object dependency, Type type, string dependencyName)
		{
			if (!type.IsInstanceOfType(dependency)) throw new ArgumentException(
				$"Cannot add {dependencyName} as object is of type '{dependency.GetType().FullName}' " +
				$"and is not an instance of provided match type {type.FullName}."
			);
		}

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

			public InjectionBuilder Then<T>(T dependency) => then(dependency, typeof(T), isWildcard: false);
			public InjectionBuilder ThenWild<T>(T dependency) => then(dependency, typeof(T), isWildcard: true);

			public InjectionBuilder Then(object dependency, Type toMatchAgainst)
			{
				RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");
				return then(dependency, toMatchAgainst, isWildcard: false);
			}
			public InjectionBuilder ThenWild(object dependency, Type toMatchAgainst)
			{
				RequireDependencySubtypeOf(dependency, toMatchAgainst, "dependency");
				return then(dependency, toMatchAgainst, isWildcard: true);
			}

			public DependencyFrame Inject() => InjectAll(this);

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
	}
}
