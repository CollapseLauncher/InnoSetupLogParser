using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable RedundantUnsafeContext

namespace LibISULR;

public ref struct BufferTools
{
    private        ReadOnlySpan<byte> data;
    private unsafe byte*              _dataPtr;
    private        int                index;

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
        this.data = data;
        fixed (byte* dataPtr = this.data)
        {
            _dataPtr = dataPtr;
        }

        index = 0;
    }

    private int ReadLength(out bool eof)
    {
        var type = data[index++];
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
                   0xFD => ReadUShort(data, ref index), // UTF-8 string length Type
                   0xFE => ReadInt(data, ref index), // Unicode/UTF-16 string length Type
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
        var  length = ReadLength(out eof);
        if (eof)
            return null;

        string result;

        if (length < 0)
        {
            length = -length;
            result = Encoding.Unicode.GetString(_dataPtr + index, length);
        }
        else if (length == 0)
        {
            return string.Empty;
        }
        else
        {
            result = Encoding.Default.GetString(_dataPtr + index, length);
        }

        index += length;
        return result;
    }

    public unsafe int WriteString(Span<byte> buffer, Encoding encoding, string? inputString)
    {
        var isUTF16 = encoding.GetType() == Encoding.Unicode.GetType();
        var strType = (byte)(isUTF16 ? 0xFE : 0xFD);
    #if NET
        MemoryMarshal.Write(buffer, strType);
    #else
            MemoryMarshal.Write(buffer, ref strType);
    #endif

        var offset = 1;
        if (inputString == null) return 0;
        var strByteLen = inputString.Length * (isUTF16 ? 2 : 1);

    #if NET
        if (isUTF16)
            MemoryMarshal.Write(buffer.Slice(offset), -strByteLen);
        else
            MemoryMarshal.Write(buffer.Slice(offset), -(ushort)strByteLen);

        offset += isUTF16 ? 4 : 2;
        offset += encoding.GetBytes(inputString, buffer.Slice(offset));
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
        var isDateEmpty = DateTime.MinValue == inputDateTime;
        var lenType     = (byte)(isDateEmpty ? 0xFF : 0xFE);
    #if NET
        MemoryMarshal.Write(buffer, lenType);
    #else
            MemoryMarshal.Write(buffer, ref lenType);
    #endif

        var offset = 1;
        if (isDateEmpty) return offset;

        Span<ushort> dateTimeInUShorts = new ushort[8];
    #if NET
        MemoryMarshal.Write(buffer.Slice(offset), -(dateTimeInUShorts.Length * 2));
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

        var dateInTimeBytes = MemoryMarshal.AsBytes(dateTimeInUShorts);
        dateInTimeBytes.CopyTo(buffer.Slice(offset));
        offset += dateTimeInUShorts.Length * 2;

        return offset;
    }

    public DateTime ReadDateTime()
    {
        bool eof;
        var  length = ReadLength(out eof);
        if (eof)
            return DateTime.MinValue;
        if (length < 0)
            length = -length;

        DateTime result;

        if (length >= 16)
        {
            var i = index;

            var year  = ReadUShort(data, ref i);
            var month = ReadUShort(data, ref i);
            i += 2; //ushort dow = ReadUShort(data,ref i);
            var day    = ReadUShort(data, ref i);
            var hour   = ReadUShort(data, ref i);
            var minute = ReadUShort(data, ref i);
            var second = ReadUShort(data, ref i);
            var ms     = ReadUShort(data, ref i);

            result = new DateTime(year, month, day, hour, minute, second, ms, DateTimeKind.Local);
        }
        else
        {
            result = DateTime.MinValue;
        }

        index += length;
        return result;
    }

    public int WriteBytes(Span<byte> buffer, ReadOnlySpan<byte> source)
    {
        byte lenType = 0xFE;
    #if NET
        MemoryMarshal.Write(buffer, lenType);

        var offset = 1;
        MemoryMarshal.Write(buffer.Slice(offset), -source.Length);
    #else
            MemoryMarshal.Write(buffer, ref lenType);

            int offset = 1;
            int sourceLengthNegative = -source.Length;
            MemoryMarshal.Write(buffer.Slice(offset), ref sourceLengthNegative);
    #endif
        offset += 4;
        if (source.Length == 0) return offset;

        source.CopyTo(buffer.Slice(offset));
        offset += source.Length;
        return offset;
    }

    public unsafe byte[]? ReadBytes()
    {
        bool eof;
        var  length = ReadLength(out eof);
        if (eof)
            return null;
        if (length < 0)
            length = -length;

        if (length == 0) return Array.Empty<byte>();

        var result = new byte[length];
        fixed (byte* resultPtr = result)
        {
            Buffer.MemoryCopy(_dataPtr + index, resultPtr, length, length);
        }

        index += length;

        return result;
    }

    private bool IsEnd => index >= data.Length;

    public List<string?> GetStringList()
    {
        var returnList = new List<string?>();
        while (!IsEnd)
        {
            var str = ReadString();
            if (str != null)
                returnList.Add(str);
        }

        return returnList;
    }

    public unsafe string[] GetStringArray()
    {
        var type = data[index++];
        if (type == 0xFF) // If the type is 0xFF (end), then set eof = true and return 0
            return Array.Empty<string>();

    #if NET
        var byteLength = type switch
                         {
                             0xFD => ReadUShort(data, ref index), // UTF-8 string length Type
                             0xFE => ReadInt(data, ref index), // Unicode/UTF-16 string length Type
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

        if (byteLength == 0) return Array.Empty<string>();

        var isUTF16 = type == 0xFE;

        var offset = 0;
        var count  = MemoryMarshal.Read<int>(data.Slice(index)) * (isUTF16 ? 2 : 1); // WTF?
        index += 4;
        var returnArray = new string[count];

        GetStringArray:
        var stringByteLength = MemoryMarshal.Read<int>(data.Slice(index)) * (isUTF16 ? 2 : 1);
        index += 4;

        var stringResult = isUTF16
            ? Encoding.Unicode.GetString(_dataPtr + index, stringByteLength)
            : Encoding.UTF8.GetString(_dataPtr + index, stringByteLength);
        index += stringByteLength;

        returnArray[offset++] = stringResult;
        if (offset < count && *(_dataPtr + index) != 0xFF) goto GetStringArray;

        return returnArray;
    }

    public int WriteStringList(Span<byte> buffer, Encoding encoding, List<string?> inputString)
    {
        int offset = 0, i = 0;

        WriteStringList:
        offset += WriteString(buffer.Slice(offset), encoding, inputString[i++]);
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
        var isUTF16 = encoding.GetType() == Encoding.Unicode.GetType();
        var strType = (byte)(isUTF16 ? 0xFE : 0xFD);
    #if NET
        MemoryMarshal.Write(buffer, strType);
    #else
            MemoryMarshal.Write(buffer, ref strType);
    #endif
        var offset = 1;

        var count                      = inputString.Length / (isUTF16 ? 2 : 1);
        var totalOfStringSize          = inputString.Sum(x => x.Length * (isUTF16 ? 2 : 1));
        var totalOfStringLengthSize    = inputString.Length * 4;
        var calculatedStringBufferSize = 4 + totalOfStringLengthSize + totalOfStringSize;
    #if NET
        MemoryMarshal.Write(buffer.Slice(offset), count == 0 ? 0 : -calculatedStringBufferSize);
    #else
            int negativeStringBufferSize = count == 0 ? 0 : -calculatedStringBufferSize;
            MemoryMarshal.Write(buffer.Slice(offset), ref negativeStringBufferSize);
    #endif
        offset += 4;

        if (count == 0) goto WriteStringArrayEOF;
    #if NET
        MemoryMarshal.Write(buffer.Slice(offset), count);
    #else
            MemoryMarshal.Write(buffer.Slice(offset), ref count);
    #endif
        offset += 4;

        var stringArrayIndex = 0;
        WriteStringArray:
    #if NET
        if (isUTF16)
            MemoryMarshal.Write(buffer.Slice(offset), inputString[stringArrayIndex].Length);
        else
            MemoryMarshal.Write(buffer.Slice(offset), (ushort)inputString[stringArrayIndex].Length);
        offset += isUTF16 ? 4 : 2;
        offset += encoding.GetBytes(inputString[stringArrayIndex++], buffer.Slice(offset));
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

    private ushort ReadUShort(ReadOnlySpan<byte> input, ref int i)
    {
        var result = MemoryMarshal.Read<ushort>(input.Slice(i));
        i += 2;
        return result;
    }

    private int ReadInt(ReadOnlySpan<byte> input, ref int i)
    {
        var result = MemoryMarshal.Read<int>(input.Slice(i));
        i += 4;
        return result;
    }
}