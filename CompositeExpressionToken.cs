// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpressionFormatter
{
	/// <summary>
	/// An ExpressionToken that comprises other ExpressionTokens.
	/// </summary>
	class CompositeExpressionToken : ExpressionToken
	{
		public readonly List<ExpressionToken> Tokens = new List<ExpressionToken> ();
		public bool AddCommas;
		int? _length;
		bool? _multiLine;

		public CompositeExpressionToken () { }

		public CompositeExpressionToken (IEnumerable<ExpressionToken> tokens, bool addCommas)
		{
			Tokens.AddRange (tokens.Where (t => t != null));
			AddCommas = addCommas;
			if (addCommas)
				foreach (ExpressionToken t in Tokens)
					if (t != null)
						t.Splittable = true;
		}

		public void AddStringToken (string s)
		{
			AddStringToken (s, false);
		}

		public void AddStringToken (string s, bool splittable)
		{
			Tokens.Add (new LeafExpressionToken (s) { Splittable = splittable });
		}

		public override void Write (StringBuilder sb, int indent)
		{
			bool first = true;
			foreach (ExpressionToken t in Tokens)
				if (t != null)
				{
					if (first)
						first = false;
					else if (AddCommas)
						sb.Append (", ");

					if (MultiLine && t.Splittable)
						WriteNewLine (sb, indent + t.SplitIndent);

					t.Write (sb, indent + t.SplitIndent);
				}
		}

		public override int Length
		{
			get
			{
				if (!_length.HasValue) _length = Tokens.Sum (t => t.Length);
				return _length.Value;
			}
		}

		public override bool MultiLine
		{
			get
			{
				if (!_multiLine.HasValue)
					_multiLine = Length > 90 || Tokens.Count () > 5 || Tokens.Any (t => t.MultiLine);
				return _multiLine.Value;
			}
			set { _multiLine = value; }
		}
	}
}
