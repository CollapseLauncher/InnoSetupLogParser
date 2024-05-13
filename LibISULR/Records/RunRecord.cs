using LibISULR.Flags;
using System;
using System.Text;

namespace LibISULR.Records;

public class RunRecord : BasePathRecord<RunFlags>
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    private string? path;
    private string? args;
    private string? workingDir;
    private string? runOnceId;
    private string? verb;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    public RunRecord(int flags, byte[] data)
        : base(flags)
    {
        var splitter = new BufferTools(data);
        path       = splitter.ReadString();
        args       = splitter.ReadString();
        workingDir = splitter.ReadString();
        runOnceId  = splitter.ReadString();
        verb       = splitter.ReadString();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var stringWriter = new BufferTools(buffer);
        var offset       = stringWriter.WriteString(buffer, Encoding.Unicode, path);
        offset           += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, args);
        offset           += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, workingDir);
        offset           += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, runOnceId);
        offset           += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, verb);
        buffer[offset++] =  0xFF;
        return offset;
    }

    public override RecordType Type => RecordType.Run;

    public override string Description => $"File: \"{path}\" Args: \"{args}\"; At \"{workingDir}\"; Flags: {Flags}";

    public override string? Path => path;

    public string? Args => args;

    public string? WorkingDir => workingDir;
}