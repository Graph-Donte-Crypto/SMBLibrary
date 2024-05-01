/* Copyright (C) 2014-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace SMBLibrary.Adapters
{
    public partial class NTFileSystemAdapter
    {
        public NTStatus GetFileSystemInformation(out FileSystemInformation result, FileSystemInformationClass informationClass)
        {
            switch (informationClass)
            {
                case FileSystemInformationClass.FileFsVolumeInformation:
                    {
                        FileFsVolumeInformation information = new()
                        {
                            SupportsObjects = false
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsSizeInformation:
                    {
                        FileFsSizeInformation information = new()
                        {
                            TotalAllocationUnits = m_fileSystem.Size / ClusterSize,
                            AvailableAllocationUnits = m_fileSystem.FreeSpace / ClusterSize,
                            SectorsPerAllocationUnit = ClusterSize / BytesPerSector,
                            BytesPerSector = BytesPerSector
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsDeviceInformation:
                    {
                        FileFsDeviceInformation information = new()
                        {
                            DeviceType = DeviceType.Disk,
                            Characteristics = DeviceCharacteristics.IsMounted
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsAttributeInformation:
                    {
                        FileFsAttributeInformation information = new()
                        {
                            FileSystemAttributes = FileSystemAttributes.CasePreservedNamed | FileSystemAttributes.UnicodeOnDisk,
                            MaximumComponentNameLength = 255,
                            FileSystemName = m_fileSystem.Name
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsControlInformation:
                    {
                        FileFsControlInformation information = new()
                        {
                            FileSystemControlFlags = FileSystemControlFlags.ContentIndexingDisabled,
                            DefaultQuotaThreshold = UInt64.MaxValue,
                            DefaultQuotaLimit = UInt64.MaxValue
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsFullSizeInformation:
                    {
                        FileFsFullSizeInformation information = new()
                        {
                            TotalAllocationUnits = m_fileSystem.Size / ClusterSize,
                            CallerAvailableAllocationUnits = m_fileSystem.FreeSpace / ClusterSize,
                            ActualAvailableAllocationUnits = m_fileSystem.FreeSpace / ClusterSize,
                            SectorsPerAllocationUnit = ClusterSize / BytesPerSector,
                            BytesPerSector = BytesPerSector
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                case FileSystemInformationClass.FileFsObjectIdInformation:
                    {
                        result = null;
                        // STATUS_INVALID_PARAMETER is returned when the file system does not implement object IDs
                        // See: https://msdn.microsoft.com/en-us/library/cc232106.aspx
                        return NTStatus.STATUS_INVALID_PARAMETER;
                    }
                case FileSystemInformationClass.FileFsSectorSizeInformation:
                    {
                        FileFsSectorSizeInformation information = new()
                        {
                            LogicalBytesPerSector = BytesPerSector,
                            PhysicalBytesPerSectorForAtomicity = BytesPerSector,
                            PhysicalBytesPerSectorForPerformance = BytesPerSector,
                            FileSystemEffectivePhysicalBytesPerSectorForAtomicity = BytesPerSector,
                            ByteOffsetForSectorAlignment = 0,
                            ByteOffsetForPartitionAlignment = 0
                        };
                        result = information;
                        return NTStatus.STATUS_SUCCESS;
                    }
                default:
                    {
                        result = null;
                        return NTStatus.STATUS_INVALID_INFO_CLASS;
                    }
            }
        }

        public NTStatus SetFileSystemInformation(FileSystemInformation information)
        {
            return NTStatus.STATUS_NOT_SUPPORTED;
        }
    }
}
