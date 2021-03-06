﻿using System;
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
		public DependencyLayer CurrentLayer { get; }
		public DependencyLayer LayerToClose { get; }

		public LayerCloseErrorEventArgs(DependencyLayer currentLayer, DependencyLayer layerToClose)
		{
			this.CurrentLayer = currentLayer;
			this.LayerToClose = layerToClose;
		}
	}
}

//*/