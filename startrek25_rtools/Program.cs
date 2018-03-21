using System;
using System.Diagnostics;
using System.IO;

namespace startrek25_rtools
{
    class MainClass
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1) {
                Console.WriteLine("No options specified.");
                return 1;
            }

            switch(args[0]) {
            case "--packfiles": {
                var cmpArchive = new Archive(args[1]);
                var uncmpArchive = new Archive(args[2]);
                PackedFileWriter writer = new PackedFileWriter(cmpArchive, uncmpArchive);
                writer.Save(args[3]);
                break;
            }
            case "--compressfile": {
                byte[] data = File.ReadAllBytes(args[1]);
                byte[] cmpData = LZSS.Encode(data);
                FileStream output = File.Open(args[2], FileMode.Create);
                output.WriteByte((byte)(data.Length&0xff));
                output.WriteByte((byte)(data.Length>>8));
                output.Write(cmpData, 0, cmpData.Length);
                output.Close();
                break;
            }
            case "--decompressfile": {
                FileStream stream = File.OpenRead(args[1]);
                byte[] cmpData = new byte[stream.Length-2];
                UInt16 uncmpSize = (UInt16)(stream.ReadByte() + (stream.ReadByte()<<8));
                stream.Read(cmpData, 0, cmpData.Length);
                byte[] data = LZSS.Decode(cmpData, cmpData.Length, uncmpSize);

                FileStream output = File.Open(args[2], FileMode.Create);
                output.Write(data, 0, data.Length);
                output.Close();
                break;
            }
            case "--dumpallfiles": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                DumpAllFiles(fileMgr, args[2]);
                break;
            }
            case "--dumpallfilescmp": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                DumpAllCompressedFiles(fileMgr, args[2]);
                break;
            }
            case "--dumpscripts": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                foreach (String s in fileMgr.GetFileList()) {
                    if (s.EndsWith(".RDF")) {
                        String roomName = s.Substring(0, s.IndexOf('.'));

                        byte[] data = fileMgr.GetFileData(s);
                        int startOffset = Helper.ReadUInt16(data, 14);
                        int endOffset = Helper.ReadUInt16(data, 16);

                        // Dump RDF file
                        FileStream stream = System.IO.File.Create("scripts/" + roomName + ".RDF");
                        stream.Write(data, 0, data.Length);
                        stream.Close();

                        String outFile = "scripts/" + roomName + ".txt";
                        File.Delete(outFile);

                        // Dump each script into the txt file
                        int offset = startOffset;
                        while (offset < endOffset) {
                            UInt32 index = Helper.ReadUInt32(data, offset);
                            int nextOffset = Helper.ReadUInt16(data, offset+4);

                            String infoString;

                            // When the last "code index" is passed, there's no indication of it,
                            // so I need to check for invalid values
                            if (nextOffset > endOffset || nextOffset <= offset+6) {
                                infoString =
                                    "\n\n=====================\n" +
                                    "Helper code\n" +
                                    "=====================\n";
                                nextOffset = endOffset;
                            }
                            else {
                                offset+=6;
                                infoString =
                                    "\n\n=====================\n" +
                                    "Index: " + index.ToString("X8") + "\n" +
                                    "=====================\n";
                            }
                            Helper.RunBashCommand("echo '" + infoString + "' >> " + outFile); 

                            String command = "objdump -b binary -mi386 -Maddr16,data16,intel -D --start-address=" + (offset) + " --stop-address=" + nextOffset;
                            command += " scripts/" + s + ">> " + outFile;
                            Helper.RunBashCommand(command);

                            offset = nextOffset;
                        }

                        stream.Close();
                    }
                }
                break;
            }
            default:
                Console.WriteLine("Unrecognized option \"" + args[0] + "\".");
                return 1;
            }

            return 0;
        }

        static void DumpAllFiles(PackedFileReader fileMgr, string directory) {
            foreach (string f in fileMgr.GetFileList()) {
                DumpFile(fileMgr, directory, f);
            }
        }

        static void DumpFile(PackedFileReader fileMgr, string directory, string filename) {
            byte[] data = fileMgr.GetFileData(filename);
            FileStream s = File.Open(directory+"/"+filename, FileMode.Create);
            s.Write(data, 0, data.Length);
            s.Close();
        }

        static void DumpAllCompressedFiles(PackedFileReader fileMgr, string directory) {
            foreach (string f in fileMgr.GetFileList()) {
                DumpCompressedFile(fileMgr, directory, f);
            }
        }

        static void DumpCompressedFile(PackedFileReader fileMgr, string directory, string filename) {
            byte[] data = fileMgr.GetCompressedFileData(filename);
            FileStream s = File.Open(directory+"/"+filename, FileMode.Create);
            s.Write(data, 0, data.Length);
            s.Close();
        }
    }
}
