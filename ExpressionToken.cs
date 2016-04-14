// Written by Joseph Albahari, LINQPad. See license.txt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Data.Linq;

namespace ExpressionFormatter
{
	/// <summary>
	/// Describes an expression node with formatting info. This is used to generate a nicely formatted lambda expression
	/// string (i.e., with line breaks).
	/// </summary>
	abstract class ExpressionToken
	{
		#region Static fields & constructor

		static Dictionary<ExpressionType, string> _binaryExpressionStrings = new Dictionary<ExpressionType, string> ();
		static Dictionary<Type, MethodInfo> _visitMethods;

		static ExpressionToken ()
		{
			ExpressionType [] binExprTypes =
			{
				ExpressionType.Add,
				ExpressionType.AddChecked,
				ExpressionType.And,
				ExpressionType.AndAlso,
				ExpressionType.Coalesce,
				ExpressionType.Divide,
				ExpressionType.Equal,
				ExpressionType.ExclusiveOr,
				ExpressionType.GreaterThan,
				ExpressionType.GreaterThanOrEqual,
				ExpressionType.LeftShift,
				ExpressionType.LessThan,
				ExpressionType.LessThanOrEqual,
				ExpressionType.Modulo,
				ExpressionType.Multiply,
				ExpressionType.MultiplyChecked,
				ExpressionType.NotEqual,
				ExpressionType.Or,
				ExpressionType.OrElse,
				ExpressionType.Power,
				ExpressionType.RightShift,
				ExpressionType.Subtract,
				ExpressionType.SubtractChecked
			};

			string [] binExprStrings = "+ + & && ?? . == ^ > >= << < <= % * * != | || ^ >> - -".Split ();

			int i = 0;
			foreach (ExpressionType t in binExprTypes)
				_binaryExpressionStrings [t] = binExprStrings [i++];

			_visitMethods =
			(
				from exType in typeof (Expression).Assembly.GetTypes ()
				where exType.IsSubclassOf (typeof (Expression)) || exType.IsSubclassOf (typeof (MemberBinding))
				join method in (typeof (ExpressionToken).GetMethods (BindingFlags.Static | BindingFlags.NonPublic))
					.Where (m => m.Name == "Visit")
				on exType equals method.GetParameters () [0].ParameterType
				select new
				{
					exType,
					method
				}
			)
			.ToDictionary (t => t.exType, t => t.method);
		}

		#endregion

		#region Static Visitor Methods

		public static ExpressionToken Visit (Expression expr)
		{
			return Visit ((object)expr);
		}

		static ExpressionToken Visit (object expr)
		{
			if (expr == null) return new LeafExpressionToken ("null");
			MethodInfo toCall;
			Type t = expr.GetType ();
			Type originalType = t;
			while (!t.IsPublic) t = t.BaseType;
			if (_visitMethods.TryGetValue (t, out toCall) || t.BaseType != null && _visitMethods.TryGetValue (t.BaseType, out toCall))
			{
				ExpressionToken et = (ExpressionToken)toCall.Invoke (null, new object [] { expr });
				return et;
			}

			if (t.FullName == "System.Data.Services.Client.ResourceSetExpression")
			{
				var prop = t.GetProperty ("MemberExpression", BindingFlags.Instance | BindingFlags.NonPublic);
				if (prop != null)
				{
					var memberExpr = prop.GetValue (expr, null) as ConstantExpression;
					if (memberExpr != null && memberExpr.Value != null)
						return new LeafExpressionToken (memberExpr.Value.ToString ());
				}
			}
			if (originalType.FullName == "System.Data.Services.Client.ResourceSetExpression")
			{
				var prop = originalType.GetProperty ("MemberExpression", BindingFlags.Instance | BindingFlags.NonPublic);
				if (prop != null)
				{
					var memberExpr = prop.GetValue (expr, null) as ConstantExpression;
					if (memberExpr != null && memberExpr.Value != null)
						return new LeafExpressionToken (memberExpr.Value.ToString ());
				}
			}

			return null;			
		}

