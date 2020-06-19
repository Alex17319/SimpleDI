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
		
		public StackedDependency(int stackLevel, object dependency)
		{
			this.stackLevel = stackLevel;
			this.dependency = dependency;
		}
	}
}

//*/