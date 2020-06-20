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

using DependencyStack = System.Collections.Generic.Stack<(int, object)>;

/* May be useful if attempting to make wildcards dependencies more efficient, though really they're not too bad atm

namespace SimpleDI
{
	internal struct WildcardDependencyStack
	{
		private Stack<MatchingTypeInfo> _matchingTypes;
		private Stack<MatchingTypeInfo> matchingTypes => _matchingTypes ?? (_matchingTypes = new Stack<MatchingTypeInfo>());


		
		private struct MatchingTypeInfo
		{
			public readonly int stackLevel;
			public readonly Type type;
			public readonly Stack<StackedDependency> stack;
		
			public MatchingTypeInfo(int stackLevel, Type type, Stack<StackedDependency> stack)
			{
				this.stackLevel = stackLevel;
				this.type = type;
				this.stack = stack;

				type.GetInterfaceMap(null);
			}
		}
		
		//	 Section root;
		//	 public Dictionary<Type, Section>;
		//	 
		//	 
		//	 private struct Section
		//	 {
		//	 	Stack<(int, Stack<StackedDependency>)> matchingDependenciesStack;
		//	 	Dictionary<Type, Section> 
		//	 }
		//	 
		//	 private struct MatchingTypeInfo
		//	 {
		//	 	public readonly int stackLevel;
		//	 	public readonly Type type;
		//	 	public readonly Stack<StackedDependency> stack;
		//	 
		//	 	public MatchingTypeInfo(int stackLevel, Type type, Stack<StackedDependency> stack)
		//	 	{
		//	 		this.stackLevel = stackLevel;
		//	 		this.type = type;
		//	 		this.stack = stack;
		//	 	}
		//	 }
	}
}

//*/