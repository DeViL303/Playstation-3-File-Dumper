using System;
using System.IO;
using System.Text;


namespace PS3_XMB_Tools
{

    public class SettingDataEntry
    {
        public ushort Checksum { get; set; }
        public ushort Length { get; set; }
        public ushort Flags { get; set; }
        public byte Type { get; set; } // 1 = integer, 2 = string
        public object Value { get; set; }
        public byte Terminator { get; set; }
        public long Offset { get; set; }
        public ushort FileNameOffset { get; set; }
        public SettingEntry FileNameEntry { get; set; }
        public bool Modified { get; set; }

        public string ValueString
        {
            get
            {
                switch (Type)
                {
                    case 0:
                        return BitConverter.ToString((byte[])Value).Replace("-", string.Empty);
                    case 1:
                        return ((int)Value).ToString("X" + (Length * 2).ToString());
                    case 2:
                        return Encoding.ASCII.GetString((byte[])Value).Trim('\0');
                    default:
                        return string.Empty;
                }
            }
        }

        public bool Load(BinaryReader reader)
        {
            Offset = reader.BaseStream.Position;
            Flags = ReadUInt16BigEndian(reader);
            FileNameOffset = ReadUInt16BigEndian(reader);
            Checksum = ReadUInt16BigEndian(reader);
            Length = ReadUInt16BigEndian(reader);
            Type = reader.ReadByte();
            switch (Type)
            {
                case 1:
                    switch (Length)
                    {
                        case 2:
                            Value = ReadInt16BigEndian(reader);
                            break;
                        case 4:
                            Value = ReadInt32BigEndian(reader);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    Value = reader.ReadBytes(Length);
                    break;
            }
            reader.BaseStream.Position = Offset + 9 + Length;
            Terminator = reader.ReadByte();
            return true;
        }

        public bool Save(BinaryWriter writer)
        {
            writer.BaseStream.Position = Offset;
            WriteUInt16BigEndian(writer, Flags);
            WriteUInt16BigEndian(writer, FileNameOffset);
            WriteUInt16BigEndian(writer, Checksum);
            if ((Type == 2 || Type == 0) && ((byte[])Value).Length > 0 && Modified)
            {
                if (((byte[])Value).Length > Length)
                    Length = (ushort)(((byte[])Value).Length);
            }
            WriteUInt16BigEndian(writer, Length);
            writer.Write(Type);
            switch (Type)
            {
                case 1:
                    switch (Length)
                    {
                        case 2:
                            WriteInt16BigEndian(writer, (short)Value);
                            break;
                        case 4:
                            WriteInt32BigEndian(writer, (int)Value);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    writer.Write((byte[])Value);
                    break;
            }
            writer.BaseStream.Position = Offset + 9 + Length;
            writer.Write(Terminator);
            return true;
        }

        private static ushort ReadUInt16BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static short ReadInt16BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private static void WriteUInt16BigEndian(BinaryWriter writer, ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            writer.Write(bytes);
        }

        private static void WriteInt16BigEndian(BinaryWriter writer, short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            writer.Write(bytes);
        }

        private static void WriteInt32BigEndian(BinaryWriter writer, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            writer.Write(bytes);
        }
    }

}