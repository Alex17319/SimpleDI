using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	public struct DependencySnapshot
	{
		internal readonly ImmutableDictionary<Type, StackedDependency> Dependencies;

		public bool IsNull => Dependencies == null;
		public static readonly DependencySnapshot Null = default;

		public bool IsEmpty => !IsNull && Dependencies.IsEmpty;
		public static readonly DependencySnapshot Empty = new DependencySnapshot(ImmutableDictionary.Create<Type, StackedDependency>());

		internal DependencySnapshot(ImmutableDictionary<Type, StackedDependency> dependencies)
		{
			this.Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
		}
	}
}

//*/