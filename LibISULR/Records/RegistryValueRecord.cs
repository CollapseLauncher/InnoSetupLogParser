namespace LibISULR.Records;

public class RegistryValueRecord : RegistryKeyRecord
{
    public RegistryValueRecord(RecordType type, int flags, byte[] data)
        : base(type, flags, data)
    {
        Value = "";
    }

    protected override void Init(ref BufferTools splitter)
    {
        base.Init(ref splitter);
        Value = splitter.ReadString()!;
    }

    public override string Description => $"View: {View}; Hive: {Hive}; Path: {Path}; Value: {Value}";

    public string Value { get; private set; }
}