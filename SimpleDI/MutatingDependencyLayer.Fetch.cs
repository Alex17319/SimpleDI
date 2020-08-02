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
	}
}

//*/