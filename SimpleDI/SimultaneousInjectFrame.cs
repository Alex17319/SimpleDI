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
	public struct SimultaneousInjectFrame : IDisposable
	{
		internal readonly _DependencyLayerInternal layer;
		internal readonly int stackLevel;
		internal readonly ImmutableStack<Type> types;

		public bool IsEmpty => this.types == null;
		public static readonly SimultaneousInjectFrame Empty = default;

		private bool _disposed;

		internal SimultaneousInjectFrame(_DependencyLayerInternal layer)
		{
			this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
			this.stackLevel = default;
			this.types = null;
			this._disposed = false;
		}

		internal SimultaneousInjectFrame(_DependencyLayerInternal layer, int stackLevel, ImmutableStack<Type> types)
		{
			if (layer == null) throw new ArgumentNullException(nameof(layer));
			if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), "Cannot be negative.");
			if (types == null) throw new ArgumentNullException(nameof(types));

			int i = 0;
			foreach (Type t in types) {
				if (t == null) throw new ArgumentException($"type at index '{i}' is null.");
				i++;
			}

			this.layer = layer;
			this.stackLevel = stackLevel;
			this.types = types;
			this._disposed = false;
		}

		private SimultaneousInjectFrame ensureHasLayer()
			=> this.layer != null
			? this
			: throw new InvalidOperationException(
				$"Current {nameof(SimultaneousInjectFrame)} has {nameof(layer)} == null, that is, " +
				$"it is not associated with any {nameof(MutatingDependencyLayer)} in order to perform more simultaneous injections."
			);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame Alongside<T>(T dependency)
			=> this.ensureHasLayer()
			.layer
			.InjectMoreSimultaneously(soFar: this, dependency, isWildcard: false);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild<T>(T dependency)
			=> this.ensureHasLayer()
			.layer
			.InjectMoreSimultaneously(soFar: this, dependency, isWildcard: true);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame Alongside(object dependency, Type toMatchAgainst)
			=> this.ensureHasLayer()
			.layer
			.InjectMoreSimultaneously(soFar: this, dependency, toMatchAgainst, isWildcard: false);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild(object dependency, Type toMatchAgainst)
			=> this.ensureHasLayer()
			.layer
			.InjectMoreSimultaneously(soFar: this, dependency, toMatchAgainst, isWildcard: true);

		public void Dispose()
		{
			if (_disposed || IsEmpty) return;
			_disposed = true;

			this.layer.CloseInjectFrame(this);
		}
	}
}

//*/