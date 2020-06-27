using System;

namespace SimpleDI
{
	public interface IDependencyLayer
	{
		IDependencyLayer Fallback { get; }

		SimultaneousInjectFrame BeginSimultaneousInject();
		FetchFrame Get<T>(out T dependency, bool useFallbacks);
		FetchFrame GetNullableOrNull<T>(out T? dependency, bool useFallbacks) where T : struct;
		FetchFrame GetOrNull<T>(out T dependency, bool useFallbacks) where T : class;
		FetchFrame GetOrNull<T>(out T? dependency, bool useFallbacks) where T : struct;
		FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency, bool useFallbacks);
		FetchFrame GetOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks) where TOuter : struct;
		FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency, bool useFallbacks) where TOuter : class;
		FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter? outerDependency, bool useFallbacks) where TOuter : struct;
		InjectFrame Inject(object dependency, Type toMatchAgainst);
		InjectFrame Inject<T>(T dependency);
		SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst);
		SimultaneousInjectFrame InjectWild<T>(T dependency);
		FetchFrame TryGet<T>(out T dependency, out bool found, bool useFallbacks);
		FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found, bool useFallbacks);
	}

	internal interface _DependencyLayerInternal : IDependencyLayer
	{
		IDisposableLayer AsDisposable();
		void MarkDisposed();

		SimultaneousInjectFrame InjectMoreSimultaneously<T>(SimultaneousInjectFrame soFar, T dependency, bool isWildcard);
		SimultaneousInjectFrame InjectMoreSimultaneously(SimultaneousInjectFrame soFar, object dependency, Type toMatchAgainst, bool isWildcard);

		bool StealthTryGet<T>(out T dependency, out int stackLevel, bool useFallbacks, out IDependencyLayer layerFoundIn);
		bool StealthTryGetOuter<TOuter>(object self, out TOuter dependency, out int stackLevel, bool useFallbacks, out IDependencyLayer layerFoundIn);

		void CloseInjectFrame(InjectFrame frame);
		void CloseInjectFrame(SimultaneousInjectFrame frame);
		void CloseFetchFrame(FetchFrame frame);
		void CloseFetchFrame(MultiFetchFrame multiFrame);
	}
}