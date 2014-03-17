﻿using Lens.Compiler;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class DivideOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "/"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Division"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			Resolve(ctx);
			loadAndConvertNumerics(ctx);

			gen.EmitDivide();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			if(left is int && right is int && right == 0)
				error(CompilerMessages.ConstantDivisionByZero);

			return left/right;
		}
	}
}
