using LibISULR.Flags;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibISULR.Records;

public class DeleteFileRecord : BasePathListRecord<DeleteFileFlags>
{
    public DeleteFileRecord(DeleteFileFlags flags = DeleteFileFlags.DisableFsRedir)
        : base((int)flags)
    {
        Paths = [];
    }

    public DeleteFileRecord(int flags, byte[] data)
        : base(flags)
    {
        Paths = new BufferTools(data).GetStringList();
    }

    public static DeleteFileRecord Create(string? path, DeleteFileFlags flags = DeleteFileFlags.DisableFsRedir)
    {
        DeleteFileRecord record = new(flags);
        record.Paths.Add(path);
        return record;
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        BufferTools writer = new(buffer);
        int         offset = writer.WriteStringList(buffer, Encoding.Unicode, Paths);
        return offset;
    }

    public override List<string?> Paths { get; }

    public override string Description => $"{string.Join(", ", Paths)}; {Flags}";

    public override RecordType Type => RecordType.DeleteFile;
}