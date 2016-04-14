// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpressionFormatter
{
	/// <summary>
	/// An ExpressionToken, that when formatted, is surrounded in brackets.
	/// </summary>
	class BracketedExpressionToken : ExpressionToken
	{
		public readonly string OpenBracket, CloseBracket;
		public readonly ExpressionToken Body;
		public bool NewLineBefore;
		public bool OmitBracketsForSingle;

		public BracketedExpressionToken (string openBracket, string closeBracket, ExpressionToken body)
			: this (openBracket, closeBracket, false, body)
		{
		}

		public BracketedExpressionToken (string openBracket, string closeBracket, bool omitBracketsForSingle, ExpressionToken body)
		{
			OpenBracket = openBracket;
			CloseBracket = closeBracket;
			OmitBracketsForSingle = omitBracketsForSingle;
			Body = body;
		}

		public override void Write (StringBuilder sb, int indent)
		{
			bool single = !(Body is CompositeExpressionToken) || ((CompositeExpressionToken)Body).Tokens.Count () == 1;
			if (!OmitBracketsForSingle || NewLineBefore) single = false;
			int openIndent = indent;
			if (NewLineBefore)
			{
				WriteNewLine (sb, indent);
				openIndent = indent;
				sb.Append (OpenBracket);
			}
			bool alreadyIndented = sb.Length > 2 && char.IsWhiteSpace (sb[sb.Length - 1]) && char.IsWhiteSpace (sb[sb.Length - 2]);
			if (Body.MultiLine) indent++;
			if (NewLineBefore || alreadyIndented)
				WriteNewLine (sb, indent);
			if (!NewLineBefore && !single)
			{
				sb.Append (OpenBracket);
				openIndent = indent;
			}
			Body.Write (sb, indent + Body.SplitIndent);
			if (Body.MultiLine) WriteNewLine (sb, openIndent);
			if (!single) sb.Append (CloseBracket);
		}

		public override int Length
		{
			get { return Body.Length + OpenBracket.Length + CloseBracket.Length; }
		}

		public override bool MultiLine
		{
			get { return Body.MultiLine || NewLineBefore; }
			set { }
		}
	}
}
