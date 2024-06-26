/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.SMB1;
using Utilities;

namespace SMBLibrary.Server.SMB1
{
    /// <summary>
    /// Negotiate helper
    /// </summary>
    internal class NegotiateHelper
    {
        public const ushort ServerMaxMpxCount = 50;
        public const ushort ServerNumberVcs = 1;
        public const ushort ServerMaxBufferSize = 65535;
        public const uint ServerMaxRawSize = 65536;

        internal static NegotiateResponse GetNegotiateResponse(SMB1Header header, NegotiateRequest request, GSSProvider securityProvider, ConnectionState state)
        {
            NegotiateResponse response = new()
            {
                DialectIndex = (ushort)request.Dialects.IndexOf(SMBServer.NTLanManagerDialect),
                SecurityMode = SecurityMode.UserSecurityMode | SecurityMode.EncryptPasswords,
                MaxMpxCount = ServerMaxMpxCount,
                MaxNumberVcs = ServerNumberVcs,
                MaxBufferSize = ServerMaxBufferSize,
                MaxRawSize = ServerMaxRawSize,
                Capabilities = Capabilities.Unicode |
                                    Capabilities.LargeFiles |
                                    Capabilities.NTSMB |
                                    Capabilities.RpcRemoteApi |
                                    Capabilities.NTStatusCode |
                                    Capabilities.NTFind |
                                    Capabilities.InfoLevelPassthrough |
                                    Capabilities.LargeRead |
                                    Capabilities.LargeWrite,
                SystemTime = DateTime.UtcNow,
                ServerTimeZone = (short)-TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes,
                DomainName = String.Empty,
                ServerName = String.Empty
            };
            
            NegotiateMessage negotiateMessage = CreateNegotiateMessage();
            NTStatus status = securityProvider.GetNTLMChallengeMessage(out state.AuthenticationContext, negotiateMessage, out ChallengeMessage challengeMessage);
            if (status == NTStatus.SEC_I_CONTINUE_NEEDED)
            {
                response.Challenge = challengeMessage.ServerChallenge;
            }
            

            return response;
        }

        internal static NegotiateResponseExtended GetNegotiateResponseExtended(NegotiateRequest request, Guid serverGuid)
        {
            NegotiateResponseExtended response = new()
            {
                DialectIndex = (ushort)request.Dialects.IndexOf(SMBServer.NTLanManagerDialect),
                SecurityMode = SecurityMode.UserSecurityMode | SecurityMode.EncryptPasswords,
                MaxMpxCount = ServerMaxMpxCount,
                MaxNumberVcs = ServerNumberVcs,
                MaxBufferSize = ServerMaxBufferSize,
                MaxRawSize = ServerMaxRawSize,
                Capabilities = Capabilities.Unicode |
                                    Capabilities.LargeFiles |
                                    Capabilities.NTSMB |
                                    Capabilities.RpcRemoteApi |
                                    Capabilities.NTStatusCode |
                                    Capabilities.NTFind |
                                    Capabilities.InfoLevelPassthrough |
                                    Capabilities.LargeRead |
                                    Capabilities.LargeWrite |
                                    Capabilities.ExtendedSecurity,
                SystemTime = DateTime.UtcNow,
                ServerTimeZone = (short)-TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes,
                ServerGuid = serverGuid
            };

            return response;
        }

        private static NegotiateMessage CreateNegotiateMessage()
        {
            NegotiateMessage negotiateMessage = new()
            {
                NegotiateFlags = NegotiateFlags.UnicodeEncoding |
                                              NegotiateFlags.OEMEncoding |
                                              NegotiateFlags.Sign |
                                              NegotiateFlags.LanManagerSessionKey |
                                              NegotiateFlags.NTLMSessionSecurity |
                                              NegotiateFlags.AlwaysSign |
                                              NegotiateFlags.Version |
                                              NegotiateFlags.Use128BitEncryption |
                                              NegotiateFlags.Use56BitEncryption,
                Version = NTLMVersion.Server2003
            };
            return negotiateMessage;
        }
    }
}
