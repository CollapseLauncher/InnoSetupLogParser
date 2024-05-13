using System;
using System.Text;

namespace LibISULR.Records;

public class MutexCheckRecord : BaseRecord
{
    public MutexCheckRecord(int flags, byte[] data)
        : base(flags)
    {
        MutexName = new BufferTools(data).ReadString();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var writer = new BufferTools(buffer);
        var offset = writer.WriteString(buffer, Encoding.Unicode, MutexName);
        buffer[offset++] = 0xFF;
        return offset;
    }

    private string? MutexName { get; }

    public override RecordType Type => RecordType.MutexCheck;

    public override string Description => $"Mutex Name: {MutexName}";
}