using System.Text.RegularExpressions;

namespace Bi.Application.Services;

/// <summary>
/// SQL字段提取器 - 从SQL语句中提取使用的字段名
/// 用于验证AI生成的SQL中的字段是否存在于表结构中
///
/// 设计原则：
/// 1. 只提取WHERE/GROUP BY/ORDER BY/HAVING中的字段（这些是真正需要验证的）
/// 2. 忽略SELECT中的别名（AS后面的名称）
/// 3. 忽略FROM/JOIN后面的表名
/// 4. 忽略中文标识符（通常是别名）
/// </summary>
public static class SqlFieldExtractor
{
    // SQL关键字列表（用于过滤）
    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "BETWEEN", "LIKE", "IS", "NULL",
        "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET", "ASC", "DESC", "DISTINCT",
        "COUNT", "SUM", "AVG", "MAX", "MIN", "AS", "ON", "JOIN", "LEFT", "RIGHT", "INNER",
        "OUTER", "CROSS", "CASE", "WHEN", "THEN", "ELSE", "END", "UNION", "ALL", "EXISTS",
        "TRUE", "FALSE", "CAST", "CONVERT", "COALESCE", "IFNULL", "NULLIF", "IF",
        "YEAR", "MONTH", "DAY", "DATE", "TIME", "DATETIME", "NOW", "CURDATE", "CURRENT_DATE",
        "DATE_FORMAT", "STR_TO_DATE", "DATEDIFF", "TIMESTAMPDIFF", "DATE_ADD", "DATE_SUB",
        "CONCAT", "SUBSTRING", "SUBSTR", "LENGTH", "TRIM", "UPPER", "LOWER", "REPLACE",
        "ROUND", "FLOOR", "CEIL", "ABS", "MOD", "POWER", "SQRT", "VALUE", "OVER", "PARTITION",
        "ROW_NUMBER", "RANK", "DENSE_RANK", "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE",
        "EXTRACT", "TO_CHAR", "TO_DATE", "INTERVAL", "USING", "NATURAL", "FULL"
    };

    // 中文字符正则（用于过滤中文别名）
    private static readonly Regex ChinesePattern = new(@"[\u4e00-\u9fa5]", RegexOptions.Compiled);

    // 数字正则
    private static readonly Regex NumberPattern = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

    /// <summary>
    /// 从SQL语句中提取需要验证的字段名
    /// 只提取WHERE/GROUP BY/ORDER BY/HAVING子句中的字段
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="tableNames">已知的表名列表（用于过滤）</param>
    /// <returns>字段名列表（去重）</returns>
    public static HashSet<string> ExtractFields(string sql, IEnumerable<string>? tableNames = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownTables = tableNames != null
            ? new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 移除SQL注释
        sql = RemoveComments(sql);

        // 移除字符串字面量
        sql = RemoveStringLiterals(sql);

        // 在SQL末尾添加一个空格和标记，确保正则能匹配到末尾的子句
        var normalizedSql = sql.Trim() + " __END__";

        // 提取WHERE子句中的字段（使用贪婪匹配到下一个关键字或结束标记）
        ExtractFieldsFromClause(normalizedSql, @"\bWHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|UNION|__END__))", fields, knownTables);

        // 提取GROUP BY子句中的字段
        ExtractFieldsFromClause(normalizedSql, @"\bGROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|UNION|__END__))", fields, knownTables);

        // 提取ORDER BY子句中的字段
        ExtractFieldsFromClause(normalizedSql, @"\bORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|OFFSET|UNION|__END__))", fields, knownTables);

        // 提取HAVING子句中的字段
        ExtractFieldsFromClause(normalizedSql, @"\bHAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|UNION|__END__))", fields, knownTables);

        // 提取JOIN ON子句中的字段
        ExtractFieldsFromClause(normalizedSql, @"\bON\s+(.+?)(?=\s+(?:WHERE|LEFT\s+JOIN|RIGHT\s+JOIN|INNER\s+JOIN|OUTER\s+JOIN|JOIN|GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|__END__))", fields, knownTables);

        // 额外：从SELECT子句中提取字段（确保能捕获到使用的字段，但排除别名）
        ExtractFieldsFromSelectClause(normalizedSql, fields, knownTables);

        return fields;
    }

    /// <summary>
    /// 从SELECT子句中提取字段名（只提取真正的字段，排除AS后的别名）
    /// 支持嵌套子查询：遍历所有SELECT...FROM模式
    /// </summary>
    private static void ExtractFieldsFromSelectClause(string sql, HashSet<string> fields, HashSet<string> knownTables)
    {
        // 使用全局匹配找到所有的 SELECT...FROM 模式（支持嵌套子查询）
        // 改用贪婪匹配并配合括号平衡来处理嵌套
        var allSelectMatches = Regex.Matches(sql, @"\bSELECT\s+(?:DISTINCT\s+)?(.+?)\s+FROM\b", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match selectMatch in allSelectMatches)
        {
            var selectContent = selectMatch.Groups[1].Value;
            ExtractFieldsFromSelectContent(selectContent, fields, knownTables);
        }
    }

    /// <summary>
    /// 从SELECT内容中提取字段（辅助方法）
    /// </summary>
    private static void ExtractFieldsFromSelectContent(string selectContent, HashSet<string> fields, HashSet<string> knownTables)
    {
        // 移除AS别名部分（AS 后面的标识符不是字段）
        // 先移除 AS xxx 格式
        selectContent = Regex.Replace(selectContent, @"\bAS\s+(`[^`]+`|""[^""]+""|[a-zA-Z_\u4e00-\u9fa5][a-zA-Z0-9_\u4e00-\u9fa5]*)", " ", RegexOptions.IgnoreCase);

        // 提取标识符（支持table.column格式）
        var identifierPattern = @"(?:`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*))(?:\.(?:`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*)))?";
        var matches = Regex.Matches(selectContent, identifierPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            string? fieldPart = null;

            // 检查是否是 table.column 格式
            if (!string.IsNullOrEmpty(match.Groups[3].Value) || !string.IsNullOrEmpty(match.Groups[4].Value))
            {
                // 有点号分隔，取第二部分作为字段名
                fieldPart = !string.IsNullOrEmpty(match.Groups[3].Value)
                    ? match.Groups[3].Value
                    : match.Groups[4].Value;
            }
            else
            {
                // 单独的标识符
                fieldPart = !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;
            }

            if (string.IsNullOrEmpty(fieldPart))
                continue;

            // 过滤SQL关键字
            if (SqlKeywords.Contains(fieldPart))
                continue;

            // 过滤数字
            if (NumberPattern.IsMatch(fieldPart))
                continue;

            // 过滤中文（通常是别名）
            if (ChinesePattern.IsMatch(fieldPart))
                continue;

            // 过滤已知表名
            if (knownTables.Contains(fieldPart))
                continue;

            // 过滤单字符（通常是表别名如 t, a, b）
            if (fieldPart.Length == 1)
                continue;

            // 过滤常见的表别名模式（t1, t2, a1, b1等）
            if (Regex.IsMatch(fieldPart, @"^[a-z]\d+$", RegexOptions.IgnoreCase))
                continue;

            fields.Add(fieldPart);
        }
    }

    /// <summary>
    /// 从指定子句中提取字段名
    /// </summary>
    private static void ExtractFieldsFromClause(string sql, string clausePattern, HashSet<string> fields, HashSet<string> knownTables)
    {
        var clauseMatch = Regex.Match(sql, clausePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!clauseMatch.Success)
            return;

        var clauseContent = clauseMatch.Groups[1].Value;

        // 提取标识符（支持table.column格式）
        // 只匹配英文字母开头的标识符（过滤中文）
        var identifierPattern = @"(?:`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*))(?:\.(?:`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*)))?";
        var matches = Regex.Matches(clauseContent, identifierPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            string? fieldPart = null;

            // 检查是否是 table.column 格式
            if (!string.IsNullOrEmpty(match.Groups[3].Value) || !string.IsNullOrEmpty(match.Groups[4].Value))
            {
                // 有点号分隔，取第二部分作为字段名
                fieldPart = !string.IsNullOrEmpty(match.Groups[3].Value)
                    ? match.Groups[3].Value
                    : match.Groups[4].Value;
            }
            else
            {
                // 单独的标识符
                fieldPart = !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;
            }

            if (string.IsNullOrEmpty(fieldPart))
                continue;

            // 过滤SQL关键字
            if (SqlKeywords.Contains(fieldPart))
                continue;

            // 过滤数字
            if (NumberPattern.IsMatch(fieldPart))
                continue;

            // 过滤中文（通常是别名）
            if (ChinesePattern.IsMatch(fieldPart))
                continue;

            // 过滤已知表名
            if (knownTables.Contains(fieldPart))
                continue;

            // 过滤单字符（通常是表别名如 t, a, b）
            if (fieldPart.Length == 1)
                continue;

            // 过滤常见的表别名模式（t1, t2, a1, b1等）
            if (Regex.IsMatch(fieldPart, @"^[a-z]\d+$", RegexOptions.IgnoreCase))
                continue;

            fields.Add(fieldPart);
        }
    }

    /// <summary>
    /// 移除SQL注释
    /// </summary>
    private static string RemoveComments(string sql)
    {
        // 移除单行注释 -- ...
        sql = Regex.Replace(sql, @"--[^\r\n]*", " ");
        // 移除多行注释 /* ... */
        sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", " ");
        return sql;
    }

    /// <summary>
    /// 移除字符串字面量
    /// </summary>
    private static string RemoveStringLiterals(string sql)
    {
        // 移除单引号字符串
        sql = Regex.Replace(sql, @"'(?:[^'\\]|\\.)*'", " ");
        // 移除双引号字符串
        sql = Regex.Replace(sql, @"""(?:[^""\\]|\\.)*""", " ");
        return sql;
    }

    /// <summary>
    /// 验证SQL中的字段是否都存在于给定的字段列表中
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="validFields">有效字段列表</param>
    /// <param name="tableNames">已知的表名列表（用于过滤）</param>
    /// <returns>不存在的字段列表</returns>
    public static List<string> ValidateFields(string sql, IEnumerable<string> validFields, IEnumerable<string>? tableNames = null)
    {
        var usedFields = ExtractFields(sql, tableNames);
        var validFieldSet = new HashSet<string>(validFields, StringComparer.OrdinalIgnoreCase);

        var invalidFields = usedFields
            .Where(f => !validFieldSet.Contains(f))
            .ToList();

        // ★ 额外检查：子查询别名作用域
        // 如果SQL有 FROM (SELECT ... AS alias ...) t 结构，
        // 外层引用的字段必须是子查询输出的别名，不能是原表的列名
        var subqueryInvalid = ValidateSubqueryAliasScope(sql, validFieldSet);
        foreach (var f in subqueryInvalid)
        {
            if (!invalidFields.Contains(f, StringComparer.OrdinalIgnoreCase))
                invalidFields.Add(f);
        }

        return invalidFields;
    }

    /// <summary>
    /// 验证子查询别名作用域
    /// 检查外层查询引用的字段是否在子查询的输出别名中
    /// 典型场景：SELECT AVG(currentage) FROM (SELECT currentage AS 当前年龄 FROM ...) t
    /// 这里 currentage 虽然是有效表字段，但在子查询上下文中应该用别名"当前年龄"
    /// </summary>
    private static List<string> ValidateSubqueryAliasScope(string sql, HashSet<string> validTableFields)
    {
        var invalidFields = new List<string>();
        if (string.IsNullOrWhiteSpace(sql)) return invalidFields;

        var cleanSql = RemoveComments(sql);
        cleanSql = RemoveStringLiterals(cleanSql);

        // 匹配 FROM (SELECT ...) alias 模式（外层包裹子查询）
        // 只处理最外层的 FROM (SELECT ...) 模式
        var subqueryPattern = @"FROM\s*\(\s*(SELECT\b.+?)\)\s*([a-zA-Z_][a-zA-Z0-9_]*)";
        var subqueryMatch = Regex.Match(cleanSql, subqueryPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!subqueryMatch.Success) return invalidFields;

        // 提取子查询的SELECT输出别名
        var innerSql = subqueryMatch.Groups[1].Value;
        var subqueryAliases = ExtractSelectAliases(innerSql);
        if (subqueryAliases.Count == 0) return invalidFields;

        // 获取外层查询引用的字段
        // 外层 = 整个SQL中子查询之前的SELECT部分 + 子查询之后的WHERE/GROUP BY等
        var subqueryStart = subqueryMatch.Index;
        var subqueryEnd = subqueryMatch.Index + subqueryMatch.Length;
        var outerBefore = cleanSql[..subqueryStart]; // SELECT ... FROM 之前
        var outerAfter = subqueryEnd < cleanSql.Length ? cleanSql[subqueryEnd..] : "";

        // 从外层部分提取使用的字段
        var outerFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractFieldsFromSelectContent(outerBefore, outerFields, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        // 也从 WHERE/GROUP BY/ORDER BY 中提取
        var outerSuffix = outerAfter + " __END__";
        ExtractFieldsFromClause(outerSuffix, @"\bWHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|__END__))", outerFields, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        ExtractFieldsFromClause(outerSuffix, @"\bGROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|__END__))", outerFields, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        ExtractFieldsFromClause(outerSuffix, @"\bORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|OFFSET|__END__))", outerFields, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        // 检查外层字段：如果字段存在于原表但不在子查询别名中，说明作用域错误
        foreach (var field in outerFields)
        {
            // 跳过 "value" 等常见的聚合别名
            if (field.Equals("value", StringComparison.OrdinalIgnoreCase)) continue;
            if (field.Equals("cnt", StringComparison.OrdinalIgnoreCase)) continue;

            // 如果字段在子查询别名中 → 正确
            if (subqueryAliases.Contains(field)) continue;

            // 如果字段在原表中但不在子查询别名中 → 作用域错误！
            if (validTableFields.Contains(field) && !subqueryAliases.Contains(field))
            {
                invalidFields.Add(field);
            }
        }

        return invalidFields;
    }

    /// <summary>
    /// 从SELECT子句中提取输出的别名列表
    /// 如：SELECT a AS 别名1, b AS 别名2 → ["别名1", "别名2"]
    /// 如果没有AS别名，则使用原字段名
    /// </summary>
    private static HashSet<string> ExtractSelectAliases(string selectSql)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(selectSql)) return aliases;

        // 提取 SELECT 和 FROM 之间的内容
        var selectMatch = Regex.Match(selectSql, @"\bSELECT\s+(?:DISTINCT\s+)?(.+?)\s+FROM\b", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!selectMatch.Success) return aliases;

        var selectContent = selectMatch.Groups[1].Value;

        // 按逗号分割各个字段表达式（注意函数内的逗号不算）
        var fieldExprs = SplitByComma(selectContent);

        foreach (var expr in fieldExprs)
        {
            var trimmed = expr.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 检查是否有 AS 别名
            var asMatch = Regex.Match(trimmed, @"\bAS\s+(`([^`]+)`|""([^""]+)""|([a-zA-Z_\u4e00-\u9fa5][a-zA-Z0-9_\u4e00-\u9fa5]*))\s*$", RegexOptions.IgnoreCase);
            if (asMatch.Success)
            {
                // 使用别名
                var alias = !string.IsNullOrEmpty(asMatch.Groups[2].Value) ? asMatch.Groups[2].Value
                    : !string.IsNullOrEmpty(asMatch.Groups[3].Value) ? asMatch.Groups[3].Value
                    : asMatch.Groups[4].Value;
                if (!string.IsNullOrEmpty(alias))
                    aliases.Add(alias);
            }
            else
            {
                // 没有AS别名，取最后一个标识符作为列名
                var identMatch = Regex.Match(trimmed, @"([a-zA-Z_][a-zA-Z0-9_]*)\s*$");
                if (identMatch.Success)
                    aliases.Add(identMatch.Groups[1].Value);
            }
        }

        return aliases;
    }

    /// <summary>
    /// 按顶层逗号分割（忽略括号内的逗号）
    /// </summary>
    private static List<string> SplitByComma(string text)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(' || text[i] == '[') depth++;
            else if (text[i] == ')' || text[i] == ']') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }
        if (start < text.Length)
            parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    /// 从SQL中提取使用的表名（FROM和JOIN后面的表）
    /// 用于检测AI是否使用了未选中的表
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <returns>表名列表（不含别名）</returns>
    public static HashSet<string> ExtractTableNames(string sql)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sql))
            return tables;

        // 移除注释和字符串字面量
        sql = RemoveComments(sql);
        sql = RemoveStringLiterals(sql);

        // 匹配 FROM table [alias] 和 JOIN table [alias] 模式
        // 表名可能是 schema.table 或 `table` 或普通标识符
        var tablePattern = @"(?:FROM|JOIN)\s+(?:`([^`]+)`|([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)?))(?:\s+(?:AS\s+)?([a-zA-Z_][a-zA-Z0-9_]*))?";
        var matches = Regex.Matches(sql, tablePattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            // 取表名（反引号内或普通标识符）
            var tableName = !string.IsNullOrEmpty(match.Groups[1].Value)
                ? match.Groups[1].Value
                : match.Groups[2].Value;

            if (!string.IsNullOrEmpty(tableName))
            {
                // 如果是 schema.table 格式，只取表名
                if (tableName.Contains('.'))
                {
                    tableName = tableName.Split('.').Last();
                }
                tables.Add(tableName);
            }
        }

        return tables;
    }

    /// <summary>
    /// 验证SQL中使用的表是否都在允许的表列表中
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="allowedTables">允许使用的表名列表</param>
    /// <returns>未允许使用的表名列表</returns>
    public static List<string> ValidateTables(string sql, IEnumerable<string> allowedTables)
    {
        var usedTables = ExtractTableNames(sql);
        var allowedSet = new HashSet<string>(allowedTables, StringComparer.OrdinalIgnoreCase);

        var invalidTables = usedTables
            .Where(t => !allowedSet.Contains(t))
            .ToList();

        return invalidTables;
    }
}

