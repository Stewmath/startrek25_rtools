using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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

            // Pack a directory of files into DATA.001, DATA.DIR, DATA.RUN.
            case "--packfiles": {
                var cmpArchive = new Archive(args[1]);
                var uncmpArchive = new Archive(args[2]);
                PackedFileWriter writer = new PackedFileWriter(cmpArchive, uncmpArchive);
                writer.Save(args[3]);
                break;
            }

            // Decompress a file
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

            // Decompress a given file
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

            // Dump uncompressed versions of all files
            case "--dumpallfiles": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                DumpAllFiles(fileMgr, args[2]);
                break;
            }

            // Dump compressed versions of all files
            case "--dumpallfilescmp": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                DumpAllCompressedFiles(fileMgr, args[2]);
                break;
            }

            // Dump text from an RDF file
            case "--dumprdftext": {
                byte[] data = File.ReadAllBytes(args[1]);
                int textPos = Int32.Parse(args[2]);
                Console.Write("\"");
                while (data[textPos] != '\0')  {
                    char c = (char)(data[textPos++]);
                    if (c == '\\' || c == '"')
                        Console.Write("\\");
                    Console.Write(c);
                }
                Console.Write("\",\n");
                break;
            }

            // Dump table of text from an RDF file
            case "--dumprdftexttable": {
                byte[] data = File.ReadAllBytes(args[1]);
                int pos = Int32.Parse(args[2]);
                Console.WriteLine("const char *text[] = {");
                while (Helper.ReadUInt16(data, pos) != 0) {
                    int textPos = Helper.ReadUInt16(data, pos);
                    Console.Write("\t\"");
                    while (data[textPos] != '\0')  {
                        char c = (char)(data[textPos++]);
                        if (c == '\\' || c == '"')
                            Console.Write("\\");
                        Console.Write(c);
                    }
                    Console.Write("\",\n");
                    pos += 2;
                }
                Console.WriteLine("\t\"\"\n};");
                break;
            }

            case "--dumpscript": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                if (args.Length >= 4)
                    DumpScript(fileMgr, args[2] + ".RDF", args[3]);
                else
                    DumpScript(fileMgr, args[2] + ".RDF", null);
                break;
            }

            // Dump "scripts" (x86 code) from RDF files into txt files (uses objdump to disassemble)
            case "--dumpallscripts": {
                var archive = new Archive(args[1]);
                var fileMgr = new PackedFileReader(archive);
                string sfxDir = (args.Length >= 3 ? args[2] : null);
                foreach (String s in fileMgr.GetFileList()) {
                    if (s.EndsWith(".RDF")) {
                        DumpScript(fileMgr, s, sfxDir);
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

        static void DumpScript(PackedFileReader fileMgr, String filename, String sfxDir) {
            String roomName = filename.Substring(0, filename.IndexOf('.'));

            byte[] data = fileMgr.GetFileData(filename);
            int startOffset = Helper.ReadUInt16(data, 14);
            int endOffset = Helper.ReadUInt16(data, 16);

            // Dump RDF file
            FileStream stream = System.IO.File.Create("scripts/" + roomName + ".RDF");
            stream.Write(data, 0, data.Length);
            stream.Close();

            String outFile = "scripts/" + roomName + ".txt";
            File.Delete(outFile);

            var stringList = new List<StringEntry>();
            IEnumerable<String> sfxList = new List<String>();
            if (sfxDir != null) {
                sfxList = Directory.GetFiles(sfxDir).Select(x => {
                        x = x.Replace(".voc", "");
                        x = x.Replace(".VOC", "");
                        int pos = x.LastIndexOf('/');
                        if (pos == -1)
                            return x;
                        return x.Substring(pos+1);
                    });
            }

            // First pass: search for strings
            int offset = 0;
            while (offset < data.Length) {
                int so = offset;
                if (((char)(data[offset])) == '#') { // Search for audio markers
                    offset++;
                    int backslashes=0;
                    while (offset < data.Length && ((char)data[offset]) != '#' && data[offset] != '\0') {
                        if (data[offset] == '\\')
                            backslashes++;
                        offset++;
                    }
                    if (offset == data.Length || data[offset] != '#' || backslashes != 1)
                        continue;
                    string s = Helper.GetStringFromBuf(data, so);
                    string id = "";
                    for (offset=so+1; offset<data.Length && data[offset] != '#'; offset++) {
                        id += (char)data[offset];
                    }
                    stringList.Add(new StringEntry(so, so + s.Length + 1, s, id));
                    offset = so + s.Length;
                }
                else { // Search for known filenames
                    string s = Helper.GetStringFromBuf(data, offset);
                    foreach (string f in sfxList) {
                        if (String.Equals(s, f, StringComparison.OrdinalIgnoreCase)) {
                            offset += f.Length;
                            stringList.Add(new StringEntry(so, offset+1, s, ""));
                            break;
                        }
                    }
                }
                offset++;
            }

            // Print out the strings to be copied into an enum
            Console.WriteLine("enum Strings {");
            foreach (StringEntry e in stringList) {
                if (e.identifier.Length == 0)
                    continue;
                Console.WriteLine("TX_" + e.identifier + ",");
            }
            Console.WriteLine("}");

            // Print out the actual definitions of the strings
            Console.WriteLine("\nString contents:");
            foreach (StringEntry e in stringList) {
                if (e.identifier.Length == 0)
                    continue;
                Console.Write(e.start.ToString("X4") + ": ");
                Console.WriteLine("\"" + e.str.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",");
            }

            // Dump each script into the txt file
            offset = startOffset;
            int slIndex = 0;
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
                    infoString =
                        "\n\n=====================\n" +
                        "Event: " + EventToString(index, offset) + "\n" +
                        "=====================\n";
                    offset+=6;
                }

                Helper.RunBashCommand("echo '" + infoString + "' >> " + outFile); 
                while (slIndex < stringList.Count && stringList[slIndex].end <= offset) {
                    slIndex++;
                }

                while (offset < nextOffset) {
                    if (slIndex < stringList.Count && stringList[slIndex].start == offset) {
                        StringEntry ent = stringList[slIndex];
                        Helper.AppendToFile(outFile, "\nString (0x" + ent.start.ToString("X4") + "): '" + ent.str + "'");
                        offset = ent.end;
                        slIndex++;
                    }
                    else if (slIndex < stringList.Count && stringList[slIndex].start < nextOffset) {
                        int end = stringList[slIndex].start;
                        Helper.Objdump("scripts/" + filename, outFile, offset, end);
                        offset = end;
                    }
                    else {
                        Helper.Objdump("scripts/" + filename, outFile, offset, nextOffset);
                        break;
                    }
                }

                offset = nextOffset;
            }

            stream.Close();
        }

        static String EventToString(UInt32 index, int offset) {
            String[] actions = {
                "Tick",
                "Walk",
                "Use",
                "Get",
                "Look",
                "Talk"
            };

            var action = (Byte)(index >> 0);
            var b1 = (Byte)(index >> 8);
            var b2 = (Byte)(index >> 16);
            var b3 = (Byte)(index >> 24);

            String retString;
            switch (action) {
            case 0:
                retString = "Tick " + (b1 | (b2 << 8));
                break;
            case 2: // USE
                retString = actions[action] + " " + ItemToString(b1) + ", " + ItemToString(b2);
                break;
            case 1:
            case 3:
            case 4:
            case 5:
                retString = actions[action] + " " + ItemToString(b1);
                break;
            case 6:
                retString = "Touched warp " + b1;
                break;
            case 7:
                retString = "Touched hotspot " + b1;
                break;
            case 8:
                retString = "Timer " + b1 + " expired";
                break;
            case 10:
                retString = "Finished animation (" + b1 + ")";
                break;
            case 12:
                retString = "Finished walking (" + b1 + ")";
                break;
            default:
                retString = "";
                break;
            }

            string rawString = action.ToString("X2") + " " + b1.ToString("X2") + " " + b2.ToString("X2") + " " + b3.ToString("X2");
            if (retString.Length == 0)
                retString = rawString;
            else
                retString += " (" + rawString + ")";

            return retString + " (offset in RDF: 0x" + offset.ToString("X4") + ")";
        }

        static String ItemToString(Byte index) {
            if (index == 0)
                return "KIRK";
            else if (index == 1)
                return "SPOCK";
            else if (index == 2)
                return "MCCOY";
            else if (index == 3)
                return "REDSHIRT";
            else if (index >= 0x40 && (index - 0x40) < ItemNames.Length)
                return ItemNames[index - 0x40];
            return "0x"+index.ToString("X2"); // TODO
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

        static string[] ItemNames = {
            "IPHASERS",
            "IPHASERK",
            "IHAND",
            "IROCK",
            "ISTRICOR",
            "IMTRICOR",
            "IDEADGUY",
            "ICOMM",
            "IPBC",
            "IRLG",
            "IWRENCH",
            "IINSULAT",
            "ISAMPLE",
            "ICURE",
            "IDISHES",
            "IRT",
            "IRTWB",
            "ICOMBBIT",
            "IJNKMETL",
            "IWIRING",
            "IWIRSCRP",
            "IPWF",
            "IPWE",
            "IDEADPH",
            "IBOMB",
            "IMETAL",
            "ISKULL",
            "IMINERAL",
            "IMETEOR",
            "ISHELLS",
            "IDEGRIME",
            "ILENSES",
            "IDISKS",
            "IANTIGRA",
            "IN2GAS",
            "IO2GAS",
            "IH2GAS",
            "IN2O",
            "INH3",
            "IH2O",
            "IWROD",
            "IIROD",
            "IREDGEM_A",
            "IREDGEM_B",
            "IREDGEM_C",
            "IGRNGEM_A",
            "IGRNGEM_B",
            "IGRNGEM_C",
            "IBLUGEM_A",
            "IBLUGEM_B",
            "IBLUGEM_C",
            "ICONECT",
            "IS8ROCKS",
            "IIDCARD",
            "ISNAKE",
            "IFERN",
            "ICRYSTAL",
            "IKNIFE",
            "IDETOXIN",
            "IBERRY",
            "IDOOVER",
            "IALIENDV",
            "ICAPSULE",
            "IMEDKIT",
            "IBEAM",
            "IDRILL",
            "IHYPO",
            "IFUSION",
            "ICABLE1",
            "ICABLE2",
            "ILMD",
            "IDECK",
            "ITECH"
        };
    }

    class StringEntry {
        public int start, end;
        public string str;
        public string identifier;
        public StringEntry(int start, int end, string s, string id) {
            this.start = start;
            this.end = end;
            this.str = s;
            this.identifier = id;

            int pos = identifier.IndexOf('\\');
            if (pos != -1)
                identifier = identifier.Substring(pos+1);
        }
    }
}