		static ExpressionToken Visit (BinaryExpression exp)
		{
			CompositeExpressionToken t = new CompositeExpressionToken ();

			ExpressionToken left = Visit (exp.Left);
			ExpressionToken right = Visit (exp.Right);

			if (exp.NodeType == ExpressionType.ArrayIndex)
			{
				t.Tokens.Add (left);
				t.Tokens.Add (new BracketedExpressionToken ("[", "]", right));
				return t;
			}

			string symbol;
			if (_binaryExpressionStrings.TryGetValue (exp.NodeType, out symbol))
			{
				t.Tokens.Add (left);
				t.AddStringToken (" " + symbol + " ");
				t.Tokens.Add (right);
				right.Splittable = true;
				right.SplitIndent = 1;
				return new BracketedExpressionToken ("(", ")", true, t);
			}

			return t;		
		}

		static ExpressionToken Visit (ConditionalExpression exp)
		{
			var t = new CompositeExpressionToken ();
			t.Tokens.Add (Visit (exp.Test));
			t.AddStringToken (" ? ", true);
			t.Tokens.Add (Visit (exp.IfTrue));
			t.AddStringToken (" : ", true);
			t.Tokens.Add (Visit (exp.IfFalse));
			return t;
		}

		static ExpressionToken Visit (ConstantExpression exp)
		{
			if (exp.Value != null && exp.Type.IsGenericType && exp.Type.GetGenericTypeDefinition () == typeof (Table<>))
			{
				PropertyInfo contextProp = exp.Value.GetType ().GetProperty ("Context");
				if (contextProp != null)
				{
					object dc = contextProp.GetValue (exp.Value, null);
					if (dc != null)
					{
						PropertyInfo tableProp = dc.GetType ().GetProperties ().First (p => p.PropertyType == exp.Type);
						if (tableProp != null) return new LeafExpressionToken (tableProp.Name);
					}
				}
			}
			else if (exp.Value != null && exp.Type.IsGenericType && 
				(exp.Type.FullName.StartsWith ("System.Data.Objects.ObjectQuery`1", StringComparison.Ordinal) ||
				exp.Type.FullName.StartsWith ("System.Data.Objects.ObjectSet`1", StringComparison.Ordinal) ||
				exp.Type.FullName.StartsWith ("System.Data.Services.Client.DataServiceQuery`1")))
			{
				return new LeafExpressionToken (exp.Type.GetGenericArguments ()[0].Name);
			}
			string value = exp.Value == null ? "null" : exp.Value is string ? ('"' + (string)exp.Value + '"') : exp.Value.ToString ();
			return new LeafExpressionToken (value);
		}

		static ExpressionToken Visit (InvocationExpression exp)
		{
			var t = new CompositeExpressionToken ();
			t.AddStringToken ("Invoke" + (exp.Arguments.Count == 0 ? "" : " "), true);
			t.Tokens.Add (
				new BracketedExpressionToken (
					"(", ")", new CompositeExpressionToken (exp.Arguments.Select (a => Visit (a)), true))
				);
			return t;
		}

		static ExpressionToken Visit (LambdaExpression exp)
		{
			var t = new CompositeExpressionToken ();
			string s = "";
			if (exp.Parameters.Count != 1) s = "(";
			s += string.Join (", ", exp.Parameters.Select (p => CleanIdentifier (p.Name)).ToArray ());
			if (exp.Parameters.Count != 1) s += ")";
			s += " => ";
			t.AddStringToken (s);
			ExpressionToken body = Visit (exp.Body);
			if (body != null)
			{
				body.Splittable = true;
				body.SplitIndent = 1;
				t.Tokens.Add (body);
			}
			return t;
		}

		static ExpressionToken Visit (ListInitExpression exp)
		{
			var t = new CompositeExpressionToken ();
			t.Tokens.Add (Visit (exp.NewExpression));
			var outer = new CompositeExpressionToken ();
			outer.AddCommas = true;
			foreach (var init in exp.Initializers)
				outer.Tokens.Add (
					new BracketedExpressionToken (
						"(", ")", true, new CompositeExpressionToken (init.Arguments.Select (a => Visit (a)), true))
					);
			t.Tokens.Add (new BracketedExpressionToken (" { ", " } ", outer));
			return t;
		}

		static ExpressionToken Visit (MemberExpression exp)
		{
			CompositeExpressionToken t = new CompositeExpressionToken ();
			var constExp = exp.Expression as ConstantExpression;
			// Strip out captured variables:
			if (constExp != null && constExp.Value != null && constExp.Value.GetType ().IsNested && constExp.Value.GetType ().Name.StartsWith ("<", StringComparison.Ordinal)
				|| constExp != null && constExp.Value is DataContext)
				return new LeafExpressionToken (exp.Member.Name);
			else if (exp.Expression != null)
				t.Tokens.Add (Visit (exp.Expression));
			else
				t.AddStringToken (exp.Member.DeclaringType.Name);			
			t.AddStringToken ("." + CleanIdentifier (exp.Member.Name));
			return t;
		}

