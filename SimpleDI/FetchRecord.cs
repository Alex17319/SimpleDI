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
	internal struct FetchRecord
	{
		internal readonly DependencyLayer layerFoundAt;
		internal readonly int stackLevelFoundAt;

		internal bool IsNull => layerFoundAt == null;
		internal readonly static FetchRecord Null = default;

		internal FetchRecord(DependencyLayer layerFoundAt, int stackLevelFoundAt)
		{
			this.layerFoundAt = layerFoundAt ?? throw new ArgumentNullException(nameof(layerFoundAt));
			this.stackLevelFoundAt = stackLevelFoundAt;
		}
	}
}

//*/