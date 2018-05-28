using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class Helper {
    public static UInt16 ReadUInt16(IList<byte> data, int index) {
        return (UInt16)((data[index]) + (data[index+1]<<8));
    }

    public static UInt32 ReadUInt32(IList<byte> data, int index) {
        return (UInt32)((data[index]) + (data[index+1]<<8) + (data[index+2]<<16) + (data[index+3]<<24));
    }

    public static void RunBashCommand(String command) {
        command = command.Replace("\"", "\\\"");
        Process p = Process.Start("/usr/bin/bash", "-c \"" + command + "\"");
        while (!p.HasExited);
    }

    public static void AppendToFile(String outFile, String s) {
        s = s.Replace("'", "'\"'\"'");
        RunBashCommand("echo '" + s + "' >> " + outFile); 
    }

    public static void Objdump(String inFile, String outFile, int start, int end) {
        String command = "objdump -b binary -mi386 -Maddr16,data16,intel -D --start-address=" + (start) + " --stop-address=" + end;
        command += " " + inFile;
        command += " | tail -n +6";
        command += ">> " + outFile;
        RunBashCommand(command);
    }

    public static bool IsText(byte b) {
        char c = (char)b;
        return (c >= 'a' && c <= 'z')
            || (c >= 'Z' && c <= 'Z')
            || (c >= '0' && c <= '9')
            || (c == ',' || c == '.')
            || (c == '#' || c == '\'')
            || (c == '(' || c == ')');
    }

    public static String GetStringFromBuf(byte[] data, int offset) {
        string s = "";
        while (offset < data.Length && data[offset] != '\0') {
            s += (char)data[offset];
            offset++;
        }
        if (offset == data.Length)
            return "";
        return s;
    }
}
