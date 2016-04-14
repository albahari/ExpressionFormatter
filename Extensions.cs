// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionFormatter
{
	public static class Extensions
	{
		public static string Format (this Expression expr)
		{
			var token = ExpressionToken.Visit (expr);
			return token == null ? null : token.ToString ();
		}

		internal static bool IsAnonymous (this Type type)
		{
			if (!string.IsNullOrEmpty (type.Namespace) || !type.IsGenericType) return false;
			return IsAnonymous (type.Name);
		}

		internal static bool IsAnonymous (string typeName)
		{
			// Optimization to improve perf when called from UserCache
			return
				typeName.Length > 5 &&
					(typeName [0] == '<' && typeName [1] == '>' && (typeName [5] == 'A' && typeName [6] == 'n' || typeName.IndexOf ("anon", StringComparison.OrdinalIgnoreCase) > -1) ||
					typeName [0] == 'V' && typeName [1] == 'B' && typeName [2] == '$' && typeName [3] == 'A' && typeName [4] == 'n');
		}

		internal static string FormatName (this Type t, bool fullname = false)
		{
			return fullname ? t.FullName : t.Name;
		}
	}
}
