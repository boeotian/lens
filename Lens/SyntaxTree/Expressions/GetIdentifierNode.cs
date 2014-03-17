﻿using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	internal class GetIdentifierNode : IdentifierNodeBase, IPointerProvider
	{
		private MethodEntity m_Method;
		private GlobalPropertyInfo m_Property;
		private LocalName m_LocalConstant;

		public bool PointerRequired { get; set; }
		public bool RefArgumentRequired { get; set; }

		public override bool IsConstant { get { return m_LocalConstant != null; } }
		public override dynamic ConstantValue { get { return m_LocalConstant != null ? m_LocalConstant.ConstantValue : base.ConstantValue; } }

		public GetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var local = LocalName ?? ctx.CurrentScopeFrame.FindName(Identifier);
			if (local != null)
			{
				// only local constants are cached
				// because mutable variables could be closured later on
				if (local.IsConstant && local.IsImmutable && ctx.Options.UnrollConstants)
					m_LocalConstant = local;

				return local.Type;
			}

			try
			{
				var methods = ctx.MainType.ResolveMethodGroup(Identifier);
				if (methods.Length > 1)
					error(CompilerMessages.FunctionInvocationAmbiguous, Identifier);

				m_Method = methods[0];
				return FunctionalHelper.CreateFuncType(m_Method.ReturnType, m_Method.GetArgumentTypes(ctx));
			}
			catch (KeyNotFoundException) { }

			try
			{
				m_Property = ctx.ResolveGlobalProperty(Identifier);
				return m_Property.PropertyType;
			}
			catch (KeyNotFoundException)
			{
				error(CompilerMessages.IdentifierNotFound, Identifier);
			}

			return typeof (Unit);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var resultType = Resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			// local name is not cached because it can be closured.
			// if the identifier is actually a local constant, the 'compile' method is not invoked at all
			var local = LocalName ?? ctx.CurrentScopeFrame.FindName(Identifier);
			if (local != null)
			{
				if(local.IsImmutable && RefArgumentRequired)
					error(CompilerMessages.ConstantByRef);

				if (local.IsClosured)
				{
					if (local.ClosureDistance == 0)
						getClosuredLocal(ctx, local);
					else
						getClosuredRemote(ctx, local);
				}
				else
				{
					getLocal(ctx, local);
				}

				return;
			}

			// load pointer to global function
			if (m_Method != null)
			{
				var ctor = resultType.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

				gen.EmitNull();
				gen.EmitLoadFunctionPointer(m_Method.MethodInfo);
				gen.EmitCreateObject(ctor);

				return;
			}

			// get a property value
			if (m_Property != null)
			{
				var id = m_Property.PropertyId;
				if(!m_Property.HasGetter)
					error(CompilerMessages.GlobalPropertyNoGetter, Identifier);

				var type = m_Property.PropertyType;
				if (m_Property.GetterMethod != null)
				{
					gen.EmitCall(m_Property.GetterMethod.MethodInfo);
				}
				else
				{
					var method = typeof (GlobalPropertyHelper).GetMethod("Get").MakeGenericMethod(type);
					gen.EmitConstant(ctx.ContextId);
					gen.EmitConstant(id);
					gen.EmitCall(method);
				}
				return;
			}

			error(CompilerMessages.IdentifierNotFound, Identifier);
		}

		/// <summary>
		/// Gets a closured variable that has been declared in the current scope.
		/// </summary>
		private void getClosuredLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariable);

			var clsField = ctx.CurrentScope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldBuilder, PointerRequired || RefArgumentRequired);
		}

		/// <summary>
		/// Gets a closured variable that has been imported from outer scopes.
		/// </summary>
		private void getClosuredRemote(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadArgument(0);

			var dist = name.ClosureDistance;
			var type = (Type)ctx.CurrentType.TypeBuilder;
			while (dist > 1)
			{
				var rootField = ctx.ResolveField(type, EntityNames.ParentScopeFieldName);
				gen.EmitLoadField(rootField.FieldInfo);

				type = rootField.FieldType;
				dist--;
			}

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldInfo, PointerRequired || RefArgumentRequired);
		}

		private void getLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;
			var ptr = PointerRequired || RefArgumentRequired;

			if (name.ArgumentId.HasValue)
			{
				gen.EmitLoadArgument(name.ArgumentId.Value, ptr);
				if(name.IsRefArgument && !ptr)
					gen.EmitLoadFromPointer(name.Type);
			}
			else
			{
				gen.EmitLoadLocal(name, ptr);
			}
		}

		public override string ToString()
		{
			return string.Format("get({0})", Identifier);
		}

		#region Equality

		protected bool Equals(GetIdentifierNode other)
		{
			return base.Equals(other)
				   && RefArgumentRequired.Equals(other.RefArgumentRequired)
				   && PointerRequired.Equals(other.PointerRequired);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((GetIdentifierNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hash = base.GetHashCode();
				hash = (hash * 397) ^ PointerRequired.GetHashCode();
				hash = (hash * 397) ^ RefArgumentRequired.GetHashCode();
				return hash;
			}
		}

		#endregion
	}
}
