using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CIRCUS_MES
{
    class Script
    {
        public void Load(string filePath)
        {
            var data = File.ReadAllBytes(filePath);
            using var stream = new MemoryStream(data);
            Read(stream);
            Parse();
        }

        public void Save(string filePath)
        {
            using var writer = new BinaryWriter(File.Create(filePath));

            writer.Write(_jmpAddrList.Count);

            foreach (var e in _jmpAddrList)
            {
                writer.Write(e);
            }

            foreach (var e in _unkList)
            {
                writer.Write(e);
            }

            writer.Write(_codeSection);

            writer.Flush();
        }

        readonly List<uint> _jmpAddrList = new();
        readonly List<ushort> _unkList = new();
        byte[] _codeSection;

        readonly Assembly _assembly = new();

        void Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);

            _jmpAddrList.Clear();
            _unkList.Clear();
            _assembly.Clear();

            int numCmd = reader.ReadInt32();
            
            for (int i = 0; i < numCmd; i++)
            {
                _jmpAddrList.Add(reader.ReadUInt32());
            }

            for (int i = 0; i < numCmd; i++)
            {
                _unkList.Add(reader.ReadUInt16());
            }

            var codeSize = Convert.ToInt32(stream.Length - stream.Position);
            _codeSection = reader.ReadBytes(codeSize);
        }

        void Parse()
        {
            var stream = new MemoryStream(_codeSection);
            var reader = new BinaryReader(stream);

            reader.ReadByte(); // Version
            reader.ReadInt16(); // 0xAB

            while (stream.Position < stream.Length)
            {
                ReadCmd(reader);
            }

            if (_assembly.BytesLength != stream.Length - 3)
            {
                throw new Exception("Failed parsing code");
            }
        }

        void ReadCmd(BinaryReader reader)
        {
            var inst = Instruction.Unknow;

            var addr = Convert.ToInt32(reader.BaseStream.Position);

            var code = reader.ReadByte();

            //Debug.Assert(code != 0x87);

            if (code >= 0x3B)
            {
                if (code >= 0x48)
                {
                    if (code >= 0x69)
                    {
                        if (code >= 0x6E)
                        {
                            reader.ReadInt16();
                            reader.ReadInt16();
                            reader.ReadInt16();
                            reader.ReadInt16();
                        }
                        else
                        {
                            inst = Instruction.LoadEncryptStr;
                            reader.ReadCString();
                        }
                    }
                    else
                    {
                        reader.ReadCString();
                    }
                }
                else
                {
                    reader.ReadByte();
                    reader.ReadCString();
                }
            }
            else
            {
                reader.ReadByte();
                reader.ReadByte();
            }

            // Peek extra parameters
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                if (reader.ReadByte() == 0x77)
                {
                    reader.ReadInt16();
                    reader.ReadInt16();
                    reader.ReadInt16();
                    reader.ReadInt16();
                }
                else
                {
                    reader.BaseStream.Position--;
                }
            }

            var length = Convert.ToInt32(reader.BaseStream.Position) - addr;

            _assembly.Add(addr, length, inst);
        }

        Instruct[] GetStrings()
        {
            return _assembly.Instructs
                .Where(a => a.Instruction == Instruction.LoadEncryptStr)
                .ToArray();
        }

        public void ExportText(string filePath, string readEncoding)
        {
            var loadStrInst = GetStrings();

            if (loadStrInst.Length == 0)
                return;

            using var writer = File.CreateText(filePath);

            var encoding = Encoding.GetEncoding(readEncoding);

            foreach (var e in loadStrInst)
            {
                if (e.Length <= 2)
                    continue;

                var length = e.Length - 2;
                var bytes = new byte[length];
                Array.Copy(_codeSection, e.Address + 1, bytes, 0, length);

                DecryptString(bytes);

                var s = encoding.GetString(bytes);

                s = s.Replace("\r", "\\r");
                s = s.Replace("\n", "\\n");

                writer.WriteLine($"◇{e.Address:X8}◇{s}");
                writer.WriteLine($"◆{e.Address:X8}◆{s}");
                writer.WriteLine();
            }
        }

        public void ImportText(string filePath, string saveEncoding)
        {
            var loadStrInst = GetStrings();

            if (loadStrInst.Length == 0)
                return;

            var translated = new Dictionary<int, string>();

            using (var reader = new StreamReader(filePath))
            {
                var _lineNo = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var lineNo = _lineNo++;

                    if (line.Length == 0)
                        continue;

                    if (line[0] != '◆')
                        continue;

                    var m = Regex.Match(line, "◆(.+?)◆(.+$)");

                    if (!m.Success || m.Groups.Count != 3)
                        throw new Exception($"Bad format at line: {lineNo}");

                    var offset = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                    var s = m.Groups[2].Value;

                    s = s.Replace("\\r", "\r");
                    s = s.Replace("\\n", "\n");

                    translated.Add(offset, s);
                }
            }

            if (translated.Count == 0)
                return;

            var encoding = Encoding.GetEncoding(saveEncoding);

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write(_codeSection, 0, 3);

            foreach (var inst in _assembly.Instructs)
            {
                inst.NewAddress = (int)stream.Position;

                if (inst.Instruction == Instruction.LoadEncryptStr &&
                    translated.TryGetValue(inst.Address, out string str))
                {
                    var bytes = encoding.GetBytes(str);

                    EncryptString(bytes);

                    writer.Write(_codeSection[inst.Address]);
                    writer.Write(bytes);
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write(_codeSection, inst.Address, inst.Length);
                }

                var jmpAddrIndex = _jmpAddrList.FindIndex(a => (a & 0x7fffffff) == inst.Address);
                if (jmpAddrIndex != -1)
                {
                    var addrVal = _jmpAddrList[jmpAddrIndex] & 0x80000000;
                    addrVal |= (uint)inst.NewAddress & 0x7fffffff;
                    _jmpAddrList[jmpAddrIndex] = addrVal;
                }
            }

            writer.Flush();

            _codeSection = stream.ToArray();
        }

        static void DecryptString(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] += 0x20;

                if (bytes[i] == 0x24)
                {
                    bytes[i] = 0x20;
                }
            }
        }

        static void EncryptString(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x20)
                {
                    bytes[i] = 0x24;
                }

                bytes[i] -= 0x20;
            }
        }

        enum Instruction
        {
            Unknow,
            LoadEncryptStr,
        }

        class Instruct
        {
            public int Address { get; }
            public int NewAddress { get; set; }
            public int Length { get; }
            public Instruction Instruction;

            public Instruct(int address, int length, Instruction instruction)
            {
                Address = address;
                Length = length;
                Instruction = instruction;
            }
        }

        class Assembly
        {
            public List<Instruct> Instructs { get; }

            public Assembly()
            {
                Instructs = new List<Instruct>();
            }

            public void Add(int address, int length, Instruction instruction)
            {
                Instructs.Add(new Instruct(address, length, instruction));
            }

            public void Clear()
            {
                Instructs.Clear();
            }

            public int BytesLength
            {
                get => Instructs.Sum(a => a.Length);
            }
        }
    }
}
