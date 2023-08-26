using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Text.RegularExpressions;

namespace SehensWerte.Utils
{
    public static class SqlQuery
    {
        static public List<string> ExtractColumnNamesFromQuery(string sqlQuery)
        {
            Queue<string> tokens = Tokenise(sqlQuery);

            List<string> columnNames = new List<string>();
            if (next(tokens).ToUpper() == "SELECT")
            {
                while (peek(tokens).ToUpper() switch { "" => false, "FROM" => false, _ => true })
                {
                    columnNames.Add(ParseName(tokens));
                    if (peek(tokens) == ",")
                    {
                        next(tokens);
                    }
                }
                if (next(tokens).ToUpper() == "FROM")
                {
                    string from = next(tokens) + ".";
                    columnNames = columnNames.Select(x => x.StartsWith(from) ? x.Substring(from.Length) : x).ToList();
                }
            }

            return columnNames;
        }

        private static string ParseName(Queue<string> tokens)
        {
            string[] hideFunctions = { "to_json" };
            StringBuilder fullClause = new StringBuilder();
            string name = "";
            while (peek(tokens).ToUpper() switch { "" => false, "," => false, "FROM" => false, _ => true })
            {
                name = next(tokens, fullClause);
                if (hideFunctions.Contains(name) && peek(tokens) == "(")
                {
                    name = functionName(tokens, fullClause);
                }
                else if (name == "(")
                {
                    skipToClosingBracket(tokens, fullClause);
                }
            }
            name = name == "(" ? fullClause.ToString() : name;
            return Regex.Replace(name.Trim(), @"\s*([,()])\s*", "$1"); // remove extra spaces
        }

        private static string functionName(Queue<string> tokens, StringBuilder? sum = null)
        {
            string name;
            next(tokens, sum); // skip (
            name = "";
            while (peek(tokens) != ")")
            {
                name = name + next(tokens, sum) + " ";
            }
            next(tokens, sum); // skip )
            return name;
        }

        private static void skipToClosingBracket(Queue<string> tokens, StringBuilder? sum = null)
        {
            while (peek(tokens) != ")")
            {
                if (next(tokens, sum) == "(")
                {
                    skipToClosingBracket(tokens, sum);
                }
            }
            next(tokens, sum); // skip )
        }

        private static string peek(Queue<string> tokens)
        {
            string token;
            return tokens.TryPeek(out token) ? token : "";
        }

        private static string next(Queue<string> tokens, StringBuilder? sum = null)
        {
            string token;
            token = tokens.TryDequeue(out token) ? token : "";
            sum?.Append(token + " ");
            return token;
        }

        private static Queue<string> Tokenise(string sqlQuery)
        {
            // split into
            // mnemonics including a and a.b
            // (
            // , 
            // numeric literals including 1 2.3 -4 -5.6 .7

            return new Queue<string>(
                Regex.Matches(sqlQuery, @"('[^']*'|[A-Za-z_][A-Za-z0-9_.]*|[-.]?\d+(\.\d*)?|\S)")
                .Select(x => x.Value));
        }
    }

    [TestClass]
    public class SqlQueryTests
    {
        [TestMethod]
        public void TestExtractColumns()
        {
            string test = @"select 
            name_with_underscores_and_1_numbers_2_,
            name,
            table.name,
            table_name.name,
            table_1.col2,
            to_json(jsondate),
            count(simple.thing),
            (select count(thing) from thing where thing=true and thing>1.234 and (other thing)) as user_sessions,
            -123.456,
            5,
            0.5,
            .5
        from table_name
        things following table name
";
            var result = SqlQuery.ExtractColumnNamesFromQuery(test);
            CollectionAssert.AreEqual(
                result,
                new string[] {
            "name_with_underscores_and_1_numbers_2_",
            "name",
            "table.name",
            "name",
            "table_1.col2",
            "jsondate",
            "count(simple.thing)",
            "user_sessions",
            "-123.456",
            "5",
            "0.5",
            ".5"
                });

        }
    }
}