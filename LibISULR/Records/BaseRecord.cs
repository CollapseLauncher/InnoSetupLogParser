using System;
using System.Collections.Generic;

namespace LibISULR.Records;

public abstract class BaseRecord
{
    public BaseRecord(int flagsNum)
    {
        FlagsNum = flagsNum;
    }

    public int FlagsNum { get; private set; }

    public abstract RecordType Type { get; }

    public abstract string Description { get; }

    public abstract int UpdateContent(Span<byte> buffer);

    public override string ToString()
    {
        return $"Type: {Type}. Desc: {Description}";
    }
}

public abstract class BasePathListRecord<TFlags> : BaseRecord<TFlags>
    where TFlags : Enum
{
    protected BasePathListRecord(int flags)
        : base(flags)
    {
    }

    public abstract List<string?> Paths { get; }
}

public abstract class BasePathRecord<TFlags> : BaseRecord<TFlags>
    where TFlags : Enum
{
    protected BasePathRecord(int flags)
        : base(flags)
    {
    }

    public abstract string? Path { get; }
}

public abstract class BaseRecord<TFlags> : BaseRecord
    where TFlags : Enum
{
    protected BaseRecord(int flags)
        : base(flags)
    {
        Flags = (TFlags)Enum.ToObject(typeof(TFlags), flags);
    }

    public TFlags Flags { get; private set; }
}