using System;
using System.Text;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
    public struct Platform
    {
        private static readonly List<string> PlatformNames = new List<string>()
        {
            "Win32",
            "x64",
            "ORBIS",
            "Durango"
        };
        private static readonly Dictionary<string, int> PlatformIndex = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase)
        {
            {"Win32", 0 },
            {"x64", 1 },
            {"ORBIS", 2 },
            {"Durango", 3 }
        };

        public string Name { get; private set; }
        public int Index { get; private set; }
        public Platform(string p)
        {
            Index = PlatformIndex[p];
            Name = PlatformNames[Index];
        }

        public bool Win32 { get { return Index == 0; } }
        public bool x64 { get { return Index == 1; } }
        public bool Orbis { get { return Index == 2; } }
        public bool Durango { get { return Index == 3; } }

        public override string ToString()
        {
            return Name;
        }
    }
}
