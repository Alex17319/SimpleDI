﻿using System;
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
	//	public struct FetchFrame : IDisposable
	//	{
	//		internal const int NoPrevious = -1;
	//		private const int CleanupFreeFlag = -2;
	//	
	//		internal readonly DependencyLayer layerSearchingFrom;
	//		internal readonly object dependency;
	//		internal readonly int stackLevelBeforeFetch;
	//	
	//		/// <summary>True when the dependency fetched was a value-type, false when it was a reference-type.</summary>
	//		public bool IsCleanupFree => dependency == null;
	//	
	//		public static readonly FetchFrame CleanupFree = default;
	//	
	//		private bool _disposed;
	//	
	//		internal FetchFrame(
	//			DependencyLayer layerSearchingFrom,
	//			object dependency,
	//			int stackLevelBeforeFetch
	//		) {
	//			if (stackLevelBeforeFetch < 0) throw new ArgumentOutOfRangeException(
	//				nameof(stackLevelBeforeFetch),
	//				stackLevelBeforeFetch,
	//				"Cannot be negative."
	//			);
	//	
	//			this.layerSearchingFrom = layerSearchingFrom ?? throw new ArgumentNullException(nameof(layerSearchingFrom));
	//			this.dependency = dependency ?? throw new ArgumentNullException(nameof(layerSearchingFrom));
	//			this._disposed = false;
	//			this.stackLevelBeforeFetch = stackLevelBeforeFetch;
	//		}
	//	
	//		/// <summary>
	//		/// <see langword="[Call inside using()]"></see>
	//		/// Fluent inteface to fetch another dependency - calls <seealso cref="Dependencies.Fetch{T}(out T)"/>
	//		/// and returns a frame that encompasses the combined result
	//		/// </summary>
	//		/// <typeparam name="T"></typeparam>
	//		/// <param name="dependency"></param>
	//		/// <returns></returns>
	//		public MultiFetchFrame And<T>(out T dependency)
	//			=> MultiFetchFrame.From(this).And(out dependency);
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