﻿using System;
using System.Text;

namespace LibISULR.Records;

public class StartInstallRecord : BaseRecord
{
    public StartInstallRecord(int flags, byte[] data)
        : base(flags)
    {
        var splitter = new BufferTools(data);
        ComputerName   = splitter.ReadString();
        UserName       = splitter.ReadString();
        ApplicationDir = splitter.ReadString();
        Time           = splitter.ReadDateTime();
    }

    public override int UpdateContent(Span<byte> buffer)
    {
        var stringWriter = new BufferTools(buffer);
        var offset       = stringWriter.WriteString(buffer, Encoding.Unicode, ComputerName);
        offset                  += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, UserName);
        offset                  += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, ApplicationDir);
        offset                  += stringWriter.WriteDateTime(buffer.Slice(offset), Time);
        buffer.Slice(offset)[0] =  0xFF;
        return offset + 1;
    }

    public string? ComputerName { get; }

    public string? UserName { get; }

    public string? ApplicationDir { get; }

    public DateTime Time { get; }

    public override RecordType Type => RecordType.StartInstall;

    public override string Description =>
        $"Computer: {ComputerName}; User: {UserName}; Dir: {ApplicationDir}; At: {Time}";
}