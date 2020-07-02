using SimpleDI.TryGet;
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


		ImmutableStack<StackFrame> _stack = ImmutableStack.Create(StackFrame.Base);

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
			=> _stack.Peek().dependencies.TryGetValue(typeof(T), out StackedDependency dep)
			? Logic.Succeed(
				out dependency, (T)dep.dependency,
				out stackLevel, dep.stackLevel,
				out layerFoundIn, this
			)
			: useFallbacks
			? Logic.SucceedIf(DependencyLayer.StealthTryFetch(
				this.Fallback,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			))
			: Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

		private protected override bool StealthTryFetchOuter<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> self == null
			? throw new ArgumentNullException(nameof(self))
			: self.GetType().IsValueType
			? throw new ArgumentTypeException(
				$"Only reference-type dependencies may fetch outer dependencies from when they were injected. " +
				$"Object '{self}' is of type '{self.GetType().FullName}', which is a value-type.",
				nameof(self)
			)
			: this._stack.Peek().fetchRecords.TryGetValue(self, out FetchRecord mostRecentFetch)
			? Logic.Succeed(
				out dependency, 
			)
			: Logic.Fail(out dependency, out stackLevel, out layerFoundIn);

		private struct StackFrame
		{
			public readonly int stackLevel;

			public readonly ImmutableDictionary<Type, StackedDependency> dependencies;

			// Maps from a dependency that has been fetched to the stack level that it was originally injected at
			public readonly ImmutableDictionary<object, FetchRecord> fetchRecords;

			public static readonly StackFrame Null = default;
			public bool IsNull => this.dependencies == null;

			public static readonly StackFrame Base = new StackFrame(
				0,
				ImmutableDictionary.Create<Type, StackedDependency>(),
				ImmutableDictionary.Create<object, FetchRecord>()
			);

			public bool IsBase
				=> this.stackLevel == Base.stackLevel
				&& ReferenceEquals(this.dependencies, Base.dependencies)
				&& ReferenceEquals(this.fetchRecords, Base.fetchRecords);

			public StackFrame(
				int stackLevel,
				ImmutableDictionary<Type, StackedDependency> dependencies,
				ImmutableDictionary<object, FetchRecord> fetchRecords
			) {
				if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Cannot be negative");

				this.stackLevel = stackLevel;
				this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
				this.fetchRecords = fetchRecords ?? throw new ArgumentNullException(nameof(fetchRecords));
			}
		}
	}
}

//*/