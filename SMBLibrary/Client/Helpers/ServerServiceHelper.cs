/* Copyright (C) 2014-2021 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using SMBLibrary.RPC;
using SMBLibrary.Services;
using Utilities;

namespace SMBLibrary.Client
{
    public class ServerServiceHelper
    {
        public static List<string> ListShares(INTFileStore namedPipeShare, ShareType? shareType, out NTStatus status)
        {
            return ListShares(namedPipeShare, "*", shareType, out status);
        }

        /// <param name="serverName">
        /// When a Windows Server host is using Failover Cluster and Cluster Shared Volumes, each of those CSV file shares is associated
        /// with a specific host name associated with the cluster and is not accessible using the node IP address or node host name.
        /// </param>
        public static List<string> ListShares(INTFileStore namedPipeShare, string serverName, ShareType? shareType, out NTStatus status)
        {
            status = NamedPipeHelper.BindPipe(namedPipeShare, ServerService.ServicePipeName, ServerService.ServiceInterfaceGuid, ServerService.ServiceVersion, out object pipeHandle, out int maxTransmitFragmentSize);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return null;
            }

            NetrShareEnumRequest shareEnumRequest = new()
            {
                InfoStruct = new ShareEnum()
            };
            shareEnumRequest.InfoStruct.Level = 1;
            shareEnumRequest.InfoStruct.Info = new ShareInfo1Container();
            shareEnumRequest.PreferedMaximumLength = UInt32.MaxValue;
            shareEnumRequest.ServerName = @"\\" + serverName;
            RequestPDU requestPDU = new()
            {
                Flags = PacketFlags.FirstFragment | PacketFlags.LastFragment
            };
            requestPDU.DataRepresentation.CharacterFormat = CharacterFormat.ASCII;
            requestPDU.DataRepresentation.ByteOrder = ByteOrder.LittleEndian;
            requestPDU.DataRepresentation.FloatingPointRepresentation = FloatingPointRepresentation.IEEE;
            requestPDU.OpNum = (ushort)ServerServiceOpName.NetrShareEnum;
            requestPDU.Data = shareEnumRequest.GetBytes();
            requestPDU.AllocationHint = (uint)requestPDU.Data.Length;
            byte[] input = requestPDU.GetBytes();
            int maxOutputLength = maxTransmitFragmentSize;
            status = namedPipeShare.DeviceIOControl(pipeHandle, (uint)IoControlCode.FSCTL_PIPE_TRANSCEIVE, input, out byte[] output, maxOutputLength);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return null;
            }
            if (RPCPDU.GetPDU(output, 0) is not ResponsePDU responsePDU)
            {
                status = NTStatus.STATUS_NOT_SUPPORTED;
                return null;
            }

            byte[] responseData = responsePDU.Data;
            while ((responsePDU.Flags & PacketFlags.LastFragment) == 0)
            {
                status = namedPipeShare.ReadFile(out output, pipeHandle, 0, maxOutputLength);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    return null;
                }
                responsePDU = RPCPDU.GetPDU(output, 0) as ResponsePDU;
                if (responsePDU == null)
                {
                    status = NTStatus.STATUS_NOT_SUPPORTED;
                    return null;
                }
                responseData = ByteUtils.Concatenate(responseData, responsePDU.Data);
            }
            namedPipeShare.CloseFile(pipeHandle);
            NetrShareEnumResponse shareEnumResponse = new(responseData);
            if (shareEnumResponse.InfoStruct.Info is not ShareInfo1Container shareInfo1 || shareInfo1.Entries == null)
            {
                if (shareEnumResponse.Result == Win32Error.ERROR_ACCESS_DENIED)
                {
                    status = NTStatus.STATUS_ACCESS_DENIED;
                }
                else
                {
                    status = NTStatus.STATUS_NOT_SUPPORTED;
                }
                return null;
            }

            List<string> result = [];
            foreach (ShareInfo1Entry entry in shareInfo1.Entries)
            {
                if (!shareType.HasValue || shareType.Value == entry.ShareType.ShareType)
                {
                    result.Add(entry.NetName.Value);
                }
            }
            return result;
        }
    }
}
