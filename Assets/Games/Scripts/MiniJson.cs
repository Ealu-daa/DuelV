using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

/// <summary>
/// 依存ライブラリなしの軽量JSONパーサー/シリアライザ。
/// Deserialize結果: Dictionary&lt;string,object&gt; / List&lt;object&gt; / string / double / bool / null
/// </summary>
public static class MiniJson
{
    public static object Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int i = 0;
        return ParseValue(json, ref i);
    }

    public static string Serialize(object obj)
    {
        var sb = new StringBuilder();
        WriteValue(obj, sb);
        return sb.ToString();
    }

    // ---------- Parse ----------

    static object ParseValue(string s, ref int i)
    {
        SkipWhitespace(s, ref i);
        char c = s[i];
        switch (c)
        {
            case '{': return ParseObject(s, ref i);
            case '[': return ParseArray(s, ref i);
            case '"': return ParseString(s, ref i);
            case 't':
                i += 4; return true;
            case 'f':
                i += 5; return false;
            case 'n':
                i += 4; return null;
            default:
                return ParseNumber(s, ref i);
        }
    }

    static Dictionary<string, object> ParseObject(string s, ref int i)
    {
        var dict = new Dictionary<string, object>();
        i++; // {
        SkipWhitespace(s, ref i);
        if (s[i] == '}') { i++; return dict; }
        while (true)
        {
            SkipWhitespace(s, ref i);
            string key = ParseString(s, ref i);
            SkipWhitespace(s, ref i);
            i++; // :
            object val = ParseValue(s, ref i);
            dict[key] = val;
            SkipWhitespace(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == '}') { i++; break; }
        }
        return dict;
    }

    static List<object> ParseArray(string s, ref int i)
    {
        var list = new List<object>();
        i++; // [
        SkipWhitespace(s, ref i);
        if (s[i] == ']') { i++; return list; }
        while (true)
        {
            object val = ParseValue(s, ref i);
            list.Add(val);
            SkipWhitespace(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == ']') { i++; break; }
        }
        return list;
    }

    static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // "
        while (s[i] != '"')
        {
            if (s[i] == '\\')
            {
                i++;
                char esc = s[i];
                switch (esc)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'u':
                        string hex = s.Substring(i + 1, 4);
                        sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(esc); break;
                }
            }
            else
            {
                sb.Append(s[i]);
            }
            i++;
        }
        i++; // closing "
        return sb.ToString();
    }

    static object ParseNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            i++;
        string numStr = s.Substring(start, i - start);
        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }

    static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    // ---------- Write ----------

    static void WriteValue(object obj, StringBuilder sb)
    {
        if (obj == null) { sb.Append("null"); return; }

        if (obj is string str) { WriteString(str, sb); return; }
        if (obj is bool b) { sb.Append(b ? "true" : "false"); return; }
        if (obj is int || obj is long) { sb.Append(obj.ToString()); return; }
        if (obj is float || obj is double)
        {
            sb.Append(System.Convert.ToDouble(obj).ToString(CultureInfo.InvariantCulture));
            return;
        }
        if (obj is IDictionary<string, object> dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(kv.Key, sb);
                sb.Append(':');
                WriteValue(kv.Value, sb);
            }
            sb.Append('}');
            return;
        }
        if (obj is IEnumerable list)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteValue(item, sb);
            }
            sb.Append(']');
            return;
        }
        // フォールバック
        WriteString(obj.ToString(), sb);
    }

    static void WriteString(string s, StringBuilder sb)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }
}
