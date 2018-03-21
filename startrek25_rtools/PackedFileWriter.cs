using System;
using System.IO;
using System.Text;
public class PackedFileWriter {
    Archive cmpArchive;
    Archive uncmpArchive;

    public PackedFileWriter(Archive cmpArchive, Archive uncmpArchive) {
        this.cmpArchive = cmpArchive;
        this.uncmpArchive = uncmpArchive;
    }

    public void Save(string directory) {
        directory = directory+'/';
        FileStream dataDir = File.Open(directory+"DATA.DIR", FileMode.Create);
        FileStream dataRun = File.Open(directory+"DATA.RUN", FileMode.Create);
        FileStream data001 = File.Open(directory+"DATA.001", FileMode.Create);

        FileInfo[] files = cmpArchive.GetAllFiles();
        Array.Sort(files, (f1,f2) => {
            string e1 = GetExtension(f1.Name);
            string e2 = GetExtension(f2.Name);
            int extCmp = e1.CompareTo(e2);
            if (extCmp != 0)
                return extCmp;

            return f1.Name.CompareTo(f2.Name);
        });
        foreach (FileInfo f in files) {
            byte[] cmpData = File.ReadAllBytes(f.FullName);
            int uncmpSize = (int)uncmpArchive.GetFileInfo(f.Name).Length;

            string filename, extension;
            int dotIndex = f.Name.IndexOf('.');
            if (dotIndex == -1) {
                filename = f.Name;
                extension = "";
            }
            else {
                filename = f.Name.Substring(0, dotIndex);
                extension = f.Name.Substring(dotIndex+1);
            }
            filename = filename.ToUpper();
            extension = extension.ToUpper();

            if ((data001.Position&1) != 0)
                data001.WriteByte(0);

            Console.WriteLine(f.Name + ": 0x{0:X}", data001.Position);

            WriteString(dataDir, filename, 8);
            WriteString(dataDir, extension, 3);
            Write24Bit(dataDir, (int)data001.Position);

            WriteWord(data001, (UInt16)uncmpSize);
            WriteWord(data001, (UInt16)cmpData.Length);
            data001.Write(cmpData, 0, cmpData.Length);
        }

        dataDir.Close();
        dataRun.Close();
        data001.Close();
    }

    // private methods
    void WriteString(Stream stream, string s, int len) {
        byte[] data = new ASCIIEncoding().GetBytes(s);

        if (data.Length > len)
            throw new Exception("Filename or extension too long: \"" + s + "\".");

        stream.Write(data, 0, data.Length);
        for (int i=0; i<len-data.Length; i++)
            stream.WriteByte(0);
    }

    void Write24Bit(Stream stream, int value) {
        stream.WriteByte((byte)((value>>0)&0xff));
        stream.WriteByte((byte)((value>>8)&0xff));
        stream.WriteByte((byte)((value>>16)&0xff));
    }

    void WriteWord(Stream stream, UInt16 value) {
        stream.WriteByte((byte)((value>>0)&0xff));
        stream.WriteByte((byte)((value>>8)&0xff));
    }

    string GetExtension(string filename) {
        int dotIndex = filename.IndexOf('.');
        if (dotIndex < 0)
            return "";
        return filename.Substring(dotIndex+1);
    }

    string GetBasename(string filename) {
        int dotIndex = filename.IndexOf('.');
        if (dotIndex < 0)
            return filename;
        return filename.Substring(0, dotIndex);
    }
}
