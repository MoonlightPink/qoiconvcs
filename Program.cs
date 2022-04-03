using System;
using System.Reflection.Emit;

namespace MyApp // Note: actual namespace depends on the project name.
{
    internal class Program {
        static void Main(string[] args) {
            args = new string[1] { "test.png" };

            if (args.Length == 0) {
                Console.WriteLine("画像ファイルを指定すると、QOI圧縮してQOIファイルを書き出し、QOI展開してpngファイルに書き出します。");
                return;
            }

            var srcfn = args[0];
            var qoifn = System.IO.Path.ChangeExtension(srcfn, ".qoi");
            var dstpngfn = System.IO.Path.ChangeExtension(srcfn, ".dst.png");

            Console.WriteLine("元画像ファイル読み込み " + srcfn);
            var srcbm = new System.Drawing.Bitmap(srcfn);

            Console.WriteLine("QOI圧縮");
            var data = Codecs.Qoi_BitmapHelper.Encode(srcbm);

            Console.WriteLine("QOIファイル書き出し " + qoifn);
            using (var wfs = new System.IO.StreamWriter(qoifn)) {
                wfs.BaseStream.Write(data);
            }

            Console.WriteLine("QOI展開");
            var decodebm = Codecs.Qoi_BitmapHelper.Decode(data);

            Console.WriteLine("pngファイル書き出し " + dstpngfn);
            decodebm.Save(dstpngfn);
        }
    }
}
