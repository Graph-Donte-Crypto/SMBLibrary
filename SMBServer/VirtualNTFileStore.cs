/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using SMBLibrary;

namespace SMBServer
{
    public class VirtualHandle
    {
        public Guid Handle { get; set; }
        public string Path { get; set; }
    }

    public class VirtualFile
    {
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime ChangeTime { get; set; }
        public long AllocationSize { get; set; }
        public long EndOfFile { get; set; }
        public FileAttributes FileAttributes { get; set; }
        public uint Reserved { get; set; }
    }

    public class VirtualNTFileStore : INTFileStore
    {
        public Dictionary<Guid, VirtualHandle> VirtualHandles { get; set; } = [];
        public Dictionary<string, VirtualFile> VirtualFiles { get; set; } = [];
        public VirtualNTFileStore()
        {
        }
        public NTStatus Cancel(object ioRequest)
        {
            throw new NotImplementedException();
        }

        public NTStatus CloseFile(object handle)
        {
            throw new NotImplementedException();
        }

        public NTStatus CreateFile(out object handle, out FileStatus fileStatus, string path, AccessMask desiredAccess, SMBLibrary.FileAttributes fileAttributes, ShareAccess shareAccess, CreateDisposition createDisposition, CreateOptions createOptions, SecurityContext securityContext)
        {
            var access = new Access(desiredAccess);
            var vh = new VirtualHandle()
            {
                Handle = Guid.NewGuid(),
                Path = path,
            };
            VirtualHandles.Add(vh.Handle, vh);
            handle = vh.Handle;
            fileStatus = FileStatus.FILE_OPENED;
            return NTStatus.STATUS_SUCCESS;
        }

        public NTStatus DeviceIOControl(object handle, uint ctlCode, byte[] input, out byte[] output, int maxOutputLength)
        {
            throw new NotImplementedException();
        }

        public NTStatus FlushFileBuffers(object handle)
        {
            throw new NotImplementedException();
        }

        public NTStatus GetFileInformation(out FileInformation result, object handle, FileInformationClass informationClass)
        {
            if (informationClass == FileInformationClass.FileNetworkOpenInformation)
            {
                result = new FileNetworkOpenInformation
                {
                    
                };
            }
            throw new NotImplementedException();
        }

        public NTStatus GetFileSystemInformation(out FileSystemInformation result, FileSystemInformationClass informationClass)
        {
            throw new NotImplementedException();
        }

        public NTStatus GetSecurityInformation(out SecurityDescriptor result, object handle, SecurityInformation securityInformation)
        {
            throw new NotImplementedException();
        }

        public NTStatus LockFile(object handle, long byteOffset, long length, bool exclusiveLock)
        {
            throw new NotImplementedException();
        }

        public NTStatus NotifyChange(out object ioRequest, object handle, NotifyChangeFilter completionFilter, bool watchTree, int outputBufferSize, OnNotifyChangeCompleted onNotifyChangeCompleted, object context)
        {
            throw new NotImplementedException();
        }

        public NTStatus QueryDirectory(out List<QueryDirectoryFileInformation> result, object handle, string fileName, FileInformationClass informationClass)
        {
            throw new NotImplementedException();
        }

        public NTStatus ReadFile(out byte[] data, object handle, long offset, int maxCount)
        {
            throw new NotImplementedException();
        }

        public NTStatus SetFileInformation(object handle, FileInformation information)
        {
            throw new NotImplementedException();
        }

        public NTStatus SetFileSystemInformation(FileSystemInformation information)
        {
            throw new NotImplementedException();
        }

        public NTStatus SetSecurityInformation(object handle, SecurityInformation securityInformation, SecurityDescriptor securityDescriptor)
        {
            throw new NotImplementedException();
        }

        public NTStatus UnlockFile(object handle, long byteOffset, long length)
        {
            throw new NotImplementedException();
        }

        public NTStatus WriteFile(out int numberOfBytesWritten, object handle, long offset, byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}