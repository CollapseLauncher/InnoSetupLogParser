namespace LibISULR.Records;

public class DeleteIniEntryRecord : DeleteIniSectionRecord
{
    private string entry;

    public DeleteIniEntryRecord(int flags, byte[] data)
        : base(flags, data)
    {
        entry = "";
    }

    protected override void Init(ref BufferTools splitter)
    {
        base.Init(ref splitter);
        entry = splitter.ReadString()!;
    }

    public string Entry => entry;

    public override RecordType Type => RecordType.IniDeleteEntry;

    public override string Description =>
        $"File: \"{Filename}\"; Section: \"{Section}\"; Entry: {entry}; Flags: {Flags}";
}