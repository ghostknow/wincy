using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wincy
{
    internal enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// Minimal JSON tree. Hand-rolled on purpose: the app targets the .NET Framework
    /// that ships with Windows and pulls in no external packages, and this keeps the
    /// on-disk history/settings human readable with stable key ordering.
    /// </summary>
    internal sealed class JsonValue
    {
        public JsonKind Kind;
        public string Text;
        public double Number;
        public bool Bool;
        public List<JsonValue> Items;
        public List<KeyValuePair<string, JsonValue>> Members;

        public static JsonValue NewObject()
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.Object;
            v.Members = new List<KeyValuePair<string, JsonValue>>();
            return v;
        }

        public static JsonValue NewArray()
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.Array;
            v.Items = new List<JsonValue>();
            return v;
        }

        public static JsonValue FromString(string s)
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.String;
            v.Text = s;
            return v;
        }

        public static JsonValue FromNumber(double d)
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.Number;
            v.Number = d;
            return v;
        }

        public static JsonValue FromBool(bool b)
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.Bool;
            v.Bool = b;
            return v;
        }

        public static JsonValue Null()
        {
            JsonValue v = new JsonValue();
            v.Kind = JsonKind.Null;
            return v;
        }

        public JsonValue Set(string name, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("Not a JSON object");
            if (Members == null) Members = new List<KeyValuePair<string, JsonValue>>();
            for (int i = 0; i < Members.Count; i++)
            {
                if (string.Equals(Members[i].Key, name, StringComparison.Ordinal))
                {
                    Members[i] = new KeyValuePair<string, JsonValue>(name, value);
                    return this;
                }
            }
            Members.Add(new KeyValuePair<string, JsonValue>(name, value));
            return this;
        }

        public JsonValue Set(string name, string value)
        {
            return Set(name, value == null ? Null() : FromString(value));
        }

        public JsonValue Set(string name, int value)
        {
            return Set(name, FromNumber(value));
        }

        public JsonValue Set(string name, bool value)
        {
            return Set(name, FromBool(value));
        }

        public JsonValue Add(JsonValue value)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException("Not a JSON array");
            Items.Add(value);
            return this;
        }

        public JsonValue Get(string name)
        {
            if (Kind != JsonKind.Object || Members == null) return null;
            for (int i = 0; i < Members.Count; i++)
            {
                if (string.Equals(Members[i].Key, name, StringComparison.Ordinal)) return Members[i].Value;
            }
            return null;
        }

        public string GetString(string name, string fallback)
        {
            JsonValue v = Get(name);
            if (v == null || v.Kind != JsonKind.String || v.Text == null) return fallback;
            return v.Text;
        }

        public bool GetBool(string name, bool fallback)
        {
            JsonValue v = Get(name);
            if (v == null) return fallback;
            if (v.Kind == JsonKind.Bool) return v.Bool;
            if (v.Kind == JsonKind.Number) return Math.Abs(v.Number) > double.Epsilon;
            return fallback;
        }

        public int GetInt(string name, int fallback)
        {
            JsonValue v = Get(name);
            if (v == null) return fallback;
            if (v.Kind == JsonKind.Number) return (int)Math.Round(v.Number);
            if (v.Kind == JsonKind.String)
            {
                int parsed;
                if (int.TryParse(v.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;
            }
            return fallback;
        }

        public double GetDouble(string name, double fallback)
        {
            JsonValue v = Get(name);
            if (v == null) return fallback;
            if (v.Kind == JsonKind.Number) return v.Number;
            return fallback;
        }

        public JsonValue GetArray(string name)
        {
            JsonValue v = Get(name);
            if (v == null || v.Kind != JsonKind.Array) return null;
            return v;
        }

        public List<string> GetStringList(string name)
        {
            List<string> list = new List<string>();
            JsonValue arr = GetArray(name);
            if (arr == null) return list;
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonValue item = arr.Items[i];
                if (item != null && item.Kind == JsonKind.String && item.Text != null) list.Add(item.Text);
            }
            return list;
        }
    }

    internal static class Json
    {
        public static string Write(JsonValue value, bool pretty)
        {
            StringBuilder sb = new StringBuilder(1024);
            WriteValue(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Indent(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            for (int i = 0; i < depth; i++) sb.Append("  ");
        }

        private static void WriteValue(StringBuilder sb, JsonValue value, bool pretty, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            switch (value.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(value.Bool ? "true" : "false");
                    break;
                case JsonKind.Number:
                    WriteNumber(sb, value.Number);
                    break;
                case JsonKind.String:
                    WriteString(sb, value.Text);
                    break;
                case JsonKind.Array:
                    if (value.Items == null || value.Items.Count == 0)
                    {
                        sb.Append("[]");
                        break;
                    }
                    sb.Append('[');
                    for (int i = 0; i < value.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Indent(sb, pretty, depth + 1);
                        WriteValue(sb, value.Items[i], pretty, depth + 1);
                    }
                    Indent(sb, pretty, depth);
                    sb.Append(']');
                    break;
                case JsonKind.Object:
                    if (value.Members == null || value.Members.Count == 0)
                    {
                        sb.Append("{}");
                        break;
                    }
                    sb.Append('{');
                    for (int i = 0; i < value.Members.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Indent(sb, pretty, depth + 1);
                        WriteString(sb, value.Members[i].Key);
                        sb.Append(':');
                        if (pretty) sb.Append(' ');
                        WriteValue(sb, value.Members[i].Value, pretty, depth + 1);
                    }
                    Indent(sb, pretty, depth);
                    sb.Append('}');
                    break;
            }
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                sb.Append('0');
                return;
            }
            if (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 9e15)
            {
                sb.Append(((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture));
                return;
            }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '\b':
                            sb.Append("\\b");
                            break;
                        case '\f':
                            sb.Append("\\f");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (c < ' ' || c == '\u2028' || c == '\u2029')
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            Parser p = new Parser(text);
            return p.ParseDocument();
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text)
            {
                _text = text;
                _index = 0;
            }

            public JsonValue ParseDocument()
            {
                SkipWhitespace();
                if (_index >= _text.Length) throw Error("empty document");
                JsonValue value = ParseValue();
                SkipWhitespace();
                if (_index < _text.Length) throw Error("unexpected trailing content");
                return value;
            }

            private Exception Error(string message)
            {
                int line = 1;
                int column = 1;
                for (int i = 0; i < _index && i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                return new FormatException(string.Format(CultureInfo.InvariantCulture,
                    "JSON error at line {0}, column {1}: {2}", line, column, message));
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\uFEFF') _index++;
                    else break;
                }
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();
                if (_index >= _text.Length) throw Error("unexpected end of document");
                char c = _text[_index];
                switch (c)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return JsonValue.FromString(ParseString());
                    case 't':
                        Expect("true");
                        return JsonValue.FromBool(true);
                    case 'f':
                        Expect("false");
                        return JsonValue.FromBool(false);
                    case 'n':
                        Expect("null");
                        return JsonValue.Null();
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return JsonValue.FromNumber(ParseNumber());
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private void Expect(string literal)
            {
                if (_index + literal.Length > _text.Length ||
                    string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                {
                    throw Error("expected '" + literal + "'");
                }
                _index += literal.Length;
            }

            private JsonValue ParseObject()
            {
                JsonValue obj = JsonValue.NewObject();
                _index++; // {
                SkipWhitespace();
                if (_index < _text.Length && _text[_index] == '}')
                {
                    _index++;
                    return obj;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (_index >= _text.Length || _text[_index] != '"') throw Error("expected object key");
                    string key = ParseString();
                    SkipWhitespace();
                    if (_index >= _text.Length || _text[_index] != ':') throw Error("expected ':'");
                    _index++;
                    JsonValue value = ParseValue();
                    obj.Set(key, value);
                    SkipWhitespace();
                    if (_index >= _text.Length) throw Error("unterminated object");
                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }
                    if (_text[_index] == '}')
                    {
                        _index++;
                        return obj;
                    }
                    throw Error("expected ',' or '}'");
                }
            }

            private JsonValue ParseArray()
            {
                JsonValue arr = JsonValue.NewArray();
                _index++; // [
                SkipWhitespace();
                if (_index < _text.Length && _text[_index] == ']')
                {
                    _index++;
                    return arr;
                }
                while (true)
                {
                    arr.Add(ParseValue());
                    SkipWhitespace();
                    if (_index >= _text.Length) throw Error("unterminated array");
                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }
                    if (_text[_index] == ']')
                    {
                        _index++;
                        return arr;
                    }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                _index++; // opening quote
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    if (_index >= _text.Length) throw Error("unterminated string");
                    char c = _text[_index++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_index >= _text.Length) throw Error("unterminated escape");
                        char e = _text[_index++];
                        switch (e)
                        {
                            case '"':
                                sb.Append('"');
                                break;
                            case '\\':
                                sb.Append('\\');
                                break;
                            case '/':
                                sb.Append('/');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'u':
                                if (_index + 4 > _text.Length) throw Error("bad unicode escape");
                                int code;
                                if (!int.TryParse(_text.Substring(_index, 4), NumberStyles.HexNumber,
                                        CultureInfo.InvariantCulture, out code))
                                {
                                    throw Error("bad unicode escape");
                                }
                                sb.Append((char)code);
                                _index += 4;
                                break;
                            default:
                                throw Error("bad escape '\\" + e + "'");
                        }
                        continue;
                    }
                    if (c < ' ')
                    {
                        // Lenient: keep raw control characters out of the way.
                        sb.Append(' ');
                        continue;
                    }
                    sb.Append(c);
                }
            }

            private double ParseNumber()
            {
                int start = _index;
                if (_index < _text.Length && _text[_index] == '-') _index++;
                while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                if (_index < _text.Length && _text[_index] == '.')
                {
                    _index++;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                }
                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    _index++;
                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                }
                string raw = _text.Substring(start, _index - start);
                double value;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw Error("bad number '" + raw + "'");
                }
                return value;
            }
        }
    }
}
