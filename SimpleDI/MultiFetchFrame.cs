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
		internal const int NoPrevious = -1;

		internal ImmutableStack<(object dependency, int prevFetchStackLevel)> dependencies;

		/// <summary>True when all dependencies fetched were value-types, false when any were reference-types.</summary>
		/// <remarks>True if and only if no dependencies are stored.</remarks>
		public bool IsCleanupFree => dependencies == null;

		private bool _disposed;

		private MultiFetchFrame(ImmutableStack<(object dependency, int prevFetchStackLevel)> dependencies)
		{
			this.dependencies = dependencies; // ?? throw new ArgumentNullException(nameof(dependencies));
			this._disposed = false;
		}

		internal static MultiFetchFrame From(FetchFrame f)
			=> f.IsCleanupFree
			? new MultiFetchFrame()
			: new MultiFetchFrame(ImmutableStack.Create((f.dependency, f.prevFetchStackLevel)));

		private MultiFetchFrame Plus(FetchFrame f)
			=> this.IsCleanupFree && f.IsCleanupFree
			? new MultiFetchFrame()
			: this.IsCleanupFree
			? From(f)
			: f.IsCleanupFree
			? this
			: new MultiFetchFrame(this.dependencies.Push((f.dependency, f.prevFetchStackLevel)));

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// Fluent inteface to get another dependency - calls <seealso cref="Dependencies.Get{T}(out T)"/>
		/// and returns a frame that encompasses the combined result
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public MultiFetchFrame And<T>(out T dependency)
			=> Plus(Dependencies.Get(out dependency));

		public void Dispose()
		{
			if (_disposed || IsCleanupFree) return;
			_disposed = true;

			Dependencies.CloseFetchFrame(this);
		}
	}
}

//*/