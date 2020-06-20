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
	public struct MultiFetchFrame : IDisposable
	{
		internal const int NoPrevious = -1;

		internal (object dependency, int prevFetchStackLevel)[] dependencies;

		/// <summary>True when all dependencies fetched were value-types, false when any were reference-types.</summary>
		public bool IsCleanupFree => dependencies == null;

		private bool _disposed;

		internal MultiFetchFrame((object dependency, int prevFetchStackLevel)[] dependencies)
		{
			this.dependencies = dependencies;
			this._disposed = false;
		}
	
		public void Dispose()
		{
			if (_disposed || IsCleanupFree) return;
			_disposed = true;

			Dependencies.CloseFetchFrame(this);
		}
	}
}

//*/