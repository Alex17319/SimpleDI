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
	public partial class DependencyLayer
	{
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

			return new FetchFrame(layerSearchingFrom: this, dInfo.dependency, prevFetch);

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

			return new FetchFrame(layerSearchingFrom: this, outerInfo.dependency, prevOuterFetch);

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

		private bool tryGetFromFetchRecord(
			object key,
			out FetchRecord value,
			bool useFallbacks,
			out DependencyLayer layerFoundIn
		) {
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

		private bool tryGetFromDependencyStacks(
			Type key,
			out SearchableStack<StackedDependency> value,
			bool useFallbacks,
			out DependencyLayer layerFoundIn
		) {
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



		internal void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			closeFetchedDependency_internal(frame.dependency, frame.prevFetch);
		}

		internal void CloseFetchFrame(MultiFetchFrame multiFrame)
		{
			if (multiFrame.IsCleanupFree) return;

			foreach (FetchFrame f in multiFrame.frames)
			{
				closeFetchedDependency_internal(f.dependency, f.prevFetch);
			}
		}

		private void closeFetchedDependency_internal(object dependency, FetchRecord prevFetch)
		{
			// Only ever look in/edit current layer (the record is only ever added to the
			// current layer, as in general we must not modify other layers).

			if (prevFetch.IsNull)
			{
				if (!_fetchRecords.Remove(dependency)) throw noEntryPresentException();
			}
			else
			{
				if (!_fetchRecords.ContainsKey(dependency)) throw noEntryPresentException();
				_fetchRecords[dependency] = prevFetch;
			}

			FetchFrameCloseException noEntryPresentException() => new FetchFrameCloseException(
				$"No entry in fetch record available to remove for object '{dependency}' " +
				$"(with reference hashcode '{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dependency)}').",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);
		}
	}
}

//*/