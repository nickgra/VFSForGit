using GVFS.Common;
using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace GVFS.Platform.Mac
{
    public partial class MacFileSystem : IPlatformFileSystem
    {
        public bool SupportsFileMode { get; } = true;

        public void FlushFileBuffers(string path)
        {
            // TODO(Mac): Use native API to flush file
        }

        public void MoveAndOverwriteFile(string sourceFileName, string destinationFilename)
        {
            if (Rename(sourceFileName, destinationFilename) != 0)
            {
                NativeMethods.ThrowLastWin32Exception($"Failed to renname {sourceFileName} to {destinationFilename}");
            }
        }

        public void CreateHardLink(string newFileName, string existingFileName)
        {
            // TODO(Mac): Use native API to create a hardlink
            File.Copy(existingFileName, newFileName);
        }

        public void ChangeMode(string path, int mode)
        {
           Chmod(path, mode);
        }

        public bool TryGetNormalizedPath(string path, out string normalizedPath, out string errorMessage)
        {
            return MacFileSystem.TryGetNormalizedPathImplementation(path, out normalizedPath, out errorMessage);
        }

        public bool HydrateFile(string fileName, byte[] buffer)
        {
            return NativeFileReader.TryReadFirstByteOfFile(fileName, buffer);
        }

        public unsafe void WriteFile(ITracer tracer, byte* originalData, long originalSize, string destination)
        {
            int fileDescriptor = 1;
            try
            {
                fileDescriptor = NativeFileReader.Open(destination, NativeFileReader.WriteOnly | NativeFileReader.Create);
                IntPtr result = Write(fileDescriptor, originalData, (IntPtr)originalSize);
                if (result.ToInt32() == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            catch
            {
                tracer.RelatedError("Error writing file {0}. ERRNO: {1}", destination, Marshal.GetLastWin32Error());
                throw;
            }
            finally
            {
                NativeFileReader.Close(fileDescriptor);
            }
        }

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string pathname, int mode);

        [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
        private static extern int Rename(string oldPath, string newPath);

        [DllImport("libc", EntryPoint = "write", SetLastError = true)]
        private static unsafe extern IntPtr Write(int fileDescriptor, void* buf, IntPtr count);

        private class NativeFileReader
        {
            public const int ReadOnly = 0x0000;
            public const int WriteOnly = 0x0001;

            public const int Create = 0x0200;

            public static bool TryReadFirstByteOfFile(string fileName, byte[] buffer)
            {
                int fileDescriptor = 1;
                bool readStatus;
                try
                {
                    fileDescriptor = Open(fileName, ReadOnly);
                    readStatus = TryReadOneByte(fileDescriptor, buffer);
                }
                finally
                {
                    Close(fileDescriptor);
                }

                return readStatus;
            }

            [DllImport("libc", EntryPoint = "open", SetLastError = true)]
            public static extern int Open(string path, int flag);

            [DllImport("libc", EntryPoint = "close", SetLastError = true)]
            public static extern int Close(int fd);

            [DllImport("libc", EntryPoint = "read", SetLastError = true)]
            public static extern int Read(int fd, [Out] byte[] buf, int count);

            private static bool TryReadOneByte(int fileDescriptor, byte[] buffer)
            {
                int numBytes = Read(fileDescriptor, buffer, 1);

                if (numBytes == -1)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
