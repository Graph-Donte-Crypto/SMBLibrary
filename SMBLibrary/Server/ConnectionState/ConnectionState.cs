/* Copyright (C) 2014-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.NetBios;
using Utilities;

namespace SMBLibrary.Server
{
    internal delegate void LogDelegate(Severity severity, string message);

    internal class ConnectionState
    {
        public Socket ClientSocket { get; private set; }
        public IPEndPoint ClientEndPoint { get; private set; }
        public NBTConnectionReceiveBuffer ReceiveBuffer { get; private set; }
        public BlockingQueue<SessionPacket> SendQueue { get; private set; }
        public DateTime CreationDT { get; private set; }
        public DateTime LastReceiveDT { get; private set; }
        private Reference<DateTime> LastSendDTRef { get; set; } // We must use a reference because the sender thread will keep using the original ConnectionState object
        public DateTime LastSendDT => LastSendDTRef.Value;
        private readonly LogDelegate LogToServerHandler;
        public SMBDialect Dialect;
        public GSSContext AuthenticationContext;

        public ConnectionState(Socket clientSocket, IPEndPoint clientEndPoint, LogDelegate logToServerHandler)
        {
            ClientSocket = clientSocket;
            ClientEndPoint = clientEndPoint;
            ReceiveBuffer = new NBTConnectionReceiveBuffer();
            SendQueue = new BlockingQueue<SessionPacket>();
            CreationDT = DateTime.UtcNow;
            LastReceiveDT = DateTime.UtcNow;
            LastSendDTRef = DateTime.UtcNow;
            LogToServerHandler = logToServerHandler;
            Dialect = SMBDialect.NotSet;
        }

        public ConnectionState(ConnectionState state)
        {
            ClientSocket = state.ClientSocket;
            ClientEndPoint = state.ClientEndPoint;
            ReceiveBuffer = state.ReceiveBuffer;
            SendQueue = state.SendQueue;
            CreationDT = state.CreationDT;
            LastReceiveDT = state.LastReceiveDT;
            LastSendDTRef = state.LastSendDTRef;
            LogToServerHandler = state.LogToServerHandler;
            Dialect = state.Dialect;
        }

        /// <summary>
        /// Free all resources used by the active sessions in this connection
        /// </summary>
        public virtual void CloseSessions()
        {
        }

        public virtual List<SessionInformation> GetSessionsInformation()
        {
            return [];
        }

        public void LogToServer(Severity severity, string message)
        {
            message = String.Format("[{0}] {1}", ConnectionIdentifier, message);
            LogToServerHandler?.Invoke(severity, message);
        }

        public void LogToServer(Severity severity, string message, params object[] args)
        {
            LogToServer(severity, String.Format(message, args));
        }

        public void UpdateLastReceiveDT()
        {
            LastReceiveDT = DateTime.UtcNow;
        }

        public void UpdateLastSendDT()
        {
            LastSendDTRef.Value = DateTime.UtcNow;
        }

        public string ConnectionIdentifier 
            => ClientEndPoint is not null 
                ? ClientEndPoint.Address + ":" + ClientEndPoint.Port 
                : string.Empty;
    }
}
