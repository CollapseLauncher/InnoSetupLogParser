using System;
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo

namespace LibISULR.Records;

public class AbstractRecord(RecordType type, int extraData, byte[] data) : BaseRecord(extraData)
{
    public override int UpdateContent(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override RecordType Type { get; } = type;

    public uint ExtraData { get; } = (uint)extraData;

    public byte[] Data { get; } = data;

    public override string Description => $"Extra flags: {ExtraData}";
}