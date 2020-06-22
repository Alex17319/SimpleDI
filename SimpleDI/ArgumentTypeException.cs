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
	public class ArgumentTypeException : ArgumentException
	{
		public ArgumentTypeException() { }
		public ArgumentTypeException(string message) : base(message) { }
		public ArgumentTypeException(string message, Exception inner) : base(message, inner) { }
		public ArgumentTypeException(string paramName, string message) : base(message, paramName) { }
		public ArgumentTypeException(string paramName, string message, Exception inner) : base(message, paramName, inner) { }
		protected ArgumentTypeException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}

//*/