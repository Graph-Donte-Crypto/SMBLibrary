/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SMBLibrary;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.Server;
using SMBLibrary.Win32;
using SMBLibrary.Win32.Security;
using Utilities;

namespace SMBServer
{
    public class VirtualNTFileStore : INTFileStore
    {
        public class VirtualFile
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public string Name { get; set; }
            public byte[] Content { get; set; }
        }
        public Dictionary<string, VirtualFile> PathToFiles { get; set; } = [];
        public Dictionary<Guid, VirtualFile> Handles { get; set; } = [];
        public VirtualNTFileStore()
        {
            PathToFiles.Add("/", new VirtualFile
            {
                Content = null,
                IsFolder = true,
                Path = "/",
                Name = "/"
            });
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
            throw new NotImplementedException();
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
    public class VirtualShare(string name) : ISMBShare
    {
        public string Name { get; set; } = name;

        public INTFileStore FileStore { get; set; } = new VirtualNTFileStore();

        public CachingPolicy CachingPolicy => CachingPolicy.NoCaching;

        public bool IsFileSystemShare => true;

        public bool HasAccess(SecurityContext securityContext, string path, FileAccess requestedAccess) => true;
    }
    public partial class ServerUI
    {
        private SMBLibrary.Server.SMBServer Server;
        private SMBLibrary.Server.NameServer NameServer;
        private LogWriter LogWriter;

        public ServerUI()
        {
            InitializeComponent();
        }

        public static KeyValuePairList<string, IPAddress>  GetIPAddresses()
        {
            List<IPAddress> localIPs = NetworkInterfaceHelper.GetHostIPAddresses();
            KeyValuePairList<string, IPAddress> list = new()
            {
                { "Any", IPAddress.Any }
            };
            foreach (IPAddress address in localIPs)
            {
                list.Add(address.ToString(), address);
            }
            return list;
        }

        public void Start(IPAddress iPAddress, SMBTransportType transportType, bool useIntegratedWindowsSecurity) {
            IPAddress serverAddress = iPAddress;
            
            NTLMAuthenticationProviderBase authenticationMechanism;
            if (useIntegratedWindowsSecurity)
            {
                authenticationMechanism = new IntegratedNTLMAuthenticationProvider();
            }
            else
            {
                UserCollection users;
                try
                {
                    users = SettingsHelper.ReadUserSettings();
                }
                catch
                {
                    Console.WriteLine("Cannot read " + SettingsHelper.SettingsFileName);
                    return;
                }

                authenticationMechanism = new IndependentNTLMAuthenticationProvider(users.GetUserPassword);
            }

            List<ShareSettings> sharesSettings;
            try
            {
                sharesSettings = SettingsHelper.ReadSharesSettings();
            }
            catch (Exception)
            {
                Console.WriteLine("Cannot read " + SettingsHelper.SettingsFileName);
                return;
            }

            SMBShareCollection shares = [];
            foreach (ShareSettings shareSettings in sharesSettings)
            {
                FileSystemShare share = InitializeShare(shareSettings);
                shares.Add(share);
            }
            shares.Add(new VirtualShare("VirtualShare"));

            GSSProvider securityProvider = new(authenticationMechanism);
            Server = new SMBLibrary.Server.SMBServer(shares, securityProvider);
            LogWriter = new LogWriter();
            // The provided logging mechanism will synchronously write to the disk during server activity.
            // To maximize server performance, you can disable logging by commenting out the following line.
            Server.LogEntryAdded += new EventHandler<LogEntry>(LogWriter.OnLogEntryAdded);

            try
            {
                Server.Start(serverAddress, transportType, chkSMB1.Checked, chkSMB2.Checked);
                if (transportType == SMBTransportType.NetBiosOverTCP)
                {
                    if (serverAddress.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Equals(serverAddress, IPAddress.Any))
                    {
                        IPAddress subnetMask = NetworkInterfaceHelper.GetSubnetMask(serverAddress);
                        NameServer = new NameServer(serverAddress, subnetMask);
                        NameServer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
        }

        public void Stop()
        {
            Server.Stop();
            LogWriter.CloseLogFile();

            NameServer?.Stop();
        }

        private void chkSMB1_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkSMB1.Checked)
            {
                chkSMB2.Checked = true;
            }
        }

        private void chkSMB2_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkSMB2.Checked)
            {
                chkSMB1.Checked = true;
            }
        }

        public static FileSystemShare InitializeShare(ShareSettings shareSettings)
        {
            string shareName = shareSettings.ShareName;
            string sharePath = shareSettings.SharePath;
            List<string> readAccess = shareSettings.ReadAccess;
            List<string> writeAccess = shareSettings.WriteAccess;
            FileSystemShare share = new(shareName, new NTDirectoryFileSystem(sharePath));
            share.AccessRequested += (object sender, AccessRequestArgs args) =>
            {
                bool hasReadAccess = readAccess.Contains("Users") || readAccess.Contains(args.UserName);
                bool hasWriteAccess = readAccess.Contains("Users") || writeAccess.Contains(args.UserName);
                if (args.RequestedAccess == FileAccess.Read)
                {
                    args.Allow = hasReadAccess;
                }
                else if (args.RequestedAccess == FileAccess.Write)
                {
                    args.Allow = hasWriteAccess;
                }
                else // FileAccess.ReadWrite
                {
                    args.Allow = hasReadAccess && hasWriteAccess;
                }
            };
            return share;
        }

    }
}