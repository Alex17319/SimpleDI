using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	public struct InjectFrame : IDisposable
	{
		internal readonly DependencyLayer layer;
		internal readonly int stackLevel;
		internal readonly Type type;

		public bool IsNull => this.type == null;
		public static readonly InjectFrame Null = default;

		private bool _disposed;

		internal InjectFrame(DependencyLayer layer, int stackLevel, Type type)
		{
			if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), "Cannot be negative.");

			this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
			this.stackLevel = stackLevel;
			this.type = type ?? throw new ArgumentNullException(nameof(type));
			this._disposed = false;
		}

		internal SimultaneousInjectFrame asSimultaneous() => new SimultaneousInjectFrame(
			this.layer,
			this.stackLevel,
			ImmutableStack.Create(this.type)
		);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame Alongside<T>(T dependency)
			=> this.asSimultaneous().Alongside(dependency);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild<T>(T dependency)
			=> this.asSimultaneous().AlongsideWild(dependency);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame Alongside(object dependency, Type toMatchAgainst)
			=> this.asSimultaneous().Alongside(dependency, toMatchAgainst);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild(object dependency, Type toMatchAgainst)
			=> this.asSimultaneous().AlongsideWild(dependency, toMatchAgainst);

		public void Dispose()
		{
			if (_disposed || IsNull) return;
			_disposed = true;

			this.layer.CloseInjectFrame(this);
		}
	}
}

//*/