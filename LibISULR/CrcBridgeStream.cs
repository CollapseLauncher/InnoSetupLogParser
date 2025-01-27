using System;
using System.Data;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.InnoUninstallerLog;

public class CrcBridgeStream : Stream
{
    private const int DefaultBufferLen = 4 << 10;

    private readonly Stream _sourceStream;
    private readonly bool   _leaveOpen;

    // ReSharper disable FieldCanBeMadeReadOnly.Local
    private byte[] _blockBuffer;
    private bool   _skipCrcCheck;
    private bool   _isWriteMode;
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    private int _dataAvailable;
    private int _dataPos;

    public CrcBridgeStream(Stream sourceStream, bool leaveOpen = false, bool skipCrcCheck = false,
                           bool   isWriteMode = false)
    {
        _sourceStream = sourceStream;
        _leaveOpen    = leaveOpen;
        _skipCrcCheck = skipCrcCheck;
        _isWriteMode  = isWriteMode;

        _blockBuffer = new byte[DefaultBufferLen];
        if (!isWriteMode) FillBuffer();
    }

    ~CrcBridgeStream()
    {
        Dispose();
    }

    private void FlushBuffer()
    {
        if (_dataPos != 0) FinalizeBlock();

        _dataAvailable = DefaultBufferLen;
        _blockBuffer   = new byte[_dataAvailable];

        _dataPos = 0;
    }

    public void FinalizeBlock()
    {
        uint crcHash    = Crc32.HashToUInt32(_blockBuffer.AsSpan(0, _dataPos));
        int writtenLen = _dataPos;
        int notSize    = ~writtenLen;

        UninstallCrcHeader blockHeader = new()
        {
            Crc     = crcHash,
            NotSize = (uint)notSize,
            Size    = (uint)writtenLen
        };

        int    offsetDummy       = 0;
        int    blockHeaderSizeOf = Marshal.SizeOf<UninstallCrcHeader>();
        byte[] headerBuffer      = new byte[blockHeaderSizeOf];
        InnoUninstallLog.TrySerializeStruct(blockHeader, ref offsetDummy, blockHeaderSizeOf, headerBuffer);
    #if NET
        _sourceStream.Write(headerBuffer);
    #else
            sourceStream.Write(headerBuffer, 0, headerBuffer.Length);
    #endif
        _sourceStream.Write(_blockBuffer, 0, writtenLen);
    }

    private void FillBuffer()
    {
        // Get the structure of the block info (including its Crc)
        InnoUninstallLog.ReadTStructure(_sourceStream, out UninstallCrcHeader blockHeader);
        if (_blockBuffer.Length != blockHeader.Size)
            _blockBuffer = new byte[blockHeader.Size];

        // SANITY CHECK: Check the buffer size if it's matching or not
        if (!_skipCrcCheck && blockHeader.Size != ~blockHeader.NotSize)
            throw new Exception($"File buffer size record is not match " +
                                $"from {blockHeader.Size} to ~{blockHeader.NotSize} (or {~blockHeader.NotSize})");

        // Try load the buffer and check for the Crc
        _dataPos = 0;
    #if NET
        _dataAvailable = _sourceStream.ReadAtLeast(_blockBuffer, (int)blockHeader.Size);
    #else
            dataAvailable = sourceStream.Read(blockBuffer, 0, (int)blockHeader.Size);
    #endif
        uint crcHash = Crc32.HashToUInt32(_blockBuffer.AsSpan(0, _dataAvailable));

        // Check the Crc
        if (!_skipCrcCheck && crcHash != blockHeader.Crc)
            throw new DataException($"Header Crc32 isn't match! Getting {crcHash} while expecting {blockHeader.Crc}");
    }

    private int ReadBytes(byte[] buffer, int offset, int count)
    {
        if (_isWriteMode)
            throw new InvalidOperationException("You can't do read operation while stream is in write mode!");
        while (count > 0)
        {
            if (_dataAvailable == 0)
                FillBuffer();

            int dataToRead = count;
            if (dataToRead > _dataAvailable)
                dataToRead = _dataAvailable;

            Array.Copy(_blockBuffer, _dataPos, buffer, offset, dataToRead);
            offset        += dataToRead;
            count         -= dataToRead;
            _dataPos       += dataToRead;
            _dataAvailable -= dataToRead;
        }

        return offset;
    }

    private int ReadBytes(Span<byte> buffer)
    {
        if (_isWriteMode)
            throw new InvalidOperationException("You can't do read operation while stream is in write mode!");
        int count  = buffer.Length;
        int offset = 0;
        while (count > 0)
        {
            if (_dataAvailable == 0)
                FillBuffer();

            int dataToRead = count;
            if (dataToRead > _dataAvailable)
                dataToRead = _dataAvailable;

            _blockBuffer.AsSpan(_dataPos, dataToRead)
                       .CopyTo(buffer.Slice(offset, dataToRead));

            offset        += dataToRead;
            count         -= dataToRead;
            _dataPos       += dataToRead;
            _dataAvailable -= dataToRead;
        }

        return offset;
    }

    private void WriteBytes(byte[] buffer, int offset, int count)
    {
        if (!_isWriteMode)
            throw new InvalidOperationException("You can't do write operation while stream isn't in write mode!");
        while (count > 0)
        {
            if (_dataAvailable == 0)
                FlushBuffer();

            int dataToWrite = count;
            if (dataToWrite > _dataAvailable)
                dataToWrite = _dataAvailable;

            Array.Copy(buffer, offset, _blockBuffer, _dataPos, dataToWrite);
            offset        += dataToWrite;
            count         -= dataToWrite;
            _dataPos       += dataToWrite;
            _dataAvailable -= dataToWrite;
        }
    }

    private void WriteBytes(ReadOnlySpan<byte> buffer)
    {
        if (!_isWriteMode)
            throw new InvalidOperationException("You can't do write operation while stream isn't in write mode!");
        int count  = buffer.Length;
        int offset = 0;
        while (count > 0)
        {
            if (_dataAvailable == 0)
                FlushBuffer();

            int dataToWrite = count;
            if (dataToWrite > _dataAvailable)
                dataToWrite = _dataAvailable;

            buffer.Slice(offset, dataToWrite)
                  .CopyTo(_blockBuffer.AsSpan(_dataPos, dataToWrite));

            offset        += dataToWrite;
            count         -= dataToWrite;
            _dataPos       += dataToWrite;
            _dataAvailable -= dataToWrite;
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

    public override bool CanRead => !_isWriteMode;

    public override bool CanSeek => false;

    public override bool CanWrite => _isWriteMode;

    public override void Flush()
    {
        _sourceStream.Flush();
    }

    public override long Length => _sourceStream.Length;

    public override long Position
    {
        get => _sourceStream.Position - _dataAvailable;
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
            if (!_leaveOpen)
                _sourceStream.Dispose();
    }

#if NET
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!_leaveOpen) await _sourceStream.DisposeAsync();
    }
#endif
}