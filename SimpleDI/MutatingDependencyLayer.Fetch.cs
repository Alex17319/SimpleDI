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
		public override bool TryFetch<T>(out T dependency, bool useFallbacks)
		{
			if (!this._dependencyStacks.TryGetValue(typeof(T), out var stack) || stack.Count == 0) {
				// No stack found, or found stack is empty

				if (!useFallbacks && this.Fallback == null)
					return Logic.Fail(out dependency);

				return Logic.SucceedIf(DependencyLayer.TryFetch(
					this.Fallback,
					out dependency,
					useFallbacks
				)); 
			}

			var dInfo = stack.Peek();

			if (dInfo.IsNull) return Logic.Fail(out dependency);

			dInfo.RunOnFetch();

			return Logic.Succeed(
				out dependency, (T)dInfo.dependency
			);
		}
	}
}

//*/