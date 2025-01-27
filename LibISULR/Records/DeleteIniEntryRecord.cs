namespace LibISULR.Records;

public class DeleteIniEntryRecord : DeleteIniSectionRecord
{
    public DeleteIniEntryRecord(int flags, byte[] data)
        : base(flags, data)
    {
        Entry = "";
    }

    protected override void Init(ref BufferTools splitter)
    {
        base.Init(ref splitter);
        Entry = splitter.ReadString()!;
    }

    public string Entry { get; private set; }

    public override RecordType Type => RecordType.IniDeleteEntry;

    public override string Description =>
        $"File: \"{Filename}\"; Section: \"{Section}\"; Entry: {Entry}; Flags: {Flags}";
}