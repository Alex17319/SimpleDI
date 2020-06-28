using SimpleDI.DisposeExceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	public static class Dependencies
	{
		[ThreadStatic]
		private static DependencyLayer _currentLayer = new MutatingDependencyLayer();
		public static DependencyLayer CurrentLayer => _currentLayer;

		public static event EventHandler<LayerCloseErrorEventArgs> LayerCloseMismatch;



		public static SafeDisposeExceptionsRegion SafeDisposeExceptions()
			=> DisposeExceptionsManager.SafeDisposeExceptions();

		public static IDisposableLayer NewLayer()
		{
			_currentLayer = new MutatingDependencyLayer(fallback: CurrentLayer);
			return _currentLayer.AsDisposable();
		}

		internal static void CloseLayer(DependencyLayer layer)
		{
			if (layer != CurrentLayer) {
				LayerCloseMismatch?.Invoke(null, new LayerCloseErrorEventArgs(CurrentLayer, layer));
			}

			if (layer.Fallback == null) {
				throw new InvalidDIStateException(
					"Cannot close root dependency layer (Fallback == null).",
					DisposeExceptionsManager.WrapLastExceptionThrown()
				);
			}

			if (!isParentOfLayer(layer, CurrentLayer))
			{
				// Layers are only created and added to the fallback-stack by calling NewLayer(),
				// and only removed by calling CloseLayer() via Dispose(), which only runs once.
				// If a layer has been added, and has not yet been disposed/closed, then it must be
				// possible to find it.
				// So if we failed to find it, reflection must have been used to either create a
				// layer without adding it, or used to modify the fallback of a layer.
				// Someones done that, throw an exception
				throw new InvalidDIStateException(
					$"{nameof(MutatingDependencyLayer)} '{layer}' was never opened.",
					DisposeExceptionsManager.WrapLastExceptionThrown()
				);
			}

			// Now that we know it's safe to do so (we will eventually hit the layer to close),
			// step up from the current layer, marking each layer closed as we go, until we reach
			// the layer to close, and close it.
			var l = CurrentLayer;
			while (l != null) {
				if (l == layer) {
					l.MarkDisposed(); // just in case
					_currentLayer = l.Fallback; // close all layers below l.Fallback
					return;
				}

				l.MarkDisposed();
				l = l.Fallback;
			}
		}

		private static bool isParentOfLayer(DependencyLayer parent, DependencyLayer child)
		{
			var l = child;
			while (l != null) {
				if (l == parent) return true;
				l = l.Fallback;
			}
			return false;
		}



		public static InjectFrame Inject<T>(T dependency)
			=> CurrentLayer.Inject(dependency);

		public static InjectFrame Inject(object dependency, Type toMatchAgainst)
			=> CurrentLayer.Inject(dependency, toMatchAgainst);

		public static SimultaneousInjectFrame InjectWild<T>(T dependency)
			=> CurrentLayer.InjectWild(dependency);

		public static SimultaneousInjectFrame InjectWild(object dependency, Type toMatchAgainst)
			=> CurrentLayer.InjectWild(dependency, toMatchAgainst);

		public static SimultaneousInjectFrame BeginSimultaneousInject()
			=> CurrentLayer.BeginSimultaneousInject();

		public static FetchFrame Get<T>(out T dependency)
			=> CurrentLayer.Get(out dependency, useFallbacks: true);

		public static FetchFrame GetOrNull<T>(out T dependency) where T : class
			=> CurrentLayer.GetOrNull(out dependency, useFallbacks: true);

		public static FetchFrame GetOrNull<T>(out T? dependency) where T : struct
			=> CurrentLayer.GetOrNull(out dependency, useFallbacks: true);

		public static FetchFrame GetNullableOrNull<T>(out T? dependency) where T : struct
			=> CurrentLayer.GetNullableOrNull(out dependency, useFallbacks: true);

		public static FetchFrame TryGet<T>(out T dependency, out bool found)
			=> CurrentLayer.TryGet(out dependency, out found, useFallbacks: true);

		public static FetchFrame GetOuter<TOuter>(object self, out TOuter outerDependency)
			=> CurrentLayer.GetOuter(self, out outerDependency, useFallbacks: true);

		public static FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter outerDependency) where TOuter : class
			=> CurrentLayer.GetOuterOrNull(self, out outerDependency, useFallbacks: true);

		public static FetchFrame GetOuterOrNull<TOuter>(object self, out TOuter? outerDependency) where TOuter : struct
			=> CurrentLayer.GetOuterOrNull(self, out outerDependency, useFallbacks: true);

		public static FetchFrame GetOuterNullableOrNull<TOuter>(object self, out TOuter? outerDependency) where TOuter : struct
			=> CurrentLayer.GetOuterNullableOrNull(self, out outerDependency, useFallbacks: true);

		public static FetchFrame TryGetOuter<TOuter>(object self, out TOuter outerDependency, out bool found)
			=> CurrentLayer.TryGetOuter(self, out outerDependency, out found, useFallbacks: true);
	}
}