		static ExpressionToken Visit (MemberInitExpression exp)
		{
			CompositeExpressionToken t = new CompositeExpressionToken ();
			if (exp.NewExpression.Type.IsAnonymous())
				t.AddStringToken ("new ");
			else
				t.Tokens.Add (Visit (exp.NewExpression));
			t.Tokens.Add (
				new BracketedExpressionToken (
					"{", "}", new CompositeExpressionToken (exp.Bindings.Select (b => Visit (b)), true) { MultiLine = true })
				{
					NewLineBefore = true
				}
			);
			return t;
		}

		static ExpressionToken Visit (MethodCallExpression exp)
		{
			bool extensionMethod = Attribute.IsDefined (exp.Method, typeof (ExtensionAttribute));
			string methodName = exp.Method.Name;

			CompositeExpressionToken t = new CompositeExpressionToken ();
			if (extensionMethod)
			{
				if (exp.Method.DeclaringType == typeof (Queryable)) t.MultiLine = true;
				t.Tokens.Add (Visit (exp.Arguments [0]));
				t.AddStringToken ("." + methodName + " ", true);
				t.Tokens.Last ().SplitIndent = 1;
				var args = new CompositeExpressionToken (exp.Arguments.Skip (1).Select (a => Visit (a)), true);
				if (exp.Method.DeclaringType == typeof (Queryable) && exp.Arguments.Count () > 2) args.MultiLine = true;
				args.SplitIndent = 1;
				t.Tokens.Add (
					new BracketedExpressionToken (
						"(", ")", args)
					);				
			}
			else
			{
				if (exp.Object == null)
					t.AddStringToken (exp.Method.DeclaringType.FormatName());
				else
					t.Tokens.Add (Visit (exp.Object));
				if (exp.Method.IsSpecialName)
				{
					var prop = exp.Method.DeclaringType.GetProperties ().Where (p => p.GetAccessors ().Contains (exp.Method)).FirstOrDefault ();
					if (prop != null)
					{
						t.Tokens.Add (
							new BracketedExpressionToken (
								" [", "]", new CompositeExpressionToken (exp.Arguments.Select (a => Visit (a)), true) )
								);
						return t;
					}
				}
				t.AddStringToken ("." + methodName + (exp.Arguments.Count == 0 ? "" : " "), true);
				t.Tokens.Last ().SplitIndent = 1;
				t.Tokens.Add (
					new BracketedExpressionToken (
						"(", ")", new CompositeExpressionToken (exp.Arguments.Select (a => Visit (a)), true) { SplitIndent = 1 })
					);
			}
			return t;
		}

		static ExpressionToken Visit (NewArrayExpression exp)
		{
			bool newArrayInit = exp.NodeType == ExpressionType.NewArrayInit;
			CompositeExpressionToken t = new CompositeExpressionToken ();
			bool anon = exp.Type.IsAnonymous();
			Type type = exp.Type;
			if (type.IsArray) type = type.GetElementType ();
			string typeName = anon ? "" : (type.FormatName ());
			string newText = "new " + typeName;
			if (newArrayInit && exp.Expressions.Any ())
				newText += "[] ";
			else if (newArrayInit)
				newText += "[0]";
			t.AddStringToken (newText);
			if (!newArrayInit || exp.Expressions.Any ())
				t.Tokens.Add (
						new BracketedExpressionToken (
							newArrayInit ? "{ " : "[",
							newArrayInit ? " } " : "]",
							new CompositeExpressionToken (exp.Expressions.Select (a => Visit (a)), true))
						);
			return t;
		}

