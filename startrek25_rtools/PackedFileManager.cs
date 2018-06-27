using System;
using System.Collections.Generic;
using System.IO;

class PackedFileReader {
    Archive archive;

    FileStream _dirStream;
    FileStream _dataStream;
    FileStream _dataRunStream;

    FileStream DirStream {
        get {
            if (_dirStream == null)
                _dirStream = archive.getFileReadStream("data.dir");
            return _dirStream;
        }
    }

    FileStream DataStream {
        get {
            if (_dataStream == null)
                _dataStream = archive.getFileReadStream("data.001");
            return _dataStream;
        }
    }

    FileStream DataRunStream {
        get {
            if (_dataRunStream == null)
                _dataRunStream = archive.getFileReadStream("data.run");
            return _dataRunStream;
        }
    }



    public PackedFileReader(Archive a) {
        archive = a;
    }

    /**
     * Return a list of files in the packed data.
     */
    public IList<String> GetFileList() {
        var filenames = new List<String>();

        DirStream.Seek(0, SeekOrigin.Begin);

        for (int index=0; index<GetNumFiles(); index++) {
            string filename = GetIndexFilename(index);

            for (int i=0;; i++) {
                //Console.WriteLine(filename + (i==0 ? "*" : ""));
                filenames.Add(filename);

                if (i == GetNumFileParts(index)-1)
                    break;

                filename = AddToFileIndex(filename, 1);
                if (filename == "") {
                    Console.WriteLine("ASSUMPTION BROKEN");
                    break;
                }
            }
        }

        return filenames;
    }
    
    /**
     * Get the number of files in the packed data.
     */
    public int GetNumFiles() {
        DirStream.Seek(0, SeekOrigin.Begin);
        return (int)(DirStream.Length/14);
    }

    /**
     * Get the uncompressed data of a file.
     */
    public byte[] GetFileData(string filename, int part=0) {
        LZSS.processingFile = filename;
        /*
        if (part == 0)
            Console.WriteLine(filename);
            */
        int index = GetFilenameIndex(filename);

        if (index == -1) {
            string newFilename = AddToFileIndex(filename, -1);
            if (newFilename != "") {
                try {
                    return GetFileData(newFilename, part+1);
                }
                catch(FileNotFoundException) {}
            }
            throw new FileNotFoundException("File \"" + filename + "\" not found.");
        }

        DirStream.Seek(index*14 + 11, SeekOrigin.Begin);

        int indexOffset = DirStream.ReadByte() + (DirStream.ReadByte()<<8) + (DirStream.ReadByte()<<16);

        if ((indexOffset & (1 << 23)) != 0) {
            indexOffset &= 0xFFFF;
            return GetType2FileData(indexOffset, part);
        } else {
            indexOffset &= 0xFFFFFF;
            return GetType1FileData(indexOffset);
        }
    }

    /**
     * Get the compressed data of a file. (First 2 bytes are uncompressed size.)
     */
    public byte[] GetCompressedFileData(string filename, int part=0) {
        /*
        if (part == 0)
            Console.WriteLine(filename);
            */
        int index = GetFilenameIndex(filename);

        if (index == -1) {
            string newFilename = AddToFileIndex(filename, -1);
            if (newFilename != "") {
                try {
                    return GetCompressedFileData(newFilename, part+1);
                }
                catch(FileNotFoundException) {}
            }
            throw new FileNotFoundException("File \"" + filename + "\" not found.");
        }

        DirStream.Seek(index*14 + 11, SeekOrigin.Begin);

        int indexOffset = DirStream.ReadByte() + (DirStream.ReadByte()<<8) + (DirStream.ReadByte()<<16);

        /*
        if ((indexOffset & 1) != 0) {
            // This is normal for "type 2" data?
            Console.WriteLine("WARN: bit 0 of indexOffset is set");
        }
        */

        if ((indexOffset & (1 << 23)) != 0) {
            indexOffset &= 0xFFFF;
            return GetCompressedType2FileData(indexOffset, part);
        } else {
            indexOffset &= 0xFFFFFF;
            return GetCompressedType1FileData(indexOffset);
        }
    }

    // private methods

