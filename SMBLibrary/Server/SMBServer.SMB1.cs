/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using SMBLibrary.NetBios;
using SMBLibrary.Server.SMB1;
using SMBLibrary.SMB1;
using Utilities;

namespace SMBLibrary.Server
{
    public partial class SMBServer
    {
        private void ProcessSMB1Message(SMB1Message message, ref ConnectionState state)
        {
            SMB1Header header = new();
            PrepareResponseHeader(header, message.Header);
            List<SMB1Command> responses = [];

            bool isBatchedRequest = (message.Commands.Count > 1);
            foreach (SMB1Command command in message.Commands)
            {
                List<SMB1Command> commandResponses = ProcessSMB1Command(header, command, ref state);
                responses.AddRange(commandResponses);

                if (header.Status != NTStatus.STATUS_SUCCESS)
                {
                    break;
                }
            }

            if (isBatchedRequest)
            {
                if (responses.Count > 0)
                {
                    // The server MUST batch the response into an AndX Response chain.
                    SMB1Message reply = new()
                    {
                        Header = header
                    };
                    for (int index = 0; index < responses.Count; index++)
                    {
                        if (reply.Commands.Count == 0 ||
                            reply.Commands[^1] is SMBAndXCommand)
                        {
                            reply.Commands.Add(responses[index]);
                            responses.RemoveAt(index);
                            index--;
                        }
                        else
                        {
                            break;
                        }
                    }
                    EnqueueMessage(state, reply);
                }
            }

            foreach (SMB1Command response in responses)
            {
                SMB1Message reply = new()
                {
                    Header = header
                };
                reply.Commands.Add(response);
                EnqueueMessage(state, reply);
            }
        }

        /// <summary>
        /// May return an empty list
        /// </summary>
        private List<SMB1Command> ProcessSMB1Command(SMB1Header header, SMB1Command command, ref ConnectionState state)
        {
            if (state.Dialect == SMBDialect.NotSet)
            {
                if (command is NegotiateRequest request)
                {
                    if (request.Dialects.Contains(SMBServer.NTLanManagerDialect))
                    {
                        state = new SMB1ConnectionState(state)
                        {
                            Dialect = SMBDialect.NTLM012
                        };
                        m_connectionManager.AddConnection(state);
                        if (EnableExtendedSecurity && header.ExtendedSecurityFlag)
                        {
                            return NegotiateHelper.GetNegotiateResponseExtended(request, m_serverGuid);
                        }
                        else
                        {
                            return NegotiateHelper.GetNegotiateResponse(header, request, SecurityProvider, state);
                        }
                    }
                    else
                    {
                        return new NegotiateResponseNotSupported();
                    }
                }
                else
                {
                    // [MS-CIFS] An SMB_COM_NEGOTIATE exchange MUST be completed before any other SMB messages are sent to the server
                    header.Status = NTStatus.STATUS_INVALID_SMB;
                    return new ErrorResponse(command.CommandName);
                }
            }
            else if (command is NegotiateRequest)
            {
                // There MUST be only one SMB_COM_NEGOTIATE exchange per SMB connection.
                // Subsequent SMB_COM_NEGOTIATE requests received by the server MUST be rejected with error responses.
                header.Status = NTStatus.STATUS_INVALID_SMB;
                return new ErrorResponse(command.CommandName);
            }
            else
            {
                return ProcessSMB1Command(header, command, (SMB1ConnectionState)state);
            }
        }