		static ExpressionToken Visit (NewExpression exp)
		{
			CompositeExpressionToken t = new CompositeExpressionToken ();
			Type type = exp.Type;
			if (exp.Constructor != null) type = exp.Constructor.DeclaringType;
			t.AddStringToken ("new " + (type.IsAnonymous()
				? ""
				: type.FormatName ())
				+ (exp.Arguments.Count == 0 ? "" : " ")
				);

			if (exp.Members == null || exp.Members.Count == 0)
			{
				CompositeExpressionToken body = new CompositeExpressionToken (exp.Arguments.Select (a => Visit (a)), true);
				t.Tokens.Add (new BracketedExpressionToken ("(", ")", body));
			}
			else
			{
				int i = 0;
				var multi = new CompositeExpressionToken { MultiLine = true, AddCommas = true };
				foreach (Expression argExpr in exp.Arguments)
				{
					MemberInfo mi = exp.Members [i++];
					PropertyInfo pi = mi as PropertyInfo;
					if (pi == null) pi = mi.DeclaringType
						.GetProperties (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						.FirstOrDefault (p => p.GetAccessors ().Contains (mi));
					if (pi != null)
					{
						var assignment = new CompositeExpressionToken ();
						assignment.AddStringToken (CleanIdentifier (pi.Name) + " = ");
						assignment.Tokens.Add (Visit (argExpr));
						assignment.Splittable = true;
						multi.Tokens.Add (assignment);
					}
				}
				t.Tokens.Add (new BracketedExpressionToken ("{", "}", multi) { NewLineBefore = true });
			}
			return t;
		}

		static ExpressionToken Visit (ParameterExpression exp)
		{
			string name = exp.Name ?? "<param>";
			return new LeafExpressionToken (CleanIdentifier (name));
		}

		static ExpressionToken Visit (TypeBinaryExpression exp)
		{
			var t = new CompositeExpressionToken ();
			t.Tokens.Add (Visit (exp.Expression));
			t.AddStringToken (" is ");
			t.AddStringToken (exp.TypeOperand.FormatName ());
			return new BracketedExpressionToken ("(", ")", t);
		}

		static ExpressionToken Visit (UnaryExpression exp)
		{
			if (exp.NodeType == ExpressionType.Quote) return Visit (exp.Operand);
			var t = new CompositeExpressionToken ();

			switch (exp.NodeType)
			{
				case ExpressionType.Convert:
					if (exp.Operand.Type.IsSubclassOf (exp.Type)) return Visit (exp.Operand);
					t.AddStringToken ("(" + exp.Type.FormatName () + ")");
					break;
				case ExpressionType.TypeAs:
					t.Tokens.Add (Visit (exp.Operand));
					t.AddStringToken (" as ");
					t.AddStringToken (exp.Type.FormatName ());
					return t;
				case ExpressionType.Not:
					t.AddStringToken ("!");
					break;
				case ExpressionType.UnaryPlus:
					t.AddStringToken ("+");
					break;
				case ExpressionType.Negate:
				case ExpressionType.NegateChecked:
					t.AddStringToken ("-");
					break;
				default:
					t.AddStringToken (exp.NodeType.ToString ());
					break;
			}

			t.Tokens.Add (new BracketedExpressionToken ("(", ")", true, Visit (exp.Operand)));
			return t;
		}

		static ExpressionToken Visit (MemberAssignment mb)
		{
			var t = new CompositeExpressionToken ();
			t.AddStringToken (CleanIdentifier (mb.Member.Name) + " = ");
			t.Tokens.Add (Visit (mb.Expression));
			t.Splittable = true;
			return t;
		}

		static ExpressionToken Visit (MemberListBinding mb)
		{
			return null;
		}

		static ExpressionToken Visit (MemberMemberBinding mb)
		{
			return null;
		}

		static string CleanIdentifier (string name)
		{
			if (name == null) return null;
			if (name.StartsWith ("<>h__TransparentIdentifier", StringComparison.Ordinal)) return "temp" + name.Substring (26);
			return name;
		}

		#endregion

		#region Instance Members

		public abstract int Length { get; }
		public abstract bool MultiLine { get; set; }

		public bool Splittable;
		public int SplitIndent;

		public abstract void Write (StringBuilder sb, int indent);

		protected void WriteNewLine (StringBuilder sb, int indent)
		{
			int trailingSpaces = 0;
			int last = sb.Length - 1;
			while (last > 0 && sb [last] == ' ') { trailingSpaces++; last--; }
			if (last > 0 && sb [last] == '\n')
			{
				int dif = trailingSpaces - indent * 3;
				if (dif == 0) return;
				if (dif > 0) sb.Remove (sb.Length - dif, dif);
				else sb.Append ("".PadRight (-dif));
			}
			else
				sb.Append ("\r\n".PadRight (2 + indent * 3));
		}

		public override string ToString ()
		{
			StringBuilder sb = new StringBuilder ();
			Write (sb, 0);
			return sb.ToString ();
		}

		#endregion
	}
}
