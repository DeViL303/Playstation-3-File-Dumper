using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PS3_XMB_Tools
{ 
public class SettingEntry
{
    public uint ID { get; set; }
    public byte Value { get; set; }
    public string Setting { get; set; }
    public long Offset { get; set; }
    public long EndOffset { get; set; }
    public bool DataExists { get; set; }

    public bool Load(BinaryReader reader)
    {
        Offset = reader.BaseStream.Position;
        ID = ReadUInt32BigEndian(reader);
        Value = reader.ReadByte();
        if (Value == 0xEE) // 0xAABBCCDDEE
            return false;
        Setting = ReadNullTerminatedString(reader);
        EndOffset = reader.BaseStream.Position;
        return true;
    }

    public override string ToString()
    {
        return Setting;
    }

    public bool IsEntryOffset(long offset)
    {
        return Offset <= offset && EndOffset > offset;
    }

    public void Save(BinaryWriter writer)
    {
        writer.BaseStream.Position = Offset;
        WriteUInt32BigEndian(writer, ID);
        writer.Write(Value);
        WriteNullTerminatedString(writer, Setting);
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        List<byte> bytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 0)
        {
            bytes.Add(b);
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private static void WriteNullTerminatedString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0); // null-terminator
    }
}
}