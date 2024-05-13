using System;
using System.Data;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.InnoUninstallerLog;

public class CrcBridgeStream : Stream
{
    private const int _defaultBufferLen = 4 << 10;

    private readonly Stream sourceStream;
    private readonly bool   leaveOpen;

    // ReSharper disable FieldCanBeMadeReadOnly.Local
    private byte[] blockBuffer;
    private bool   skipCrcCheck;
    private bool   isWriteMode;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private int dataAvailable;
    private int dataPos;

    public CrcBridgeStream(Stream sourceStream, bool leaveOpen = false, bool skipCrcCheck = false,
                           bool   isWriteMode = false)
    {
        this.sourceStream = sourceStream;
        this.leaveOpen    = leaveOpen;
        this.skipCrcCheck = skipCrcCheck;
        this.isWriteMode  = isWriteMode;

        blockBuffer = new byte[_defaultBufferLen];
        if (!isWriteMode) FillBuffer();
    }

    ~CrcBridgeStream()
    {
        Dispose();
    }

    private void FlushBuffer()
    {
        if (dataPos != 0) FinalizeBlock();

        dataAvailable = _defaultBufferLen;
        blockBuffer   = new byte[dataAvailable];

        dataPos = 0;
    }

    public void FinalizeBlock()
    {
        var crcHash    = Crc32.HashToUInt32(blockBuffer.AsSpan(0, dataPos));
        var writtenLen = dataPos;
        var notSize    = ~writtenLen;

        var blockHeader = new TUninstallCrcHeader
        {
            Crc     = crcHash,
            NotSize = (uint)notSize,
            Size    = (uint)writtenLen
        };

        var offsetDummy       = 0;
        var blockHeaderSizeOf = Marshal.SizeOf<TUninstallCrcHeader>();
        var headerBuffer      = new byte[blockHeaderSizeOf];
        InnoUninstallLog.TrySerializeStruct(blockHeader, ref offsetDummy, blockHeaderSizeOf, headerBuffer);
    #if NET
        sourceStream.Write(headerBuffer);
    #else
            sourceStream.Write(headerBuffer, 0, headerBuffer.Length);
    #endif
        sourceStream.Write(blockBuffer, 0, writtenLen);
    }

    private void FillBuffer()
    {
        // Get the structure of the block info (including its Crc)
        InnoUninstallLog.ReadTStructure(sourceStream, out TUninstallCrcHeader blockHeader);
        if (blockBuffer.Length != blockHeader.Size)
            blockBuffer = new byte[blockHeader.Size];

        // SANITY CHECK: Check the buffer size if it's matching or not
        if (!skipCrcCheck && blockHeader.Size != ~blockHeader.NotSize)
            throw new Exception($"File buffer size record is not match " +
                                $"from {blockHeader.Size} to ~{blockHeader.NotSize} (or {~blockHeader.NotSize})");

        // Try load the buffer and check for the Crc
        dataPos = 0;
    #if NET
        dataAvailable = sourceStream.ReadAtLeast(blockBuffer, (int)blockHeader.Size);
    #else
            dataAvailable = sourceStream.Read(blockBuffer, 0, (int)blockHeader.Size);
    #endif
        var crcHash = Crc32.HashToUInt32(blockBuffer.AsSpan(0, dataAvailable));

        // Check the Crc
        if (!skipCrcCheck && crcHash != blockHeader.Crc)
            throw new DataException($"Header Crc32 isn't match! Getting {crcHash} while expecting {blockHeader.Crc}");
    }

    private int ReadBytes(byte[] buffer, int offset, int count)
    {
        if (isWriteMode)
            throw new InvalidOperationException($"You can't do read operation while stream is in write mode!");
        while (count > 0)
        {
            if (dataAvailable == 0)
                FillBuffer();

            var dataToRead = count;
            if (dataToRead > dataAvailable)
                dataToRead = dataAvailable;

            Array.Copy(blockBuffer, dataPos, buffer, offset, dataToRead);
            offset        += dataToRead;
            count         -= dataToRead;
            dataPos       += dataToRead;
            dataAvailable -= dataToRead;
        }

        return offset;
    }

    private int ReadBytes(Span<byte> buffer)
    {
        if (isWriteMode)
            throw new InvalidOperationException($"You can't do read operation while stream is in write mode!");
        var count  = buffer.Length;
        var offset = 0;
        while (count > 0)
        {
            if (dataAvailable == 0)
                FillBuffer();

            var dataToRead = count;
            if (dataToRead > dataAvailable)
                dataToRead = dataAvailable;

            blockBuffer.AsSpan(dataPos, dataToRead)
                       .CopyTo(buffer.Slice(offset, dataToRead));

            offset        += dataToRead;
            count         -= dataToRead;
            dataPos       += dataToRead;
            dataAvailable -= dataToRead;
        }

        return offset;
    }

    private void WriteBytes(byte[] buffer, int offset, int count)
    {
        if (!isWriteMode)
            throw new InvalidOperationException($"You can't do write operation while stream isn't in write mode!");
        while (count > 0)
        {
            if (dataAvailable == 0)
                FlushBuffer();

            var dataToWrite = count;
            if (dataToWrite > dataAvailable)
                dataToWrite = dataAvailable;

            Array.Copy(buffer, offset, blockBuffer, dataPos, dataToWrite);
            offset        += dataToWrite;
            count         -= dataToWrite;
            dataPos       += dataToWrite;
            dataAvailable -= dataToWrite;
        }
    }

    private void WriteBytes(ReadOnlySpan<byte> buffer)
    {
        if (!isWriteMode)
            throw new InvalidOperationException($"You can't do write operation while stream isn't in write mode!");
        var count  = buffer.Length;
        var offset = 0;
        while (count > 0)
        {
            if (dataAvailable == 0)
                FlushBuffer();

            var dataToWrite = count;
            if (dataToWrite > dataAvailable)
                dataToWrite = dataAvailable;

            buffer.Slice(offset, dataToWrite)
                  .CopyTo(blockBuffer.AsSpan(dataPos, dataToWrite));

            offset        += dataToWrite;
            count         -= dataToWrite;
            dataPos       += dataToWrite;
            dataAvailable -= dataToWrite;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadBytes(buffer, offset, count);
    }
#if NET
    public override int Read(Span<byte> buffer)
    {
        return ReadBytes(buffer);
    }
#endif
    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteBytes(buffer, offset, count);
    }
#if NET
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        WriteBytes(buffer);
    }
#endif

    public override bool CanRead => !isWriteMode;

    public override bool CanSeek => false;

    public override bool CanWrite => isWriteMode;

    public override void Flush()
    {
        sourceStream.Flush();
    }

    public override long Length => sourceStream.Length;

    public override long Position
    {
        get => sourceStream.Position - dataAvailable;
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            if (!leaveOpen)
                sourceStream.Dispose();
    }

#if NET
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!leaveOpen) await sourceStream.DisposeAsync();
    }
#endif
}