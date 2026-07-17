using System;
using System.Collections.Generic;
using System.Text;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Minimal OSC 1.0 message codec (no external dependency). Supports the argument
    /// types show-control tools actually send — int32 ('i'), float32 ('f'),
    /// string ('s') — and ignores others gracefully. Bundles are not unpacked (the
    /// PMSDK address space is stateless single messages). Pure static functions so the
    /// codec is unit-testable without sockets.
    /// </summary>
    public static class PMSDKOsc
    {
        public class Message
        {
            public string Address;
            public List<object> Args = new List<object>();

            public int GetInt(int i, int fallback = 0)
            {
                if (i >= Args.Count) return fallback;
                if (Args[i] is int v) return v;
                if (Args[i] is float f) return (int)f;
                return fallback;
            }

            public float GetFloat(int i, float fallback = 0f)
            {
                if (i >= Args.Count) return fallback;
                if (Args[i] is float v) return v;
                if (Args[i] is int n) return n;
                return fallback;
            }

            public string GetString(int i, string fallback = "")
            {
                if (i >= Args.Count || !(Args[i] is string s)) return fallback;
                return s;
            }
        }

        /// <summary>Parse a single OSC message packet. Returns null if malformed or a bundle.</summary>
        public static Message Parse(byte[] data, int length)
        {
            try
            {
                if (data == null || length < 4 || data[0] != '/') return null;
                int pos = 0;

                string address = ReadPaddedString(data, length, ref pos);
                if (string.IsNullOrEmpty(address)) return null;

                var msg = new Message { Address = address };
                if (pos >= length) return msg; // no type tags: address-only message

                string tags = ReadPaddedString(data, length, ref pos);
                if (string.IsNullOrEmpty(tags) || tags[0] != ',') return msg;

                for (int t = 1; t < tags.Length; t++)
                {
                    switch (tags[t])
                    {
                        case 'i': msg.Args.Add(ReadInt(data, length, ref pos)); break;
                        case 'f': msg.Args.Add(ReadFloat(data, length, ref pos)); break;
                        case 's': msg.Args.Add(ReadPaddedString(data, length, ref pos)); break;
                        case 'T': msg.Args.Add(1); break;   // OSC true (no payload)
                        case 'F': msg.Args.Add(0); break;   // OSC false
                        default: return msg;                 // unknown tag: stop parsing args
                    }
                }
                return msg;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Build an OSC message (for tests / loopback / future feedback).</summary>
        public static byte[] Build(string address, params object[] args)
        {
            var bytes = new List<byte>();
            WritePaddedString(bytes, address);
            var tags = new StringBuilder(",");
            var payload = new List<byte>();
            foreach (var a in args)
            {
                switch (a)
                {
                    case int i: tags.Append('i'); WriteBigEndian(payload, BitConverter.GetBytes(i)); break;
                    case float f: tags.Append('f'); WriteBigEndian(payload, BitConverter.GetBytes(f)); break;
                    case string s: tags.Append('s'); WritePaddedString(payload, s); break;
                    default: throw new ArgumentException("Unsupported OSC arg type: " + a?.GetType());
                }
            }
            WritePaddedString(bytes, tags.ToString());
            bytes.AddRange(payload);
            return bytes.ToArray();
        }

        // ---------------- internals ----------------

        private static string ReadPaddedString(byte[] d, int len, ref int pos)
        {
            int start = pos;
            while (pos < len && d[pos] != 0) pos++;
            if (pos >= len && start == pos) return null;
            string s = Encoding.ASCII.GetString(d, start, pos - start);
            // consume the null + pad to 4
            pos = ((pos / 4) + 1) * 4;
            return s;
        }

        private static void WritePaddedString(List<byte> outBytes, string s)
        {
            var raw = Encoding.ASCII.GetBytes(s);
            outBytes.AddRange(raw);
            int pad = 4 - (raw.Length % 4);
            for (int i = 0; i < pad; i++) outBytes.Add(0); // at least one null, pad to 4
        }

        private static int ReadInt(byte[] d, int len, ref int pos)
        {
            if (pos + 4 > len) throw new ArgumentException("truncated int");
            int v = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];
            pos += 4;
            return v;
        }

        private static float ReadFloat(byte[] d, int len, ref int pos)
        {
            if (pos + 4 > len) throw new ArgumentException("truncated float");
            var tmp = new byte[4];
            if (BitConverter.IsLittleEndian)
            {
                tmp[0] = d[pos + 3]; tmp[1] = d[pos + 2]; tmp[2] = d[pos + 1]; tmp[3] = d[pos];
            }
            else
            {
                Array.Copy(d, pos, tmp, 0, 4);
            }
            pos += 4;
            return BitConverter.ToSingle(tmp, 0);
        }

        private static void WriteBigEndian(List<byte> outBytes, byte[] native)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(native);
            outBytes.AddRange(native);
        }
    }
}
