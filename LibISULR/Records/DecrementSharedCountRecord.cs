using LibISULR.Flags;
using System;

namespace LibISULR.Records;

public class DecrementSharedCountRecord : BasePathRecord<DecrementSharedCountFlags>
{
    public DecrementSharedCountRecord(int extra, byte[] data)
        : base(extra)
    {
        Path = new BufferTools(data).ReadString();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override string? Path { get; }

    public override RecordType Type => RecordType.DecrementSharedCount;

    public override string Description => $"Path: {Path}";
}