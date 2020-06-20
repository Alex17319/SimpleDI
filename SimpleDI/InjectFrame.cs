﻿using System;
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
		internal readonly int stackLevel;
		internal readonly Type type;

		public bool IsNull => this.type == null;

		private bool _disposed;

		internal InjectFrame(int stackLevel)
		{
			this.stackLevel = stackLevel;
			this.type = null;
			this._disposed = false;
		}

		internal InjectFrame(int stackLevel, Type type)
		{
			if (stackLevel < 0) throw new ArgumentOutOfRangeException(nameof(stackLevel), "Cannot be negative.");

			this.stackLevel = stackLevel;
			this.type = type ?? throw new ArgumentNullException(nameof(type));
			this._disposed = false;
		}

		private SimultaneousInjectFrame asSimultaneous() => new SimultaneousInjectFrame(
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
			=> Dependencies.AndInjectSimultaneously(soFar: this.asSimultaneous(), dependency, isWildcard: false);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dependency"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild<T>(T dependency)
			=> Dependencies.AndInjectSimultaneously(soFar: this.asSimultaneous(), dependency, isWildcard: true);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame Alongside(object dependency, Type toMatchAgainst)
			=> Dependencies.AndInjectSimultaneously(soFar: this.asSimultaneous(), dependency, isWildcard: false);

		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <param name="dependency"></param>
		/// <param name="toMatchAgainst"></param>
		/// <returns></returns>
		public SimultaneousInjectFrame AlongsideWild(object dependency, Type toMatchAgainst)
			=> Dependencies.AndInjectSimultaneously(soFar: this.asSimultaneous(), dependency, isWildcard: true);

		public void Dispose()
		{
			if (_disposed || IsNull) return;
			_disposed = true;

			Dependencies.CloseFrame(this);
		}
	}
}

//*/