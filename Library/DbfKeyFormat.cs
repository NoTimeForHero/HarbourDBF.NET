using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarbourDBF.NET.Beta;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HarbourDBF.NET
{
    /// <summary>Ограниченное описание преобразования ключа Harbour.</summary>
    public sealed class DbfKeyFormat
    {
        private readonly object[][] steps;
        private DbfKeyFormat(Type inputType, int? width, object[][] steps) { InputType = inputType; Width = width; this.steps = steps; }
        internal Type InputType { get; }
        internal int? Width { get; }

        public static DbfKeyFormat Str(int width, int decimals = 0)
        {
            CheckWidth(width);
            if (decimals != 0) throw new ArgumentOutOfRangeException(nameof(decimals), "Only integer STR keys are supported.");
            return new DbfKeyFormat(typeof(int), width, new[] { new object[] { "STR", width } });
        }

        public static DbfKeyFormat PadRight(int width)
        {
            CheckWidth(width);
            return new DbfKeyFormat(typeof(string), width, new[] { new object[] { "PADR", width } });
        }

        public static DbfKeyFormat AllTrim() => new DbfKeyFormat(typeof(string), null, new[] { new object[] { "ALLTRIM" } });

        public DbfKeyFormat ThenPadRight(int width)
        {
            CheckWidth(width);
            return Append(new object[] { "PADR", width }, width);
        }

        public DbfKeyFormat Upper() => Append(new object[] { "UPPER" }, Width);

        private DbfKeyFormat Append(object[] step, int? width) => new DbfKeyFormat(InputType, width, steps.Concat(new[] { step }).ToArray());

        private static void CheckWidth(int width)
        {
            if (width < 1 || width > 254) throw new ArgumentOutOfRangeException(nameof(width));
        }

        internal string Expression(string input)
        {
            foreach (var step in steps)
            {
                switch ((string)step[0])
                {
                    case "STR": input = "STR(" + input + "," + step[1] + ",0)"; break;
                    case "PADR": input = "PADR(" + input + "," + step[1] + ")"; break;
                    case "ALLTRIM": input = "ALLTRIM(" + input + ")"; break;
                    case "UPPER": input = "UPPER(" + input + ")"; break;
                }
            }
            return input;
        }

        internal string Format(object value, Encoding encoding)
        {
            if (value == null || value.GetType() != InputType) throw new ArgumentException("Invalid key value type.", nameof(value));
            var strict = (Encoding)encoding.Clone();
            strict.EncoderFallback = EncoderFallback.ExceptionFallback;
            if (value is string text)
            {
                if (text.IndexOf('\0') >= 0) throw new ArgumentException("A key cannot contain a null character.", nameof(value));
                strict.GetByteCount(text);
            }
            string json = JsonConvert.SerializeObject(new { value, steps });
            JToken result = InternalUniversal.ProcedureCall<JToken>("FORMAT_KEY", json, strict);
            if (result == null || result.Type != JTokenType.String) throw new InvalidOperationException("Unexpected formatted key result.");
            return result.Value<string>();
        }
    }
}
