﻿namespace Lens.Compiler.Entities
{
    /// <summary>
    /// The base class of a type-contained entity.
    /// </summary>
    internal abstract class TypeContentsBase
    {
        #region Constructor

        protected TypeContentsBase(TypeEntity type)
        {
            ContainerType = type;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the current entity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type that contains current entity.
        /// </summary>
        public readonly TypeEntity ContainerType;

        /// <summary>
        /// The kind of the current entity.
        /// </summary>
        public TypeContentsKind Kind;

        /// <summary>
        /// Creates the assembly instances for the current entity.
        /// </summary>
        public abstract void PrepareSelf();

        #endregion
    }
}