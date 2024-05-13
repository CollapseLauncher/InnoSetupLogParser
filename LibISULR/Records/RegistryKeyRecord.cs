using LibISULR.Flags;
using Microsoft.Win32;
using System;
using System.Text;

namespace LibISULR.Records;

public class RegistryKeyRecord : BaseRecord
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    private RecordType   type;
    private string?      path;
    private RegistryHive hive;
    private RegistryView view;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    public RegistryKeyRecord(RecordType type, int flags, byte[] data)
        : base(flags)
    {
        this.type = type;

        var splitter = new BufferTools(data);
        Init(ref splitter);

        var f = (RegFlags)flags;
        view = (f & RegFlags.Reg_64BitKey) != 0 ? RegistryView.Registry64 : RegistryView.Registry32;
        hive = (RegistryHive)(f & RegFlags.Reg_KeyHandleMask);
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var offset = new BufferTools(buffer).WriteString(buffer, Encoding.Unicode, path);
        buffer[offset++] = 0xFF;
        return offset;
    }

    protected virtual void Init(ref BufferTools splitter)
    {
        path = splitter.ReadString();
    }

    public string? Path => path;

    public RegistryHive Hive => hive;

    public RegistryView View => view;

    public override RecordType Type => type;

    public override string Description => $"View: {view}; Hive: {hive}; Path: {path}";
}