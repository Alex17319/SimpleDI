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

namespace SimpleDI.DisposeExceptions
{
	public class RegionCloseErrorEventArgs : EventArgs
	{
		public SafeDisposeExceptionsRegion Region { get; }
		public int CurrentRegionLevel { get; }
		public long CurrentRegionID { get; }

		public RegionCloseErrorEventArgs(SafeDisposeExceptionsRegion region, int currentRegionLevel, long currentRegionID)
		{
			this.Region = region;
			this.CurrentRegionLevel = currentRegionLevel;
			this.CurrentRegionID = currentRegionID;
		}
	}
}

//*/