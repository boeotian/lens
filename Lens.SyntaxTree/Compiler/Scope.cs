﻿using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The scope information of a specific method.
	/// </summary>
	internal class Scope
	{
		/// <summary>
		/// The name of a field that contains a pointer to root type.
		/// </summary>
		public const string ParentScopeFieldName = "<root>";

		/// <summary>
		/// The template for implicitly defined local variables.
		/// </summary>
		public const string ImplicitVariableNameTemplate = "<loc_{0}>";

		/// <summary>
		/// The template name for a local variable that stores the pointer to current closure instance.
		/// </summary>
		public const string ClosureInstanceVariableNameTemplate = "<inst_{0}>";

		/// <summary>
		/// The template for closure type field names.
		/// </summary>
		public const string ClosureFieldNameTemplate = "<f_{0}>";

		/// <summary>
		/// The template for closure type names.
		/// </summary>
		public const string ClosureTypeNameTemplate = "<ClosuredClass{0}>";

		/// <summary>
		/// The template for closure method names.
		/// </summary>
		public const string ClosureMethodNameTemplate = "<ClosuredMethod{0}>";

		public Scope()
		{
			Names = new Dictionary<string, LocalName>();
		}

		/// <summary>
		/// A scope that contains current scope;
		/// </summary>
		public Scope OuterScope;

		/// <summary>
		/// The lookup table of names defined in current scope.
		/// </summary>
		public Dictionary<string, LocalName> Names;

		/// <summary>
		/// The name of the closure class.
		/// </summary>
		public TypeEntity ClosureType { get; private set; }

		/// <summary>
		/// The ID for the type closured in current scope.
		/// </summary>
		public int? ClosureTypeId { get; private set; }

		/// <summary>
		/// The local variable ID that stores a pointer to current closure object.
		/// </summary>
		public LocalName ClosureVariable { get; private set; }

		#region Methods

		/// <summary>
		/// Register arguments as local variables.
		/// </summary>
		public void InitializeScope(Context ctx)
		{
			var method = ctx.CurrentMethod;
			if (method.Arguments == null)
				return;

			for(var idx = 0; idx < method.Arguments.Count; idx++)
			{
				var arg = method.Arguments[idx];
				DeclareName(arg.Name, ctx.ResolveType(arg.TypeSignature), false, arg.IsRefArgument);
			}
		}

		/// <summary>
		/// Gets information about a local name.
		/// </summary>
		public LocalName FindName(string name)
		{
			LocalName local = null;
			find(name, (loc, idx) => local = loc.GetClosuredCopy(idx));
			return local;
		}

		/// <summary>
		/// Declares a new name in the current scope.
		/// </summary>
		public LocalName DeclareName(string name, Type type, bool isConst, bool isRefArg = false)
		{
			if(find(name))
				throw new LensCompilerException(string.Format("A variable named '{0}' is already defined!", name));

			var n = new LocalName(name, type, isConst, isRefArg);
			Names[name] = n;
			return n;
		}

		/// <summary>
		/// Declares a new variable with autogenerated name.
		/// This name cannot be closured.
		/// </summary>
		public LocalName DeclareImplicitName(Context ctx, Type type, bool isConst)
		{
			var lb = ctx.CurrentILGenerator.DeclareLocal(type);
			var name = string.Format(ImplicitVariableNameTemplate, lb.LocalIndex);
			var ln = new LocalName(name, type, isConst) { LocalBuilder = lb };
			Names[name] = ln;
			return ln;
		}

		/// <summary>
		/// Declares a new temp variable that is instantly initialized.
		/// </summary>
		public LocalName DeclareInternalName(string name, Context ctx, Type type, bool isConst)
		{
			var lb = ctx.CurrentILGenerator.DeclareLocal(type);
			var ln = new LocalName(name, type, isConst) { LocalBuilder = lb };
			Names[name] = ln;
			return ln;
		}

		/// <summary>
		/// Checks if the variable is being referenced in another scope.
		/// </summary>
		public void ReferenceName(string name)
		{
			var found = find(
				name,
				(loc, idx) =>
				{
					var closured = idx > 0;
					if (closured)
					{
						if (loc.LocalBuilder != null)
							throw new InvalidOperationException("Cannot closure an implicit variable!");

						if(loc.IsRefArgument)
							throw new LensCompilerException("A ref argument cannot be closured!");
					}

					loc.IsClosured |= closured;
				}
			);

			if(!found)
				throw new LensCompilerException(string.Format("A variable named '{0}' does not exist in the scope!", name));
		}

		/// <summary>
		/// Creates a closure type for current closure.
		/// </summary>
		public TypeEntity CreateClosureType(Context ctx)
		{
			var closureName = string.Format(ClosureTypeNameTemplate, ctx.ClosureId);
			ClosureTypeId = ctx.ClosureId;
			ClosureType = ctx.CreateType(closureName, isSealed: true, prepare: true);
			ctx.ClosureId++;
			return ClosureType;
		}

		/// <summary>
		/// Creates a closured method in the current scope's closure type.
		/// </summary>
		public MethodEntity CreateClosureMethod(Context ctx, Type[] args)
		{
			if (ClosureType == null)
				ClosureType = CreateClosureType(ctx);

			var closureName = string.Format(ClosureMethodNameTemplate, ClosureType.ClosureMethodId);
			ClosureType.ClosureMethodId++;

			var method = ClosureType.CreateMethod(closureName, args);
			method.Scope.OuterScope = this;
			return method;
		}

		/// <summary>
		/// Registers closure entities and assigns IDs to variables.
		/// </summary>
		public void FinalizeScope(Context ctx)
		{
			foreach (var curr in Names.Values)
			{
				if (curr.IsClosured)
				{
					// create a field in the closured class
					var name = string.Format(ClosureFieldNameTemplate, curr.Name);
					curr.ClosureFieldName = name;
					ClosureType.CreateField(name, curr.Type);
				}
				else
				{
					curr.LocalBuilder = ctx.CurrentILGenerator.DeclareLocal(curr.Type);
				}
			}

			// create a field for base scope in the current type
			if(OuterScope != null && ClosureType != null)
				ClosureType.CreateField(ParentScopeFieldName, OuterScope.ClosureType.TypeBuilder);

			// register a variable for closure instance in the scope
			if (ClosureType != null)
				ClosureVariable = DeclareInternalName(string.Format(ClosureInstanceVariableNameTemplate, ClosureTypeId), ctx, ClosureType.TypeBuilder, false);
		}

		/// <summary>
		/// Finds a local name and invoke a callback.
		/// </summary>
		private bool find(string name, Action<LocalName, int> action = null)
		{
			var idx = 0;
			var scope = this;
			while (scope != null)
			{
				LocalName loc;
				if (scope.Names.TryGetValue(name, out loc))
				{
					if(action != null)
						action(loc, idx);
					return true;
				}

				idx++;
				scope = scope.OuterScope;
			}

			return false;
		}

		#endregion
	}
}
