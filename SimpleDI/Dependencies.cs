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

		public static DependencyLayer.Disposer NewLayer()
		{
			_currentLayer = new MutatingDependencyLayer(fallback: CurrentLayer);
			return new DependencyLayer.Disposer(_currentLayer);
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

		public static T Fetch<T>()
			=> CurrentLayer.Fetch<T>(useFallbacks: true);

		public static T FetchOrNull<T>() where T : class
			=> CurrentLayer.FetchOrNull<T>(useFallbacks: true);

		public static T? FetchStructOrNull<T>() where T : struct
			=> CurrentLayer.FetchStructOrNull<T>(useFallbacks: true);

		public static T? FetchNullableOrNull<T>() where T : struct
			=> CurrentLayer.FetchNullableOrNull<T>(useFallbacks: true);

		public static bool TryFetch<T>(out T dependency)
			=> CurrentLayer.TryFetch(out dependency, useFallbacks: true);
	}
}
