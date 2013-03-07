﻿using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The safe block of code.
	/// </summary>
	public class CatchNode : NodeBase, IStartLocationTrackingEntity
	{
		public CatchNode()
		{
			Code = new CodeBlockNode();	
		}

		/// <summary>
		/// The type of the exception this catch block handles.
		/// Null means any exception.
		/// </summary>
		public TypeSignature ExceptionType { get; set; }

		/// <summary>
		/// A variable to assign the exception to.
		/// </summary>
		public string ExceptionVariable { get; set; }

		/// <summary>
		/// The code block.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Code.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Code;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var backup = ctx.CurrentCatchClause;
			ctx.CurrentCatchClause = this;

			var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : typeof(Exception);
			if(!type.IsSubclassOf(typeof(Exception)))
				Error("Type '{0}' cannot be used in catch clause because it does not derive from System.Exception!", type);

			gen.BeginCatchBlock(type);

			if (string.IsNullOrEmpty(ExceptionVariable))
			{
				var tmpVar = ctx.CurrentScope.DeclareName(ExceptionVariable, type, false);
				gen.EmitSaveLocal(tmpVar);
			}

			Code.Compile(ctx, false);

			gen.EmitLeave(ctx.CurrentTryBlock.EndLabel);

			ctx.CurrentCatchClause = backup;
		}

		#region Equality members

		protected bool Equals(CatchNode other)
		{
			return Equals(ExceptionType, other.ExceptionType)
				&& string.Equals(ExceptionVariable, other.ExceptionVariable)
				&& Equals(Code, other.Code);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CatchNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (ExceptionType != null ? ExceptionType.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ExceptionVariable != null ? ExceptionVariable.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Code != null ? Code.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
