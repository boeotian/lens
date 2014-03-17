﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base node for algebraic types and records.
	/// </summary>
	internal abstract class TypeDefinitionNodeBase<T> : NodeBase
		where T : LocationEntity
	{
		protected TypeDefinitionNodeBase()
		{
			Entries = new List<T>();
		}

		/// <summary>
		/// The name of the type.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The entries of the type node.
		/// </summary>
		public List<T> Entries { get; private set; }

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			// nothing to do here
			throw new InvalidOperationException();
		}

		#region Equality members

		protected bool Equals(TypeDefinitionNodeBase<T> other)
		{
			return string.Equals(Name, other.Name) && Entries.SequenceEqual(other.Entries);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeDefinitionNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Entries != null ? Entries.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
