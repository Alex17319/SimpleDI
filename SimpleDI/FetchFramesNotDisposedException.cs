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
	public class FetchFramesNotDisposedException : FetchFrameCloseException
	{
		private readonly Action _cleaner;
		private bool _cleanDone;

		public FetchFramesNotDisposedException() { }
		public FetchFramesNotDisposedException(Action cleaner) => this._cleaner = cleaner;
		public FetchFramesNotDisposedException(Action cleaner, string message) : base(message) => this._cleaner = cleaner;
		public FetchFramesNotDisposedException(Action cleaner, string message, Exception inner) : base(message, inner) => this._cleaner = cleaner;
		protected FetchFramesNotDisposedException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

		public void CloseFrameAndDescendants()
		{
			if (_cleaner == null) throw new InvalidOperationException(
				$"this.cleaner is null, so cannot clean up all frames below parent frame."
			);
			if (_cleanDone) return;

			_cleaner();
		}
	}
}

//*/