﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	partial class Context
	{
		/// <summary>
		/// Finds a declared type by its name.
		/// </summary>
		internal TypeEntity FindType(string signature)
		{
			TypeEntity type;
			return _DefinedTypes.TryGetValue(signature, out type) ? type : null;
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			try
			{
				var local = FindType(signature);
				return local != null
					? local.TypeInfo
					: _TypeResolver.ResolveType(signature);
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message);
			}
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		public Type ResolveType(TypeSignature signature)
		{
			try
			{
				return ResolveType(signature.Signature);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(signature);
				throw;
			}
		}

		/// <summary>
		/// Resolves a field from a type by its name.
		/// </summary>
		public FieldWrapper ResolveField(Type type, string name)
		{
			if (type is TypeBuilder)
			{
				var typeEntity = _DefinedTypes[type.Name];
				var fi = typeEntity.ResolveField(name);
				return new FieldWrapper
				{
					Name = name,
					Type = type,

					FieldInfo = fi.FieldBuilder,
					IsStatic = fi.IsStatic,
					FieldType = fi.FieldBuilder.FieldType
				};
			}

			try
			{
				var field = type.GetField(name);
				if(field == null)
					throw new KeyNotFoundException();

				return new FieldWrapper
				{
					Name = name,
					Type = type,

					FieldInfo = field,
					IsStatic = field.IsStatic, 
					IsLiteral = field.IsLiteral,
					FieldType = field.FieldType
				};
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genField = genType.GetField(name);

				if (genField == null)
					throw new KeyNotFoundException();

				return new FieldWrapper
				{
					Name = name,
					Type = type,

					FieldInfo = TypeBuilder.GetField(type, genField),
					IsStatic =  genField.IsStatic,
					IsLiteral = genField.IsLiteral,
					FieldType = GenericHelper.ApplyGenericArguments(genField.FieldType, type)
				};
			}
		}

		/// <summary>
		/// Resolves a property from a type by its name.
		/// </summary>
		public PropertyWrapper ResolveProperty(Type type, string name)
		{
			// no internal properties
			if(type is TypeBuilder)
				throw new KeyNotFoundException();

			try
			{
				var pty = type.GetProperty(name);
				if (pty == null)
					throw new KeyNotFoundException();

				return new PropertyWrapper
				{
					Name = name,
					Type = type,

					Getter = pty.GetGetMethod(),
					Setter = pty.GetSetMethod(),
					IsStatic = (pty.GetGetMethod() ?? pty.GetSetMethod()).IsStatic,
					PropertyType = pty.PropertyType
				};
			}
			catch(NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genPty = genType.GetProperty(name);

				if (genPty == null)
					throw new KeyNotFoundException();

				var getter = genPty.GetGetMethod();
				var setter = genPty.GetSetMethod();

				return new PropertyWrapper
				{
					Name = name,
					Type = type,
					
					Getter = getter == null ? null : TypeBuilder.GetMethod(type, getter),
					Setter = setter == null ? null : TypeBuilder.GetMethod(type, setter),
					IsStatic = (getter ?? setter).IsStatic,
					PropertyType = GenericHelper.ApplyGenericArguments(genPty.PropertyType, type),
				};
			}
		}

		/// <summary>
		/// Resolves a constructor from a type by the list of arguments.
		/// </summary>
		public ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
		{
			if (type is TypeBuilder)
			{
				var typeEntity = _DefinedTypes[type.Name];
				var ctor = typeEntity.ResolveConstructor(argTypes);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = ctor.ConstructorBuilder,
					ArgumentTypes = ctor.GetArgumentTypes(this)
				};
			}

			try
			{
				var ctor = ResolveMethodByArgs(
					type.GetConstructors(), 
					c => c.GetParameters().Select(p => p.ParameterType).ToArray(),
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = ctor.Item1,
					ArgumentTypes = ctor.Item1.GetParameters().Select(p => p.ParameterType).ToArray()
				};
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genCtor = ResolveMethodByArgs(
					genType.GetConstructors(),
					c => c.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = TypeBuilder.GetConstructor(type, genCtor.Item1),
					ArgumentTypes = genCtor.Item3
				};
			}
		}

		/// <summary>
		/// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
		/// Generic arguments whose values can be inferred from argument types can be skipped.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] genericArgs = null)
		{
			if (type is TypeBuilder)
			{
				var typeEntity = _DefinedTypes[type.Name];
				var method = typeEntity.ResolveMethod(name, argTypes);

				if(genericArgs != null)
					Error("Cannot apply generic arguments to non-generic method '{0}'!", name);

				return new MethodWrapper
				{
					Name = name,
					Type = type,

					MethodInfo = method.MethodInfo,
					IsStatic = method.IsStatic,
					IsVirtual = method.IsVirtual,
					ArgumentTypes = method.GetArgumentTypes(this),
					GenericArguments = null,
					ReturnType = method.ReturnType
				};
			}

			try
			{
				var method = ResolveMethodByArgs(
					type.GetMethods().Where(m => m.Name == name),
					m => m.GetParameters().Select(p => p.ParameterType).ToArray(),
					argTypes
				);

				var mInfo = method.Item1;
				var expectedTypes = method.Item3;
				Type[] genericValues = null;

				if (mInfo.IsGenericMethod)
				{
					var genericDefs = mInfo.GetGenericArguments();
					genericValues = new Type[genericDefs.Length];
					GenericHelper.ResolveMethodGenericsByArgs(expectedTypes, argTypes, genericDefs, ref genericValues);

					mInfo = mInfo.MakeGenericMethod(genericValues);
				}
				else if (genericArgs != null)
				{
					Error("Cannot apply generic arguments to non-generic method '{0}'!", name);
				}

				return new MethodWrapper
				{
					Name = name,
					Type = type,

					MethodInfo = mInfo,
					IsStatic = mInfo.IsStatic,
					IsVirtual = mInfo.IsVirtual,
					ArgumentTypes = expectedTypes,
					GenericArguments = genericValues,
					ReturnType = mInfo.ReturnType
				};
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genMethod = ResolveMethodByArgs(
					genType.GetMethods().Where(m => m.Name == name),
					m => m.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type, false)).ToArray(),
					argTypes
				);

				var mInfoOriginal = genMethod.Item1;
				var mInfo = TypeBuilder.GetMethod(type, genMethod.Item1);
				var expectedTypes = genMethod.Item3;
				Type[] genericValues = null;
				Type retType = GenericHelper.ApplyGenericArguments(mInfoOriginal.ReturnType, type, false);

				if (mInfoOriginal.IsGenericMethod)
				{
					var genericDefs = mInfoOriginal.GetGenericArguments();
					genericValues = new Type[genericDefs.Length];
					GenericHelper.ResolveMethodGenericsByArgs(expectedTypes, argTypes, genericDefs, ref genericValues);

					mInfo = mInfo.MakeGenericMethod(genericValues);

					// setting generic argument values unresolved by containing type to values of current method

					if (retType.IsGenericParameter)
						retType = genericValues[Array.IndexOf(genericDefs, retType)];

					for (var idx = 0; idx < expectedTypes.Length; idx++)
						if (expectedTypes[idx].IsGenericParameter)
							expectedTypes[idx] = genericValues[Array.IndexOf(genericDefs, expectedTypes[idx])];
				}
				else if (genericArgs != null)
				{
					Error("Cannot apply generic arguments to non-generic method '{0}'!", name);
				}

				return new MethodWrapper
				{
					Name = name,
					Type = type,

					MethodInfo = mInfo,
					IsStatic = mInfoOriginal.IsStatic,
					IsVirtual = mInfoOriginal.IsVirtual,
					ArgumentTypes = expectedTypes,
					GenericArguments = genericValues,
					ReturnType = retType
				};
			}
		}

		/// <summary>
		/// Finds an extension method for current type.
		/// </summary>
		public MethodWrapper ResolveExtensionMethod(Type type, string name, Type[] argTypes, Type[] genericArgs = null)
		{
			Type[] genericValues = null;
			var method = type.FindExtensionMethod(name, argTypes);
			if (method.IsGenericMethod)
			{
				var expectedTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
				var genericDefs = method.GetGenericArguments();
				genericValues = new Type[genericDefs.Length];

				GenericHelper.ResolveMethodGenericsByArgs(expectedTypes, argTypes, genericDefs, ref genericValues);

				method = method.MakeGenericMethod(genericValues);
			}
			else if (genericArgs != null)
			{
				Error("Cannot apply generic arguments to non-generic method '{0}'!", name);
			}

			return new MethodWrapper
			{
				Name = name,
				Type = method.DeclaringType,

				MethodInfo = method,
				IsStatic = true,
				IsVirtual = false,
				ReturnType = method.ReturnType,
				ArgumentTypes = method.GetParameters().Select(p => p.ParameterType).ToArray(),
				GenericArguments = genericValues
			};
		}

		/// <summary>
		/// Resolves a global property by its name.
		/// </summary>
		internal GlobalPropertyInfo ResolveGlobalProperty(string name)
		{
			GlobalPropertyInfo ent;
			if (!_DefinedProperties.TryGetValue(name, out ent))
				throw new KeyNotFoundException();

			return ent;
		}

		/// <summary>
		/// Resolves the best-matching method-like entity within a generic list.
		/// </summary>
		/// <typeparam name="T">Type of method-like entity.</typeparam>
		/// <param name="list">List of method-like entitites.</param>
		/// <param name="argsGetter">A function that gets method entity arguments.</param>
		/// <param name="args">Desired argument types.</param>
		public static Tuple<T, int, Type[]> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Type[] args)
		{
			Func<T, Tuple<T, int, Type[]>> methodEvaluator = ent =>
			{
				var currArgs = argsGetter(ent);
				var dist = ExtensionMethodResolver.GetArgumentsDistance(args, currArgs);
				return new Tuple<T, int, Type[]>(ent, dist, currArgs);
			};

			var result = list.Select(methodEvaluator).OrderBy(rec => rec.Item2).ToArray();

			if (result.Length == 0 || result[0].Item2 == int.MaxValue)
				throw new KeyNotFoundException("No suitable method was found!");

			if (result.Length > 2)
			{
				var ambiCount = result.Skip(1).TakeWhile(i => i.Item2 == result[0].Item2).Count();
				if (ambiCount > 0)
					throw new AmbiguousMatchException();
			}

			return result[0];
		}
	}
}
