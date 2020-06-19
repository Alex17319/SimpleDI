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
	public struct DependencyFrame : IDisposable
	{
		internal readonly int stackLevel;
		internal readonly IList<Type> types;

		public bool IsNull => this.types == null;

		private bool _disposed;

		internal DependencyFrame(int stackLevel, IList<Type> types)
		{
			if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), "Cannot be negative.");

			this.stackLevel = stackLevel;
			this.types = types ?? throw new ArgumentNullException(nameof(types));
			this._disposed = false;
		}

		public void Dispose()
		{
			if (_disposed || IsNull) return;
			_disposed = true;

			Dependencies.CloseFrame(this);
		}
	}
}

//*/