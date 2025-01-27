using LibISULR.Flags;
using Microsoft.Win32;
using System;
using System.Text;
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo

namespace LibISULR.Records;

public class RegistryKeyRecord : BaseRecord
{
    public RegistryKeyRecord(RecordType type, int flags, byte[] data)
        : base(flags)
    {
        Type = type;

        BufferTools splitter = new(data);
        Init(ref splitter);

        RegFlags f = (RegFlags)flags;
        View = (f & RegFlags.Reg64BitKey) != 0 ? RegistryView.Registry64 : RegistryView.Registry32;
        Hive = (RegistryHive)(f & RegFlags.RegKeyHandleMask);
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        int offset = new BufferTools(buffer).WriteString(buffer, Encoding.Unicode, Path);
        buffer[offset++] = 0xFF;
        return offset;
    }

    protected virtual void Init(ref BufferTools splitter)
    {
        Path = splitter.ReadString();
    }

    public string? Path { get; private set; }

    public RegistryHive Hive { get; }

    public RegistryView View { get; }

    public override RecordType Type { get; }

    public override string Description => $"View: {View}; Hive: {Hive}; Path: {Path}";
}