using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StrikeMapStudio.Editor
{
    // A bounded JSON reader for embedded asset dictionaries and optional field defaults.
    // JsonUtility still maps the typed scene model. No eval, external fetch, or package needed.
    internal sealed class StrikeMapJson
    {
        private readonly string source;
        private int offset;
        private StrikeMapJson(string value) { source = value; }
        public static Dictionary<string, object> Read(string value)
        {
            var reader = new StrikeMapJson(value);
            object result = reader.Value(0);
            reader.White();
            if (reader.offset != value.Length || !(result is Dictionary<string, object>)) throw new InvalidDataException("Expected one JSON object.");
            return (Dictionary<string, object>)result;
        }
        private object Value(int depth)
        {
            if (depth > 64) throw new InvalidDataException("JSON nesting exceeds 64 levels.");
            White();
            if (offset >= source.Length) throw new InvalidDataException("Unexpected end of JSON.");
            char c = source[offset];
            if (c == '"') return String();
            if (c == '{')
            {
                offset++; White(); var result = new Dictionary<string, object>(StringComparer.Ordinal);
                if (Take('}')) return result;
                do
                {
                    White(); if (offset >= source.Length || source[offset] != '"') throw new InvalidDataException("JSON object key must be a string.");
                    string key = String(); White(); Need(':');
                    if (result.ContainsKey(key)) throw new InvalidDataException("Duplicate JSON key: " + key);
                    result.Add(key, Value(depth + 1)); White();
                    if (Take('}')) return result;
                    Need(',');
                } while (true);
            }
            if (c == '[')
            {
                offset++; White(); var result = new List<object>();
                if (Take(']')) return result;
                do { result.Add(Value(depth + 1)); White(); if (Take(']')) return result; Need(','); } while (true);
            }
            if (Literal("true")) return true;
            if (Literal("false")) return false;
            if (Literal("null")) return null;
            int start = offset;
            if (Take('-')) { }
            if (Take('0')) { }
            else { if (offset >= source.Length || source[offset] < '1' || source[offset] > '9') throw new InvalidDataException("Invalid JSON value."); while (offset < source.Length && char.IsDigit(source[offset])) offset++; }
            if (Take('.')) { int digit = offset; while (offset < source.Length && char.IsDigit(source[offset])) offset++; if (digit == offset) throw new InvalidDataException("Invalid fraction."); }
            if (offset < source.Length && (source[offset] == 'e' || source[offset] == 'E'))
            {
                offset++; if (!Take('+')) Take('-'); int digit = offset;
                while (offset < source.Length && char.IsDigit(source[offset])) offset++;
                if (digit == offset) throw new InvalidDataException("Invalid exponent.");
            }
            double number;
            if (!double.TryParse(source.Substring(start, offset - start), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsInfinity(number) || double.IsNaN(number)) throw new InvalidDataException("Non-finite JSON number.");
            return number;
        }
        private string String()
        {
            Need('"'); var result = new StringBuilder();
            while (offset < source.Length)
            {
                char c = source[offset++];
                if (c == '"') return result.ToString();
                if (c < 32) throw new InvalidDataException("Control character in JSON string.");
                if (c != '\\') { result.Append(c); continue; }
                if (offset >= source.Length) break;
                char escape = source[offset++];
                switch (escape)
                {
                    case '"': result.Append('"'); break; case '\\': result.Append('\\'); break; case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break; case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break;
                    case 'u':
                        if (offset + 4 > source.Length) throw new InvalidDataException("Truncated Unicode escape.");
                        ushort value;
                        if (!ushort.TryParse(source.Substring(offset, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) throw new InvalidDataException("Invalid Unicode escape.");
                        result.Append((char)value); offset += 4; break;
                    default: throw new InvalidDataException("Invalid JSON string escape.");
                }
            }
            throw new InvalidDataException("Unterminated JSON string.");
        }
        private bool Literal(string value)
        {
            if (offset + value.Length > source.Length || string.CompareOrdinal(source, offset, value, 0, value.Length) != 0) return false;
            offset += value.Length; return true;
        }
        private void White() { while (offset < source.Length && (source[offset] == ' ' || source[offset] == '\r' || source[offset] == '\n' || source[offset] == '\t')) offset++; }
        private bool Take(char c) { if (offset >= source.Length || source[offset] != c) return false; offset++; return true; }
        private void Need(char c) { if (!Take(c)) throw new InvalidDataException("Expected '" + c + "' in JSON."); }
    }
}
