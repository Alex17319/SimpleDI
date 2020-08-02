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
	internal struct StackedDependency
	{
		public readonly int stackLevel;
		public readonly object dependency;
		public readonly object[] injectState;
		
		public StackedDependency(int stackLevel, object dependency, object[] injectState)
		{
			this.stackLevel = stackLevel;
			this.dependency = dependency;
			this.injectState = injectState;
		}
	}
}

//*/