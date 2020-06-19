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

/*

namespace SimpleDI
{
	internal struct TypeTreeMap<T>
	{
		private readonly Type _root;
		private Dictionary<Type, (TypeTreeMap<T>, T)> _branches;

		private Dictionary<Type, (TypeTreeMap<T>, T)> branches
			=> _branches ?? (_branches = new Dictionary<Type, (TypeTreeMap<T>, T)>());

		public static readonly TypeTreeMap<T> Empty = default;

		public TypeTreeMap(Type root, IEnumerable<KeyValuePair<Type, (TypeTreeMap<T>, T)>> branches = null)
		{
			this._root = root ?? throw new ArgumentNullException(nameof(root));
			this._branches = new Dictionary<Type, (TypeTreeMap<T>, T)>();

			if (branches != null)
			{
				foreach (var b in branches) {
					if (b.Key.BaseType != root) throw new ArgumentException(
						$"A branch has type '{b.Key.FullName}' which is not a direct descentant of root type '{root.FullName}'."
					);

					this.branches.Add(b.Key, b.Value);
				}
			}
		}

		public TypeTreeMap(Type root, IEnumerable<KeyValuePair<Type, T>> descendantTypes)
		{
			if (descendantTypes == null) throw new ArgumentNullException(nameof(descendantTypes));

			this._root = root ?? throw new ArgumentNullException(nameof(root));
			this._branches = new Dictionary<Type, (TypeTreeMap<T>, T)>();


		}


		public void AddDescendantType<TDescendant>(T value) => AddDescendantType(typeof(TDescendant), value);
		public bool AddDescendantType(Type type, T value)
		{
			if (!_root.IsAssignableFrom(type)) throw new ArgumentException(
				$"Provided type '{type.FullName}' is not a subtype of the current tree's root type '{_root.FullName}'."
			);

			if (type.Bas)
			if (branches.TryGetValue(type, out var branch))
			{
				branches.
			}
		}


		//	// Equals() and GetHashCode() depend only on the tree's root type, not the value
		//	// or anything else, so Set of these behaves like a Dictionary that maps from the
		//	// tree's root type to both the tree and the value (NOPE: CAN'T GET() FROM A SET)
		//	private struct Entry
		//	{
		//		public readonly TypeTreeMap<T> tree;
		//		public readonly T value;
		//	
		//		public override int GetHashCode() => tree._root.GetHashCode();
		//		public override bool Equals(object obj) => obj is Entry e && e.tree._root == this.tree._root;
		//	}

		//	private class TypeOnlyEqComparer : IEqualityComparer<(Type, TypeTreeMap<T>, T)>
		//	{
		//		public bool Equals((Type, TypeTreeMap<T>, T) x, (Type, TypeTreeMap<T>, T) y)
		//		{
		//			return x.Item1 == y.Item1;
		//		}
		//	
		//		public int GetHashCode((Type, TypeTreeMap<T>, T) obj)
		//		{
		//			return obj.Item1.GetHashCode();
		//		}
		//	}
	}
}

//*/