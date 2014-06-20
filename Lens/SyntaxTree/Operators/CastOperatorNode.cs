﻿using System;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	internal class CastOperatorNode : TypeCheckOperatorNodeBase
	{
		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var type = Type ?? ctx.ResolveType(TypeSignature);
			ensureLambdaInferred(ctx, Expression, type);
			return type;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			var fromType = Expression.Resolve(ctx);
			var toType = Resolve(ctx);

			if (toType.IsExtendablyAssignableFrom(fromType, true))
				Expression.Emit(ctx, true);

			else if (fromType.IsNumericType() && toType.IsNumericType(true)) // (decimal -> T) is processed via op_Explicit()
				castNumeric(ctx, fromType, toType);

			else if(fromType.IsCallableType() && toType.IsCallableType())
				castDelegate(ctx, fromType, toType);

			else if (fromType == typeof (NullType))
			{
				if (toType.IsNullableType())
				{
					var tmpVar = ctx.Scope.DeclareImplicit(ctx, toType, true);
					gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
					gen.EmitInitObject(toType);
					gen.EmitLoadLocal(tmpVar.LocalBuilder);
				}

				else if (!toType.IsValueType)
				{
					Expression.Emit(ctx, true);
					gen.EmitCast(toType);
				}

				else
					error(CompilerMessages.CastNullValueType, toType);
			}

			else if (toType.IsExtendablyAssignableFrom(fromType))
			{
				Expression.Emit(ctx, true);

				// box
				if (fromType.IsValueType && toType == typeof (object))
					gen.EmitBox(fromType);

				// nullable
				else if (toType.IsNullableType() && Nullable.GetUnderlyingType(toType) == fromType)
				{
					var ctor = toType.GetConstructor(new[] {fromType});
					gen.EmitCreateObject(ctor);
				}

				else
				{
					// todo: a more elegant approach maybe?
					var castOp = fromType.GetMethods().Where(m => m.Name == "op_Explicit" || m.Name == "op_Implicit" && m.ReturnType == toType)
													  .OrderBy(m => m.Name == "op_Implicit" ? 0 : 1)
													  .FirstOrDefault();
					if (castOp != null)
						gen.EmitCall(castOp);
					else
						gen.EmitCast(toType);
				}
			}

			else if (fromType.IsExtendablyAssignableFrom(toType))
			{
				Expression.Emit(ctx, true);

				// unbox
				if (fromType == typeof (object) && toType.IsValueType)
					gen.EmitUnbox(toType);

				// cast ancestor to descendant
				else if (!fromType.IsValueType && !toType.IsValueType)
					gen.EmitCast(toType);

				else
					error(fromType, toType);
			}

			else
				error(fromType, toType);
		}

		private void castDelegate(Context ctx, Type from, Type to)
		{
			var gen = ctx.CurrentMethod.Generator;

			var toCtor = ctx.ResolveConstructor(to, new[] {typeof (object), typeof (IntPtr)});
			var fromMethod = ctx.ResolveMethod(from, "Invoke");
			var toMethod = ctx.ResolveMethod(to, "Invoke");

			var fromArgs = fromMethod.ArgumentTypes;
			var toArgs = toMethod.ArgumentTypes;

			if(fromArgs.Length != toArgs.Length || toArgs.Select((ta, id) => !ta.IsExtendablyAssignableFrom(fromArgs[id], true)).Any(x => x))
				error(CompilerMessages.CastDelegateArgTypesMismatch, from, to);

			if(!toMethod.ReturnType.IsExtendablyAssignableFrom(fromMethod.ReturnType, true))
				error(CompilerMessages.CastDelegateReturnTypesMismatch, from, to);

			if (fromMethod.IsStatic)
				gen.EmitNull();
			else
				Expression.Emit(ctx, true);

			if (from.IsGenericType && to.IsGenericType && from.GetGenericTypeDefinition() == to.GetGenericTypeDefinition())
				return;

			gen.EmitLoadFunctionPointer(fromMethod.MethodInfo);
			gen.EmitCreateObject(toCtor.ConstructorInfo);
		}

		private void castNumeric(Context ctx, Type from, Type to)
		{
			var gen = ctx.CurrentMethod.Generator;
			
			Expression.Emit(ctx, true);

			if (to == typeof (decimal))
			{
				var ctor = ctx.ResolveConstructor(typeof (decimal), new[] { from });
				if (ctor == null)
				{
					ctor = ctx.ResolveConstructor(typeof(decimal), new[] { typeof(int) });
					gen.EmitConvert(typeof(int));
				}

				gen.EmitCreateObject(ctor.ConstructorInfo);
			}
			else
			{
				gen.EmitConvert(to);
			}
		}

		private void error(Type from, Type to)
		{
			error(CompilerMessages.CastTypesMismatch, from, to);
		}

		public static bool IsImplicitlyBoolean(Type type)
		{
			return type == typeof(bool) || type.GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == typeof (bool));
		}
	}
}