        private List<SMB1Command> ProcessSMB1Command(SMB1Header header, SMB1Command command, SMB1ConnectionState state)
        {
            if (command is SessionSetupAndXRequest sessionSetupAndXRequest)
            {
                state.MaxBufferSize = sessionSetupAndXRequest.MaxBufferSize;
                return SessionSetupHelper.GetSessionSetupResponse(header, sessionSetupAndXRequest, SecurityProvider, state);
            }
            else if (command is SessionSetupAndXRequestExtended sessionSetupAndXRequestExtended)
            {
                state.MaxBufferSize = sessionSetupAndXRequestExtended.MaxBufferSize;
                return SessionSetupHelper.GetSessionSetupResponseExtended(header, sessionSetupAndXRequestExtended, SecurityProvider, state);
            }
            else if (command is EchoRequest echoRequest)
            {
                return EchoHelper.GetEchoResponse(echoRequest);
            }
            else
            {
                SMB1Session session = state.GetSession(header.UID);
                if (session == null)
                {
                    header.Status = NTStatus.STATUS_USER_SESSION_DELETED;
                    return new ErrorResponse(command.CommandName);
                }

                if (command is TreeConnectAndXRequest treeConnectAndXRequest)
                {
                    return TreeConnectHelper.GetTreeConnectResponse(header, treeConnectAndXRequest, state, m_services, Shares);
                }
                else if (command is LogoffAndXRequest)
                {
                    state.LogToServer(Severity.Information, "Logoff: User '{0}' logged off. (UID: {1})", session.UserName, header.UID);
                    SecurityProvider.DeleteSecurityContext(ref session.SecurityContext.AuthenticationContext);
                    state.RemoveSession(header.UID);
                    return new LogoffAndXResponse();
                }
                else
                {
                    ISMBShare share = session.GetConnectedTree(header.TID);
                    if (share == null)
                    {
                        state.LogToServer(Severity.Verbose, "{0} failed. Invalid TID (UID: {1}, TID: {2}).", command.CommandName, header.UID, header.TID);
                        header.Status = NTStatus.STATUS_SMB_BAD_TID;
                        return new ErrorResponse(command.CommandName);
                    }

                    switch (command)
                    {
                        case CreateDirectoryRequest createDirectoryRequest:
                            return FileStoreResponseHelper.GetCreateDirectoryResponse(header, createDirectoryRequest, share, state);
                        case DeleteDirectoryRequest deleteDirectoryRequest:
                            return FileStoreResponseHelper.GetDeleteDirectoryResponse(header, deleteDirectoryRequest, share, state);
                        case CloseRequest closeRequest:
                            return CloseHelper.GetCloseResponse(header, closeRequest, share, state);
                        case FlushRequest flushRequest:
                            return ReadWriteResponseHelper.GetFlushResponse(header, flushRequest, share, state);
                        case DeleteRequest deleteRequest:
                            return FileStoreResponseHelper.GetDeleteResponse(header, deleteRequest, share, state);
                        case RenameRequest renameRequest:
                            return FileStoreResponseHelper.GetRenameResponse(header, renameRequest, share, state);
                        case QueryInformationRequest queryInformationRequest:
                            return FileStoreResponseHelper.GetQueryInformationResponse(header, queryInformationRequest, share, state);
                        case SetInformationRequest setInformationRequest:
                            return FileStoreResponseHelper.GetSetInformationResponse(header, setInformationRequest, share, state);
                        case ReadRequest readRequest:
                            return ReadWriteResponseHelper.GetReadResponse(header, readRequest, share, state);
                        case WriteRequest writeRequest:
                            return ReadWriteResponseHelper.GetWriteResponse(header, writeRequest, share, state);
                        case CheckDirectoryRequest checkDirectoryRequest:
                            return FileStoreResponseHelper.GetCheckDirectoryResponse(header, checkDirectoryRequest, share, state);
                        case WriteRawRequest:
                            // [MS-CIFS] 3.3.5.26 - Receiving an SMB_COM_WRITE_RAW Request:
                            // the server MUST verify that the Server.Capabilities include CAP_RAW_MODE,
                            // If an error is detected [..] the Write Raw operation MUST fail and
                            // the server MUST return a Final Server Response [..] with the Count field set to zero.
                            return new WriteRawFinalResponse();
                        case SetInformation2Request setInformation2Request:
                            return FileStoreResponseHelper.GetSetInformation2Response(header, setInformation2Request, share, state);
                        case LockingAndXRequest lockingAndXRequest:
                            return LockingHelper.GetLockingAndXResponse(header, lockingAndXRequest, share, state);
                        case OpenAndXRequest openAndXRequest:
                            return OpenAndXHelper.GetOpenAndXResponse(header, openAndXRequest, share, state);
                        case ReadAndXRequest readAndXRequest:
                            return ReadWriteResponseHelper.GetReadResponse(header, readAndXRequest, share, state);
                        case WriteAndXRequest writeAndXRequest:
                            return ReadWriteResponseHelper.GetWriteResponse(header, writeAndXRequest, share, state);
                        case FindClose2Request findClose2Request:
                            return CloseHelper.GetFindClose2Response(header, findClose2Request, state);
                        case TreeDisconnectRequest treeDisconnectRequest:
                            return TreeConnectHelper.GetTreeDisconnectResponse(header, treeDisconnectRequest, share, state);
                        case TransactionRequest transactionRequest:
                            return TransactionHelper.GetTransactionResponse(header, transactionRequest, share, state);
                        case TransactionSecondaryRequest transactionSecondaryRequest:
                            return TransactionHelper.GetTransactionResponse(header, transactionSecondaryRequest, share, state);
                        case NTTransactRequest nTTransactRequest:
                            return NTTransactHelper.GetNTTransactResponse(header, nTTransactRequest, share, state);
                        case NTTransactSecondaryRequest nTTransactSecondaryRequest:
                            return NTTransactHelper.GetNTTransactResponse(header, nTTransactSecondaryRequest, share, state);
                        case NTCreateAndXRequest nTCreateAndXRequest:
                            return NTCreateHelper.GetNTCreateResponse(header, nTCreateAndXRequest, share, state);
                        case NTCancelRequest nTCancelRequest:
                            CancelHelper.ProcessNTCancelRequest(header, nTCancelRequest, share, state);
                            // [MS-CIFS] The SMB_COM_NT_CANCEL command MUST NOT send a response.
                            return [];
                    }
                }
            }

            header.Status = NTStatus.STATUS_SMB_BAD_COMMAND;
            return new ErrorResponse(command.CommandName);
        }

