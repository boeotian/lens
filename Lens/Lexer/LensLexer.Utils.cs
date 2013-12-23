﻿using Lens.SyntaxTree;

namespace Lens.Lexer
{
	internal partial class LensLexer
	{
		private StaticLexemDefinition[] Keywords = new[]
		{
			new StaticLexemDefinition("typeof", LexemType.Typeof),
			new StaticLexemDefinition("default", LexemType.Default),

			new StaticLexemDefinition("using", LexemType.Using),
			new StaticLexemDefinition("type", LexemType.Type),
			new StaticLexemDefinition("record", LexemType.Record),
			new StaticLexemDefinition("pure", LexemType.Pure),
			new StaticLexemDefinition("fun", LexemType.Fun),
			new StaticLexemDefinition("while", LexemType.While),
			new StaticLexemDefinition("do", LexemType.Do),
			new StaticLexemDefinition("if", LexemType.If),
			new StaticLexemDefinition("then", LexemType.Then),
			new StaticLexemDefinition("else", LexemType.Else),
			new StaticLexemDefinition("for", LexemType.For),
			new StaticLexemDefinition("in", LexemType.In),
			new StaticLexemDefinition("try", LexemType.Try),
			new StaticLexemDefinition("catch", LexemType.Catch),
			new StaticLexemDefinition("finally", LexemType.Finally),
			new StaticLexemDefinition("throw", LexemType.Throw),

			new StaticLexemDefinition("let", LexemType.Let),
			new StaticLexemDefinition("var", LexemType.Var),
			new StaticLexemDefinition("new", LexemType.New),
			new StaticLexemDefinition("not", LexemType.Not),
			new StaticLexemDefinition("ref", LexemType.Ref),
			new StaticLexemDefinition("is", LexemType.Is),
			new StaticLexemDefinition("as", LexemType.As),
			new StaticLexemDefinition("of", LexemType.Of),

			new StaticLexemDefinition("true", LexemType.True),
			new StaticLexemDefinition("false", LexemType.False),
			new StaticLexemDefinition("null", LexemType.Null),
		};

		private readonly static StaticLexemDefinition[] Operators = new []
		{
			new StaticLexemDefinition("()", LexemType.Unit),
			new StaticLexemDefinition("[]", LexemType.ArrayDef),

			new StaticLexemDefinition("|>", LexemType.PassRight),
			new StaticLexemDefinition("<|", LexemType.PassLeft),
			new StaticLexemDefinition("=>", LexemType.FatArrow),
			new StaticLexemDefinition("->", LexemType.Arrow),

			new StaticLexemDefinition("<:", LexemType.ShiftLeft),
			new StaticLexemDefinition(":>", LexemType.ShiftRight),

			new StaticLexemDefinition("==", LexemType.Equal),
			new StaticLexemDefinition("<=", LexemType.LessEqual),
			new StaticLexemDefinition(">=", LexemType.GreaterEqual),
			new StaticLexemDefinition("<>", LexemType.NotEqual),
			new StaticLexemDefinition("<", LexemType.Less),
			new StaticLexemDefinition(">", LexemType.Greater),
			new StaticLexemDefinition("=", LexemType.Assign),

			new StaticLexemDefinition("[[", LexemType.DoubleSquareOpen),
			new StaticLexemDefinition("]]", LexemType.DoubleSquareClose),
			new StaticLexemDefinition("[", LexemType.SquareOpen),
			new StaticLexemDefinition("]", LexemType.SquareClose),
			new StaticLexemDefinition("(", LexemType.ParenOpen),
			new StaticLexemDefinition(")", LexemType.ParenClose),
			new StaticLexemDefinition("{", LexemType.CurlyOpen),
			new StaticLexemDefinition("}", LexemType.CurlyClose),

			new StaticLexemDefinition("+", LexemType.Plus),
			new StaticLexemDefinition("-", LexemType.Minus),
			new StaticLexemDefinition("**", LexemType.Power),
			new StaticLexemDefinition("*", LexemType.Multiply),
			new StaticLexemDefinition("/", LexemType.Divide),
			new StaticLexemDefinition("%", LexemType.Remainder),
			new StaticLexemDefinition("&&", LexemType.And),
			new StaticLexemDefinition("||", LexemType.Or),
			new StaticLexemDefinition("^^", LexemType.Xor),

			new StaticLexemDefinition("@", LexemType.AtSign),
			new StaticLexemDefinition("::", LexemType.DoubleСolon),
			new StaticLexemDefinition(":", LexemType.Colon),
			new StaticLexemDefinition(",", LexemType.Comma),
			new StaticLexemDefinition("..", LexemType.DoubleDot),
			new StaticLexemDefinition(".", LexemType.Dot),
			new StaticLexemDefinition(";", LexemType.Semicolon),
			new StaticLexemDefinition("?", LexemType.QuestionMark),
			new StaticLexemDefinition("~", LexemType.Tilde),
		};

		private RegexLexemDefinition[] RegexLexems = new[]
		{
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", LexemType.Double),
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)", LexemType.Int),
			new RegexLexemDefinition(@"([a-zA-Z_][0-9a-zA-Z_]*)", LexemType.Identifier)
		};

		private void Error(string src, params object[] args)
		{
			var loc = new LocationEntity { StartLocation = getPosition() };
			throw new LensCompilerException(string.Format(src, args), loc);
		}

		/// <summary>
		/// Checks if the cursor has run outside string bounds.
		/// </summary>
		private bool inBounds()
		{
			return Position < Source.Length;
		}

		/// <summary>
		/// Checks if the cursor is at comment start.
		/// </summary>
		/// <returns></returns>
		private bool isComment()
		{
			return Position < Source.Length - 2 && Source[Position] == '/' && Source[Position + 1] == '/';
		}

		/// <summary>
		/// Skips one or more symbols.
		/// </summary>
		private void skip(int count = 1)
		{
			Position += count;
			Offset += count;
		}

		/// <summary>
		/// Returns the current position in the string.
		/// </summary>
		private LexemLocation getPosition()
		{
			return new LexemLocation
			{
				Line = Line,
				Offset = Offset
			};
		}
	}
}
