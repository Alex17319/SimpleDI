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
	public struct MultiInjectFrame : IDisposable
	{
		internal readonly int stackLevel;
		internal readonly IList<Type> types;

		public bool IsNull => this.types == null;

		private bool _disposed;

		internal MultiInjectFrame(int stackLevel, IList<Type> types)
		{
			if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), "Cannot be negative.");
			if (types == null) throw new ArgumentNullException(nameof(types));
			int i = 0;
			foreach (Type t in types) {

				i++;
			}

			this.stackLevel = stackLevel;
			this.types = types;
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