    byte[] GetType2FileData(int indexOffset, int part) {
        DataRunStream.Seek(indexOffset, SeekOrigin.Begin);

        int data001Offset = DataRunStream.ReadByte() + (DataRunStream.ReadByte()<<8) + (DataRunStream.ReadByte()<<16);

        for (int i=0; i<part; i++) {
            int size = ReadUInt16LE(DataRunStream);
            data001Offset += size;
        }

        DataStream.Seek(data001Offset, SeekOrigin.Begin);
        UInt16 uncmpSize = ReadUInt16LE(DataStream);
        UInt16 cmpSize = ReadUInt16LE(DataStream);

        return LZSS.Decode(DataStream, cmpSize, uncmpSize);
    }

    byte[] GetCompressedType2FileData(int indexOffset, int part) {
        DataRunStream.Seek(indexOffset, SeekOrigin.Begin);

        int data001Offset = DataRunStream.ReadByte() + (DataRunStream.ReadByte()<<8) + (DataRunStream.ReadByte()<<16);

        for (int i=0; i<part; i++) {
            int size = ReadUInt16LE(DataRunStream);
            data001Offset += size;
        }

        DataStream.Seek(data001Offset, SeekOrigin.Begin);
        UInt16 uncmpSize = ReadUInt16LE(DataStream);
        UInt16 cmpSize = ReadUInt16LE(DataStream);

        var data = new byte[cmpSize+2];
        data[0] = (byte)(uncmpSize&0xff);
        data[1] = (byte)(uncmpSize>>8);
        DataStream.Read(data, 2, cmpSize);
        return data;
    }

    byte[] GetType1FileData(int indexOffset) {
        DataStream.Seek(indexOffset, SeekOrigin.Begin);

        UInt16 uncmpSize = ReadUInt16LE(DataStream);
        UInt16 cmpSize = ReadUInt16LE(DataStream);

        DataStream.Seek(indexOffset+4, SeekOrigin.Begin);
        return LZSS.Decode(DataStream, cmpSize, uncmpSize);
    }


    byte[] GetCompressedType1FileData(int indexOffset) {
        DataStream.Seek(indexOffset, SeekOrigin.Begin);

        UInt16 uncmpSize = ReadUInt16LE(DataStream);
        UInt16 cmpSize = ReadUInt16LE(DataStream);

        var data = new byte[cmpSize+2];
        data[0] = (byte)(uncmpSize&0xff);
        data[1] = (byte)(uncmpSize>>8);
        DataStream.Read(data, 2, cmpSize);
        return data;
    }

    /**
     * Adds the given value to the number at the end of the string. Returns empty string on
     * failure.
     */
    string AddToFileIndex(string filename, int value) {
        int dotIndex = filename.IndexOf('.');
        if (dotIndex < 0)
            return "";

        char number = filename[dotIndex-1];
        number += (char)value;
        if (!((number >= '0' && number <= '9') || (number >= 'A' && number <= 'Z')))
            return "";

        return filename.Substring(0, dotIndex-1) + number + filename.Substring(dotIndex);
    }

    int GetNumFileParts(int index) {
        if (index == -1)
            return -1;
        DirStream.Seek(index*14+13, SeekOrigin.Begin);
        int b = DirStream.ReadByte();
        if ((b & 0x80) == 0)
            return 1;
        return b&0x7f;
    }

    int GetFilenameIndex(string filename) {
        for (int i=0; i<GetNumFiles(); i++) {
            string f = GetIndexFilename(i);
            if (f.ToLower() == filename.ToLower())
                return i;
        }
        return -1;
    }
    string GetIndexFilename(int index) {
        DirStream.Seek(index*14, SeekOrigin.Begin);

        string filename="";
        for (int i=0;i<8;i++) {
            int c = DirStream.ReadByte();
            if (c == -1)
                break;
            if (c != '\0')
                filename += (char)c;
        }

        string extension="";
        for (int i=0; i<3; i++) {
            char c = (char)DirStream.ReadByte();
            if (c != '\0')
                extension += c;
        }
        if (extension.Length != 0)
            filename += "." + extension;

        return filename;
    }

    UInt16 ReadUInt16LE(FileStream stream) {
        return (UInt16)(stream.ReadByte() + (stream.ReadByte()<<8));
    }
}
