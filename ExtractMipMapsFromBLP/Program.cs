using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace ExtractMipMapsFromBLP
{
    public static class StreamExtensions
    {
        public static T ReadStruct<T>(this Stream stream) where T : struct
        {
            var sz = Marshal.SizeOf(typeof(T));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var structure = (T)Marshal.PtrToStructure(
                pinnedBuffer.AddrOfPinnedObject(), typeof(T));
            pinnedBuffer.Free();
            return structure;
        }
    }
    class Program
    {

        struct Blp
        {
            public int blpheader;
            public int Compression;
            //0 - Uses JPEG compression
            //1 - Uses palettes (uncompressed)
            public int Flags;
            //#8 - Uses alpha channel (?)
            public int Width;
            public int Height;
            public int PictureType;
            //3 - Uncompressed index list + alpha list
            //4 - Uncompressed index list + alpha list
            //5 - Uncompressed index list
            public int PictureSubType;
            //1 - ???
            public int[] MipMapOffset;
            public int[] MipMapSize;
            public byte[] imgheader;
        }

        static void Main(string[] args)
        {
            byte[] blpdata = null;
            string filename = string.Empty;
            if (args.Length != 1)
            {
                filename = Console.ReadLine().Replace("\"", "");
                blpdata = File.ReadAllBytes(filename);
            }
            else
            {
                filename = args[0].Replace("\"", "");
                blpdata = File.ReadAllBytes(filename);

            }
            MemoryStream blpstream = new MemoryStream(blpdata);
            BinaryReader bread = new BinaryReader(blpstream);
            bread.BaseStream.Seek(0, SeekOrigin.Begin);

            Blp tmpblp = new Blp
            {
                blpheader = bread.ReadInt32(),
                Compression = bread.ReadInt32(),
                Flags = bread.ReadInt32(),
                Width = bread.ReadInt32(),
                Height = bread.ReadInt32(),
                PictureType = bread.ReadInt32(),
                PictureSubType = bread.ReadInt32(),
                MipMapOffset = new int[]{bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                    bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                    bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                    bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32()},
                MipMapSize = new int[]{bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                   bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                   bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(),
                   bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32(), bread.ReadInt32()},

            };
            if (tmpblp.Compression == 0)
            {
                long pos = bread.BaseStream.Position;
                int headersize = bread.ReadInt32();
                bread.BaseStream.Position = pos;
                tmpblp.imgheader = bread.ReadBytes(headersize);
            }
            else
            {
                tmpblp.imgheader = bread.ReadBytes(256 * 4);
            }

            int mipmapmaxsizeid = 0;
            for (int i = 0; i < tmpblp.MipMapOffset.Length; i++)
            {
                int mipmapoffset = tmpblp.MipMapOffset[i];
                int mipmapsize = tmpblp.MipMapSize[i];
                if (mipmapsize > tmpblp.MipMapSize[mipmapmaxsizeid])
                {
                    mipmapmaxsizeid = i;
                }
            }

            int needoffset = tmpblp.MipMapOffset[mipmapmaxsizeid];
            int needsize = tmpblp.MipMapSize[mipmapmaxsizeid];

            for (int i = 0; i < tmpblp.MipMapOffset.Length; i++)
            {
                tmpblp.MipMapSize[i] = needsize;
                tmpblp.MipMapOffset[i] = needoffset;
            }


            List<byte> outdata = new List<byte>();
            outdata.AddRange(BitConverter.GetBytes(tmpblp.blpheader));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.Compression));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.Flags));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.Width));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.Height));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.PictureType));
            outdata.AddRange(BitConverter.GetBytes(tmpblp.PictureSubType));

            int offset = 156 + tmpblp.imgheader.Length;



            for (int i = 0; i < tmpblp.MipMapOffset.Length; i++)
            {
                outdata.AddRange(BitConverter.GetBytes(offset));
            }

            for (int i = 0; i < tmpblp.MipMapOffset.Length; i++)
            {
                outdata.AddRange(BitConverter.GetBytes(tmpblp.MipMapSize[i]));
            }

            outdata.AddRange(tmpblp.imgheader);



            bread.BaseStream.Seek(needoffset, SeekOrigin.Begin);

            outdata.AddRange(bread.ReadBytes((int)needsize));
            File.WriteAllBytes(filename, outdata.ToArray());

        }
    }
}
