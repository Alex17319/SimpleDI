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
	public class LayerCloseErrorEventArgs : EventArgs
	{
		public IDependencyLayer CurrentLayer { get; }
		public IDependencyLayer LayerToClose { get; }

		public LayerCloseErrorEventArgs(IDependencyLayer currentLayer, IDependencyLayer layerToClose)
		{
			this.CurrentLayer = currentLayer;
			this.LayerToClose = layerToClose;
		}
	}
}

//*/