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
	/// <summary>See remarks</summary>
	/// <remarks>
	/// If the SimpleDI framework works properly and is used correctly, this should never be thrown
	/// <para/>
	/// If this is thrown, the SimpleDI framework is in an invalid state, and all future behaviour is undefined.
	/// </remarks>
	[Serializable]
	public class InvalidDIStateException : Exception
	{
		public InvalidDIStateException() { }
		public InvalidDIStateException(string message) : base(message) { }
		public InvalidDIStateException(string message, Exception inner) : base(message, inner) { }
		protected InvalidDIStateException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}

//*/