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
}
