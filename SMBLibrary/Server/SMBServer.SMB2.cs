/* Copyright (C) 2017-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using SMBLibrary.NetBios;
using SMBLibrary.Server.SMB2;
using SMBLibrary.SMB2;
using Utilities;

namespace SMBLibrary.Server
{
    public partial class SMBServer
    {
        private void ProcessSMB2RequestChain(List<SMB2Command> requestChain, ref ConnectionState state)
        {
            List<SMB2Command> responseChain = [];
            FileID? fileID = null;
            NTStatus? fileIDStatus = null;
            foreach (SMB2Command request in requestChain)
            {
                SMB2Command response;
                if (request.Header.IsRelatedOperations && RequestContainsFileID(request))
                {
                    if (fileIDStatus != null && fileIDStatus != NTStatus.STATUS_SUCCESS && fileIDStatus != NTStatus.STATUS_BUFFER_OVERFLOW)
                    {
                        // [MS-SMB2] When the current request requires a FileId and the previous request either contains
                        // or generates a FileId, if the previous request fails with an error, the server SHOULD fail the
                        // current request with the same error code returned by the previous request.
                        state.LogToServer(Severity.Verbose, "Compunded related request {0} failed because FileId generation failed.", request.CommandName);
                        response = new ErrorResponse(request.CommandName, fileIDStatus.Value);
                    }
                    else if (fileID.HasValue)
                    {
                        SetRequestFileID(request, fileID.Value);
                        response = ProcessSMB2Command(request, ref state);
                    }
                    else
                    {
                        // [MS-SMB2] When the current request requires a FileId, and if the previous request neither contains
                        // nor generates a FileId, the server MUST fail the compounded request with STATUS_INVALID_PARAMETER.
                        state.LogToServer(Severity.Verbose, "Compunded related request {0} failed, the previous request neither contains nor generates a FileId.", request.CommandName);
                        response = new ErrorResponse(request.CommandName, NTStatus.STATUS_INVALID_PARAMETER);
                    }
                }
                else
                {
                    fileID = GetRequestFileID(request);
                    response = ProcessSMB2Command(request, ref state);
                }

                if (response != null)
                {
                    UpdateSMB2Header(response, request, state);
                    responseChain.Add(response);
                    if (GeneratesFileID(response))
                    {
                        fileID = GetResponseFileID(response);
                        fileIDStatus = response.Header.Status;
                    }
                    else if (RequestContainsFileID(request))
                    {
                        fileIDStatus = response.Header.Status;
                    }
                }
            }
            if (responseChain.Count > 0)
            {
                EnqueueResponseChain(state, responseChain);
            }
        }

        /// <summary>
        /// May return null
        /// </summary>
        private SMB2Command ProcessSMB2Command(SMB2Command command, ref ConnectionState state)
        {
            if (state.Dialect == SMBDialect.NotSet)
            {
                if (command is NegotiateRequest request)
                {
                    SMB2Command response = NegotiateHelper.GetNegotiateResponse(request, SecurityProvider, state, m_transport, m_serverGuid, m_serverStartTime, m_enableSMB3);
                    if (state.Dialect != SMBDialect.NotSet)
                    {
                        state = new SMB2ConnectionState(state);
                        m_connectionManager.AddConnection(state);
                    }
                    return response;
                }
                else
                {
                    // [MS-SMB2] If the request being received is not an SMB2 NEGOTIATE Request [..]
                    // and Connection.NegotiateDialect is 0xFFFF or 0x02FF, the server MUST
                    // disconnect the connection.
                    state.LogToServer(Severity.Debug, "Invalid Connection State for command {0}", command.CommandName.ToString());
                    state.ClientSocket.Close();
                    return null;
                }
            }
            else if (command is NegotiateRequest)
            {
                // [MS-SMB2] If Connection.NegotiateDialect is 0x0202, 0x0210, 0x0300, 0x0302, or 0x0311,
                // the server MUST disconnect the connection.
                state.LogToServer(Severity.Debug, "Rejecting NegotiateRequest. NegotiateDialect is already set");
                state.ClientSocket.Close();
                return null;
            }
            else
            {
                return ProcessSMB2Command(command, (SMB2ConnectionState)state);
            }
        }

        private SMB2Command ProcessSMB2Command(SMB2Command command, SMB2ConnectionState state)
        {
            if (command is SessionSetupRequest sessionSetupRequest)
            {
                return SessionSetupHelper.GetSessionSetupResponse(sessionSetupRequest, SecurityProvider, state);
            }
            else if (command is EchoRequest)
            {
                return new EchoResponse();
            }
            else
            {
                SMB2Session session = state.GetSession(command.Header.SessionID);
                if (session == null)
                {
                    return new ErrorResponse(command.CommandName, NTStatus.STATUS_USER_SESSION_DELETED);
                }

                if (command is TreeConnectRequest treeConnectRequest)
                {
                    return TreeConnectHelper.GetTreeConnectResponse(treeConnectRequest, state, m_services, Shares);
                }
                else if (command is LogoffRequest)
                {
                    state.LogToServer(Severity.Information, "Logoff: User '{0}' logged off. (SessionID: {1})", session.UserName, command.Header.SessionID);
                    SecurityProvider.DeleteSecurityContext(ref session.SecurityContext.AuthenticationContext);
                    state.RemoveSession(command.Header.SessionID);
                    return new LogoffResponse();
                }
                else if (command.Header.IsAsync)
                {
                    // TreeID will not be present in an ASYNC header
                    if (command is CancelRequest cancelRequest)
                    {
                        return CancelHelper.GetCancelResponse(cancelRequest, state);
                    }
                }
                else
                {
                    ISMBShare share = session.GetConnectedTree(command.Header.TreeID);
                    if (share == null)
                    {
                        state.LogToServer(Severity.Verbose, "{0} failed. Invalid TreeID (SessionID: {1}, TreeID: {2}).", command.CommandName, command.Header.SessionID, command.Header.TreeID);
                        return new ErrorResponse(command.CommandName, NTStatus.STATUS_NETWORK_NAME_DELETED);
                    }

                    switch (command)
                    {
                        case TreeDisconnectRequest treeDisconnectRequest:
                            return TreeConnectHelper.GetTreeDisconnectResponse(treeDisconnectRequest, share, state);
                        case CreateRequest createRequest:
                            return CreateHelper.GetCreateResponse(createRequest, share, state);
                        case QueryInfoRequest queryInfoRequest:
                            return QueryInfoHelper.GetQueryInfoResponse(queryInfoRequest, share, state);
                        case SetInfoRequest setInfoRequest:
                            return SetInfoHelper.GetSetInfoResponse(setInfoRequest, share, state);
                        case QueryDirectoryRequest queryDirectoryRequest:
                            return QueryDirectoryHelper.GetQueryDirectoryResponse(queryDirectoryRequest, share, state);
                        case ReadRequest readRequest:
                            return ReadWriteResponseHelper.GetReadResponse(readRequest, share, state);
                        case WriteRequest writeRequest:
                            return ReadWriteResponseHelper.GetWriteResponse(writeRequest, share, state);
                        case LockRequest lockRequest:
                            return LockHelper.GetLockResponse(lockRequest, share, state);
                        case FlushRequest flushRequest:
                            return ReadWriteResponseHelper.GetFlushResponse(flushRequest, share, state);
                        case CloseRequest closeRequest:
                            return CloseHelper.GetCloseResponse(closeRequest, share, state);
                        case IOCtlRequest iOCtlRequest:
                            return IOCtlHelper.GetIOCtlResponse(iOCtlRequest, share, state);
                        case CancelRequest cancelRequest:
                            return CancelHelper.GetCancelResponse(cancelRequest, state);
                        case ChangeNotifyRequest changeNotifyRequest:
                            return ChangeNotifyHelper.GetChangeNotifyInterimResponse(changeNotifyRequest, share, state);
                    }
                }
            }

            return new ErrorResponse(command.CommandName, NTStatus.STATUS_NOT_SUPPORTED);
        }

        internal static void EnqueueResponse(ConnectionState state, SMB2Command response)
        {
            List<SMB2Command> responseChain = [response];
            EnqueueResponseChain(state, responseChain);
        }

        private static void EnqueueResponseChain(ConnectionState state, List<SMB2Command> responseChain)
        {
            byte[] signingKey = null;
            if (state is SMB2ConnectionState sMB2ConnectionState)
            {
                // Note: multiple sessions MAY be multiplexed on the same connection, so theoretically
                // we could have compounding unrelated requests from different sessions.
                // In practice however this is not a real problem.
                ulong sessionID = responseChain[0].Header.SessionID;
                if (sessionID != 0)
                {
                    SMB2Session session = sMB2ConnectionState.GetSession(sessionID);
                    if (session != null)
                    {
                        signingKey = session.SigningKey;
                    }
                }
            }

            SessionMessagePacket packet = new();
            SMB2Dialect smb2Dialect = (signingKey != null) ? ToSMB2Dialect(state.Dialect) : SMB2Dialect.SMB2xx;
            packet.Trailer = SMB2Command.GetCommandChainBytes(responseChain, signingKey, smb2Dialect);
            state.SendQueue.Enqueue(packet);
            state.LogToServer(Severity.Verbose, "SMB2 response chain queued: Response count: {0}, First response: {1}, Packet length: {2}", responseChain.Count, responseChain[0].CommandName.ToString(), packet.Length);
        }

        internal static SMB2Dialect ToSMB2Dialect(SMBDialect smbDialect)
        {
            return smbDialect switch
            {
                SMBDialect.SMB202 => SMB2Dialect.SMB202,
                SMBDialect.SMB210 => SMB2Dialect.SMB210,
                SMBDialect.SMB300 => SMB2Dialect.SMB300,
                _ => throw new ArgumentException("Unsupported SMB2 Dialect: " + smbDialect.ToString()),
            };
        }

        private static void UpdateSMB2Header(SMB2Command response, SMB2Command request, ConnectionState state)
        {
            response.Header.MessageID = request.Header.MessageID;
            response.Header.CreditCharge = request.Header.CreditCharge;
            response.Header.Credits = Math.Max((ushort)1, request.Header.Credits);
            response.Header.IsRelatedOperations = request.Header.IsRelatedOperations;
            response.Header.Reserved = request.Header.Reserved;
            if (response.Header.SessionID == 0)
            {
                response.Header.SessionID = request.Header.SessionID;
            }
            if (response.Header.TreeID == 0)
            {
                response.Header.TreeID = request.Header.TreeID;
            }
            bool signingRequired = false;
            if (state is SMB2ConnectionState sMB2ConnectionState)
            {
                SMB2Session session = sMB2ConnectionState.GetSession(response.Header.SessionID);
                if (session != null && session.SigningRequired)
                {
                    signingRequired = true;
                }
            }
            // [MS-SMB2] The server SHOULD sign the message [..] if the request was signed by the client,
            // and the response is not an interim response to an asynchronously processed request.
            bool isInterimResponse = (response.Header.IsAsync && response.Header.Status == NTStatus.STATUS_PENDING);
            response.Header.IsSigned = (request.Header.IsSigned || signingRequired) && !isInterimResponse;
        }

        private static bool RequestContainsFileID(SMB2Command command)
        {
            return (command is ChangeNotifyRequest ||
                    command is CloseRequest ||
                    command is FlushRequest ||
                    command is IOCtlRequest ||
                    command is LockRequest ||
                    command is QueryDirectoryRequest ||
                    command is QueryInfoRequest ||
                    command is ReadRequest ||
                    command is SetInfoRequest ||
                    command is WriteRequest);
        }

        private static FileID? GetRequestFileID(SMB2Command command)
        {
            return command switch
            {
                ChangeNotifyRequest changeNotifyRequest => changeNotifyRequest.FileId,
                CloseRequest closeRequest => closeRequest.FileId,
                FlushRequest flushRequest => flushRequest.FileId,
                IOCtlRequest iOCtlRequest => iOCtlRequest.FileId,
                LockRequest lockRequest => lockRequest.FileId,
                QueryDirectoryRequest queryDirectoryRequest => queryDirectoryRequest.FileId,
                QueryInfoRequest queryInfoRequest => queryInfoRequest.FileId,
                ReadRequest readRequest => readRequest.FileId,
                SetInfoRequest setInfoRequest => setInfoRequest.FileId,
                WriteRequest writeRequest => writeRequest.FileId,
                _ => null,
            };
        }

        private static void SetRequestFileID(SMB2Command command, FileID fileID)
        {
            switch (command)
            {
                case ChangeNotifyRequest changeNotifyRequest:
                    changeNotifyRequest.FileId = fileID;
                    break;
                case CloseRequest closeRequest:
                    closeRequest.FileId = fileID;
                    break;
                case FlushRequest flushRequest:
                    flushRequest.FileId = fileID;
                    break;
                case IOCtlRequest iOCtlRequest:
                    iOCtlRequest.FileId = fileID;
                    break;
                case LockRequest lockRequest:
                    lockRequest.FileId = fileID;
                    break;
                case QueryDirectoryRequest queryDirectoryRequest:
                    queryDirectoryRequest.FileId = fileID;
                    break;
                case QueryInfoRequest queryInfoRequest:
                    queryInfoRequest.FileId = fileID;
                    break;
                case ReadRequest readRequest:
                    readRequest.FileId = fileID;
                    break;
                case SetInfoRequest setInfoRequest:
                    setInfoRequest.FileId = fileID;
                    break;
                case WriteRequest writeRequest:
                    writeRequest.FileId = fileID;
                    break;
            }
        }

        private static bool GeneratesFileID(SMB2Command command)
        {
            return (command.CommandName == SMB2CommandName.Create ||
                    command.CommandName == SMB2CommandName.IOCtl);
        }

        private static FileID? GetResponseFileID(SMB2Command command)
        {
            if (command is CreateResponse createResponse)
            {
                return createResponse.FileId;
            }
            else if (command is IOCtlResponse iOCtlResponse)
            {
                return iOCtlResponse.FileId;
            }
            return null;
        }
    }
}
