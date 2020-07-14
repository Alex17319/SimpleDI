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


		SearchableStack<StackFrame> _stack = new SearchableStack<StackFrame>() { StackFrame.Base };

		//private int currentStackLevel;
		protected override int CurrentStackLevel => _stack.Count - 1;


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

		private protected override bool TryGetFromFetchRecords(object self, out FetchRecord mostRecentFetch)
			=> this._stack.Peek().fetchRecords.TryGetValue(self, out mostRecentFetch);

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

		private protected override bool StealthTryFetchOuter<TOuter>(
			int prevFetchStackLevelFoundAt,
			out TOuter dependency,
			out int stackLevel,
			bool useFallbacks,
			out DependencyLayer layerFoundIn
		) {
			Debug.Assert(prevFetchStackLevelFoundAt >= 1);

			// Try to find a dependency of type TOuter locally
			// If we can't, then try to use fallbacks if possible, and return whatever we find

			// If the previous fetch found a dependency that's in the currently available stack, get
			// the place one deeper into the stack.
			// If not, i.e. the current stack is too short, then just use the end of the stack as
			// the place to start searching from (TODO: Should we throw an exception instead?)
			// If the stack is empty, have to fallback to other layers.
			StackFrame frameFetchedFrom =
				prevFetchStackLevelFoundAt - 1 < this.CurrentStackLevel
				? this._stack[prevFetchStackLevelFoundAt - 1]
				: this._stack.Count > 0
				? this._stack.Peek()
				: StackFrame.Null;

			if (!frameFetchedFrom.IsNull && frameFetchedFrom.dependencies.TryGetValue(typeof(TOuter), out var dep))
			{
				return Logic.Succeed(
					out dependency, (TOuter)dep.dependency,
					out stackLevel, dep.stackLevel,
					out layerFoundIn, this
				);
			}


			// Didn't find a dependency of type TOuter locally; try to use fallbacks if we can
			if (useFallbacks || this.Fallback != null) return Logic.SucceedIf(DependencyLayer.StealthTryFetch(
				this.Fallback,
				out dependency,
				out stackLevel,
				useFallbacks,
				out layerFoundIn
			));

			// Can't use fallbacks either; fail
			return Logic.Fail(out dependency, out stackLevel, out layerFoundIn);
		}

		private protected override void AddToFetchRecord(object dependency, DependencyLayer layerFoundIn, int stackLevelFoundAt, out FetchRecord prevFetch)
		{
			// No need to return prevFetch here as we're using a stack of immutable dictionaries (so don't
			// have to put it back in later, just delete the later frames on the stack)
			prevFetch = FetchRecord.Null;

			_stack.Push(_stack.Peek().WithFetchRecord(
				dependency,
				new FetchRecord(layerFoundIn, stackLevelFoundAt)
			));
		}

		internal override void CloseFetchFrame(FetchFrame frame)
		{
			if (frame.IsCleanupFree) return;

			CloseFetchFrame_CheckLayer(frame);

			if (frame.stackLevelBeforeFetch + 1 < this.CurrentStackLevel)
			{
				throw new FetchFramesNotDisposedException(
					() => {

						//TODO: Cleanup logic

						closeFetchedDependency(frame.dependency, frame.prevFetch);
					},
					$"Inner fetch frames have not been disposed - current stack level = {this.CurrentStackLevel}, " +
					$"stack level after creating the frame to dispose = {frame.stackLevelBeforeFetch + 1} " +
					$"(they would normally match)." +
					$"This error may be recovered from by calling {nameof(FetchFramesNotDisposedException)}" +
					$".{nameof(FetchFramesNotDisposedException.CloseFrameAndDescendants)}()."
				);
			}
			
			if (frame.stackLevelBeforeFetch + 1 > this.CurrentStackLevel)
			{
				throw new FetchFrameCloseException(
					$"Fetch frame has already been disposed - current stack level = {this.CurrentStackLevel}, " +
					$"stack level after creating the frame to dispose = {frame.stackLevelBeforeFetch + 1} " +
					$"(they would normally match)." +
					$"This error may be ignored, and furture operation should be unaffected, " +
					$"however previous actions may have produced incorrect results (eg. the wrong dependency was fetched)."
				);
			}

			closeFetchedDependency(frame.dependency, frame.prevFetch);
		}

		private void closeFetchedDependency(object dependency, FetchRecord prevFetch)
		{
			// Only ever look in/edit current layer (the record is only ever added to the
			// current layer, as in general we must not modify other layers).

			if (!prevFetch.IsNull) throw new FetchFrameCloseException(
				$"{nameof(SafeDependencyLayer)} fetch frames must always have " +
				$"{nameof(FetchFrame.prevFetch)}.{nameof(FetchRecord.IsNull)} == {true}."
			);

			// By the time this method is called, we'll have checked that the layers

			if (!_stack.PeekSecond().fetchRecords.ContainsKey(dependency)) {
				var top = _stack.Peek();
				var removed = top.fetchRecords.Remove(dependency);
				if (top == _stack.)
			}

			FetchFrameCloseException noEntryPresentException() => new FetchFrameCloseException(
				$"No entry in fetch record available to remove for object '{dependency}' " +
				$"(with reference-equality hashcode '{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dependency)}').",
				DisposeExceptionsManager.WrapLastExceptionThrown()
			);
		}

		private struct StackFrame
		{
			//public readonly int stackLevel;

			public readonly ImmutableDictionary<Type, StackedDependency> dependencies;

			// Maps from a dependency that has been fetched to the stack level that it was originally injected at
			public readonly ImmutableDictionary<object, FetchRecord> fetchRecords;

			public static readonly StackFrame Null = default;
			public bool IsNull => this.dependencies == null;

			public static readonly StackFrame Base = new StackFrame(
				//0,
				ImmutableDictionary.Create<Type, StackedDependency>(),
				ImmutableDictionary.Create<object, FetchRecord>()
			);

			public bool IsBase
				=> //this.stackLevel == Base.stackLevel
				ReferenceEquals(this.dependencies, Base.dependencies)
				&& ReferenceEquals(this.fetchRecords, Base.fetchRecords);

			public StackFrame(
				//int stackLevel,
				ImmutableDictionary<Type, StackedDependency> dependencies,
				ImmutableDictionary<object, FetchRecord> fetchRecords
			) {
				//if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), stackLevel, "Cannot be negative");

				//this.stackLevel = stackLevel;
				this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
				this.fetchRecords = fetchRecords ?? throw new ArgumentNullException(nameof(fetchRecords));
			}

			public StackFrame WithFetchRecord(object dep, FetchRecord record)
				=> new StackFrame(this.dependencies, this.fetchRecords.Add(dep, record));
		}
	}
}

//*/