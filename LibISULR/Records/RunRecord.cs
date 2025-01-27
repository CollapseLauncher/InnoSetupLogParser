using LibISULR.Flags;
using System;
using System.Text;

namespace LibISULR.Records;

public class RunRecord : BasePathRecord<RunFlags>
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    private string? _path;
    private string? _runOnceId;
    private string? _verb;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    public RunRecord(int flags, byte[] data)
        : base(flags)
    {
        BufferTools splitter = new(data);
        _path       = splitter.ReadString();
        Args       = splitter.ReadString();
        WorkingDir = splitter.ReadString();
        _runOnceId  = splitter.ReadString();
        _verb       = splitter.ReadString();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        BufferTools stringWriter = new(buffer);
        int         offset       = stringWriter.WriteString(buffer, Encoding.Unicode, _path);
        offset           += stringWriter.WriteString(buffer[offset..], Encoding.Unicode, Args);
        offset           += stringWriter.WriteString(buffer[offset..], Encoding.Unicode, WorkingDir);
        offset           += stringWriter.WriteString(buffer[offset..], Encoding.Unicode, _runOnceId);
        offset           += stringWriter.WriteString(buffer[offset..], Encoding.Unicode, _verb);
        buffer[offset++] =  0xFF;
        return offset;
    }

    public override RecordType Type => RecordType.Run;

    public override string Description => $"File: \"{_path}\" Args: \"{Args}\"; At \"{WorkingDir}\"; Flags: {Flags}";

    public override string? Path => _path;

    public string? Args { get; }

    public string? WorkingDir { get; }
}