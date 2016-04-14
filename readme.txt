Simple Demo:


		static void Main (string [] args)
		{
			Expression<Func<int, bool>> expr = x => x % 2 == 0;
			Console.WriteLine (expr.Format ());

			Console.WriteLine ();

			var query =
				from c in "The quick brown fox uses LINQPad".AsQueryable ()
				where c != ' '
				orderby c
				select char.ToUpper (c);

			Console.WriteLine (query.Expression.Format ());
		}