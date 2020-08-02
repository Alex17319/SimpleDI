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


		private SnapshotableStack<StackFrame> _stack = new SnapshotableStack<StackFrame>(StackFrame.Base);

		//private int currentStackLevel;
		protected override int CurrentStackLevel => _stack.Count - 1;


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

		// Returns true if any dependency was successfully found, even if it's null
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



		private struct StackFrame
		{
			//public readonly int stackLevel;

			public readonly ImmutableDictionary<Type, StackedDependency> dependencies;

			//	// Maps from a dependency that has been fetched to the stack level that it was originally injected at
			//	public readonly ImmutableDictionary<object, FetchRecord> fetchRecords;

			public static readonly StackFrame Null = default;
			public bool IsNull => this.dependencies == null;

			public static readonly StackFrame Base = new StackFrame(
				//0,
				ImmutableDictionary.Create<Type, StackedDependency>()
			);

			public bool IsBase
				=> //this.stackLevel == Base.stackLevel
				ReferenceEquals(this.dependencies, Base.dependencies);

			public StackFrame(
				//int stackLevel,
				ImmutableDictionary<Type, StackedDependency> dependencies
			) {
				//if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Cannot be negative");

				//this.stackLevel = stackLevel;
				this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
			}
		}
	}
}

//*/