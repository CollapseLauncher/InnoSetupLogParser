using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable InlineOutVariableDeclaration
// ReSharper disable CheckNamespace
// ReSharper disable RedundantUnsafeContext
// ReSharper disable IdentifierTypo
// ReSharper disable InvertIf

namespace LibISULR;

public ref struct BufferTools
{
    private                 ReadOnlySpan<byte> _data;
    private readonly unsafe byte*              _dataPtr;
    private                 int                _index;

    public unsafe BufferTools(byte[] data)
        : this(data, 0)
    {
    }

    public unsafe BufferTools(byte[] data, int offset)
        : this(data.AsSpan(offset))
    {
    }

    public unsafe BufferTools(Span<byte> data)
    {
        _data = data;
        fixed (byte* dataPtr = _data)
        {
            _dataPtr = dataPtr;
        }

        _index = 0;
    }

    private int ReadLength(out bool eof)
    {
        byte type = _data[_index++];
        eof = false;

        // If the type is 0xFF (end), then set eof = true and return 0
        if (type == 0xFF)
        {
            eof = true;
            return 0;
        }

    #if NET
        return type switch
               {
                   0xFD => ReadUShort(_data, ref _index), // UTF-8 string length Type
                   0xFE => ReadInt(_data, ref _index), // Unicode/UTF-16 string length Type
                   _ => type // Type as the length
               };
    #else
            switch (type)
            {
                case 0xFD:
                    return ReadUShort(data, ref index);
                case 0xFE:
                    return ReadInt(data, ref index);
                default:
                    return type;
            }
    #endif
    }

    public unsafe string? ReadString()
    {
        bool eof;
        int  length = ReadLength(out eof);
        if (eof)
            return null;

        string result;

        switch (length)
        {
            case < 0:
                length = -length;
                result = Encoding.Unicode.GetString(_dataPtr + _index, length);
                break;
            case 0:
                return string.Empty;
            default:
                result = Encoding.Default.GetString(_dataPtr + _index, length);
                break;
        }

        _index += length;
        return result;
    }

    public unsafe int WriteString(Span<byte> buffer, Encoding encoding, string? inputString)
    {
        bool isUtf16 = encoding.GetType() == Encoding.Unicode.GetType();
        byte strType = (byte)(isUtf16 ? 0xFE : 0xFD);
    #if NET
        MemoryMarshal.Write(buffer, strType);
    #else
            MemoryMarshal.Write(buffer, ref strType);
    #endif

        int offset = 1;
        if (inputString == null) return 0;
        int strByteLen = inputString.Length * (isUtf16 ? 2 : 1);

    #if NET
        if (isUtf16)
            MemoryMarshal.Write(buffer[offset..], -strByteLen);
        else
            MemoryMarshal.Write(buffer[offset..], -(ushort)strByteLen);

        offset += isUtf16 ? 4 : 2;
        offset += encoding.GetBytes(inputString, buffer[offset..]);
    #else
            int negativeStrByteLen = -strByteLen;
            if (isUTF16)
                MemoryMarshal.Write(buffer.Slice(offset), ref negativeStrByteLen);
            else
                MemoryMarshal.Write(buffer.Slice(offset), ref negativeStrByteLen);

            offset += isUTF16 ? 4 : 2;
            fixed (byte* bufferPtr = buffer.Slice(offset))
            fixed (char* inputStringPtr = inputString)
            {
                offset += encoding.GetBytes(inputStringPtr, inputString.Length, bufferPtr, buffer.Length);
            }
    #endif
        return offset;
    }

    public int WriteDateTime(Span<byte> buffer, DateTime inputDateTime)
    {
        bool isDateEmpty = DateTime.MinValue == inputDateTime;
        byte lenType     = (byte)(isDateEmpty ? 0xFF : 0xFE);
    #if NET
        MemoryMarshal.Write(buffer, lenType);
    #else
            MemoryMarshal.Write(buffer, ref lenType);
    #endif

        int offset = 1;
        if (isDateEmpty) return offset;

        Span<ushort> dateTimeInUShorts = new ushort[8];
    #if NET
        MemoryMarshal.Write(buffer[offset..], -(dateTimeInUShorts.Length * 2));
    #else
            int dateTimeLenNegative = -(dateTimeInUShorts.Length * 2);
            MemoryMarshal.Write(buffer.Slice(offset), ref dateTimeLenNegative);
    #endif
        offset += 4;

        dateTimeInUShorts[0] = (ushort)inputDateTime.Year;
        dateTimeInUShorts[1] = (ushort)inputDateTime.Month;
        dateTimeInUShorts[2] = (ushort)inputDateTime.DayOfWeek;
        dateTimeInUShorts[3] = (ushort)inputDateTime.Day;
        dateTimeInUShorts[4] = (ushort)inputDateTime.Hour;
        dateTimeInUShorts[5] = (ushort)inputDateTime.Minute;
        dateTimeInUShorts[6] = (ushort)inputDateTime.Second;
        dateTimeInUShorts[7] = (ushort)inputDateTime.Millisecond;

        Span<byte> dateInTimeBytes = MemoryMarshal.AsBytes(dateTimeInUShorts);
        dateInTimeBytes.CopyTo(buffer[offset..]);
        offset += dateTimeInUShorts.Length * 2;

        return offset;
    }

    public DateTime ReadDateTime()
    {
        bool eof;
        int  length = ReadLength(out eof);
        if (eof)
            return DateTime.MinValue;
        if (length < 0)
            length = -length;

        DateTime result;

        if (length >= 16)
        {
            int i = _index;

            ushort year  = ReadUShort(_data, ref i);
            ushort month = ReadUShort(_data, ref i);
            i += 2; //ushort dow = ReadUShort(data,ref i);
            ushort day    = ReadUShort(_data, ref i);
            ushort hour   = ReadUShort(_data, ref i);
            ushort minute = ReadUShort(_data, ref i);
            ushort second = ReadUShort(_data, ref i);
            ushort ms     = ReadUShort(_data, ref i);

            result = new DateTime(year, month, day, hour, minute, second, ms, DateTimeKind.Local);
        }
        else
        {
            result = DateTime.MinValue;
        }

        _index += length;
        return result;
    }

    public int WriteBytes(Span<byte> buffer, ReadOnlySpan<byte> source)
    {
        const byte lenType = 0xFE;
    #if NET
        MemoryMarshal.Write(buffer, lenType);

        int offset = 1;
        MemoryMarshal.Write(buffer[offset..], -source.Length);
    #else
            MemoryMarshal.Write(buffer, ref lenType);

            int offset = 1;
            int sourceLengthNegative = -source.Length;
            MemoryMarshal.Write(buffer.Slice(offset), ref sourceLengthNegative);
    #endif
        offset += 4;
        if (source.Length == 0) return offset;

        source.CopyTo(buffer[offset..]);
        offset += source.Length;
        return offset;
    }

    public unsafe byte[]? ReadBytes()
    {
        bool eof;
        int  length = ReadLength(out eof);
        if (eof)
            return null;
        if (length < 0)
            length = -length;

        if (length == 0) return [];

        byte[] result = new byte[length];
        fixed (byte* resultPtr = result)
        {
            Buffer.MemoryCopy(_dataPtr + _index, resultPtr, length, length);
        }

        _index += length;

        return result;
    }

    private bool IsEnd => _index >= _data.Length;

    public List<string?> GetStringList()
    {
        List<string?> returnList = [];
        while (!IsEnd)
        {
            string? str = ReadString();
            if (str != null)
                returnList.Add(str);
        }

        return returnList;
    }

    public unsafe string[] GetStringArray()
    {
        byte type = _data[_index++];
        if (type == 0xFF) // If the type is 0xFF (end), then set eof = true and return 0
            return [];

    #if NET
        int byteLength = type switch
                         {
                             0xFD => ReadUShort(_data, ref _index), // UTF-8 string length Type
                             0xFE => ReadInt(_data, ref _index), // Unicode/UTF-16 string length Type
                             _ => type // Type as the length
                         };
    #else
            int byteLength = type;
            switch (type)
            {
                case 0xFD:
                    byteLength = ReadUShort(data, ref index);
                    break;
                case 0xFE:
                    byteLength = ReadInt(data, ref index);
                    break;
            }
    #endif

        if (byteLength == 0) return [];

        bool isUtf16 = type == 0xFE;

        int offset = 0;
        int count  = MemoryMarshal.Read<int>(_data[_index..]) * (isUtf16 ? 2 : 1); // WTF?
        _index += 4;
        string[] returnArray = new string[count];

        GetStringArray:
        int stringByteLength = MemoryMarshal.Read<int>(_data[_index..]) * (isUtf16 ? 2 : 1);
        _index += 4;

        string stringResult = isUtf16
            ? Encoding.Unicode.GetString(_dataPtr + _index, stringByteLength)
            : Encoding.UTF8.GetString(_dataPtr + _index, stringByteLength);
        _index += stringByteLength;

        returnArray[offset++] = stringResult;
        if (offset < count && *(_dataPtr + _index) != 0xFF) goto GetStringArray;

        return returnArray;
    }

    public int WriteStringList(Span<byte> buffer, Encoding encoding, List<string?> inputString)
    {
        int offset = 0, i = 0;

        WriteStringList:
        offset += WriteString(buffer[offset..], encoding, inputString[i++]);
        if (i < inputString.Count) goto WriteStringList;

        buffer[offset++] = 0xFF;
        return offset;
    }

#if NET
    public int WriteStringArray(Span<byte> buffer, Encoding encoding, string[] inputString)
#else
        public unsafe int WriteStringArray(Span<byte> buffer, Encoding encoding, string[] inputString)
#endif
    {
        bool isUtf16 = encoding.GetType() == Encoding.Unicode.GetType();
        byte strType = (byte)(isUtf16 ? 0xFE : 0xFD);
    #if NET
        MemoryMarshal.Write(buffer, strType);
    #else
            MemoryMarshal.Write(buffer, ref strType);
    #endif
        int offset = 1;

        int count                      = inputString.Length / (isUtf16 ? 2 : 1);
        int totalOfStringSize          = inputString.Sum(x => x.Length * (isUtf16 ? 2 : 1));
        int totalOfStringLengthSize    = inputString.Length * 4;
        int calculatedStringBufferSize = 4 + totalOfStringLengthSize + totalOfStringSize;
    #if NET
        MemoryMarshal.Write(buffer[offset..], count == 0 ? 0 : -calculatedStringBufferSize);
    #else
            int negativeStringBufferSize = count == 0 ? 0 : -calculatedStringBufferSize;
            MemoryMarshal.Write(buffer.Slice(offset), ref negativeStringBufferSize);
    #endif
        offset += 4;

        if (count == 0) goto WriteStringArrayEOF;
    #if NET
        MemoryMarshal.Write(buffer[offset..], count);
    #else
            MemoryMarshal.Write(buffer.Slice(offset), ref count);
    #endif
        offset += 4;

        int stringArrayIndex = 0;
        WriteStringArray:
    #if NET
        if (isUtf16)
            MemoryMarshal.Write(buffer[offset..], inputString[stringArrayIndex].Length);
        else
            MemoryMarshal.Write(buffer[offset..], (ushort)inputString[stringArrayIndex].Length);
        offset += isUtf16 ? 4 : 2;
        offset += encoding.GetBytes(inputString[stringArrayIndex++], buffer[offset..]);
        if (stringArrayIndex < inputString.Length) goto WriteStringArray;
    #else
            if (isUTF16)
            {
                int len = inputString[stringArrayIndex].Length;
                MemoryMarshal.Write(buffer.Slice(offset), ref len);
            }
            else
            {
                ushort len = (ushort)inputString[stringArrayIndex].Length;
                MemoryMarshal.Write(buffer.Slice(offset), ref len);
            }
            offset += isUTF16 ? 4 : 2;
            fixed (char* inputStringPtr = inputString[stringArrayIndex])
            fixed (byte* bufferPtr = buffer.Slice(offset))
            {
                int len = inputString[stringArrayIndex].Length;
                offset += encoding.GetBytes(inputStringPtr, len, bufferPtr, buffer.Length);
            }
            if (stringArrayIndex < inputString.Length) goto WriteStringArray;
    #endif

        WriteStringArrayEOF:
        buffer[offset++] = 0xFF;
        return offset;
    }

    private static ushort ReadUShort(ReadOnlySpan<byte> input, ref int i)
    {
        ushort result = MemoryMarshal.Read<ushort>(input[i..]);
        i += 2;
        return result;
    }

    private static int ReadInt(ReadOnlySpan<byte> input, ref int i)
    {
        int result = MemoryMarshal.Read<int>(input[i..]);
        i += 4;
        return result;
    }
}