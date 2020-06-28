using System;

namespace SimpleDI
{
	public abstract class DependencyLayer
	{
		/// <summary>
		/// Fallbacks are used to increase the search space, but will not be modified in any way.
		/// </summary>
		public DependencyLayer Fallback { get; }

		private bool _disposed = false;

		internal DependencyLayer()
		{
			this.Fallback = null;
		}

		internal DependencyLayer(DependencyLayer fallback)
		{
			this.Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
		}

		private void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			Dependencies.CloseLayer(this);
		}

		internal void MarkDisposed()
		{
			_disposed = true;
		}

		internal IDisposableLayer AsDisposable()
		{
			return new Disposer(this);
		}

		// Used to prevent people from randomly calling Dependencies.CurrentLayer.Dispose()
		// Instead of DependencyLayer implementing IDisposable, this class implements it,
		// and passes on the call to DependencyLayer. External code can't make a proper
		// instance of this class in order to call Dispose() (except via reflection)
		private class Disposer : IDisposableLayer
		{
			public DependencyLayer Layer { get; }

			public bool IsNull => Layer == null;
			public static readonly Disposer Null = default;

			internal Disposer(DependencyLayer layer) {
				this.Layer = layer;
			}

			public void Dispose() {
				this.Layer.Dispose();
			}
		}

		public abstract SimultaneousInjectFrame BeginSimultaneousInject();
		public abstract FetchFrame Get<T>(out T dependency, bool useFallbacks);
		public abstract FetchFrame GetNullableOrNull<T>(out T? dependency, bool useFallbacks) where T : struct;
		public abstract FetchFrame GetOrNull<T>(out T dependency, bool useFallbacks) where T : class;
		public abstract FetchFrame GetOrNull<T>(out T? dependency, bool useFallbacks) where T : struct;
		public abstract FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency, bool useFallbacks);
		public abstract FetchFrame GetOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks) where TOuter : struct;
		public abstract FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency, bool useFallbacks) where TOuter : class;
		public abstract FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks) where TOuter : struct;
		public abstract InjectFrame Inject(object dependency, Type toMatchAgainst);
		public abstract InjectFrame Inject<T>(T dependency);
		public abstract SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst);
		public abstract SimultaneousInjectFrame InjectWild<T>(T dependency);
		public abstract FetchFrame TryGet<T>(out T dependency, out bool found, bool useFallbacks);
		public abstract FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found, bool useFallbacks);

		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously<T>(SimultaneousInjectFrame soFar, T dependency, bool isWildcard);
		internal abstract SimultaneousInjectFrame InjectMoreSimultaneously(SimultaneousInjectFrame soFar, object dependency, Type toMatchAgainst, bool isWildcard);

		protected abstract bool StealthTryGet<T>(out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);
		protected abstract bool StealthTryGetOuter<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn);

		protected static bool StealthTryGet<T>(DependencyLayer @this, out T dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryGet(out dependency, out stackLevel, useFallbacks, out layerFoundIn);

		protected static bool StealthTryGetOuter<TOuter>(DependencyLayer @this, object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out DependencyLayer layerFoundIn)
			=> @this.StealthTryGetOuter(self, out dependency, out stackLevel, useFallbacks, out layerFoundIn);

		internal abstract void CloseInjectFrame(InjectFrame frame);
		internal abstract void CloseInjectFrame(SimultaneousInjectFrame frame);
		internal abstract void CloseFetchFrame(FetchFrame frame);
		internal abstract void CloseFetchFrame(MultiFetchFrame multiFrame);
	}
}