using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
    // Projname.vcxproj.filters
    // This file contains the tree-structure for organizing the files in the vcxproj so
    // that in Visual Studio you get a nice folder/file hierarchy.
    //
    // 
    // NAME.vcxproj.filters
    public class MsDevProjectFilters
    {
        private string Filepath { get; set; }
        private Dictionary<string, string> Filters { get; set; }
        private XVars Vars { get; set; }
        private List<string> FileTypes { get; set; }
        private List<string> FileNames { get; set; }
        private List<string> FileFilters { get; set; }

        private HashSet<string> AllowedFileTypes { get; set; }

        public MsDevProjectFilters(string filepath)
        {
            Filepath = filepath;
            Filters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            Vars = new XVars();
            Vars.Set("$(ProjectDir)", "");
            Vars.Set("$(SolutionDir)", "");

            FileTypes = new List<string>(2048);
            FileNames = new List<string>(2048);
            FileFilters = new List<string>(2048);

            AllowedFileTypes = new HashSet<string>();
            AllowedFileTypes.Add("ClCompile");
            AllowedFileTypes.Add("ClInclude");
            AllowedFileTypes.Add("DtpCompile");
            AllowedFileTypes.Add("NXShader");
            AllowedFileTypes.Add("CustomBuild");
        }

        private string AddFilters(string[] parts)
        {
            string path = string.Empty;
            foreach (string part in parts)
            {
                if (path == string.Empty)
                {
                    path = part;
                }
                else
                {
                    path = path + "\\" + part;
                }

                if (!Filters.TryGetValue(path, out string filter))
                {
                    Filters.Add(path, MsDevGUID.Generate(path));
                }
            }
            return path;
        }

        private bool IsValidFilename(string filename)
        {
            return (!filename.Contains("*"));
        }

        public void Add(string filetype, string filename)
        {
            if (AllowedFileTypes.Contains(filetype))
            {
                if (IsValidFilename(filename))
                {
                    // Break down into folders[] / filename
                    string filepath = Vars.Expand(filename);
                    string[] parts = Path.GetDirectoryName(filepath).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    string filter = AddFilters(parts);

                    FileTypes.Add(filetype);
                    FileNames.Add(filename);
                    FileFilters.Add(filter);
                }
            }
        }

        public void Save()
        {
            XNode main = new XNode("Project");
            main.SetAttr("ToolsVersion", "4.0");
            main.SetAttr("xmlns", "http://schemas.microsoft.com/developer/msbuild/2003");

            XNode filters = new XNode("ItemGroup");
            main.AddChild(filters);
            foreach (var f in Filters)
            {
                XNode filter = new XNode("Filter");
                filter.SetAttr("Include", f.Key);
                XNode uuid = new XNode("UniqueIdentifier");
                uuid.SetValue(f.Value);
                filter.AddChild(uuid);
                filters.AddChild(filter);
            }

            XNode fileitems = new XNode("ItemGroup");
            main.AddChild(fileitems);
            for (int i = 0; i < FileNames.Count; ++i)
            {
                XNode fileitem = new XNode(FileTypes[i]);
                fileitem.SetAttr("Include", FileNames[i]);
                XNode filefilter = new XNode("Filter");
                filefilter.SetValue(FileFilters[i]);
                fileitem.AddChild(filefilter);
                fileitems.AddChild(fileitem);
            }

			StringBuilder sb = new StringBuilder();
			main.ToXml(sb);
			File.WriteAllText(Filepath, sb.ToString());
        }
    }
}