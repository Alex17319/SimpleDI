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
		// Returns true if any dependency was successfully found, even if it's null
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

			return Logic.Succeed(
				out dependency, (T)dInfo.dependency,
				out stackLevel, dInfo.stackLevel,
				out layerFoundIn, this
			);
		}



		//	private protected override void CloseFetchedDependency(FetchFrame frame)
		//	{
		//		if (frame.stackLevelBeforeFetch + 1 < this.CurrentStackLevel)
		//		{
		//			throw new FetchFrameCloseException(
		//				$"Inner fetch frames have not been disposed - current stack level = {this.CurrentStackLevel}, " +
		//				$"stack level after creating the frame to dispose = {frame.stackLevelBeforeFetch + 1} " +
		//				$"(they would normally match)." +
		//				$"The {nameof(MutatingDependencyLayer)} class cannot recover from fetch frame close exceptions.",
		//				DisposeExceptionsManager.WrapLastExceptionThrown()
		//			);
		//		}
		//		
		//		if (frame.stackLevelBeforeFetch + 1 > this.CurrentStackLevel)
		//		{
		//			throw new FetchFrameCloseException(
		//				$"The specified fetch frame, or an outer frame, has already been disposed - " +
		//				$"current stack level = {this.CurrentStackLevel}, " +
		//				$"stack level after creating the frame to dispose = {frame.stackLevelBeforeFetch + 1} " +
		//				$"(they would normally match)." +
		//				$"The {nameof(MutatingDependencyLayer)} class cannot recover from fetch frame close exceptions.",
		//				DisposeExceptionsManager.WrapLastExceptionThrown()
		//			);
		//		}
		//	}
	}
}

//*/