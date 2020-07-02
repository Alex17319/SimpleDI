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
	public class SafeDependencyLayer : DependencyLayer
	{


		ImmutableStack<StackFrame> _stack = ImmutableStack.Create(new StackFrame(
			stackLevel: 0,
			ImmutableDictionary.Create<Type, SearchableStack<StackedDependency>>(),
			ImmutableDictionary.Create<object, FetchRecord>()
		));

		private int currentStackLevel;


		internal SafeDependencyLayer() : base() { }
		internal SafeDependencyLayer(DependencyLayer fallback) : base(fallback) { }

		public override SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst)
		{
			throw new NotImplementedException();
		}

		public override SimultaneousInjectFrame InjectWild<T>(T dependency)
		{
			throw new NotImplementedException();
		}

		protected override InjectFrame InjectInternal(object dependency, Type toMatchAgainst)
		{
			throw new NotImplementedException();
		}

		internal override void CloseInjectFrame(InjectFrame frame)
		{
			throw new NotImplementedException();
		}

		internal override void CloseInjectFrame(SimultaneousInjectFrame frame)
		{
			throw new NotImplementedException();
		}

		internal override SimultaneousInjectFrame InjectMoreSimultaneously<T>(SimultaneousInjectFrame soFar, T dependency, bool isWildcard)
		{
			throw new NotImplementedException();
		}

		internal override SimultaneousInjectFrame InjectMoreSimultaneously(SimultaneousInjectFrame soFar, object dependency, Type toMatchAgainst, bool isWildcard)
		{
			throw new NotImplementedException();
		}

		private protected override void AddToFetchRecord(object dependency, DependencyLayer layerFoundIn, int stackLevelFoundAt, out FetchRecord prevFetch)
		{
			throw new NotImplementedException();
		}

		private protected override void CloseFetchedDependency(object dependency, FetchRecord prevFetch)
		{
			throw new NotImplementedException();
		}

		private protected override bool StealthTryFetch<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			throw new NotImplementedException();
		}

		private protected override bool StealthTryFetchOuter<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
		{
			throw new NotImplementedException();
		}

		private struct StackFrame
		{
			public readonly int stackLevel;

			public readonly ImmutableDictionary<Type, SearchableStack<StackedDependency>> dependencyStacks;

			// Maps from a dependency that has been fetched to the stack level that it was originally injected at
			public readonly ImmutableDictionary<object, FetchRecord> fetchRecords;

			public bool IsNull => this.dependencyStacks == null;

			public StackFrame(
				int stackLevel,
				ImmutableDictionary<Type, SearchableStack<StackedDependency>> dependencyStacks,
				ImmutableDictionary<object, FetchRecord> fetchRecords
			) {
				if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Cannot be negative");

				this.stackLevel = stackLevel;
				this.dependencyStacks = dependencyStacks ?? throw new ArgumentNullException(nameof(dependencyStacks));
				this.fetchRecords = fetchRecords ?? throw new ArgumentNullException(nameof(fetchRecords));
			}
		}
	}
}

//*/