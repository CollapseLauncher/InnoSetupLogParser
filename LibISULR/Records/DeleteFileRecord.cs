﻿using LibISULR.Flags;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibISULR.Records
{
    public class DeleteFileRecord : BasePathListRecord<DeleteFileFlags>
    {
        public DeleteFileRecord(DeleteFileFlags flags = DeleteFileFlags.DisableFsRedir)
            : base((int)flags)
        {
            Paths = new List<string>();
        }

        public DeleteFileRecord(int flags, byte[] data)
          : base(flags)
        {
            Paths = new BufferTools(data).GetStringList();
        }

        public static DeleteFileRecord Create(string path, DeleteFileFlags flags = DeleteFileFlags.DisableFsRedir)
        {
            DeleteFileRecord record = new DeleteFileRecord(flags);
            record.Paths.Add(path);
            return record;
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            BufferTools writter = new BufferTools(buffer);
            int offset = writter.WriteStringList(buffer, Encoding.Unicode, Paths);
            return offset;
        }

        public override List<string> Paths { get; }

        public override string Description
        {
            get { return $"{string.Join(", ", Paths)}; {Flags}"; }
        }

        public override RecordType Type
        {
            get { return RecordType.DeleteFile; }
        }
    }
}
