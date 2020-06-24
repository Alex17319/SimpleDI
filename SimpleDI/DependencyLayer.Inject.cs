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
using SimpleDI.DisposeExceptions;

namespace SimpleDI
{
	public partial class DependencyLayer : IDisposable
	{
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

			return new InjectFrame(this, stackLevel++, typeof(T));
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

			return new InjectFrame(this, stackLevel++, toMatchAgainst);
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
			return new SimultaneousInjectFrame(layer: this);
		}



		internal SimultaneousInjectFrame InjectMoreSimultaneously<T>(
			SimultaneousInjectFrame soFar,
			T dependency,
			bool isWildcard
		) {
			return injectMoreSimultaneously_internal(soFar, dependency, typeof(T), isWildcard);
		}

		internal SimultaneousInjectFrame InjectMoreSimultaneously(
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

			return injectMoreSimultaneously_internal(soFar, dependency, toMatchAgainst, isWildcard);
		}



		private SimultaneousInjectFrame injectMoreSimultaneously_internal(
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

				result = new SimultaneousInjectFrame(layer: this, stackLevel, resStack);
			}
			else
			{
				addToStack_internal(dependency, toMatchAgainst);

				result = new SimultaneousInjectFrame(layer: this, stackLevel, soFar.Push(toMatchAgainst));
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


		
		internal void CloseInjectFrame(InjectFrame frame)
		{
			if (frame.layer != this) throw new InjectFrameCloseException(
				$"Cannot close inject frame as it does not belong to the current dependency layer " +
				$"(current layer = '{this}', {nameof(frame)}.{nameof(InjectFrame.layer)} = '{frame.layer}')"
			);

			if (frame.stackLevel != stackLevel) throw new InjectFrameCloseException(
				$"Cannot close inject frame with stack level '{frame.stackLevel}' " +
				$"as it is different to the current stack level '{stackLevel}'.",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);

			uninjectDependency_internal(frame.type, frame.stackLevel);
		}

		internal void CloseInjectFrame(SimultaneousInjectFrame frame)
		{
			if (frame.layer != this) throw new InjectFrameCloseException(
				$"Cannot close inject frame as it does not belong to the current dependency layer " +
				$"(current layer = '{this}', {nameof(frame)}.{nameof(InjectFrame.layer)} = '{frame.layer}')"
			);

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
	}
}

//*/