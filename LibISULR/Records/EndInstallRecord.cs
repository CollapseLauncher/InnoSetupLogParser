using System;

namespace LibISULR.Records;

public class EndInstallRecord : BaseRecord
{
    public EndInstallRecord(int flags, byte[] data)
        : base(flags)
    {
        Time = new BufferTools(data).ReadDateTime();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var offset = new BufferTools(buffer).WriteDateTime(buffer, Time);
        buffer[offset++] = 0xFF;
        return offset;
    }

    public DateTime Time { get; }

    public override RecordType Type => RecordType.EndInstall;

    public override string Description => $"At: {Time}";
}