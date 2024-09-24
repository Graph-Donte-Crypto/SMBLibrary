/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System.IO;
using SMBLibrary;
using SMBLibrary.Server;

namespace SMBServer
{
    public class VirtualShare(string name) : ISMBShare
    {
        public string Name { get; set; } = name;

        public INTFileStore FileStore { get; set; } = new VirtualNTFileStore();

        public CachingPolicy CachingPolicy => CachingPolicy.NoCaching;

        public bool IsFileSystemShare => true;

        public bool HasAccess(SecurityContext securityContext, string path, FileAccess requestedAccess) => true;
    }
}