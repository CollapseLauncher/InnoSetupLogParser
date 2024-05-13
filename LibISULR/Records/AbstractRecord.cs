using System;

namespace LibISULR.Records;

public class AbstractRecord : BaseRecord
{
    private RecordType type;
    private uint       extraData;
    private byte[]     data;

    public AbstractRecord(RecordType type, int extraData, byte[] data)
        : base(extraData)
    {
        this.type      = type;
        this.extraData = (uint)extraData;
        this.data      = data;
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override RecordType Type => type;

    public uint ExtraData => extraData;

    public byte[] Data => data;

    public override string Description => $"Extra flags: {extraData}";
}