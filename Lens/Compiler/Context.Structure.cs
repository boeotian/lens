﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	internal partial class Context
	{
		#region Methods

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.ParentSignature = parent;

			if(prepare)
				te.PrepareSelf();

			return te;
		}

		/// <summary>
		/// Creates a new type entity with given name and a resolved type for parent.
		/// </summary>
		internal TypeEntity CreateType(string name, Type parent, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.Parent = parent;

			if (prepare)
				te.PrepareSelf();

			return te;
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

			var mainType = CreateType(node.Name, prepare: true);
			mainType.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var tagName = curr.Name;
				var labelType = CreateType(tagName, mainType.TypeInfo, isSealed: true, prepare: true, defaultCtor: false);
				labelType.Kind = TypeEntityKind.TypeLabel;

				var ctor = labelType.CreateConstructor();
				if (curr.IsTagged)
				{
					labelType.CreateField("Tag", curr.TagType);

					var args = new HashList<FunctionArgument> { { "value", new FunctionArgument("value", curr.TagType) } };

					var staticCtor = MainType.CreateMethod(tagName, tagName, new string[0], true);
					ctor.Arguments = staticCtor.Arguments = args;

					ctor.Body.Add(
						Expr.SetMember(Expr.This(), "Tag", Expr.Get("value"))
					);

					staticCtor.Body.Add(
						Expr.New(tagName, Expr.Get("value"))
					);
				}
				else
				{
					var staticCtor = labelType.CreateMethod(tagName, tagName, new string[0], true);
					staticCtor.Body.Add(Expr.New(tagName));

					var pty = GlobalPropertyHelper.RegisterProperty(ContextId, labelType.TypeInfo, staticCtor, null);
					_DefinedProperties.Add(tagName, pty);
				}
			}
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

			var recType = CreateType(node.Name, isSealed: true);
			recType.Kind = TypeEntityKind.Record;

			var recCtor = recType.CreateConstructor();

			foreach (var curr in node.Entries)
			{
				var field = recType.CreateField(curr.Name, curr.Type);
				var argName = "_" + field.Name.ToLowerInvariant();

				recCtor.Arguments.Add(argName, new FunctionArgument(argName, curr.Type));
				recCtor.Body.Add(
					Expr.SetMember(Expr.This(), field.Name, Expr.Get(argName))
				);
			}
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

			validateFunction(node);
			var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true);
			method.IsPure = node.IsPure;
			method.Body = node.Body;
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		public void DeclareOpenNamespace(UsingNode node)
		{
			if(!Namespaces.ContainsKey(node.Namespace))
				Namespaces.Add(node.Namespace, true);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		public void DeclareScriptNode(NodeBase node)
		{
			MainMethod.Body.Add(node);
		}

		/// <summary>
		/// Checks if the expression returns a value and has a specified type.
		/// </summary>
		public void CheckTypedExpression(NodeBase node, Type calculatedType = null, bool allowNull = false)
		{
			var type = calculatedType ?? node.Resolve(this);

			if(!allowNull && type == typeof(NullType))
				Error(node, CompilerMessages.ExpressionNull);

			if(type.IsVoid())
				Error(node, CompilerMessages.ExpressionVoid);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Traverses the syntactic tree, searching for closures and curried methods.
		/// </summary>
		private void processClosures()
		{
			var types = _DefinedTypes.ToArray();

			// ProcessClosures() usually processes new types, hence the caching to array
			foreach (var currType in types)
				currType.Value.ProcessClosures();
		}

		/// <summary>
		/// Creates automatically generated entities.
		/// </summary>
		private void createAutoEntities()
		{
			var types = _DefinedTypes.ToArray();
			foreach (var type in types)
				type.Value.CreateEntities();
		}

		private void createEntryPoint()
		{
			var ep = MainType.CreateMethod(EntityNames.EntryPointMethodName, "Void", args: null, isStatic: true);
			ep.Body = Expr.Block(
				Expr.Invoke(Expr.New(EntityNames.MainTypeName), "Run"),
				Expr.Unit()
			);
		}

		/// <summary>
		/// Initializes the context from a stream of nodes.
		/// </summary>
		private void loadTree(IEnumerable<NodeBase> nodes)
		{
			foreach (var currNode in nodes)
			{
				if (currNode is TypeDefinitionNode)
					DeclareType(currNode as TypeDefinitionNode);
				else if (currNode is RecordDefinitionNode)
					DeclareRecord(currNode as RecordDefinitionNode);
				else if (currNode is FunctionNode)
					DeclareFunction(currNode as FunctionNode);
				else if (currNode is UsingNode)
					DeclareOpenNamespace(currNode as UsingNode);
				else
					DeclareScriptNode(currNode);
			}
		}

		private void transformTree()
		{
			if (Options.AllowSave && Options.SaveAsExe)
				createEntryPoint();

			while (_UnpreparedTypes.Count > 0 || _UnpreparedFields.Count > 0 || _UnpreparedMethods.Count > 0)
			{
				if (_UnpreparedTypes.Count > 0)
				{
					foreach (var curr in _UnpreparedTypes)
						curr.PrepareSelf();

					_UnpreparedTypes.Clear();
				}

				if (_UnpreparedFields.Count > 0)
				{
					foreach (var curr in _UnpreparedFields)
						curr.PrepareSelf();

					_UnpreparedFields.Clear();
				}

				if (_UnpreparedMethods.Count > 0)
				{
					var methods = _UnpreparedMethods.ToArray();
					_UnpreparedMethods.Clear();

					foreach(var curr in methods)
						curr.PrepareSelf();
				}
			}
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void emitCode()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
		}

		/// <summary>
		/// Create a type entry without setting its parent info.
		/// </summary>
		private TypeEntity createTypeCore(string name, bool isSealed, bool defaultCtor)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error(CompilerMessages.TypeDefined, name);

			var te = new TypeEntity(this)
			{
				Name = name,
				IsSealed = isSealed,
			};
			_DefinedTypes.Add(name, te);

			if (defaultCtor)
				te.CreateConstructor();

			return te;
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
			foreach (var curr in _DefinedTypes)
				if(!curr.Value.IsImported)
					curr.Value.TypeBuilder.CreateType();

			if (Options.AllowSave)
			{
				if (Options.SaveAsExe)
				{
					var ep = ResolveMethod(ResolveType(EntityNames.MainTypeName), EntityNames.EntryPointMethodName);
					MainAssembly.SetEntryPoint(ep.MethodInfo, PEFileKinds.ConsoleApplication);
				}

				MainAssembly.Save(Options.FileName);
			}
		}

		/// <summary>
		/// Checks if the function does not collide with internal functions.
		/// </summary>
		private void validateFunction(FunctionNode node)
		{
			if (node.Arguments.Count > 0)
				return;

			if (node.Name == EntityNames.RunMethodName || node.Name == EntityNames.EntryPointMethodName)
				Error(CompilerMessages.ReservedFunctionRedefinition, node.Name);
		}

		#endregion
	}
}