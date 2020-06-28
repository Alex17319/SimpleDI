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
	internal class RefEqualityComparer : IEqualityComparer<object>
	{
		public RefEqualityComparer() { }

		public new bool Equals(object x, object y) => ReferenceEquals(x, y);

		public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}

//*/