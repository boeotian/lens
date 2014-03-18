﻿namespace Lens.Compiler.Entities
{
	/// <summary>
	/// The base class of a type-contained entity.
	/// </summary>
	internal abstract class TypeContentsBase : IPreparableEntity
	{
		/// <summary>
		/// The name of the current entity.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The type that contains current entity.
		/// </summary>
		public TypeEntity ContainerType { get; set; }

		/// <summary>
		/// Creates the assembly instances for the current entity.
		/// </summary>
		public abstract void PrepareSelf();
	}

	internal interface IPreparableEntity
	{
		void PrepareSelf();
	}
}
