﻿using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "%"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Modulus"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			Resolve(ctx);
			loadAndConvertNumerics(ctx);
			gen.EmitRemainder();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left % right;
		}
	}
}
