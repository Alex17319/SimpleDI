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
	//	public struct MultiFetchFrame : IDisposable
	//	{
	//		internal readonly DependencyLayer layerSearchingFrom;
	//		internal readonly ImmutableStack<FetchFrame> frames;
	//	
	//		private bool _disposed;
	//	
	//		/// <summary>True when all dependencies fetched were value-types, false when any were reference-types.</summary>
	//		/// <remarks>True if and only if no dependencies are stored.</remarks>
	//		// Currently implemented by only adding in FetchFrames that need cleanup - one's that don't are skipped.
	//		public bool IsCleanupFree => frames == null || frames.IsEmpty;
	//	
	//		public int StackLevelBeforeLastFetch => this.IsCleanupFree ? -1 : frames.Peek().stackLevelBeforeFetch;
	//	
	//		private MultiFetchFrame(DependencyLayer layerSearchingFrom, ImmutableStack<FetchFrame> frames)
	//		{
	//			this.layerSearchingFrom = layerSearchingFrom ?? throw new ArgumentNullException(nameof(layerSearchingFrom));
	//			this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
	//			this._disposed = false;
	//		}
	//	
	//		internal static MultiFetchFrame From(FetchFrame f) => new MultiFetchFrame(
	//			f.layerSearchingFrom,
	//			f.IsCleanupFree ? ImmutableStack.Create<FetchFrame>() : ImmutableStack.Create(f)
	//		);
	//	
	//		private MultiFetchFrame Plus(FetchFrame f)
	//			=> f.layerSearchingFrom != this.layerSearchingFrom
	//			? throw new ArgumentException(
	//				$"Cannot combine with fetch frame searching from a different layer " +
	//				$"(required layer = '{this.layerSearchingFrom}', provided layer = '{f.layerSearchingFrom}') " +
	//				$"(i.e. this.{nameof(this.layerSearchingFrom)} != {nameof(f)}.{nameof(f.layerSearchingFrom)})."
	//			)
	//			: !this.IsCleanupFree && f.stackLevelBeforeFetch != this.StackLevelBeforeLastFetch + 1
	//			? throw new ArgumentException(
	//				$"{nameof(FetchFrame)} to be added must have {nameof(FetchFrame)}.{nameof(FetchFrame.stackLevelBeforeFetch)} " +
	//				$"one greater than this.{nameof(StackLevelBeforeLastFetch)}." +
	//				$"(required stack level = {this.StackLevelBeforeLastFetch + 1}, provided stack level = {f.stackLevelBeforeFetch})."
	//			)
	//			: this.IsCleanupFree && f.IsCleanupFree
	//			? new MultiFetchFrame()
	//			: this.IsCleanupFree
	//			? From(f)
	//			: f.IsCleanupFree
	//			? this
	//			: new MultiFetchFrame(this.layerSearchingFrom, this.frames.Push(f));
	//	
	//		/// <summary>
	//		/// <see langword="[Call inside using()]"></see>
	//		/// Fluent inteface to get another dependency - calls <seealso cref="Dependencies.Fetch{T}(out T)"/>
	//		/// and returns a frame that encompasses the combined result
	//		/// </summary>
	//		/// <typeparam name="T"></typeparam>
	//		/// <param name="dependency"></param>
	//		/// <returns></returns>
	//		public MultiFetchFrame And<T>(out T dependency, bool useFallbacks = true)
	//			=> Plus(this.layerSearchingFrom.Fetch(out dependency, useFallbacks));
	//	
	//		public void Dispose()
	//		{
	//			if (_disposed || IsCleanupFree) return;
	//			_disposed = true;
	//	
	//			layerSearchingFrom.CloseFetchFrame(this);
	//		}
	//	}
}

//*/