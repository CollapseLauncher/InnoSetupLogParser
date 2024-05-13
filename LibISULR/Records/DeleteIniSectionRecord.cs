using LibISULR.Flags;
using System;

namespace LibISULR.Records;

public class DeleteIniSectionRecord : BaseRecord<IniFlags>
{
    private string filename;
    private string section;

    public DeleteIniSectionRecord(int flags, byte[] data)
        : base(flags)
    {
        var splitter = new BufferTools(data);
        Init(ref splitter);
        filename = "";
        section  = "";
    }

    protected virtual void Init(ref BufferTools splitter)
    {
        filename = splitter.ReadString()!;
        section  = splitter.ReadString()!;
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public string Filename => filename;

    public string Section => section;

    public override RecordType Type => RecordType.IniDeleteSection;

    public override string Description => $"File: \"{filename}\"; Section: \"{section}\"; Flags: {Flags}";
}