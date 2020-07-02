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
			if (stackLevelFoundAt < 0) throw new ArgumentOutOfRangeException(
				nameof(stackLevelFoundAt),
				stackLevelFoundAt,
				"Cannot be negative"
			);

			this.layerFoundAt = layerFoundAt ?? throw new ArgumentNullException(nameof(layerFoundAt));
			this.stackLevelFoundAt = stackLevelFoundAt;
		}

		//	public override bool Equals(object obj)
		//	{
		//		if (!(obj is FetchRecord fr)) return false;
		//		if (this.IsNull || fr.IsNull) return this.IsNull == fr.IsNull;
		//	
		//		return Equals(this.layerFoundAt, fr.layerFoundAt) && this.stackLevelFoundAt == fr.stackLevelFoundAt;
		//	}
		//	
		//	public override int GetHashCode()
		//	{
		//		if (this.IsNull) return 0;
		//	
		//		var hash = 17;
		//		hash = hash * 23 + this.layerFoundAt.GetHashCode();
		//		hash = hash * 23 + this.stackLevelFoundAt.GetHashCode();
		//		return hash;
		//	}
		//	
		//	public static bool operator ==(FetchRecord record1, FetchRecord record2) => record1.Equals(record2);
		//	public static bool operator !=(FetchRecord record1, FetchRecord record2) => !(record1 == record2);
	}
}

//*/