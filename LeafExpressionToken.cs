// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpressionFormatter
{
	/// <summary>
	/// An ExpressionToken that's written as a simple text string.
	/// </summary>
	class LeafExpressionToken : ExpressionToken
	{
		public readonly string Text;

		public LeafExpressionToken (string text)
		{
			Text = text;
		}

		public override void Write (StringBuilder sb, int indent)
		{
			sb.Append (Text);
		}

		public override int Length
		{
			get { return Text.Length; }
		}

		public override bool MultiLine
		{
			get { return false; }
			set { }
		}
	}
}
