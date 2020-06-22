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
	public struct SafeDisposeExceptionsRegion : IDisposable
	{
		public const long MinValidRegionID = 1; // default(SafeDisposeExceptionRegion) will produce an invalid instance

		internal const int RegionLevel_NoneOpen = 0;
		internal const int RegionLevel_OneOpen = RegionLevel_NoneOpen + 1;

		public readonly int level;
		public readonly long ID;
		public readonly long parentRegionID;
		private bool _disposed;

		public bool IsNull => this.ID < MinValidRegionID; // Invalid instances are considered null and are not disposed

		internal SafeDisposeExceptionsRegion(int regionLevel, long regionID, long parentRegionID)
		{
			if (regionLevel < RegionLevel_OneOpen) throw new ArgumentOutOfRangeException(
				nameof(regionLevel),
				$"Must be at least {nameof(RegionLevel_OneOpen)} = {RegionLevel_OneOpen}."
			);
			if (regionID < MinValidRegionID) throw new ArgumentOutOfRangeException(
				nameof(regionID),
				$"Must be at least {nameof(MinValidRegionID)} = {MinValidRegionID}."
			);
			if (parentRegionID < MinValidRegionID) throw new ArgumentOutOfRangeException(
				nameof(parentRegionID),
				$"Must be at least {nameof(MinValidRegionID)} = {MinValidRegionID}."
			);

			this.level = regionLevel;
			this.ID = regionID;
			this.parentRegionID = parentRegionID;
			this._disposed = false;
		}

		public void Dispose()
		{
			if (_disposed || IsNull) return;
			_disposed = true;

			DisposeExceptionsManager.CloseSafeDisposeExceptionsRegion(this);
		}

		public override string ToString()
		{
			return (
				$"{{[{nameof(SafeDisposeExceptionsRegion)}] " +
				$"{nameof(level)}: {level}, " +
				$"{nameof(ID)}: {ID}, " +
				$"{nameof(parentRegionID)}: {parentRegionID}, " +
				$"{nameof(_disposed)}: {_disposed}}}"
			);
		}
	}
}

//*/