using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace BuggyBunny
{
    public partial class GUI : Form
    {
        public GUI()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            //WARNING: A LOT OF HARDCODED STUFF!!!

            string basePath = Path.GetDirectoryName(textBox1.Text) + "\\extract\\";
            if (Directory.Exists(basePath))
            {
                if (MessageBox.Show("Extracted files already exist, do you want to overwrite them?", "File conflict.", MessageBoxButtons.YesNo) == DialogResult.Yes) ;
                else return;
            }

            string fileInfo = ""; //This is used to print the info.csv file for reconstruction

            //Open files
            BinaryReader meta = new BinaryReader(File.OpenRead(textBox1.Text));
            BinaryReader data = new BinaryReader(File.OpenRead(textBox2.Text));

            //Read in text offsets and sizes
            uint offsetText = FindUintOffset(meta, 0x4A492D2E);
            meta.BaseStream.Position = offsetText + 0x4;
            fileInfo += offsetText.ToString("X8") + ","; //Write offset to info file
            fileInfo += "6,"; //Write count to info file HARDCODED!
            entry[] texts = new entry[6]; //HARCODED!
            for (uint i = 0; i < 6; i++) texts[i] = new entry(meta.ReadUInt32(), meta.ReadUInt32()); //HARCODED!

            //Read in sound offset and size
            uint offsetSound = FindUintOffset(meta, 0x1002D2E);
            meta.BaseStream.Position = offsetSound + 0xD; //HARDCODED!
            fileInfo += offsetSound.ToString("X8") + ","; //Write offset to info file
            entry sound = new entry(meta.ReadUInt32(), meta.ReadUInt32());

            //Read in texture table
            uint offsetImage = FindUintOffset(meta, 0x2A292D2E);
            meta.BaseStream.Position = offsetImage + 0x3; //HARDCODED!
            fileInfo += offsetImage.ToString("X8") + ","; //Write offset to info file
            List<entry> images = new List<entry>();
            while (true)
            {
                var test = meta.ReadByte();
                if (test != 0x2A) break;
                images.Add(new entry(meta.ReadUInt32(), meta.ReadUInt32()));
            }
            fileInfo += images.Count; //Write count to info file

            //Extract the data to files
            if (!Directory.Exists(basePath + "texts")) Directory.CreateDirectory(basePath + "texts");
            if (!Directory.Exists(basePath + "images")) Directory.CreateDirectory(basePath + "images");
            //Texts
            for (uint i = 0; i < 6; i++) //HARDCODED!
            {
                uint count = 0;
                data.BaseStream.Position = texts[i].offset;
                data.BaseStream.Position += data.ReadUInt16();

                while (data.BaseStream.Position < (texts[i].offset + texts[i].length))
                {
                    if (!Directory.Exists(basePath + "texts\\lang_" + i)) Directory.CreateDirectory(basePath + "texts\\lang_" + i);

                    if (data.ReadByte() != 0x0)
                    {
                        data.BaseStream.Position -= 1;
                        File.WriteAllBytes(basePath + "texts\\lang_" + i + "\\" + count + ".txt", Program.ReadNullTerminatedAsciiStringFromBr2(data));
                        count++;
                    }
                }
            }
            //Images
            for (int i = 0; i < images.Count; i++)
            {
                data.BaseStream.Position = images[i].offset;
                File.WriteAllBytes(basePath + "images\\" + i + ".bin", data.ReadBytes((int)images[i].length));
            }
            //Three bytes at the end
            File.WriteAllBytes(basePath + "endbytes.bin", data.ReadBytes(3));
            //Sound
            data.BaseStream.Position = sound.offset;
            File.WriteAllBytes(basePath + "sound.bin", data.ReadBytes((int)sound.length));
            //Info file
            File.WriteAllText(basePath + "fileInfo.csv", fileInfo);

            //Close the streams
            meta.Close();
            data.Close();

            System.Media.SystemSounds.Exclamation.Play(); //Signify the thing finishing by annoying user with a sound XD
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            //Read id the fileInfo.csv
            string basePath = Path.GetDirectoryName(textBox1.Text) + "\\extract\\";
            string[] fileInfo = File.ReadAllText(basePath + "fileInfo.csv").Split(',');
            //Do a thing to backup the original file and make sure to not overwrite it
            if (!File.Exists(Path.GetDirectoryName(textBox2.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox2.Text) + "_original.bin"))
                File.Move(textBox2.Text, Path.GetDirectoryName(textBox2.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox2.Text) + "_original.bin");
            BinaryWriter bw = new BinaryWriter(File.Create(Path.GetDirectoryName(textBox2.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox2.Text) + ".bin"));

            //Write sounds
            bw.Write(File.ReadAllBytes(basePath + "sound.bin")); //Sound file should be an exact multiple of 16 in size, thus 4-byte aligned by default
            uint soundLength = (uint)bw.BaseStream.Position; //A hacky way to get the size of the file

            //Pack up the texts
            uint[] textLengths = new uint[6];
            for (uint i = 0; i < 6; i++) //HARDCODED!
            {
                BinaryWriter tempWriter = new BinaryWriter(new MemoryStream()); //Temporary stream to write the actual text to, while pointers get written to file on disk
                uint count = (uint)Directory.GetFiles(basePath + "texts\\lang_" + i + "\\").Length;
                ushort offset = (ushort)(count * 2);
                for (uint c = 0; c < count; c++)
                {
                    byte[] file = File.ReadAllBytes(basePath + "texts\\lang_" + i + "\\" + c + ".txt");
                    bw.Write(offset);
                    offset += (ushort)(file.Length + 1);
                    tempWriter.Write(file);
                    tempWriter.Write((byte)0x0);
                }
                byte delta = (byte)(4 - (offset % 4));
                for (int c = 0; c < delta; c++)
                {
                    tempWriter.Write((byte)0x0); //This makes the file 4-byte aligned
                    offset++;
                }
                tempWriter.BaseStream.Position = 0;
                tempWriter.BaseStream.CopyTo(bw.BaseStream); //Copy the text data to the file
                tempWriter.Close(); //Close the temporary writer
                textLengths[i] = offset;
            }

            //Pack up the images
            uint imgCount = (uint)Directory.GetFiles(basePath + "images\\").Length;
            uint[] imgLengths = new uint[imgCount];
            for (uint i = 0; i < imgCount; i++)
            {
                var file = File.OpenRead(basePath + "images\\" + i + ".bin");
                imgLengths[i] = (uint)file.Length;
                file.CopyTo(bw.BaseStream); //Do a straight copy because these files are already 4-byte aligned
                file.Close();
            }

            //Pack up three bytes at the end
            bw.Write(File.ReadAllBytes(basePath + "endbytes.bin"));

            //Close the stream for output file
            bw.Close();

            //Check file counts
            uint imageCountOrig = uint.Parse(fileInfo[4]);
            //If they don't match up, warn user through CMD
            if (imgCount != imageCountOrig) Console.WriteLine("Texture count mismatch!");

            //Update meta
            byte[] meta = File.ReadAllBytes(textBox1.Text);
            //Texts
            uint metaOffset = Convert.ToUInt32(fileInfo[0], 16) + 0x4;
            uint offsetTemp = soundLength;
            for (uint i = 0; i < 6; i++)
            {
                WriteUIntToByteArray(meta, metaOffset, offsetTemp);
                offsetTemp += textLengths[i];
                metaOffset += 4;
                WriteUIntToByteArray(meta, metaOffset, textLengths[i]);
                metaOffset += 4;
            }
            //Sound
            metaOffset = Convert.ToUInt32(fileInfo[2], 16) + 0x11;
            WriteUIntToByteArray(meta, metaOffset, soundLength);
            //Images
            metaOffset = Convert.ToUInt32(fileInfo[3], 16) + 0x3;
            for (uint i = 0; i < imageCountOrig; i++)
            {
                metaOffset += 0x1;
                WriteUIntToByteArray(meta, metaOffset, offsetTemp);
                offsetTemp += imgLengths[i];
                metaOffset += 4;
                WriteUIntToByteArray(meta, metaOffset, imgLengths[i]);
                metaOffset += 4;
            }

            //Do a thing to backup the original file and make sure to not overwrite it
            if (!File.Exists(Path.GetDirectoryName(textBox1.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox1.Text) + "_original.bin"))
                File.Move(textBox1.Text, Path.GetDirectoryName(textBox1.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox1.Text) + "_original.bin");
            //Write the file
            File.WriteAllBytes(Path.GetDirectoryName(textBox1.Text) + "\\" + Path.GetFileNameWithoutExtension(textBox1.Text) + ".bin", meta);

            System.Media.SystemSounds.Exclamation.Play(); //Signify the thing finishing by annoying user with a sound XD
        }

        static void WriteUIntToByteArray(byte[] array, uint offset, uint value)
        {
            for (uint i = 0; i < 4; i++)
            {
                byte[] valueArray = BitConverter.GetBytes(value);
                array[offset + i] = valueArray[i];
            }
        }

        static uint FindUintOffset(BinaryReader br, uint value)
        {
            //Auto Offset Finder ver 1.0 XD
            try
            {
                int position = 0;
                while (true)
                {
                    br.BaseStream.Position = position;
                    if (br.ReadUInt32() == value) return (uint)position;
                    position++;
                }
            }
            catch
            {
                throw new Exception("Couldn't find section! Aborting!");
            }
        }

        class entry
        {
            public uint offset;
            public uint length;

            public entry(uint offset, uint length)
            {
                this.offset = offset;
                this.length = length;
            }
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.Text = textBox1.Text.Replace("\"", ""); //Replace all quote marks with nothing since those aren't allowed in file paths anyway
            if (textBox1.Text.EndsWith("arquivo_02.bin")) textBox1.Text = Path.GetDirectoryName(textBox1.Text) + "\\arquivo_00.bin";
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            textBox2.Text = textBox2.Text.Replace("\"", ""); //Replace all quote marks with nothing since those aren't allowed in file paths anyway
            if (textBox2.Text.EndsWith("arquivo_00.bin")) textBox2.Text = Path.GetDirectoryName(textBox2.Text) + "\\arquivo_02.bin";
        }
    }
}
