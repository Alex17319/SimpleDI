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
		internal readonly ImmutableDictionary<Type, StackedDependency> dependencies;

		public bool IsNull => dependencies == null;
		public static readonly DependencySnapshot Null = default;

		public bool IsEmpty => !IsNull && dependencies.IsEmpty;
		public static readonly DependencySnapshot Empty = new DependencySnapshot(ImmutableDictionary.Create<Type, StackedDependency>());

		internal DependencySnapshot(ImmutableDictionary<Type, StackedDependency> dependencies)
		{
			this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
		}

		public T Fetch<T>() {
			if (dependencies.TryGetValue(typeof(T), out var dInfo)) {
				dInfo.RunOnFetchFromSnapshot();
				return (T)dInfo.Dependency;
			}
			throw new DependencyNotFoundException(typeof(T));
		}

		public T FetchOrNull<T>() where T : class
			=> throw new NotImplementedException();

		public T? FetchStructOrNull<T>() where T : struct
			=> throw new NotImplementedException();

		public T? FetchNullableOrNull<T>() where T : struct
			=> throw new NotImplementedException();

		public bool TryFetch<T>(out T dependency)
			=> throw new NotImplementedException();
	}
}

//*/