        internal static void EnqueueMessage(ConnectionState state, SMB1Message response)
        {
            SessionMessagePacket packet = new()
            {
                Trailer = response.GetBytes()
            };
            state.SendQueue.Enqueue(packet);
            state.LogToServer(Severity.Verbose, "SMB1 message queued: {0} responses, First response: {1}, Packet length: {2}", response.Commands.Count, response.Commands[0].CommandName.ToString(), packet.Length);
        }

        private static void PrepareResponseHeader(SMB1Header responseHeader, SMB1Header requestHeader)
        {
            responseHeader.Status = NTStatus.STATUS_SUCCESS;
            responseHeader.Flags = HeaderFlags.CaseInsensitive | HeaderFlags.CanonicalizedPaths | HeaderFlags.Reply;
            responseHeader.Flags2 = HeaderFlags2.NTStatusCode;
            if ((requestHeader.Flags2 & HeaderFlags2.LongNamesAllowed) > 0)
            {
                responseHeader.Flags2 |= HeaderFlags2.LongNamesAllowed | HeaderFlags2.LongNameUsed;
            }
            if ((requestHeader.Flags2 & HeaderFlags2.ExtendedAttributes) > 0)
            {
                responseHeader.Flags2 |= HeaderFlags2.ExtendedAttributes;
            }
            if ((requestHeader.Flags2 & HeaderFlags2.ExtendedSecurity) > 0)
            {
                responseHeader.Flags2 |= HeaderFlags2.ExtendedSecurity;
            }
            if ((requestHeader.Flags2 & HeaderFlags2.Unicode) > 0)
            {
                responseHeader.Flags2 |= HeaderFlags2.Unicode;
            }
            responseHeader.MID = requestHeader.MID;
            responseHeader.PID = requestHeader.PID;
            responseHeader.UID = requestHeader.UID;
            responseHeader.TID = requestHeader.TID;
        }
    }
}
