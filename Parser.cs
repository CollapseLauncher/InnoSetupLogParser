using LibISULR;
using LibISULR.Records;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable CollectionNeverQueried.Global

namespace Hi3Helper.EncTool.Parser.InnoUninstallerLog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Size = 0x1C0)]
    public struct UninstallLogHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
        private byte[] ID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
        private byte[] AppId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
        private byte[] AppName;
        public int Version;
        public int RecordsCount;
        public int FileEndOffset;
        public int UninstallFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x6C)]
        public byte[] ReservedHeaderBytes;
        public uint Crc;

        public bool IsLog64Bit
        {
            get => ReadStringFromByte(ID).Equals(InnoUninstallLog.X64IdSignature);
            set
            {
                string signatureToWrite = value ? InnoUninstallLog.X64IdSignature : InnoUninstallLog.X86IdSignature;
                WriteStringToByte(ref signatureToWrite, ID);
            }
        }

        public string AppIdStr
        {
            get => ReadStringFromByte(AppId);
            set => WriteStringToByte(ref value, AppId);
        }

        public string AppNameStr
        {
            get => ReadStringFromByte(AppName);
            set => WriteStringToByte(ref value, AppName);
        }

#if NET
        private string ReadStringFromByte(byte[] target)
        {
            int offset = Array.IndexOf<byte>(target, 0);
            string result = Encoding.UTF8.GetString(target.AsSpan(0, offset));
            return result;
        }

        private void WriteStringToByte(ref string input, byte[] target)
        {
            if (input.Length > target.Length) throw new ArgumentOutOfRangeException($"The input string cannot be more than {target.Length} characters!");
            int writtenOffset = Encoding.UTF8.GetBytes(input, target);
            target.AsSpan(writtenOffset).Fill(0);
        }
#else
        private unsafe string ReadStringFromByte(byte[] target)
        {
            int offset = Array.IndexOf<byte>(target, 0);
            fixed (byte* ptr = target)
            {
                string result = Encoding.UTF8.GetString(ptr, offset);
                return result;
            }
        }

        private unsafe void WriteStringToByte(ref string input, byte[] target)
        {
            if (input.Length > target.Length) throw new ArgumentOutOfRangeException($"The input string cannot be more than {target.Length} characters!");
            fixed (char* charPtr = input)
            fixed (byte* bufferPtr = target)
            {
                int writtenOffset = Encoding.UTF8.GetBytes(charPtr, input.Length, bufferPtr, target.Length);
                target.AsSpan(writtenOffset).Fill(0);
            }
        }
