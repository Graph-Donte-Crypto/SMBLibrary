/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using SMBLibrary.Authentication.GSSAPI;

namespace SMBLibrary
{
    public class SecurityContext
    {
        public string UserName { get; private set; }
        public string MachineName { get; private set; }
        public IPEndPoint ClientEndPoint { get; private set; }
        public GSSContext AuthenticationContext;
        public object AccessToken;

        public SecurityContext(string userName, string machineName, IPEndPoint clientEndPoint, GSSContext authenticationContext, object accessToken)
        {
            UserName = userName;
            MachineName = machineName;
            ClientEndPoint = clientEndPoint;
            AuthenticationContext = authenticationContext;
            AccessToken = accessToken;
        }
    }
}
