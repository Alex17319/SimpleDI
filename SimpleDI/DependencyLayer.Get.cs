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
using SimpleDI.TryGet;

namespace SimpleDI
{
	public partial class MutatingDependencyLayer
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
		public override FetchFrame Get<T>(out T dependency, bool useFallbacks)
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
		public override FetchFrame GetOrNull<T>(out T dependency, bool useFallbacks)
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
		public override FetchFrame GetOrNull<T>(out T? dependency, bool useFallbacks)
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
		public override FetchFrame GetNullableOrNull<T>(out T? dependency, bool useFallbacks)
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
		public override FetchFrame TryGet<T>(out T dependency, out bool found, bool useFallbacks)
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
			this.addToFetchRecord(dependency, layerFoundIn, stackLevel, out var prevFetch);

			return new FetchFrame(layerSearchingFrom: this, dependency, prevFetch);
		}

		protected override bool StealthTryGet<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			if (!this._dependencyStacks.TryGetValue(typeof(T), out var stack) || stack.Count == 0) {
				// No stack found, or found stack is empty
				if (useFallbacks && this.Fallback != null)
					return DependencyLayer.StealthTryGet(
						this.Fallback,
						out dependency,
						out stackLevel,
						useFallbacks,
						out layerFoundIn
					);
				else return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);
			}

			var dInfo = stack.Peek();
			dependency = (T)dInfo.dependency;

			// Fail if a null has been added to hide earlier dependencies
			if (dependency == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

			// Otherwise, succeed
			stackLevel = dInfo.stackLevel;
			layerFoundIn = this;
			return true;
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
		public override FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
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
		public override FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency, bool useFallbacks)
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
		public override FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
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
		public override FetchFrame GetOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks)
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
		public override FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found, bool useFallbacks)
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
			this.addToFetchRecord(outerDependency, layerOuterFoundIn, outerStackLevel, out FetchRecord prevOuterFetch);

			return new FetchFrame(layerSearchingFrom: this, outerDependency, prevOuterFetch);
		}

		protected override bool StealthTryGetOuter<TOuter>(
			object self,
			out TOuter dependency,
			out int stackLevel,
			bool useFallbacks,
			out DependencyLayer layerFoundIn
		) {
			if (self == null) throw new ArgumentNullException(nameof(self));

			if (self.GetType().IsValueType) throw new ArgumentTypeException(
				$"Only reference-type dependencies may fetch outer dependencies from when they were injected. " +
				$"Object '{self}' is of type '{self.GetType().FullName}', which is a value-type.",
				nameof(self)
			);

			// Try to find a fetch record locally
			// If we can't, then try to use fallbacks if possible, calling the current method recursively
			if (!this._fetchRecords.TryGetValue(self, out FetchRecord mostRecentFetch))
			{
				if (!useFallbacks || this.Fallback == null) throw new ArgumentException(
					$"No record is available of a dependency fetch having been performed for object '{self}' " +
					$"(of type '{self.GetType().FullName}'). " +
					$"Depending on how this occurred (incorrect call or invalid state), continued operation may be undefined."
				);

				return DependencyLayer.StealthTryGetOuter(
					this.Fallback,
					self,
					out dependency,
					out stackLevel,
					useFallbacks,
					out layerFoundIn
				);
			}
			
			// Try to find a dependency of type TOuter locally
			// If we can't, then try to use fallbacks if possible, and return whatever we find 
			if (!this._dependencyStacks.TryGetValue(typeof(TOuter), out var stack) || stack.Count == 0)
			{
				if (!useFallbacks || this.Fallback == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);
				
				return DependencyLayer.StealthTryGet(
					this.Fallback,
					out dependency,
					out stackLevel,
					useFallbacks,
					out layerFoundIn
				);
			}

			// If we successfully found a dependency of type TOuter locally,
			// then we can't just return the top of the stack - as that might've
			// been added after the found fetch record was written. To fix this,
			// we do a binary search through the TOuter stack to find the last
			// dependency of type TOuter added before the fetch record was written.

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

			// Now, if prevPos >= 0, we've sucessfully found the dependency locally

			if (pos >= 0) {
				var outerInfo = stack[prevPos];

				// TODO: Should it be possible to hide non-nullable value type dependencies?? Currently isn't
				// Fail if a null has been added to hide earlier dependencies
				if (outerInfo.dependency == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

				dependency = (TOuter)outerInfo.dependency;
				stackLevel = outerInfo.stackLevel;
				layerFoundIn = this;
				return true;
			}

			// But otherwise, we need to fall back to previous layers (if we can)
				
			// First check that we can fall back (if not, fail)
			if (!useFallbacks || this.Fallback == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

			// Fallback to previous layers
			return DependencyLayer.StealthTryGet(
				this.Fallback,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			);
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



		internal override void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			closeFetchedDependency_internal(frame.dependency, frame.prevFetch);
		}

		internal override void CloseFetchFrame(MultiFetchFrame multiFrame)
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