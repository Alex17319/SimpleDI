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
	/// <summary>Thrown if a requested dependency is not available</summary>
	/// <remarks>
	/// May be thrown with correct use of the SimpleDI framework if a dependency is just not available.
	/// <para/>
	/// The framework remains in a valid state after throwing this exception
	/// </remarks>
	[Serializable]
	public class DependencyNotFoundException : Exception
	{
		public DependencyNotFoundException() { }
		public DependencyNotFoundException(string message) : base(message) { }
		public DependencyNotFoundException(string message, Exception inner) : base(message, inner) { }
		public DependencyNotFoundException(Type searchType)
			: base($"No dependency matching type '{searchType.FullName}' could be found.")
		{ }
		
		protected DependencyNotFoundException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}

//*/