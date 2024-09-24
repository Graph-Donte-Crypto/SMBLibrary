using System;

namespace SMBLibrary
{
    /// <summary>
    /// [MS-DTYP] 2.4.3 - ACCESS_MASK
    /// </summary>
    [Flags]
    public enum AccessMask : uint
    {
        // The bits in positions 16 through 31 are object specific.
        DELETE = 0x00010000,
        READ_CONTROL = 0x00020000,
        WRITE_DAC = 0x00040000,
        WRITE_OWNER = 0x00080000,
        SYNCHRONIZE = 0x00100000,
        ACCESS_SYSTEM_SECURITY = 0x01000000,
        MAXIMUM_ALLOWED = 0x02000000,
        GENERIC_ALL = 0x10000000,
        GENERIC_EXECUTE = 0x20000000,
        GENERIC_WRITE = 0x40000000,
        GENERIC_READ = 0x80000000,
    }

    public struct Access(AccessMask mask)
    {
        public bool DELETE = mask.HasFlag(AccessMask.DELETE);
        public bool READ_CONTROL = mask.HasFlag(AccessMask.READ_CONTROL);
        public bool WRITE_DAC = mask.HasFlag(AccessMask.WRITE_DAC);
        public bool WRITE_OWNER = mask.HasFlag(AccessMask.WRITE_OWNER);
        public bool SYNCHRONIZE = mask.HasFlag(AccessMask.SYNCHRONIZE);
        public bool ACCESS_SYSTEM_SECURITY = mask.HasFlag(AccessMask.ACCESS_SYSTEM_SECURITY);
        public bool MAXIMUM_ALLOWED = mask.HasFlag(AccessMask.MAXIMUM_ALLOWED);
        public bool GENERIC_ALL = mask.HasFlag(AccessMask.GENERIC_ALL);
        public bool GENERIC_EXECUTE = mask.HasFlag(AccessMask.GENERIC_EXECUTE);
        public bool GENERIC_WRITE = mask.HasFlag(AccessMask.GENERIC_WRITE);
        public bool GENERIC_READ = mask.HasFlag(AccessMask.GENERIC_READ);
    }
}
