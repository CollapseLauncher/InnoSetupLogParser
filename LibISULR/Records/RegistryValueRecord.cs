namespace LibISULR.Records;

public class RegistryValueRecord : RegistryKeyRecord
{
    private string value;

    public RegistryValueRecord(RecordType type, int flags, byte[] data)
        : base(type, flags, data)
    {
        value = "";
    }

    protected override void Init(ref BufferTools splitter)
    {
        base.Init(ref splitter);
        value = splitter.ReadString()!;
    }

    public override string Description => $"View: {View}; Hive: {Hive}; Path: {Path}; Value: {value}";

    public string Value => value;
}