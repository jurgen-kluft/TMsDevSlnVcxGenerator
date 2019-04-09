namespace Glob
{
    enum TokenKind
    {
        Wildcard = 0,               // *
        CharacterWildcard = 1,      // ?
        DirectoryWildcard = 2,      // **

        CharacterSetStart = 3,     // [
        CharacterSetEnd = 4,       // ]
        CharacterSetInvert = 1,      // !

        LiteralSetStart = 5,       // {
        LiteralSetSeperator = 6,   // ,
        LiteralSetEnd = 7,         // }

        PathSeparator = 8,          // / \

		Identifier = 11,              // Letter or Number

        WindowsRoot = 20,           // :

        EOT = 100,
    }
}