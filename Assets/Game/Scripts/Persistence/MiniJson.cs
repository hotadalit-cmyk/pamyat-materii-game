using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace PM.Persistence {
/// <summary>
/// Минимальный детерминированный JSON (объекты/массивы/числа/строки/bool/null).
/// Зависимостей нет: работает и в Unity, и в .NET harness (D-20). Числа — инвариантная
/// культура с round-trip форматом, чтобы save→load не терял точность.
/// </summary>
public sealed class MiniJson {
    // ---------- Writer (корректная машина состояний контейнеров) ----------
    public sealed class Writer {
        private readonly StringBuilder _sb = new StringBuilder(256);
        private sealed class Frame {
            public bool IsObject;
            public bool HasItems;   // уже записан хотя бы один элемент
            public bool AwaitValue; // после Key — значение без запятой
        }
        private readonly Stack<Frame> _frames = new Stack<Frame>();
        public Writer() { }
        private Frame Top {
            get {
                if (_frames.Count == 0) throw new MiniJsonException("value_outside_container");
                return _frames.Peek();
            }
        }
        private void BeforeScalar() {
            if (_frames.Count == 0) return; // корневой скаляр разрешён
            var f = Top;
            if (f.IsObject) {
                if (!f.AwaitValue) throw new MiniJsonException("object_value_needs_key");
                f.AwaitValue = false;      // значение после ключа — без запятой
            } else if (f.HasItems) {
                _sb.Append(',');           // элементы массива через запятую
            }
        }
        public Writer BeginObject() { BeforeContainer(); _sb.Append('{'); _frames.Push(new Frame { IsObject = true }); return this; }
        public Writer BeginArray() { BeforeContainer(); _sb.Append('['); _frames.Push(new Frame { IsObject = false }); return this; }
        private void BeforeContainer() {
            if (_frames.Count == 0) return; // корень
            var f = Top;
            if (f.IsObject) {
                if (!f.AwaitValue) throw new MiniJsonException("object_value_needs_key");
                f.AwaitValue = false;
            } else if (f.HasItems) {
                _sb.Append(',');
            }
        }
        public Writer EndObject() {
            var f = Top;
            if (!f.IsObject) throw new MiniJsonException("mismatched_end");
            if (f.AwaitValue) throw new MiniJsonException("dangling_key");
            _frames.Pop();
            _sb.Append('}');
            MarkItem();
            return this;
        }
        public Writer EndArray() {
            var f = Top;
            if (f.IsObject) throw new MiniJsonException("mismatched_end");
            _frames.Pop();
            _sb.Append(']');
            MarkItem();
            return this;
        }
        private void MarkItem() {
            if (_frames.Count > 0) _frames.Peek().HasItems = true;
        }
        public Writer Key(string k) {
            var f = Top;
            if (!f.IsObject) throw new MiniJsonException("key_in_array");
            if (f.HasItems) _sb.Append(',');
            f.HasItems = true;
            f.AwaitValue = true;
            _sb.Append(Escape(k ?? "")).Append(':');
            return this;
        }
        public Writer Value(double v) { BeforeScalar(); _sb.Append(v.ToString("R", CultureInfo.InvariantCulture)); MarkItem(); return this; }
        public Writer Value(long v) { BeforeScalar(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); MarkItem(); return this; }
        public Writer Value(bool v) { BeforeScalar(); _sb.Append(v ? "true" : "false"); MarkItem(); return this; }
        public Writer Value(string v) { BeforeScalar(); _sb.Append(Escape(v ?? "")); MarkItem(); return this; }
        public Writer NullValue() { BeforeScalar(); _sb.Append("null"); MarkItem(); return this; }
        public override string ToString() {
            if (_frames.Count != 0) throw new MiniJsonException("unclosed_container");
            return _sb.ToString();
        }
        internal static string Escape(string s) {
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
    // ---------- Parser ----------
    public static object Parse(string json) {
        var p = new Parser(json);
        var v = p.ParseValue();
        p.SkipWs();
        if (!p.AtEnd) throw new MiniJsonException("trailing_data");
        return v;
    }
    public sealed class MiniJsonException : System.Exception {
        public MiniJsonException(string m) : base(m) { }
    }
    public sealed class Parser {
        private readonly string _s; private int _i;
        public Parser(string s) { _s = s ?? ""; }
        public bool AtEnd => _i >= _s.Length;
        public void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
        private char Next() {
            if (AtEnd) throw new MiniJsonException("unexpected_end");
            return _s[_i++];
        }
        public object ParseValue() {
            SkipWs();
            char c = Next();
            switch (c) {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': Expect("rue"); return true;
                case 'f': Expect("alse"); return false;
                case 'n': Expect("ull"); return null;
                default:
                    _i--;
                    return ParseNumber();
            }
        }
        private void Expect(string rest) {
            for (int i = 0; i < rest.Length; i++)
                if (Next() != rest[i]) throw new MiniJsonException("bad_literal");
        }
        private Dictionary<string, object> ParseObject() {
            var dict = new Dictionary<string, object>();
            SkipWs();
            if (Next() == '}') return dict;
            _i--;
            while (true) {
                SkipWs();
                char q = Next();
                if (q != '"') throw new MiniJsonException("expected_key");
                var key = ParseString();
                SkipWs();
                if (Next() != ':') throw new MiniJsonException("expected_colon");
                dict[key] = ParseValue();
                SkipWs();
                char sep = Next();
                if (sep == '}') return dict;
                if (sep != ',') throw new MiniJsonException("expected_comma_or_brace");
            }
        }
        private List<object> ParseArray() {
            var list = new List<object>();
            SkipWs();
            if (Next() == ']') return list;
            _i--;
            while (true) {
                list.Add(ParseValue());
                SkipWs();
                char sep = Next();
                if (sep == ']') return list;
                if (sep != ',') throw new MiniJsonException("expected_comma_or_bracket");
            }
        }
        private string ParseString() {
            var sb = new StringBuilder();
            while (true) {
                char c = Next();
                if (c == '"') return sb.ToString();
                if (c == '\\') {
                    char e = Next();
                    switch (e) {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new MiniJsonException("bad_unicode");
                            sb.Append((char)System.Convert.ToInt32(_s.Substring(_i, 4), 16));
                            _i += 4;
                            break;
                        default: throw new MiniJsonException("bad_escape");
                    }
                } else sb.Append(c);
            }
        }
        private double ParseNumber() {
            int start = _i;
            while (_i < _s.Length && "-+.eE0123456789".IndexOf(_s[_i]) >= 0) _i++;
            var tok = _s.Substring(start, _i - start);
            if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new MiniJsonException("bad_number:" + tok);
            return v;
        }
    }
}
}
