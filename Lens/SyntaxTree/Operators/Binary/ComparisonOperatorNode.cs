﻿using System;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Binary
{
    /// <summary>
    /// A node representing object comparison operations.
    /// </summary>
    internal class ComparisonOperatorNode : BinaryOperatorNodeBase
    {
        #region Constructor

        public ComparisonOperatorNode(ComparisonOperatorKind kind = default(ComparisonOperatorKind))
        {
            Kind = kind;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The kind of equality operator.
        /// </summary>
        public ComparisonOperatorKind Kind { get; set; }

        #endregion

        #region Operator basics

        protected override string OperatorRepresentation
        {
            get
            {
                switch (Kind)
                {
                    case ComparisonOperatorKind.Equals: return "==";
                    case ComparisonOperatorKind.NotEquals: return "<>";
                    case ComparisonOperatorKind.Less: return "<";
                    case ComparisonOperatorKind.LessEquals: return "<=";
                    case ComparisonOperatorKind.Greater: return ">";
                    case ComparisonOperatorKind.GreaterEquals: return ">=";

                    default: throw new ArgumentException("Comparison operator kind is invalid!");
                }
            }
        }

        protected override string OverloadedMethodName
        {
            get
            {
                switch (Kind)
                {
                    case ComparisonOperatorKind.Equals: return "op_Equality";
                    case ComparisonOperatorKind.NotEquals: return "op_Inequality";
                    case ComparisonOperatorKind.Less: return "op_LessThan";
                    case ComparisonOperatorKind.LessEquals: return "op_LessThanOrEqual";
                    case ComparisonOperatorKind.Greater: return "op_GreaterThan";
                    case ComparisonOperatorKind.GreaterEquals: return "op_GreaterThanOrEqual";

                    default: throw new ArgumentException("Comparison operator kind is invalid!");
                }
            }
        }

        protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new ComparisonOperatorNode(Kind) {LeftOperand = left, RightOperand = right};
        }

        #endregion

        #region Resolve

        protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
        {
            var isEquality = Kind == ComparisonOperatorKind.Equals || Kind == ComparisonOperatorKind.NotEquals;
            return CanCompare(leftType, rightType, isEquality) ? typeof(bool) : null;
        }

        /// <summary>
        /// Checks if two types can be compared.
        /// </summary>
        private bool CanCompare(Type left, Type right, bool equalityOnly)
        {
            // there's an overridden method
            if (OverloadedMethod != null)
                return true;

            // string .. string
            if (left == typeof(string) && right == left)
                return true;

            // numeric .. numeric
            if (left.IsNumericType() && right.IsNumericType())
                return left.IsUnsignedIntegerType() == right.IsUnsignedIntegerType();

            if (equalityOnly)
            {
                // Nullable<T> .. (Nullable<T> | T | null)
                if (left.IsNullableType())
                    return left == right || Nullable.GetUnderlyingType(left) == right || right == typeof(NullType);

                if (right.IsNullableType())
                    return Nullable.GetUnderlyingType(right) == left || left == typeof(NullType);

                // ref type .. null
                if ((right == typeof(NullType) && !left.IsValueType) || (left == typeof(NullType) && !right.IsValueType))
                    return true;

                if (left is TypeBuilder && left == right)
                    return true;

                if (left == right)
                    return left.IsAnyOf(typeof(bool));
            }

            return false;
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            var leftType = LeftOperand.Resolve(ctx);
            var rightType = RightOperand.Resolve(ctx);
            var isEquality = Kind == ComparisonOperatorKind.Equals || Kind == ComparisonOperatorKind.NotEquals;

            if (!CanCompare(leftType, rightType, isEquality))
                Error(CompilerMessages.TypesIncomparable, leftType, rightType);

            if (isEquality)
                EmitEqualityComparison(ctx, leftType, rightType);
            else
                EmitRelation(ctx, leftType, rightType);
        }

        /// <summary>
        /// Emits code for equality and inequality comparison.
        /// </summary>
        private void EmitEqualityComparison(Context ctx, Type left, Type right)
        {
            var gen = ctx.CurrentMethod.Generator;

            // compare two strings
            if (left == right && left == typeof(string))
            {
                LeftOperand.Emit(ctx, true);
                RightOperand.Emit(ctx, true);

                var method = typeof(string).GetMethod("Equals", new[] {typeof(string), typeof(string)});
                gen.EmitCall(method);

                if (Kind == ComparisonOperatorKind.NotEquals)
                    EmitInversion(gen);

                return;
            }

            // compare primitive types
            if ((left.IsNumericType() && right.IsNumericType()) || (left == right && left == typeof(bool)))
            {
                if (left == typeof(bool))
                {
                    LeftOperand.Emit(ctx, true);
                    RightOperand.Emit(ctx, true);
                }
                else
                {
                    LoadAndConvertNumerics(ctx);
                }

                gen.EmitCompareEqual();

                if (Kind == ComparisonOperatorKind.NotEquals)
                    EmitInversion(gen);

                return;
            }

            // compare nullable against another nullable, it's base type or null
            if (left.IsNullableType())
            {
                if (left == right || Nullable.GetUnderlyingType(left) == right)
                    EmitNullableComparison(ctx, LeftOperand, RightOperand);
                else if (right == typeof(NullType))
                    EmitHasValueCheck(ctx, LeftOperand);

                return;
            }

            if (right.IsNullableType())
            {
                if (Nullable.GetUnderlyingType(right) == left)
                    EmitNullableComparison(ctx, RightOperand, LeftOperand);
                else if (left == typeof(NullType))
                    EmitHasValueCheck(ctx, RightOperand);

                return;
            }

            // compare a reftype against a null
            if (left == typeof(NullType) || right == typeof(NullType))
            {
                LeftOperand.Emit(ctx, true);
                RightOperand.Emit(ctx, true);
                gen.EmitCompareEqual();

                if (Kind == ComparisonOperatorKind.NotEquals)
                    EmitInversion(gen);

                return;
            }

            if (left is TypeBuilder && left == right)
            {
                var equals = ctx.ResolveMethod(left, "Equals", new[] {typeof(object)});

                LeftOperand.Emit(ctx, true);
                RightOperand.Emit(ctx, true);

                gen.EmitCall(equals.MethodInfo);

                if (Kind == ComparisonOperatorKind.NotEquals)
                    EmitInversion(gen);

                return;
            }

            throw new ArgumentException("Unknown types to compare!");
        }

        /// <summary>
        /// Emits code for comparing a nullable 
        /// </summary>
        private void EmitNullableComparison(Context ctx, NodeBase nullValue, NodeBase otherValue)
        {
            var gen = ctx.CurrentMethod.Generator;

            var nullType = nullValue.Resolve(ctx);
            var otherType = otherValue.Resolve(ctx);
            var otherNull = otherType.IsNullableType();

            var getValOrDefault = nullType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
            var hasValueGetter = nullType.GetProperty("HasValue").GetGetMethod();

            var falseLabel = gen.DefineLabel();
            var endLabel = gen.DefineLabel();

            Local nullVar, otherVar = null;
            nullVar = ctx.Scope.DeclareImplicit(ctx, nullType, true);
            if (otherNull)
                otherVar = ctx.Scope.DeclareImplicit(ctx, otherType, true);

            // $tmp = nullValue
            nullValue.Emit(ctx, true);
            gen.EmitSaveLocal(nullVar.LocalBuilder);

            if (otherNull)
            {
                // $tmp2 = otherValue
                otherValue.Emit(ctx, true);
                gen.EmitSaveLocal(otherVar.LocalBuilder);
            }

            // $tmp == $tmp2
            gen.EmitLoadLocal(nullVar.LocalBuilder, true);
            gen.EmitCall(getValOrDefault);

            if (otherNull)
            {
                gen.EmitLoadLocal(otherVar.LocalBuilder, true);
                gen.EmitCall(getValOrDefault);
            }
            else
            {
                otherValue.Emit(ctx, true);
            }

            gen.EmitBranchNotEquals(falseLabel);

            // otherwise, compare HasValues
            gen.EmitLoadLocal(nullVar.LocalBuilder, true);
            gen.EmitCall(hasValueGetter);

            if (otherNull)
            {
                gen.EmitLoadLocal(otherVar.LocalBuilder, true);
                gen.EmitCall(hasValueGetter);

                gen.EmitCompareEqual();
            }

            if (Kind == ComparisonOperatorKind.NotEquals)
                EmitInversion(gen);

            gen.EmitJump(endLabel);

            gen.MarkLabel(falseLabel);
            gen.EmitConstant(false);

            gen.MarkLabel(endLabel);
        }

        /// <summary>
        /// Checks if the nullable expression is null.
        /// </summary>
        private void EmitHasValueCheck(Context ctx, NodeBase nullValue)
        {
            var gen = ctx.CurrentMethod.Generator;
            var nullType = nullValue.Resolve(ctx);
            var nullVar = ctx.Scope.DeclareImplicit(ctx, nullType, true);
            var hasValueGetter = nullType.GetProperty("HasValue").GetGetMethod();

            nullValue.Emit(ctx, true);
            gen.EmitSaveLocal(nullVar.LocalBuilder);

            gen.EmitLoadLocal(nullVar.LocalBuilder, true);
            gen.EmitCall(hasValueGetter);

            // sic! get_HasValue == true when value != null
            if (Kind == ComparisonOperatorKind.Equals)
                EmitInversion(gen);
        }

        /// <summary>
        /// Emits code for inverting the relation.
        /// </summary>
        private void EmitInversion(ILGenerator gen)
        {
            gen.EmitConstant(false);
            gen.EmitCompareEqual();
        }

        /// <summary>
        /// Emits code for relation comparison: greater, less, etc.
        /// </summary>
        private void EmitRelation(Context ctx, Type left, Type right)
        {
            var gen = ctx.CurrentMethod.Generator;

            // string comparisons
            if (left == typeof(string))
            {
                LeftOperand.Emit(ctx, true);
                RightOperand.Emit(ctx, true);

                var method = typeof(string).GetMethod("Compare", new[] {typeof(string), typeof(string)});
                gen.EmitCall(method);

                if (Kind.IsAnyOf(ComparisonOperatorKind.Less, ComparisonOperatorKind.GreaterEquals))
                {
                    gen.EmitConstant(-1);
                    gen.EmitCompareEqual();
                    if (Kind == ComparisonOperatorKind.GreaterEquals)
                        EmitInversion(gen);
                }
                else
                {
                    gen.EmitConstant(1);
                    gen.EmitCompareEqual();
                    if (Kind == ComparisonOperatorKind.LessEquals)
                        EmitInversion(gen);
                }
            }

            // numeric comparison
            LoadAndConvertNumerics(ctx);
            if (Kind.IsAnyOf(ComparisonOperatorKind.Less, ComparisonOperatorKind.GreaterEquals))
            {
                gen.EmitCompareLess();
                if (Kind == ComparisonOperatorKind.GreaterEquals)
                    EmitInversion(gen);
            }
            else
            {
                gen.EmitCompareGreater();
                if (Kind == ComparisonOperatorKind.LessEquals)
                    EmitInversion(gen);
            }
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic left, dynamic right)
        {
            switch (Kind)
            {
                case ComparisonOperatorKind.Equals: return left == right;
                case ComparisonOperatorKind.NotEquals: return left != right;
                case ComparisonOperatorKind.Less: return left < right;
                case ComparisonOperatorKind.LessEquals: return left <= right;
                case ComparisonOperatorKind.Greater: return left > right;
                case ComparisonOperatorKind.GreaterEquals: return left >= right;
            }

            return null;
        }

        #endregion

        #region Debug

        protected bool Equals(ComparisonOperatorNode other)
        {
            return base.Equals(other) && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ComparisonOperatorNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (int) Kind;
            }
        }

        #endregion
    }

    /// <summary>
    /// The kind of comparison operators.
    /// </summary>
    public enum ComparisonOperatorKind
    {
        Equals,
        NotEquals,
        Less,
        LessEquals,
        Greater,
        GreaterEquals
    }
}