#endif
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    public struct UninstallCrcHeader
    {
        public uint Size, NotSize, Crc;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xA, Pack = 1)]
    public struct UninstallFileRec
    {
        public RecordType TUninstallRecType;
        public int ExtraData;
        public uint DataSize;
    }

    public class InnoUninstallLog : IDisposable
    {
        internal const string              X86IdSignature = "Inno Setup Uninstall Log (b)";
        internal const string              X64IdSignature = "Inno Setup Uninstall Log (b) 64-bit";
        public         UninstallLogHeader Header;
        public         List<BaseRecord>?   Records;

        public void Dispose()
        {
            Records?.Clear();
        }

        public static InnoUninstallLog Load(string path, bool skipCrcCheck = false)
        {
            // Load the file as stream
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Load(stream, skipCrcCheck);
            }
        }

        public void Save(string path)
        {
            // Save the record to file path by using Stream
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Save(stream);
            }
        }

        public static InnoUninstallLog Load(Stream stream, bool skipCrcCheck = false)
        {
            // SANITY CHECK: Check the stream if it's readable
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable!", "stream");

            // Read header structures
            ReadTUninstallLogHeader(stream, skipCrcCheck, out UninstallLogHeader headerStruct);

            // Initialize the result class
            InnoUninstallLog result = new()
            {
                Header = headerStruct,
                Records = []
            };

            // Assign the stream to the reader and leave it open
            using (CrcBridgeStream crcStream = new(stream, true, skipCrcCheck))
            {
                // Start reading records and its header
                int start = 0;
            ReadHeaderRecords:
                ReadTStructure(crcStream, out UninstallFileRec uninstallFileRec); // Read the header and load it into the struct
                byte[] buffer = new byte[uninstallFileRec.DataSize]; // Initialize the buffer for the data
#if NET
                crcStream.ReadExactly(buffer); // Then read the crc stream to the buffer
#else
                crcStream.Read(buffer, 0, buffer.Length);
#endif

                // Try to create the record and add it into the record list
                result.Records.Add(RecordFactory.CreateRecord(uninstallFileRec.TUninstallRecType, uninstallFileRec.ExtraData, buffer));
                if (++start < headerStruct.RecordsCount) goto ReadHeaderRecords; // Do loop if the record still remains

                // Return the list
                return result;
            }
        }

        public void Save(Stream stream)
        {
            // Borrowing the write buffer with size (at least) 128 KB from ArrayPool<T>
            byte[] writeBuffer = ArrayPool<byte>.Shared.Rent(128 << 10);
            long lastStreamPosition = 0;
            try
            {
                // Write the TUninstallLogHeader struct.
                // If the stream isn't seekable, then set the size of the stream in header to 0xFFFFFFFF (or -1)
                // and write the TUninstallLogHeader first. If seekable, then the struct will be written once all
                // the records have been written into the stream.
                // ROUTINE A (if not Seekable):
                //     Set the size to 0xFFFFFFFF (-1) -> Write the TUninstallLogHeader struct -> Write the records
                // 
                // ROUTINE B (if Seekable):
                //     Seek the stream based on the struct size -> Write the records -> Back to the last stream position
                //     -> Set the size based on Stream size -> Write the TUninstallLogHeader struct
                if (!stream.CanSeek)
                    WriteTUninstallLogHeader(stream, -1, Records, Header);
                else
                {
                    int tHeadStructSize = Marshal.SizeOf<UninstallLogHeader>();
                    lastStreamPosition = stream.Position;
                    stream.Seek(tHeadStructSize, SeekOrigin.Current);
                }

                // Initialize the CrcBridgeStream in write mode
                using (CrcBridgeStream crcStream = new(stream, true, false, true))
                {
                    // Get the size of the record header (TUninstallLogHeader)
                    // Note: The size should at least 10 bytes expected
                    int headerSizeOf = Marshal.SizeOf<UninstallFileRec>();
                    // Iterate the records
                    if (Records != null)
                        foreach (BaseRecord record in Records)
                        {
                            // Set the offset of the buffer and update the content into the write buffer
                            int offset = 0;
                            // Move and start buffer forward (based on headerSizeOf) to reserve space for TUninstallFileRec
                            uint dataLen = (uint)record.UpdateContent(writeBuffer.AsSpan(headerSizeOf));

                            // Initialize the record header (TUninstallFileRec) and try to serialize it into buffer
                            UninstallFileRec dataRec = new()
                            {
                                DataSize          = dataLen,
                                ExtraData         = record.FlagsNum,
                                TUninstallRecType = record.Type
                            };
                            _ = TrySerializeStruct(dataRec, ref offset, headerSizeOf, writeBuffer);

                            // Write the buffer with the size of headerSizeOf and dataLen
                        #if NET
                            crcStream.Write(writeBuffer.AsSpan(0, headerSizeOf + (int)dataLen));
                        #else
                        crcStream.Write(writeBuffer, 0, headerSizeOf + (int)dataLen);
                        #endif
                        }

                    // Finalize the crc block inside CrcBridgeStream
                    crcStream.FinalizeBlock();
                }

                // As per described detail about the header, if the stream is seekable, then
                // do the ROUTINE B
                if (stream.CanSeek)
                {
                    stream.Seek(lastStreamPosition, SeekOrigin.Begin);
                    WriteTUninstallLogHeader(stream, (int)stream.Length, Records, Header);
                }
            }
            // Catch all exception
            finally
            {
                // Return the write buffer to ArrayPool
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }
        }

        private static void WriteTUninstallLogHeader(Stream stream, int lengthOfStream, List<BaseRecord>? records, UninstallLogHeader header)
        {
            if (records == null) return;
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<UninstallLogHeader>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Reset the fileEndOffset
                header.FileEndOffset = lengthOfStream;

                // Update the record counts
                header.RecordsCount = records.Count;

                // Serialize the struct into bytes
                _ = TrySerializeStruct(header, ref i, sizeOf, structBuffer);

                // Get the hash
                uint crc32Header = Crc32.HashToUInt32(structBuffer.AsSpan(0, sizeOf - 4));
#if NET
                MemoryMarshal.Write(structBuffer.AsSpan(sizeOf - 4), crc32Header);
#else
                MemoryMarshal.Write(structBuffer.AsSpan(sizeOf - 4), ref crc32Header);
#endif

                // Write the buffer into stream
                stream.Write(structBuffer, 0, sizeOf);
            }
            // Catch all exception
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }

        private static void ReadTUninstallLogHeader(Stream stream, bool skipCrcCheck, out UninstallLogHeader header)
        {
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<UninstallLogHeader>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Read the stream and store into buffer
#if NET
                stream.ReadExactly(structBuffer, 0, sizeOf);
#else
                stream.Read(structBuffer, 0, sizeOf);
#endif

                // Deserialize the struct
                if (!TryDeserializeStruct(structBuffer, ref i, sizeOf, out header))
                    throw new InvalidDataException("Header struct is invalid!");

                // Try to calculate the Crc32 hash of the header and throw if not match
                uint crc32Header = Crc32.HashToUInt32(structBuffer.AsSpan(0, sizeOf - 4));
                if (!skipCrcCheck && crc32Header != header.Crc)
                    throw new DataException($"Header Crc32 isn't match! Getting {crc32Header} while expecting {header.Crc}");
            }
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }

        internal static void ReadTStructure<[DynamicallyAccessedMembers(
              DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors
            )]T>(Stream stream, out T header)
            where T : struct
        {
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<T>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Read the stream and store into buffer
#if NET
                stream.ReadExactly(structBuffer, 0, sizeOf);
#else
                stream.Read(structBuffer, 0, sizeOf);
#endif

                // Deserialize the struct
                if (!TryDeserializeStruct(structBuffer, ref i, sizeOf, out header))
                    throw new InvalidDataException("Header struct is invalid!");
            }
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }

        internal static bool TryDeserializeStruct<[DynamicallyAccessedMembers(
              DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors
            )]T>(byte[] data, ref int pos, int size, out T? output)
        {
            output = default;
            if (data.Length < size || data.Length - size < pos) return false;

            IntPtr bufferPtr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, pos, bufferPtr, size);

            output = Marshal.PtrToStructure<T>(bufferPtr);
            Marshal.FreeHGlobal(bufferPtr);
            pos += size;
            return true;
        }

        internal static bool TrySerializeStruct<T>(T input, ref int pos, int size, byte[] output)
        {
            if (pos + size > output.Length) return false;

            IntPtr dataPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(input!, dataPtr, true);
            Marshal.Copy(dataPtr, output, pos, size);
            Marshal.FreeHGlobal(dataPtr);
            pos += size;
            return true;
        }
    }
}
