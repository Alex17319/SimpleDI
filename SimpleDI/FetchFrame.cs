using System;
using System.Collections;
using System.Collections.Generic;
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
	public struct FetchFrame : IDisposable
	{
		internal const int NoPrevious = -1;
		private const int CleanupFreeFlag = -2;

		internal readonly object dependency;
		internal readonly int prevFetchStackLevel;

		/// <summary>True when the dependency fetched were value-types, false when any were reference-types.</summary>
		public bool IsCleanupFree => prevFetchStackLevel == CleanupFreeFlag;

		public static readonly FetchFrame CleanupFree = default;

		private bool _disposed;

		internal FetchFrame(object dependency, int prevFetchStackLevel)
		{
			this.dependency = dependency;
			this.prevFetchStackLevel = prevFetchStackLevel;
			this._disposed = false;
		}

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fluent inteface to fetch another dependency - calls <seealso cref="Dependencies.Get{T}(out T)"/>
		/// and returns a frame that encompasses the combined result
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public MultiFetchFrame And<T>(out T dependency)
			=> MultiFetchFrame.From(this).And(out dependency);

		public void Dispose()
		{
			if (_disposed || IsCleanupFree) return;
			_disposed = true;

			Dependencies.CloseFetchFrame(this);
		}
	}

	//	// Renaming of DependencyFrame for clarity when using Dependencies.Get()
	//	public struct DependencyFetchFrame : IDisposable
	//	{
	//		internal readonly DependencyFrame inner;
	//	
	//		internal DependencyFetchFrame(DependencyFrame inner)
	//		{
	//			this.inner = inner;
	//		}
	//	
	//		public void Dispose()
	//		{
	//			this.inner.Dispose();
	//		}
	//	
	//		public static implicit operator DependencyFetchFrame(DependencyFrame x) => new DependencyFetchFrame(x);
	//		public static implicit operator DependencyFrame(DependencyFetchFrame x) => x.inner;
	//	}
}

//*/