using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleDI
{
	public interface IDisposableLayer : IDisposable
	{
		DependencyLayer Layer { get; }
	}

	//	internal interface _DisposableLayerInternal : IDisposableLayer
	//	{
	//		void MarkDisposed();
	//	}
}

//*/