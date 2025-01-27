using System;

namespace LibISULR.Flags
{
  [Flags]
  enum RegFlags : uint
  {
    RegKeyHandleMask = 0x80FFFFFF,
    Reg64BitKey = 0x01000000,
  }
}