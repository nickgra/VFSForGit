﻿using GVFS.Common;
using GVFS.Common.FileSystem;
using GVFS.Common.Tracing;
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

        public bool HydrateFile(ITracer activity, string fileName, byte[] buffer)
        {
            bool output = NativeFileReader.TryReadFirstByteOfFile(activity, fileName, buffer);
            return output;
        }

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string pathname, int mode);

        [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
        private static extern int Rename(string oldPath, string newPath);

        private class NativeFileReader
        {
            private const int ReadOnly = 0x0000;

            public static bool TryReadFirstByteOfFile(ITracer activity, string fileName, byte[] buffer)
            {
                int fileDescriptor = Open(fileName, ReadOnly);
                if (fileDescriptor == -1)
                {
                    int errno = Marshal.GetLastWin32Error();
                    activity.RelatedError("Failed to open() " + fileName);
                    activity.RelatedError("Errno is: " + errno);
                }
                return TryReadOneByte(activity, fileDescriptor, buffer);
            }

            private static bool TryReadOneByte(ITracer activity, int fileDescriptor, byte[] buffer)
            {
                int numBytes = Read(fileDescriptor, buffer, 1);

                if (numBytes == -1)
                {
                    int errno = Marshal.GetLastWin32Error();
                    activity.RelatedError("Failed to read() " + fileDescriptor);
                    activity.RelatedError("Errno is: " + errno);
                    return false;
                }

                return true;
            }

            [DllImport("libc", EntryPoint = "open", SetLastError = true)]
            private static extern int Open(string path, int flag);

            [DllImport("libc", EntryPoint = "read", SetLastError = true)]
            private static extern int Read(int fd, [Out] byte[] buf, int count);
        }
    }
}
