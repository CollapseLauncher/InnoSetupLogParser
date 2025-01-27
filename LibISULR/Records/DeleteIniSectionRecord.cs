using LibISULR.Flags;
using System;
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable VirtualMemberCallInConstructor

namespace LibISULR.Records;

public class DeleteIniSectionRecord : BaseRecord<IniFlags>
{
    public DeleteIniSectionRecord(int flags, byte[] data)
        : base(flags)
    {
        BufferTools splitter = new(data);
        Init(ref splitter);
        Filename = "";
        Section  = "";
    }

    protected virtual void Init(ref BufferTools splitter)
    {
        Filename = splitter.ReadString()!;
        Section  = splitter.ReadString()!;
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public string Filename { get; private set; }

    public string Section { get; private set; }

    public override RecordType Type => RecordType.IniDeleteSection;

    public override string Description => $"File: \"{Filename}\"; Section: \"{Section}\"; Flags: {Flags}";
}