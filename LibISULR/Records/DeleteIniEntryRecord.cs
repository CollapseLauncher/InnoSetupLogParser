﻿namespace LibISULR.Records
{
    public class DeleteIniEntryRecord : DeleteIniSectionRecord
    {
        private string entry;

        public DeleteIniEntryRecord(int flags, byte[] data)
          : base(flags, data)
        {

        }

        protected override void Init(ref BufferTools splitter)
        {
            base.Init(ref splitter);
            entry = splitter.ReadString();
        }

        public string Entry
        {
            get { return entry; }
        }

        public override RecordType Type
        {
            get { return RecordType.IniDeleteEntry; }
        }

        public override string Description
        {
            get { return $"File: \"{Filename}\"; Section: \"{Section}\"; Entry: {entry}; Flags: {Flags}"; }
        }
    }
}
