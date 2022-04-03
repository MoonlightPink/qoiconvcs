Use Codecs_Qoi.cs only.

THE QUITE OK IMAGE FORMAT
Specification Version 1.0, 2022.01.05 – qoiformat.org – Dominic Szablewski
https://qoiformat.org/

C# porting by NUlliiON https://github.com/NUlliiON/QoiSharp 61d9218 on 23 Dec 2021

Single-header library version by Moonlight. 2022/04/03

Encoder:
public static byte[] Encode(byte[] Pixels, int _Width, int _Height, EChannels _Channels, EColorSpace ColorSpace = EColorSpace.SRgb);

Decoder:
public class CDecodeResult {
  public int Width = 0;
  public int Height = 0;
  public EChannels Channels = EChannels.Rgb;
  public EColorSpace ColorSpace = EColorSpace.SRgb;
  public byte[] Pixels = null;
}
public static CDecodeResult Decode(byte[] data);

