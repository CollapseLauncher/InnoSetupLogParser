using LibISULR.Flags;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibISULR.Records;

public class DeleteDirOrFilesRecord : BasePathListRecord<DeleteDirOrFilesFlags>
{
    public DeleteDirOrFilesRecord(DeleteDirOrFilesFlags flags = DeleteDirOrFilesFlags.DisableFsRedir)
        : base((int)flags)
    {
        Paths = new List<string?>();
    }

    public DeleteDirOrFilesRecord(int flags, byte[] data)
        : base(flags)
    {
        Paths = new BufferTools(data).GetStringList();
    }

    public static DeleteDirOrFilesRecord Create(string? path,
                                                DeleteDirOrFilesFlags flags = DeleteDirOrFilesFlags.IsDir |
                                                                              DeleteDirOrFilesFlags.DisableFsRedir)
    {
        var record = new DeleteDirOrFilesRecord(flags);
        record.Paths.Add(path);
        return record;
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var writer = new BufferTools(buffer);
        var offset = writer.WriteStringList(buffer, Encoding.Unicode, Paths);
        return offset;
    }

    public override List<string?> Paths { get; }

    public override string Description => $"{string.Join(", ", Paths)}; {Flags}";

    public override RecordType Type => RecordType.DeleteDirOrFiles;
}