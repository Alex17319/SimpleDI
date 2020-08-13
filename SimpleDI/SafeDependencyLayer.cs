using SimpleDI.DisposeExceptions;
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

		// TODO: If possible just directly use a stack of DependencySnapshots
		// TODO: Switch from having a SnapshottedDependency struct to just using StackedDependency with a flag set,
		// so that the immutable dictionary snapshot can be reused as a starting point for a layer as planned.
		private SnapshotableStack<StackFrame> _stack = new SnapshotableStack<StackFrame>(StackFrame.Base);

		protected override int CurrentStackLevel => _stack.Count - 1;

		public override bool SnapshotReady => throw new NotImplementedException();

		internal SafeDependencyLayer() : base() { }
		internal SafeDependencyLayer(DependencyLayer fallback) : base(fallback) { }

		private StackFrame getStackFrame(int stackLevel)
			=> _stack.ElementAt(CurrentStackLevel - stackLevel);

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

		public override bool TryFetch<T>(out T dependency, bool useFallbacks)
			=> _stack.Peek().dependencies.TryGetValue(typeof(T), out StackedDependency dep)
			? Logic.SucceedIf(
				!dep.IsNull,
				out dependency, (T)dep.dependency
			)
			: useFallbacks
			? Logic.SucceedIf(DependencyLayer.TryFetch(
				this.Fallback,
				out dependency,
				useFallbacks
			))
			: Logic.Fail(out dependency);

		public override DependencySnapshot Snapshot(bool useFallbacks)
		{
			throw new NotImplementedException();
		}

		private protected override void AddAsFallbackToSnapshot(ImmutableDictionary<Type, SnapshottedDependency>.Builder snapshotBuilder)
		{
			throw new NotImplementedException();
		}

		private struct StackFrame
		{
			public readonly ImmutableDictionary<Type, StackedDependency> dependencies;

			public static readonly StackFrame Null = default;
			public bool IsNull => this.dependencies == null;

			public static readonly StackFrame Base = new StackFrame(
				ImmutableDictionary.Create<Type, StackedDependency>()
			);

			public bool IsBase => ReferenceEquals(this.dependencies, Base.dependencies);

			public StackFrame(
				ImmutableDictionary<Type, StackedDependency> dependencies
			) {
				this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
			}
		}
	}
}

//*/