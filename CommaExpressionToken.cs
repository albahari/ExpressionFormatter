// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpressionFormatter
{
	class CommaExpressionToken : LeafExpressionToken
	{
		public CommaExpressionToken () : base (", ") { }
	}
}
