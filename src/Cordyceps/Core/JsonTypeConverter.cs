using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cordyceps.Core
{
    /// <summary>
    /// Converts between JSON Schema types and CLR types, with cross-type coercion
    /// for MCP clients that may send string-encoded numbers or vice versa.
    ///
    /// <para>Uses Newtonsoft rather than System.Text.Json: this assembly targets net48 and loads
    /// inside the Rhino process, which already has System.Memory 4.0.1.1 / Unsafe 4.0.6.0 loaded
    /// for Roslyn. System.Text.Json needs strictly newer versions of both, and a .gha cannot
    /// supply binding redirects to the host process, so it would fail at load. Newtonsoft is a
    /// single self-contained assembly and is already a dependency.</para>
    /// </summary>
    public static class JsonTypeConverter
    {
        /// <summary>
        /// Maps a CLR type to its JSON Schema type string.
        /// </summary>
        public static string GetJsonType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long)) return "integer";
            if (type == typeof(double) || type == typeof(float)) return "number";
            if (type == typeof(bool)) return "boolean";
            return "string";
        }

        private static bool IsNumber(JToken t) =>
            t.Type == JTokenType.Integer || t.Type == JTokenType.Float;

        private static bool IsString(JToken t) => t.Type == JTokenType.String;

        /// <summary>Raw JSON text of a token, matching JsonElement.GetRawText() semantics.</summary>
        private static string RawText(JToken t) => t.ToString(Formatting.None);

        /// <summary>
        /// Converts a JSON token to the specified CLR type, with cross-type coercion.
        /// Handles cases where MCP clients send string-encoded numbers ("300" instead of 300).
        /// </summary>
        public static object ConvertJsonValue(JToken element, Type targetType)
        {
            if (element == null)
                throw new InvalidOperationException($"Cannot convert missing value to {targetType.Name}");

            if (targetType == typeof(string))
                return IsString(element) ? element.Value<string>() : RawText(element);

            if (targetType == typeof(int))
            {
                if (IsNumber(element))
                {
                    // Accept whole-valued doubles (clients often send 300.0 for 300);
                    // reject true fractions with a clear error instead of a FormatException.
                    var d = element.Value<double>();
                    if (!double.IsNaN(d) && !double.IsInfinity(d)
                        && d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue)
                        return (int)d;
                    throw new InvalidOperationException(
                        $"Cannot convert number '{RawText(element)}' to int: not a whole number in the int range");
                }
                if (IsString(element) &&
                    int.TryParse(element.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                    return intVal;
                throw new InvalidOperationException(
                    $"Cannot convert {element.Type} '{RawText(element)}' to int");
            }

            if (targetType == typeof(long))
            {
                if (IsNumber(element))
                {
                    // Upper bound is exclusive: long.MaxValue rounds UP to 2^63 as a double,
                    // so d <= long.MaxValue would admit 2^63 and overflow on the cast.
                    if (element.Type == JTokenType.Integer)
                        return element.Value<long>();
                    var d = element.Value<double>();
                    if (!double.IsNaN(d) && !double.IsInfinity(d)
                        && d == Math.Floor(d) && d >= long.MinValue && d < 9223372036854775808.0)
                        return (long)d;
                    throw new InvalidOperationException(
                        $"Cannot convert number '{RawText(element)}' to long: not a whole number in the long range");
                }
                if (IsString(element) &&
                    long.TryParse(element.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
                    return longVal;
                throw new InvalidOperationException(
                    $"Cannot convert {element.Type} '{RawText(element)}' to long");
            }

            if (targetType == typeof(double))
            {
                if (IsNumber(element))
                    return element.Value<double>();
                if (IsString(element) &&
                    double.TryParse(element.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var dblVal))
                    return dblVal;
                throw new InvalidOperationException(
                    $"Cannot convert {element.Type} '{RawText(element)}' to double");
            }

            if (targetType == typeof(float))
            {
                if (IsNumber(element))
                    return (float)element.Value<double>();
                if (IsString(element) &&
                    float.TryParse(element.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var fltVal))
                    return fltVal;
                throw new InvalidOperationException(
                    $"Cannot convert {element.Type} '{RawText(element)}' to float");
            }

            if (targetType == typeof(bool))
            {
                if (element.Type == JTokenType.Boolean)
                    return element.Value<bool>();
                if (IsString(element) && bool.TryParse(element.Value<string>(), out var boolVal))
                    return boolVal;
                // Number -> bool: nonzero is true, zero is false, for ANY numeric value,
                // matching C-style truthiness. Never fails on large or fractional numbers.
                if (IsNumber(element))
                    return element.Value<double>() != 0;
                throw new InvalidOperationException(
                    $"Cannot convert {element.Type} '{RawText(element)}' to bool");
            }

            return IsString(element) ? element.Value<string>() : RawText(element);
        }
    }
}
