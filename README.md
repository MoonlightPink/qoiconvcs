Use Codecs_Qoi.cs only.<br>
<br>
THE QUITE OK IMAGE FORMAT<br>
Specification Version 1.0, 2022.01.05 – qoiformat.org – Dominic Szablewski<br>
https://qoiformat.org/<br>
<br>
C# porting by NUlliiON https://github.com/NUlliiON/QoiSharp 61d9218 on 23 Dec 2021<br>
<br>
Single-header library version by Moonlight. 2022/04/03<br>
<br>
Encoder:<br>
public static byte[] Encode(byte[] Pixels, int _Width, int _Height, EChannels _Channels, EColorSpace ColorSpace = EColorSpace.SRgb);<br>
<br>
Decoder:<br>
public class CDecodeResult {<br>
  public int Width = 0;<br>
  public int Height = 0;<br>
  public EChannels Channels = EChannels.Rgb;<br>
  public EColorSpace ColorSpace = EColorSpace.SRgb;<br>
  public byte[] Pixels = null;<br>
}<br>
public static CDecodeResult Decode(byte[] data);<br>
