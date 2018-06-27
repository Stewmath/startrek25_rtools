using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class LZSS {
    public static string processingFile;

    public static byte[] Decode(Stream indata, int compressedSize, int uncompressedSize) {
        byte[] buf = new byte[compressedSize];
        indata.Read(buf, 0, compressedSize);
        return Decode(buf, compressedSize, uncompressedSize);
    }
    public static byte[] Decode(byte[] indata, int compressedSize, int uncompressedSize) {
        UInt32 N = 0x1000; /* History buffer size */
        byte[] histbuff = new byte[N]; /* History buffer */
        UInt32 bufpos = 0;
        List<byte> outLzssBufData = new List<byte>();
        int readBytes = 0;

        for (;;) {
            if (readBytes >= compressedSize)
                break;

            int flagbyte = indata[readBytes++];

            for (byte i = 0; i < 8; i++) {
                if (readBytes >= compressedSize)
                    break;

                if ((flagbyte & (1 << i)) == 0) {
                    int offsetlen = indata[readBytes] + (indata[readBytes+1]<<8);
                    readBytes+=2;

                    if (offsetlen == -1 || readBytes > compressedSize)
                        break;

                    UInt32 length = (UInt32)((offsetlen & 0xF) + 3);
                    UInt32 offset = (UInt32)((bufpos - (offsetlen >> 4)) & (N - 1));
                    for (UInt32 j = 0; j < length; j++) {
                        byte tempa = histbuff[(offset + j) & (N - 1)];
                        outLzssBufData.Add(tempa);
                        histbuff[bufpos] = tempa;
                        bufpos = (bufpos + 1) & (N - 1);
                    }
                } else {
                    int tempa = indata[readBytes++];

                    outLzssBufData.Add((byte)tempa);
                    histbuff[bufpos] = (byte)tempa;
                    bufpos = (bufpos + 1) & (N - 1);
                }
            }
        }

        if (uncompressedSize != 0 && outLzssBufData.Count != uncompressedSize) {
            Console.WriteLine("Size mismatch: expected " + uncompressedSize + ", got " + outLzssBufData.Count);
            Console.WriteLine("Warning while processing \"" + processingFile + "\"");
        }

        return outLzssBufData.ToArray();
    }

    static Dictionary<EncodeEntry, int> encodeStrings;
    static EncodeData[] encodeAnswers;

    /**
     * Note: memory requirements for this function are absurd. Uses 1.5 gigabytes
     * for a 63K file (ie. bitmaps as large as the screen).
     */
    public static byte[] Encode(byte[] inData) {
        encodeStrings = new Dictionary<EncodeEntry, int>(new EncodeEntry.EqualityComparer());
        encodeAnswers = new EncodeData[inData.Length];

        for (int pos=1; pos<inData.Length; pos++) {
            for (int len=3; len<=12 && len<=pos; len++) {
                int i = pos-len;
                var value = new EncodeEntry(inData, i, len);
                encodeStrings[value] = i;
            }
            EncodeHlpr(inData, pos);
        }

        return EncodeHlpr(inData, inData.Length-1).data.ToArray();
    }

    /**
     * Returns Tuple with the encoded data, an int for the key position, and
     * an int for the number of bits used in the key.
     * @param pos   The position of the next byte to encode (everything after is ignored)
     */
    static EncodeData EncodeHlpr(byte[] inData, int pos) {
        if (encodeAnswers[pos] != null)
            return encodeAnswers[pos];

        if (pos == 0) {
            var data = new List<byte>();
            data.Add(0x01);
            data.Add(inData[pos]);
            encodeAnswers[0] = new EncodeData(data, 0, 1);
            return encodeAnswers[0];
        }

        var possibilities = new List<EncodeData>();
        EncodeData poss;

        if (possibilities.Count == 0) {
            poss = new EncodeData(EncodeHlpr(inData, pos-1));
            if (poss.keyLen == 8) {
                poss.keyPos = poss.data.Count;
                poss.keyLen = 1;
                poss.data.Add(0x01);
            }
            else {
                poss.data[poss.keyPos] |= (byte)((1<<poss.keyLen));
                poss.keyLen++;
            }
            poss.data.Add(inData[pos]);
            possibilities.Add(poss);
        }

        for (int len=3; len<=12 && len<=pos+1; len++) {
            int position;
            if (!encodeStrings.TryGetValue(new EncodeEntry(inData, pos-len+1, len), out position))
                continue;
            int offset = (pos-len+1) - position;
            if (offset >= 0x1000)
                continue;

            poss = new EncodeData(EncodeHlpr(inData, pos-len));
            if (poss.keyLen == 8) {
                poss.keyPos = poss.data.Count;
                poss.keyLen = 1;
                poss.data.Add(0x00);
            }
            else
                poss.keyLen++;

            UInt16 word = (UInt16)((len-3) | (offset<<4));
            poss.data.Add((byte)(word&0xff));
            poss.data.Add((byte)(word>>8));
            possibilities.Add(poss);
        }

        // Choose shortest candidate
        encodeAnswers[pos] = possibilities.Aggregate(
                (acc, val) => acc.data.Count < val.data.Count ? acc
                : acc.keyLen < val.keyLen ? acc : val);
        return encodeAnswers[pos];
    }

    // Just puts it in LZSS format without actually compressing
    public static byte[] EncodeFake(byte[] indata) {
        var outData = new List<byte>();

        int i=8;
        foreach (byte b in indata) {
            if (i==8) {
                outData.Add(0xff);
                i=0;
            }
            outData.Add(b);
            i++;
        }

        return outData.ToArray();
    }

    class EncodeData {
        public List<byte> data;
        public int keyPos, keyLen;

        public EncodeData(List<byte> data, int keyPos, int keyLen) {
            this.data = new List<byte>(data);
            this.keyPos = keyPos;
            this.keyLen = keyLen;
        }

        public EncodeData(EncodeData d) {
            this.data = new List<byte>(d.data);
            this.keyPos = d.keyPos;
            this.keyLen = d.keyLen;
        }
    }

    class EncodeEntry {
        byte[] data;
        int start, len;
        public EncodeEntry(byte[] data, int start, int len) {
            this.data = data;
            this.start = start;
            this.len = len;
        }

        public class EqualityComparer : EqualityComparer<EncodeEntry> {
            public override bool Equals(EncodeEntry a, EncodeEntry b) {
                if (a.len != b.len)
                    return false;
                for (int i=0; i<a.len; i++) {
                    if (a.data[a.start+i] != b.data[b.start+i])
                        return false;
                }
                return true;
            }
            public override int GetHashCode(EncodeEntry a) {
                int hash=0;
                for (int i=a.start; i<a.start+a.len; i++) {
                    hash*=31;
                    hash+=a.data[i];
                }
                return hash;
            }
        }
    }

    class MyEqualityComparer : EqualityComparer<byte[]> {
        public override bool Equals(byte[] x, byte[] y) {
            if (x.Length != y.Length)
                return false;
            for (int i=0; i<x.Length; i++) {
                if (x[i] != y[i])
                    return false;
            }
            return true;
        }
        public override int GetHashCode(byte[] obj) {
            int hash=0;
            foreach (byte b in obj) {
                hash*=31;
                hash+=b;
            }
            return hash;
        }
    }
}
