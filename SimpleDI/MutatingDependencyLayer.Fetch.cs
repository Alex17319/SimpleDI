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
	public sealed partial class MutatingDependencyLayer
	{
		private protected override bool StealthTryFetch<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			if (!this._dependencyStacks.TryGetValue(typeof(T), out var stack) || stack.Count == 0) {
				// No stack found, or found stack is empty
				if (useFallbacks && this.Fallback != null)
					return Logic.SucceedIf(DependencyLayer.StealthTryFetch(
						this.Fallback,
						out dependency,
						out stackLevel,
						useFallbacks,
						out layerFoundIn
					));
				else return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);
			}

			var dInfo = stack.Peek();

			// Fail if a null has been added to hide earlier dependencies
			if (dInfo.dependency == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

			// Otherwise, succeed
			return Logic.Succeed(
				out dependency, (T)dInfo.dependency,
				out stackLevel, dInfo.stackLevel,
				out layerFoundIn, this
			);
		}

		private protected override bool StealthTryFetchOuter<TOuter>(
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
			// If we can't, then try to use fallbacks if possible, calling the current method recursively, and return
			if (!this._fetchRecords.TryGetValue(self, out FetchRecord mostRecentFetch))
			{
				if (!useFallbacks || this.Fallback == null) throw new ArgumentException(
					$"No record is available of a dependency fetch having been performed for object '{self}' " +
					$"(of type '{self.GetType().FullName}'). " +
					$"Depending on how this occurred (incorrect call or invalid state), continued operation may be undefined."
				);

				return Logic.SucceedIf(DependencyLayer.StealthTryFetchOuter(
					this.Fallback,
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
			return Logic.SucceedIf(DependencyLayer.StealthTryFetchOuter(
				mostRecentFetch.layerFoundAt,
				mostRecentFetch.stackLevelFoundAt,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			));
		}

		private protected override bool StealthTryFetchOuter<TOuter>(
			int prevFetchStackLevelFoundAt,
			out TOuter dependency,
			out int stackLevel,
			bool useFallbacks,
			out DependencyLayer layerFoundIn
		) {
			// Try to find a dependency of type TOuter locally
			// If we can't, then try to use fallbacks if possible, and return whatever we find
			if (!this._dependencyStacks.TryGetValue(typeof(TOuter), out var stack) || stack.Count == 0)
			{
				if (!useFallbacks || this.Fallback == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);
				
				return Logic.SucceedIf(DependencyLayer.StealthTryFetch(
					this.Fallback,
					out dependency,
					out stackLevel,
					useFallbacks,
					out layerFoundIn
				));
			}

			// If we successfully found a dependency of type TOuter locally,
			// then we can't just return the top of the stack - as that might've
			// been added after the found fetch record was written. To fix this,
			// we do a binary search through the TOuter stack to find the last
			// dependency of type TOuter added before the fetch record was written.

			int pos = stack.BinarySearch(
				new StackedDependency(prevFetchStackLevelFoundAt, null),
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

				return Logic.Succeed(
					out dependency, (TOuter)outerInfo.dependency,
					out stackLevel, outerInfo.stackLevel,
					out layerFoundIn, this
				);
			}

			// But otherwise, we need to fall back to previous layers (if we can)
				
			// First check that we can fall back (if not, fail)
			if (!useFallbacks || this.Fallback == null) return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

			// Fallback to previous layers
			return Logic.SucceedIf(DependencyLayer.StealthTryFetch(
				this.Fallback,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			));
		}


		private protected override void AddToFetchRecord(
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



		private protected override void CloseFetchedDependency(object dependency, FetchRecord prevFetch)
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