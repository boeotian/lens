﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	internal class ShiftOperatorNode : BinaryOperatorNodeBase
	{
		public bool IsLeft { get; set; }

		protected override string OperatorRepresentation
		{
			get { return IsLeft ? "<:" : ":>"; }
		}

		protected override string OverloadedMethodName
		{
			get { return IsLeft ? "op_LeftShift" : "op_RightShift"; }
		}

		/// <summary>
		/// Extra hint for function composition.
		/// </summary>
		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx);

			if (leftType.IsCallableType())
			{
				// add left operand's return type as hint to right operand
				var leftDelegate = ReflectionHelper.WrapDelegate(leftType);
				if (RightOperand is GetMemberNode)
				{
					var mbr = RightOperand as GetMemberNode;
					if (mbr.TypeHints == null || mbr.TypeHints.Count == 0)
						mbr.TypeHints = new List<TypeSignature> {leftDelegate.ReturnType.FullName};
				}
				else if(RightOperand is LambdaNode)
				{
					var lambda = RightOperand as LambdaNode;
					lambda.Resolve(ctx);
					if (lambda.MustInferArgTypes)
						lambda.SetInferredArgumentTypes(new[] {leftDelegate.ReturnType});
				}

				var rightType = RightOperand.Resolve(ctx);

				if (!ReflectionHelper.CanCombineDelegates(leftType, rightType))
					error(Translations.CompilerMessages.DelegatesNotCombinable, leftType, rightType);
			}

			return base.resolve(ctx, mustReturn);
		}

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx, mustReturn);
			var rightType = RightOperand.Resolve(ctx, mustReturn);

			if (leftType.IsCallableType() && rightType.IsCallableType())
			{
				var leftVar = ctx.Unique.TempVariableName();
				var rightVar = ctx.Unique.TempVariableName();
				var delegateType = ReflectionHelper.WrapDelegate(leftType);
				var argDefs = delegateType.ArgumentTypes.Select(x => Expr.Arg(ctx.Unique.AnonymousArgName(), x.FullName)).ToArray();

				return Expr.Lambda(
					argDefs,
					Expr.Block(
						Expr.Let(leftVar, LeftOperand),
						Expr.Let(rightVar, RightOperand),
						Expr.Invoke(
							Expr.Get(rightVar),
							Expr.Invoke(
								Expr.Get(leftVar),
								argDefs.Select(x => Expr.Get(x.Name)).ToArray()
							)
						)
					)
				);
			}

			return base.expand(ctx, mustReturn);
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if (leftType.IsAnyOf(typeof (int), typeof (long)) && rightType == typeof (int))
				return leftType;

			if (!IsLeft && ReflectionHelper.CanCombineDelegates(leftType, rightType))
				return ReflectionHelper.CombineDelegates(leftType, rightType);

			return null;
		}

		protected override void emitOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			LeftOperand.Emit(ctx, true);
			RightOperand.Emit(ctx, true);

			gen.EmitShift(IsLeft);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return IsLeft ? left << right : left >> right;
		}
	}
}
