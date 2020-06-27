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
	public struct MultiFetchFrame : IDisposable
	{
		internal readonly DependencyLayer layerSearchingFrom;
		internal ImmutableStack<FetchFrame> frames;
		private readonly bool _needsCleanup;

		private bool _disposed;

		/// <summary>True when all dependencies fetched were value-types, false when any were reference-types.</summary>
		/// <remarks>True if and only if no dependencies are stored.</remarks>
		public bool IsCleanupFree => !_needsCleanup;

		private MultiFetchFrame(DependencyLayer layerSearchingFrom, ImmutableStack<FetchFrame> frames)
		{
			this.layerSearchingFrom = layerSearchingFrom ?? throw new ArgumentNullException(nameof(layerSearchingFrom));
			this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
			this._disposed = false;
			this._needsCleanup = !frames.IsEmpty && frames.Any(d => !d.IsCleanupFree);
		}

		internal static MultiFetchFrame From(FetchFrame f) => new MultiFetchFrame(
			f.layerSearchingFrom,
			f.IsCleanupFree ? ImmutableStack.Create<FetchFrame>() : ImmutableStack.Create(f)
		);

		private MultiFetchFrame Plus(FetchFrame f)
			=> f.layerSearchingFrom != this.layerSearchingFrom
			? throw new ArgumentException(
				$"Cannot combine with fetch frame searching from a different layer " +
				$"(required layer = '{this.layerSearchingFrom}', provided layer = '{f.layerSearchingFrom}')."
			)
			: this.IsCleanupFree && f.IsCleanupFree
			? new MultiFetchFrame()
			: this.IsCleanupFree
			? From(f)
			: f.IsCleanupFree
			? this
			: new MultiFetchFrame(this.layerSearchingFrom, this.frames.Push(f));

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fluent inteface to get another dependency - calls <seealso cref="Dependencies.Get{T}(out T)"/>
		/// and returns a frame that encompasses the combined result
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public MultiFetchFrame And<T>(out T dependency, bool useFallbacks = true)
			=> Plus(this.layerSearchingFrom.Get(out dependency, useFallbacks));

		public void Dispose()
		{
			if (_disposed || IsCleanupFree) return;
			_disposed = true;

			layerSearchingFrom.CloseFetchFrame(this);
		}
	}
}

//*/