using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codecs {
    internal class Qoi_BitmapHelper {
        private static byte[] BitmapToByteArray(System.Drawing.Bitmap bitmap, int Channels) {
            System.Drawing.Imaging.BitmapData bmpdata = null;

            try {
                bmpdata = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
                if ((bmpdata.Width * Channels) != bmpdata.Stride) { throw new Exception("bmpdata.Width != bmpdata.Stride " + bmpdata.Width + " " + bmpdata.Stride); }
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                System.Runtime.InteropServices.Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            } finally {
                if (bmpdata != null) { bitmap.UnlockBits(bmpdata); }
            }
        }
        private static void ByteArrayToBitmap(byte[] bytedata, System.Drawing.Bitmap bitmap, int Channels) {
            System.Drawing.Imaging.BitmapData bmpdata = null;

            try {
                bmpdata = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                if ((bmpdata.Width * Channels) != bmpdata.Stride) { throw new Exception("bmpdata.Width != bmpdata.Stride " + bmpdata.Width + " " + bmpdata.Stride); }
                int numbytes = bmpdata.Stride * bitmap.Height;
                IntPtr ptr = bmpdata.Scan0;

                System.Runtime.InteropServices.Marshal.Copy(bytedata, 0, ptr, numbytes);
            } finally {
                if (bmpdata != null) { bitmap.UnlockBits(bmpdata); }
            }
        }

        public static byte[] Encode(System.Drawing.Bitmap bm) {
            Codecs.Qoi.EChannels Channels;

            if (bm.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb) {
                Channels = Codecs.Qoi.EChannels.Rgb;
            } else {
                if (bm.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb) {
                    Channels = Codecs.Qoi.EChannels.RgbWithAlpha;
                } else {
                    throw new Exception("Support RGB24 or ARGB32 only.");
                }
            }

            byte[] Pixels = BitmapToByteArray(bm, (Channels == Codecs.Qoi.EChannels.Rgb) ? 3 : 4);

            return (Codecs.Qoi.Encode(Pixels, bm.Width, bm.Height, Channels));
        }

        public static System.Drawing.Bitmap Decode(byte[] data) {
            var res = Codecs.Qoi.Decode(data);

            var bm = new System.Drawing.Bitmap(res.Width, res.Height, (res.Channels == Codecs.Qoi.EChannels.Rgb) ? System.Drawing.Imaging.PixelFormat.Format24bppRgb : System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            ByteArrayToBitmap(res.Pixels, bm, (int)res.Channels);

            return (bm);
        }
    }
}
