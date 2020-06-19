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

	[Serializable]
	public class DependencyNotFoundException : Exception
	{
		public DependencyNotFoundException() { }
		public DependencyNotFoundException(string message) : base(message) { }
		public DependencyNotFoundException(string message, Exception inner) : base(message, inner) { }
		protected DependencyNotFoundException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}

//*/