using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BuggyBunny
{
    class Program
    {
        static void Main(string[] args)
        {
            //Launch GUI if no arguments are provided
            if (args.Length == 0)
            {
                Application.Run(new GUI());
                return;
            }
            
            //Process CMD stuff
            switch (args[0])
            {
                case "-dectxt":
                    ExtractTextsToTx2(args);
                    break;
                case "-enctxt":
                    PackTextsToData(args);
                    break;
                case "-offset":
                    OffsetImagePointers(args);
                    break;
                default:
                    Console.WriteLine("You messed up!");
                    break;
            }
        }

        static void ExtractTextsToTx2(string[] args)
        {
            BinaryReader br = new BinaryReader(File.OpenRead(args[1]));
            string outPath = Path.GetDirectoryName(args[1]) + "\\" + Path.GetFileNameWithoutExtension(args[1]) + "\\";
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);
            ushort newPos = (ushort)br.ReadUInt16(); 
            br.BaseStream.Position = newPos;
            uint i = 0;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                if (br.ReadByte() != 0x0)
                {
                    br.BaseStream.Position -= 1; //This is a bodge and a person at MS should get fired for it's existance
                    File.WriteAllBytes(outPath + i + ".txt", ReadNullTerminatedAsciiStringFromBr2(br));
                    i++;
                }
                else break;
            }
        }

        static void PackTextsToData(string[] args)
        {
            string[] files = Directory.GetFiles(args[1]);
            ushort[] offsets = new ushort[files.Length];
            BinaryWriter bw1 = new BinaryWriter(File.Create(args[1] + "_edit.bin"));
            BinaryWriter bw2 = new BinaryWriter(new MemoryStream());
            ushort count = (ushort)(files.Length * 2);
            for (uint i = 0; i < files.Length; i++)
            {
                byte[] file = File.ReadAllBytes(args[1] + "\\" + i + ".txt");
                bw1.Write(count);
                count += (ushort)(file.Length + 1);
                bw2.Write(file);
                bw2.Write((byte)0x0);
            }
            bw2.BaseStream.Position = 0;
            bw2.BaseStream.CopyTo(bw1.BaseStream);
        }

        public static byte[] ReadNullTerminatedAsciiStringFromBr2(BinaryReader br)
        {
            List<byte> list = new List<byte>();
            byte current = br.ReadByte();
            while (current != 0x0)
            {
                list.Add(current);
                current = br.ReadByte();
            }
            return list.ToArray();
        }

        static void OffsetImagePointers(string[] args)
        {
            BinaryReader br = new BinaryReader(File.OpenRead(args[1]));
            string outPath = Path.GetDirectoryName(args[1]) + "\\" + Path.GetFileNameWithoutExtension(args[1]);
            BinaryWriter bw = new BinaryWriter(File.OpenWrite(outPath + "_edit"));
            uint offset = uint.Parse(args[2]);
            for (int i = 0; i < br.BaseStream.Length / 9; i++)
            {
                var test = br.ReadByte();
                if (test != 0x2A) break;
                bw.Write((byte)0x2A);
                bw.Write(br.ReadUInt32() + offset);
                bw.Write(br.ReadUInt32());
            }
        }
    }
}
