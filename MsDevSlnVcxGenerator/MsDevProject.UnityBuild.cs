using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
    public static class MsDevProjectUnityBuild
    {
        public static void ScanCongoFile(string filepath, List<string> includeDirectories, out List<string> filesincluded)
        {
			filesincluded = new List<string>();
			if (File.Exists(filepath))
            {
                string[] lines = File.ReadAllLines(filepath);
                foreach (string l in lines)
                {
                    string line = l.Trim();
                    if (line.StartsWith("#include"))
                    {
						//
						// The following statement is used to include a source file
						// Possibly followed by a comment ('// Comment' or '/* Comment */') on the same line
						//
						// #include "name.cpp"\
						//

						//
						// I do not believe '<' - '>' are used, e.g.:
						//                        
						// #include <name.cpp>
						//
						// Well it seems they are used! Let's replace those characters.
						//
						line = line.Replace("<", "\"");
						line = line.Replace(">", "\"");

						string include = line.Between('"', '"');
						include = include.Replace("/", "\\");

						// It seems Conglomerate_*.cpp files also include header files.
						// We here are only interested in *.cpp and *.c files.
						if (include.EndsWith(".cpp") || include.EndsWith(".c"))
						{
							// Relative to current unity build file?
							foreach (string basepath in includeDirectories)
							{
								string includepath = Path.Combine(basepath, include);
								if (File.Exists(includepath))
								{
									filesincluded.Add(includepath);
									break;
								}
							}
						}
                    }
                }
            }
        }
    }
}
