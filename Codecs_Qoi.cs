
// THE QUITE OK IMAGE FORMAT
// Specification Version 1.0, 2022.01.05 – qoiformat.org – Dominic Szablewski
// https://qoiformat.org/

// Released under the MIT license
// https://opensource.org/licenses/mit-license.php

// C# porting by NUlliiON https://github.com/NUlliiON/QoiSharp 61d9218 on 23 Dec 2021

// Single-header library version by Moonlight. 2022/04/03

// Encoder:
// public static byte[] Encode(byte[] Pixels, int _Width, int _Height, EChannels _Channels, EColorSpace ColorSpace = EColorSpace.SRgb);

// Decoder:
// public class CDecodeResult {
//   public int Width = 0;
//   public int Height = 0;
//   public EChannels Channels = EChannels.Rgb;
//   public EColorSpace ColorSpace = EColorSpace.SRgb;
//   public byte[] Pixels = null;
// }
// public static CDecodeResult Decode(byte[] data);

using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Codecs {
    internal class Qoi {
        public class QoiEncodingException : Exception {
            public QoiEncodingException(string message) : base(message) {
            }
        }

        public class QoiDecodingException : Exception {
            public QoiDecodingException(string message) : base(message) {
            }
        }

        public enum EChannels {
            Rgb = 3, // 3-channel format containing data for Red, Green, Blue.
            RgbWithAlpha = 4,// 4-channel format containing data for Red, Green, Blue, and Alpha.
        }

        public enum EColorSpace {
            SRgb = 0, // Gamma scaled RGB channels and a linear alpha channel.
            Linear = 1, // All channels are linear.
        }

        public static byte[] Encode(byte[] Pixels, int _Width, int _Height, EChannels _Channels, EColorSpace ColorSpace = EColorSpace.SRgb) {
            if (_Width <= 0) { throw new QoiEncodingException($"Invalid width: {_Width}"); }
            if (_Height <= 0) { throw new QoiEncodingException($"Invalid height: {_Height}"); }

            var Width = (uint)_Width;
            var Height = (uint)_Height;
            var Channels = (uint)_Channels;

            if (Height >= MaxPixels / Width) { throw new QoiEncodingException($"Invalid height: {Height}. Maximum for this image is {MaxPixels / Width - 1}"); }

            using (var ms = new System.IO.MemoryStream()) {
                var bw = new System.IO.BinaryWriter(ms);

                Write32BE(bw, CHeader.Magic);
                Write32BE(bw, Width);
                Write32BE(bw, Height);
                bw.Write((byte)Channels);
                bw.Write((byte)ColorSpace);

                var Index = new TColor[HashTableSize];

                var prev = new TColor();
                prev.a = 255;

                var now = new TColor();
                now.a = 255;

                uint run = 0;

                var pixelsLength = Width * Height * Channels;
                var pixelsEnd = pixelsLength - Channels;

                var buf = new byte[4096];
                var bufidx = 0;

                for (uint pxPos = 0; pxPos < pixelsLength; pxPos += Channels) {
                    now.r = Pixels[pxPos + 0];
                    now.g = Pixels[pxPos + 1];
                    now.b = Pixels[pxPos + 2];
                    if (Channels == 4) { now.a = Pixels[pxPos + 3]; }

                    if (now.Equals(prev)) {
                        run++;
                        if (run == 62 || pxPos == pixelsEnd) {
                            buf[bufidx++] = (byte)(CCode.Run | (run - 1));
                            run = 0;
                        }
                    } else {
                        if (run > 0) {
                            buf[bufidx++] = (byte)(CCode.Run | (run - 1));
                            run = 0;
                        }

                        var indexPos = CalculateHashTableIndex.Exec(now.pack32);// now.CalculateHashTableIndex();

                        if (now.Equals(Index[indexPos])) {
                            buf[bufidx++] = (byte)(CCode.Index | indexPos);
                        } else {
                            Index[indexPos] = now;

                            if (now.a == prev.a) {
                                var vr = now.r - prev.r;
                                var vg = now.g - prev.g;
                                var vb = now.b - prev.b;
                                if (vr is > -3 and < 2 &&
                                    vg is > -3 and < 2 &&
                                    vb is > -3 and < 2) {
                                    buf[bufidx++] = (byte)(CCode.Diff | (vr + 2) << 4 | (vg + 2) << 2 | (vb + 2));
                                } else {
                                    var vgr = vr - vg;
                                    var vgb = vb - vg;
                                    if (vgr is > -9 and < 8 &&
                                        vg is > -33 and < 32 &&
                                        vgb is > -9 and < 8) {
                                        buf[bufidx + 0] = (byte)(CCode.Luma | (vg + 32));
                                        buf[bufidx + 1] = (byte)((vgr + 8) << 4 | (vgb + 8));
                                        bufidx += 2;
                                    } else {
                                        buf[bufidx + 0] = CCode.Rgb;
                                        buf[bufidx + 1] = now.r;
                                        buf[bufidx + 2] = now.g;
                                        buf[bufidx + 3] = now.b;
                                        bufidx += 4;
                                    }
                                }
                            } else {
                                buf[bufidx + 0] = CCode.Rgba;
                                buf[bufidx + 1] = now.r;
                                buf[bufidx + 2] = now.g;
                                buf[bufidx + 3] = now.b;
                                buf[bufidx + 4] = now.a;
                                bufidx += 5;
                            }
                        }

                        prev = now;
                    }

                    if ((buf.Length - 5) < bufidx) {
                        bw.Write(buf, 0, bufidx);
                        bufidx = 0;
                    }
                }

                bw.Write(buf, 0, bufidx);

                bw.Write(CFutter.Padding);

                return (ms.ToArray());
            }
        }

        public class CDecodeResult {
            public int Width = 0;
            public int Height = 0;
            public EChannels Channels = EChannels.Rgb;
            public EColorSpace ColorSpace = EColorSpace.SRgb;
            public byte[] Pixels = null;
        }
        public static CDecodeResult Decode(byte[] data) {
            if (data.Length < CHeader.HeaderSize + CFutter.Padding.Length) { throw new QoiDecodingException("File too short"); }
            if (!CHeader.IsValidMagic(data[..4])) { throw new QoiDecodingException("Invalid file magic"); }// TODO: add magic value

            var res = new CDecodeResult();
            res.Width = data[4] << 24 | data[5] << 16 | data[6] << 8 | data[7];
            res.Height = data[8] << 24 | data[9] << 16 | data[10] << 8 | data[11];
            res.Channels = (EChannels)data[12];
            res.ColorSpace = (EColorSpace)data[13];
            res.Pixels = null;

            if (res.Width <= 0) { throw new QoiDecodingException($"Invalid width: {res.Width}"); }
            if (res.Height <= 0) { throw new QoiDecodingException($"Invalid height: {res.Height}"); }
            if (res.Height >= MaxPixels / res.Width) { throw new QoiDecodingException($"Invalid height: {res.Height}. Maximum for this image is { MaxPixels / res.Width - 1}"); }
            if (res.Channels is not EChannels.Rgb and not EChannels.RgbWithAlpha) { throw new QoiDecodingException($"Invalid number of channels: {res.Channels}"); }

            var Pixels = new byte[res.Width * res.Height * (int)res.Channels]; // newで確保した配列はゼロで初期化されているらしい？ clang fuzzing harness 対策
            res.Pixels = Pixels;

            var Index = new TColor[HashTableSize];
            for (var IndexPos = 0; IndexPos < Index.Length; IndexPos++) { // TODO: delete
                Index[IndexPos].a = 255;
            }

            var now = new TColor();
            now.a = 255;

            var p = (uint)CHeader.HeaderSize;

            for (var pxPos = 0; pxPos < Pixels.Length; pxPos += (int)res.Channels) {
                var b1 = data[p++];

                if (b1 == CCode.Rgb) {
                    now.r = data[p++];
                    now.g = data[p++];
                    now.b = data[p++];
                } else if (b1 == CCode.Rgba) {
                    now.r = data[p++];
                    now.g = data[p++];
                    now.b = data[p++];
                    now.a = data[p++];
                } else if ((b1 & CCode.Mask2) == CCode.Index) {
                    var indexPos = (byte)(b1 & ~CCode.Mask2);
                    now = Index[indexPos];
                } else if ((b1 & CCode.Mask2) == CCode.Diff) {
                    now.r += (byte)(((b1 >> 4) & 0x03) - 2);
                    now.g += (byte)(((b1 >> 2) & 0x03) - 2);
                    now.b += (byte)((b1 & 0x03) - 2);
                } else if ((b1 & CCode.Mask2) == CCode.Luma) {
                    var b2 = data[p++];
                    var vg = (b1 & 0x3F) - 32;
                    now.r += (byte)(vg - 8 + ((b2 >> 4) & 0x0F));
                    now.g += (byte)vg;
                    now.b += (byte)(vg - 8 + (b2 & 0x0F));
                } else if ((b1 & CCode.Mask2) == CCode.Run) {
                    byte run = (byte)(b1 & 0x3F);
                    for (byte i = 0; i < run; i++) {
                        Pixels[pxPos + 0] = now.r;
                        Pixels[pxPos + 1] = now.g;
                        Pixels[pxPos + 2] = now.b;
                        if (res.Channels == EChannels.RgbWithAlpha) { Pixels[pxPos + 3] = now.a; }
                        pxPos += (int)res.Channels;
                    }
                }

                Index[CalculateHashTableIndex.Exec(now.pack32)] = now;

                Pixels[pxPos + 0] = now.r;
                Pixels[pxPos + 1] = now.g;
                Pixels[pxPos + 2] = now.b;
                if (res.Channels == EChannels.RgbWithAlpha) { Pixels[pxPos + 3] = now.a; }
            }

            for (var idx = 0; idx < CFutter.Padding.Length; idx++) { // Futter check.
                if (data[data.Length - CFutter.Padding.Length + idx] != CFutter.Padding[idx]) {
                    throw new QoiDecodingException("Invalid padding");
                }
            }

            return (res);
        }

        private static void Write32BE(System.IO.BinaryWriter bw, int v) {
            bw.Write((byte)(v >> 24));
            bw.Write((byte)(v >> 16));
            bw.Write((byte)(v >> 8));
            bw.Write((byte)v);
        }
        private static void Write32BE(System.IO.BinaryWriter bw, uint v) {
            bw.Write((byte)(v >> 24));
            bw.Write((byte)(v >> 16));
            bw.Write((byte)(v >> 8));
            bw.Write((byte)v);
        }

        /// <summary>
        /// 2GB is the max file size that this implementation can safely handle. We guard
        /// against anything larger than that, assuming the worst case with 5 bytes per 
        /// pixel, rounded down to a nice clean value. 400 million pixels ought to be 
        /// enough for anybody.
        /// </summary>
        private static int MaxPixels = 400_000_000;

        private class CCode {
            public const byte Index = 0x00;
            public const byte Diff = 0x40;
            public const byte Luma = 0x80;
            public const byte Run = 0xC0;
            public const byte Rgb = 0xFE;
            public const byte Rgba = 0xFF;
            public const byte Mask2 = 0xC0;
        }

        private const int HashTableSize = 64;

        private class CHeader {
            public const byte HeaderSize = 14;
            public const string MagicString = "qoif";

            public static readonly int Magic = CalculateMagic(MagicString.AsSpan());

            public static bool IsValidMagic(byte[] magic) => CalculateMagic(magic) == Magic;

            private static int CalculateMagic(ReadOnlySpan<char> chars) => chars[0] << 24 | chars[1] << 16 | chars[2] << 8 | chars[3];
            private static int CalculateMagic(ReadOnlySpan<byte> data) => data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3];
        }

        private class CFutter {
            public static readonly byte[] Padding = { 0, 0, 0, 0, 0, 0, 0, 1 };
        }

        private class CCalculateHashTableIndex {
            public Func<UInt32, UInt32> Exec;
            public CCalculateHashTableIndex() {
                var method = new DynamicMethod("CalculateHashTableIndex", typeof(UInt32), new Type[] { typeof(UInt32) });
                var il = method.GetILGenerator();

                // 32bitカラーを64bit(32H|32L)カラーに複製する
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Conv_U8);
                il.Emit(OpCodes.Dup);
                EmitLdc_I4(il, 32);
                il.Emit(OpCodes.Shl);
                il.Emit(OpCodes.Or);

                // 各要素を分離する
                unchecked {
                    il.Emit(OpCodes.Ldc_I8, (long)0xFF00FF0000FF00FF);
                }
                il.Emit(OpCodes.And);

                // 重み付けで乗算する
                il.Emit(OpCodes.Ldc_I8, (long)((UInt64)3 << 56 | (UInt64)5 << 16 | (UInt64)7 << 40 | (UInt64)11));
                il.Emit(OpCodes.Mul);

                // 結果を取り出す
                EmitLdc_I4(il, 56);
                il.Emit(OpCodes.Shr_Un);

                // ハッシュサイズで割った余り
                EmitLdc_I4(il, HashTableSize - 1);
                il.Emit(OpCodes.And);

                il.Emit(OpCodes.Ret);

                Exec = (Func<UInt32, UInt32>)method.CreateDelegate(typeof(Func<UInt32, UInt32>));
            }
            private static void EmitLdc_I4(ILGenerator il, int value) {
                switch (value) {
                    case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default:
                        if (value >= -128 && value <= 127) {
                            il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                        } else {
                            il.Emit(OpCodes.Ldc_I4, value);
                        }
                        break;
                }
            }
        }
        private static CCalculateHashTableIndex CalculateHashTableIndex = new CCalculateHashTableIndex();

        [StructLayout(LayoutKind.Explicit)]
        private struct TColor {
            [FieldOffset(0)] public UInt32 pack32;
            [FieldOffset(0)] public byte r;
            [FieldOffset(1)] public byte g;
            [FieldOffset(2)] public byte b;
            [FieldOffset(3)] public byte a;

            public TColor() { pack32 = 0; r = 0; g = 0; b = 0; a = 0; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(TColor other) => (pack32 == other.pack32);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CalculateHashTableIndex_CS() { // 未使用
                return (((r * 3) + (g * 5) + (b * 7) + (a * 11)) % HashTableSize);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint CalculateHashTableIndex_CPP() { // 未使用
                const UInt64 constant = (UInt64)3 << 56 | (UInt64)5 << 16 | (UInt64)7 << 40 | (UInt64)11;
                UInt64 v = ((UInt64)pack32 | (UInt64)pack32 << 32) & 0xFF00FF0000FF00FF;
                return (((uint)((v * constant) >> 56)) % HashTableSize);
            }

        }
    }
}