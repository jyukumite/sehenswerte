using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SehensWerte.Utils
{
    public static class SqlQuery
    {
        static public String MainQuery(string sqlQuery)
        {
            Queue<string> tokens = Tokenise(sqlQuery);
            return MainQuery(tokens);
        }

        static public List<string> ExtractColumnNamesFromQuery(string sqlQuery)
        {
            Queue<string> tokens = Tokenise(sqlQuery);

            List<string> columnNames = new List<string>();
            if (MainQuery(tokens).ToUpper() == "SELECT")
            {
                while (tokens.Count() != 0 && peek(tokens).ToUpper() switch { "" => false, "FROM" => false, _ => true })
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

        private static string MainQuery(Queue<string> tokens)
        {
            if (peek(tokens).ToUpper() == "BEGIN") // transaction
            {
                next(tokens); // BEGIN
                if (!next(tokens).ToUpper().StartsWith("TRAN")) return "";
                if (peek(tokens) != ";")
                {
                    next(tokens); // skip transaction name
                }
                if (next(tokens) != ";") return "";
            }

            while (tokens.Count() != 0 && peek(tokens).ToUpper() == "WITH") // temporary tables
            {
                // WITH temp_table AS (select 1,2,3) SELECT ...
                next(tokens); // WITH
                next(tokens); // name of temp table
                if (next(tokens).ToUpper() != "AS") return "";
                if (next(tokens).ToUpper() != "(") return "";
                skipToClosingBracket(tokens);
            }
            return next(tokens);
        }

        private static string ParseName(Queue<string> tokens)
        {
            string[] hideFunctions = { "to_json" };
            StringBuilder fullClause = new StringBuilder();
            string name = "";
            while (tokens.Count() != 0 && peek(tokens).ToUpper() switch { "" => false, "," => false, "FROM" => false, _ => true })
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
            while (tokens.Count() != 0 && peek(tokens) != ")")
            {
                name = name + next(tokens, sum) + " ";
            }
            next(tokens, sum); // skip )
            return name;
        }

        private static void skipToClosingBracket(Queue<string> tokens, StringBuilder? sum = null)
        {
            while (tokens.Count() != 0 && peek(tokens) != ")")
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
            return tokens.TryPeek(out token!) ? token : "";
        }

        private static string next(Queue<string> tokens, StringBuilder? sum = null)
        {
            string token;
            token = tokens.TryDequeue(out token!) ? token : "";
            sum?.Append(token + " ");
            return token;
        }

        public static Queue<string> Tokenise(string sqlQuery)
        {
            var pattern = @"(:[A-Za-z_][A-Za-z0-9_]*)"    // parameters, e.g. :param_name
                        + @"|'([^']|'')*'"                // single-quoted strings, including escaped single quotes
                        + @"|""([^""]|"""")*"""           // double-quoted strings, including escaped double quotes
                        + @"|[A-Za-z_][A-Za-z0-9_.]*"     // identifiers and object/column names, like table.column
                        + @"|[-+]?(?:\d+\.\d*|\.\d+|\d+)" // numbers, including integers and decimals
                        + @"|[!=<>:@\-+*/%&|^#~]{2,3}"    // operators (tradeoff between false positives and complexity but arguably better than \W)
                        + @"|\S";                         // any single non-whitespace character as a fallback

            return new Queue<string>(
                Regex.Matches(sqlQuery, pattern)
                .Select(x => x.Value));
        }

        public static string Parameterise(string baseQuery, Dictionary<string, object?>? parameters)
        {
            // Insert parameters replacing named :variable callouts in the query

            var tokens = Tokenise(baseQuery);
            var result = new StringBuilder();
            var test = new HashSet<string>();
            string prevToken = " ";
            foreach (var token in tokens)
            {
                if (result.Length == 0 || token == ";" || token == ")" || token == "," || prevToken == "(" )
                {
                    // concatenate without space, for readability
                }
                else
                {
                    result.Append(" ");
                }
                if (token.StartsWith(":"))
                {
                    string paramName = token.Substring(1);
                    if (!(parameters?.TryGetValue(paramName, out var paramValue) ?? false))
                    {
                        throw new ArgumentException($"Missing parameter {paramName}");
                    }
                    result.Append(EscapeParameter(paramValue));
                    test.Add(paramName);
                }
                else
                {
                    result.Append(token);
                }
                prevToken = token;
            }
            if (test.Count != (parameters?.Count ?? 0))
            {
                throw new ArgumentException($"Parameter count mismatch {test} unique parameters in query, {parameters.Count} parameters");
            }
            return result.ToString();
        }

        public static string Parameterise(string baseQuery, object?[]? parameters)
        {
            // Insert parameters in order to replace positional %s callouts in the query

            if (parameters == null)
            {
                // if NULL parameters are given, return the baseQuery as it has already been %% trimmed
                return baseQuery;
            }
            int paramIndex = 0;
            var result = new StringBuilder();
            int i = 0;
            int length = baseQuery.Length;

            while (i < length - 1)
            {
                if (baseQuery[i] == '%')
                {
                    if (baseQuery[i + 1] == '%') // %%
                    {
                        result.Append('%');
                    }
                    else if (baseQuery[i + 1] == 's') // %s
                    {
                        if (parameters == null || paramIndex >= parameters.Length)
                        {
                            throw new ArgumentException("Parameter count mismatch (too few parameters)");
                        }
                        var param = parameters[paramIndex++]; // allow exception
                        result.Append(EscapeParameter(param));
                    }
                    else
                    {
                        result.Append(baseQuery[i]);
                        result.Append(baseQuery[i + 1]);
                    }
                    i += 2;
                }
                else
                {
                    result.Append(baseQuery[i]);
                    i++;
                }
            }
            if (i < length)
            {
                result.Append(baseQuery[i++]);
            }
            if (paramIndex != parameters.Length)
            {
                throw new ArgumentException("Parameter count mismatch (too many parameters)");
            }
            return result.ToString();
        }

        public static string EscapeParameter(object? param)
        {
            return param switch
            {
                null => "NULL",
                string str => EscapeString(str),
                int or double or float or decimal => Convert.ToString(param, CultureInfo.InvariantCulture) ?? "NULL",
                DateTime dateTime => $"'{dateTime:yyyy-MM-dd HH:mm:ss}'",
                Guid guid => $"UUID('{guid.ToString()}')",
                bool => (bool)param ? "True" : "False",
                _ => EscapeString(param?.ToString() ?? "NULL")
            };

            static string EscapeString(string param)
            {
                bool cr = param.Contains((char)10) || param.Contains((char)13);
                string v = param
                    .Replace("'", "''")
                    .Replace("\\", "\\\\")
                    .Replace("\n\r", "\n")
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\\n")
                    .Replace("\n", "\\n");
                return $"{(cr ? "E" : "")}'{v}'";
            }
        }

        public static string UnescapeString(string value)
        {
            return value.Replace("\\n", "\n").Replace("\n", "\r\n").Replace("\\\\", "\\");
        }
    }

    [TestClass]
    public class SqlQueryTests
    {
        [TestMethod]
        public void TestExtractColumns()
        {
            string test = @"
            begin transaction t1;
            WITH temp_table AS (select a, b, c)
            select 
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
            commit tran t1;
";
            Assert.AreEqual("select", SqlQuery.MainQuery(test));

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

        [TestMethod]
        public void ParameteriseList()
        {
            var testCases = new[]
            {
                new
                {
                    Query = "SELECT * FROM users WHERE username = %s AND age = %s;",
                    Parameters = (object?[]?) new object[] { "bob", 25 },
                    Expected = "SELECT * FROM users WHERE username = 'bob' AND age = 25;"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = '%% \" %%';",
                    Parameters = (object?[]?)null,
                    Expected = "SELECT * FROM users WHERE username = '%% \" %%';"
                },
                new
                {
                    Query = "SELECT * FROM table WHERE value LIKE '%%s%%' AND id = %s;",
                    Parameters = (object?[]?) new object[] { 42 },
                    Expected = "SELECT * FROM table WHERE value LIKE '%s%' AND id = 42;"
                },
                new
                {
                    Query = "INSERT INTO logs (message) VALUES (%s);",
                    Parameters = (object?[]?) new object[] { "Error occurred at % in module %s" },
                    Expected = "INSERT INTO logs (message) VALUES ('Error occurred at % in module %s');"
                },
                new
                {
                    Query = "INSERT INTO table (name, age, birthdate, active) VALUES (%s, %s, %s, %s);",
                    Parameters = (object?[]?) new object[] { "Alice", 30, new DateTime(1990, 1, 1), true },
                    Expected = "INSERT INTO table (name, age, birthdate, active) VALUES ('Alice', 30, '1990-01-01 00:00:00', True);"
                },
                new
                {
                    Query = "INSERT INTO table (name, description) VALUES (%s, %s);",
                    Parameters = (object?[]?) new object?[] { "Item", null },
                    Expected = "INSERT INTO table (name, description) VALUES ('Item', NULL);"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = %s;",
                    Parameters = (object?[]?) new object[] { "Robert'); DROP TABLE Students" },
                    Expected = "SELECT * FROM users WHERE username = 'Robert''); DROP TABLE Students';"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = %s;",
                    Parameters = (object?[]?) new object[] { "%%%% \" %%%%" },
                    Expected = "SELECT * FROM users WHERE username = '%%%% \" %%%%';"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = %s AND age = %s;",
                    Parameters = (object?[]?) new object[] { "bob" },
                    Expected = "exception"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = %s;",
                    Parameters = (object?[]?) new object[] { "bob", 25 },
                    Expected = "exception"
                },
            };

            foreach (var testCase in testCases)
            {
                string result = "";
                try
                {
                    result = SqlQuery.Parameterise(testCase.Query, testCase.Parameters);
                    Assert.AreEqual(testCase.Expected, result, $"Query: {testCase.Query}, Parameters: [{(testCase.Parameters == null ? "null" : string.Join(", ", testCase.Parameters))}]");
                }
                catch (ArgumentException)
                {
                    Assert.AreEqual(testCase.Expected, "exception");
                }
            }
        }

        [TestMethod]
        public void ParameteriseDict()
        {
            var testCases = new[]
            {
                new
                {
                    Query = "SELECT COUNT(*) FROM users WHERE username = :username AND age = :age;",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "username", "bob" },
                        { "age", 25 }
                    },
                    Expected = "SELECT COUNT (*) FROM users WHERE username = 'bob' AND age = 25;"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = ':hello';",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "hello", "foo" }
                    },
                    Expected = "exception"
                },
                new
                {
                    Query = "INSERT INTO logs (message) VALUES (:message);",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "message", "Error occurred at % in module %s" }
                    },
                    Expected = "INSERT INTO logs (message) VALUES ('Error occurred at % in module %s');"
                },
                new
                {
                    Query = "INSERT INTO table (name, age, birthdate, active, ageagain) VALUES (:name, :age, :birthdate, :active, :age);",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "name", "Alice" },
                        { "age", 30 },
                        { "birthdate", new DateTime(1990, 1, 1) },
                        { "active", true }
                    },
                    Expected = "INSERT INTO table (name, age, birthdate, active, ageagain) VALUES ('Alice', 30, '1990-01-01 00:00:00', True, 30);"
                },
                new
                {
                    Query = "INSERT INTO table (name, description) VALUES (:name, :description);",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "name", "Item" },
                        { "description", null }
                    },
                    Expected = "INSERT INTO table (name, description) VALUES ('Item', NULL);"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = :username;",
                    Parameters = new Dictionary<string, object?> 
                    { 
                        { "username", "Robert'); DROP TABLE Students" } 
                    },
                    Expected = "SELECT * FROM users WHERE username = 'Robert''); DROP TABLE Students';"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = :username;",
                    Parameters = new Dictionary<string, object?>()
                    {
                        { "username", null }
                    },
                    Expected = "SELECT * FROM users WHERE username = NULL;"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = :username AND age = :age;",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "username", "bob" }
                    },
                    Expected = "exception"
                },
                new
                {
                    Query = "SELECT * FROM users WHERE username = :username;",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "username", "bob" },
                        { "age", 25 }
                    },
                    Expected = "exception"
                },
            };

            foreach (var testCase in testCases)
            {
                string result = "";
                try
                {
                    result = SqlQuery.Parameterise(testCase.Query, testCase.Parameters);
                    Assert.AreEqual(testCase.Expected, result, $"Query: {testCase.Query}, Parameters: [{string.Join(", ", testCase.Parameters?.Select(x => x.Key + ":" + x.Value))}]");
                }
                catch (ArgumentException)
                {
                    Assert.AreEqual(testCase.Expected, "exception");
                }
            }
        }


        [TestMethod]
        public void TestTokeniser()
        {
            string test = @"
select 
    name_with_underscores_and_1_numbers_2_,
    'hel (lo' as name,
    table.name,
    table_name.name,
    table_1.col2,
    to_json(jsondate),
    count(simple.thing),
    (select count(thing) from thing where thing=true and thing>1.234 and (other thing)) as foo,
    -123.456,
    5,
    0.5,
    .5,
    column1 != column2,
    column3 <> column4,
    value1 := value2,
    json_data ->> 'key',
    json_data -> 'key',
    cast_column::text,
    col1 && col2,
    col3 || col4
from ""table_name""
where something='he l)lo  ';
# #> #>> & && &< &> -> ->> -|- << <<= <@ <^ >> >>= >^ @> @@ | ~ ~=
+= -= *= /= %= &= ^-= |*=
: :: :=

";

            var tokens = SqlQuery.Tokenise(test);

            var expectedTokens = new List<string>
            {
                "select",
                "name_with_underscores_and_1_numbers_2_", ",",
                "'hel (lo'", "as", "name", ",",
                "table.name", ",",
                "table_name.name", ",",
                "table_1.col2", ",",
                "to_json", "(", "jsondate", ")", ",",
                "count", "(", "simple.thing", ")", ",",
                "(", "select", "count", "(", "thing", ")", "from", "thing", "where", "thing", "=", "true", "and", "thing", ">", "1.234", "and", "(", "other", "thing", ")", ")", "as", "foo", ",",
                "-123.456", ",",
                "5", ",",
                "0.5", ",",
                ".5", ",",
                "column1", "!=", "column2", ",",
                "column3", "<>", "column4", ",",
                "value1", ":=", "value2", ",",
                "json_data", "->>", "'key'", ",",
                "json_data", "->", "'key'", ",",
                "cast_column", "::", "text", ",",
                "col1", "&&", "col2", ",",
                "col3", "||", "col4",
                "from", @"""table_name""",
                "where", "something", "=", "'he l)lo  '", ";",
                "#", "#>", "#>>", "&", "&&", "&<", "&>", "->", "->>", "-|-", "<<", "<<=", "<@", "<^", ">>", ">>=", ">^", "@>", "@@", "|", "~", "~=",
                "+=", "-=", "*=", "/=", "%=", "&=", "^-=", "|*=",
                ":", "::", ":=",
            };

            var tokenList = tokens.ToList();
            for (int loop = 0; loop < Math.Min(tokenList.Count, expectedTokens.Count); loop++)
            {
                Assert.AreEqual(expectedTokens[loop], tokenList[loop], $"Expected '{expectedTokens[loop]}', got '{tokenList[loop]}'.");
            }
            Assert.AreEqual(expectedTokens.Count, tokenList.Count);
        }

        [TestClass]
        public class EscapeParameterUnitTests
        {
            [TestMethod]
            public void TestEscapeParameter()
            {
                var testCases = new List<(object? Input, string Expected)>
                {
                    (null, "NULL"),
                    ("simple string", "'simple string'"),
                    ("O'Neill", "'O''Neill'"),
                    ("string with \n newline", "E'string with \\n newline'"),
                    ("string with \r carriage return", "E'string with \\n carriage return'"),
                    ("string with \r\n both", "E'string with \\n both'"),
                    ("string with 'single quotes'", "'string with ''single quotes'''"),
                    ("string with \r carriage return and 'quotes'", "E'string with \\n carriage return and ''quotes'''"),
                    (123, "123"),
                    (-456, "-456"),
                    (123.456, "123.456"),
                    (-123.456, "-123.456"),
                    (0.0, "0"),
                    (1.23e4, "12300"),
                    (123.456m, "123.456"),
                    (-123.456m, "-123.456"),
                    (new DateTime(2021, 12, 31, 23, 59, 59), "'2021-12-31 23:59:59'"),
                    (DateTime.MinValue, "'0001-01-01 00:00:00'"),
                    (DateTime.MaxValue, "'9999-12-31 23:59:59'"),
                    (Guid.Empty, $"UUID('{Guid.Empty.ToString()}')"),
                    (new Guid("e02fa0e4-01ad-090a-c130-0d05a0008ba0"), $"UUID('e02fa0e4-01ad-090a-c130-0d05a0008ba0')"),
                    (true, "True"),
                    (false, "False"),
                    (new object(), $"'System.Object'"),
                    (new { Name = "John Doe" }, $"'{{ Name = John Doe }}'"),
                };

                foreach (var testCase in testCases)
                {
                    var result = SqlQuery.EscapeParameter(testCase.Input);
                    Assert.AreEqual(testCase.Expected, result, $"Failed for input: {testCase.Input}");
                }
            }
        }
    }
}