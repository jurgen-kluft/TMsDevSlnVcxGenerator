using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
    public class MsDevSolution
    {
        public string GUID { get; set; }
        public string Name { get; set; }
        public string SlnDir { get; set; }
        public string Filename { get; set; }
        public string SlnVersion { get; set; }
        public string MsVcVersion { get; set; }
        public string MsVcName { get; set; }
        public string PlatformToolset { get; set; }
        private List<Platform> SlnPlatforms { get; set; }
        private List<string> SlnConfigs { get; set; }

        public ProjectList ListOfProjects { get; set; }

        private XNode Main { get; set; }

        public class ProjectList
        {
            class Project
            {
				public string Name { get; private set; }
				public MsDevProject Instance { get; private set; }
				public XNode Node { get; }

				private string Main { get; set; }
				private string Settings { get; set; }
				private string Reference { get; set; }
				private HashSet<string> Dependencies { get; set; }
				private List<string> MergeList{ get; set; }
				private HashSet<string> MergeSet { get; set; }
				private HashSet<string> Packages { get; set; }

				private List<Platform> Platforms { get; set; }
				public List<string> Configs { get; set; }
				private HashSet<string> ConfigSet { get; set; }
				private Dictionary<string, string> ConfigMapping { get; set; }

				public Project(string prjname, string prjdir, XNode node, string slndir, List<Platform> platforms, Dictionary<string,string> configmap)
                {
					Name = prjname;
                    Instance = null;
                    Node = node;
					Main = string.Empty;
					Settings = string.Empty;
					Reference = string.Empty;
					Dependencies = new HashSet<string>();
					MergeList = new List<string>();
					MergeSet = new HashSet<string>();
					Packages = new HashSet<string>();

					Platforms = new List<Platform>();
					Configs = new List<string>();
					ConfigSet = new HashSet<string>();
					ConfigMapping = new Dictionary<string, string>();

					foreach(Platform p in platforms)
					{
						Platforms.Add(p);
					}
					foreach (var c in configmap)
					{
						AddConfig(c.Key, c.Value);
					}
					string prjguid = MsDevGUID.Generate(prjname + prjdir);
					Instance = new MsDevProject(prjname, prjdir, prjguid, slndir, Platforms, Configs);
				}

				public bool HasPlatform(Platform platform)
				{
					foreach(Platform p in Platforms)
					{
						if (p.Index == platform.Index)
							return true;
					}
					return false;
				}

				public string MapConfig(string slnconfig)
				{
					if (ConfigMapping.ContainsKey(slnconfig))
					{
						return ConfigMapping[slnconfig];
					}
					return slnconfig;
				}

				private void AddConfig(string config, string target)
				{
					if (!ConfigMapping.ContainsKey(config))
					{
						ConfigMapping.Add(config, target);
					}
					else
					{
						ConfigMapping[config] = target;
					}
					if (!ConfigSet.Contains(target))
					{
						ConfigSet.Add(target);
						Configs.Add(target);
					}
				}

                public void SetSettings(string filepath)
                {
					Settings = filepath;
				}

                public void SetMain(string filepath)
                {
					Main = filepath;
                }

				public void SetReference(string filepath)
				{
					Reference = filepath;
				}

				public void AddDependency(string filepath)
				{
					Dependencies.Add(filepath);
				}

				public void AddMerge(string filepath)
                {
					if (!MergeSet.Contains(filepath))
					{
						MergeSet.Add(filepath);
						MergeList.Add(filepath);
					}
				}

                public void AddPackage(string filepath)
                {
					Packages.Add(filepath);
                }

                public void ResolveSource()
                {
                    Instance.ResolveSource();
                }

				public void LoadSettings()
				{
					Instance.LoadSettings(Settings);
				}
				public void LoadMain()
				{
					if (!String.IsNullOrWhiteSpace(Main))
					{
						Instance.LoadMain(Main);
					}
				}
				public void LoadReference()
				{
					if (!String.IsNullOrEmpty(Reference))
					{
						Instance.LoadReference(Reference);
					}
				}
				public void Merge()
				{
					foreach (string package in Packages)
					{
						AddMerge(package);
					}

					foreach (string merge in MergeList)
					{
						Instance.Merge(merge);
					}
				}

				public void CollectVars()
				{
					Instance.CollectVars();
				}
			}

			private string SlnDir { get; set; }
			private List<string> SlnConfigs { get; set; }
			private List<Platform> SlnPlatforms { get; set; }

			private Dictionary<string, Project> Name2Project { get; set; }

            private List<Project> Projects { get; set; }

			private Dictionary<Project, HashSet<string>> ProjectDependencies { get; set; }
			private Dictionary<string, HashSet<string>> ProjectPackages { get; set; }

            public ProjectList(string slndir, List<string> slnconfigs, List<Platform> slnplatforms)
            {
				SlnDir = slndir;
				SlnConfigs = new List<string>();
				SlnConfigs.AddRange(slnconfigs);
				SlnPlatforms = new List<Platform>();
				SlnPlatforms.AddRange(slnplatforms);

				Name2Project = new Dictionary<string, Project>(StringComparer.InvariantCultureIgnoreCase);
                Projects = new List<Project>();
				ProjectDependencies = new Dictionary<Project, HashSet<string>>();
				ProjectPackages = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
			}

			public void Save()
			{
				foreach (Project p in Projects)
				{
					p.Instance.Save();
				}
			}

			public void Foreach(Action<string, string, string> action)
			{
				foreach (Project p in Projects)
				{
					action(p.Instance.Name, p.Instance.PrjDir, p.Instance.GUID);
				}
			}

			public void Foreach(string slnconfig, Platform slnplatform, Action<string, string, string, string, string> action)
			{
				foreach (Project p in Projects)
				{
					if (p.HasPlatform(slnplatform))
					{
						string config = p.MapConfig(slnconfig);
						action(p.Instance.Name, p.Instance.PrjDir, p.Instance.GUID, config, slnplatform.ToString());
					}
				}
			}

			private Project AddProject(string prjname, string prjdir, XNode node, List<Platform> platforms, Dictionary<string,string> configmap)
            {
				ProjectList.Project project = new Project(prjname, prjdir, node, SlnDir, platforms, configmap);
				Name2Project.Add(prjname, project);
                Projects.Add(project);
                return project;
            }

            public void Construct()
            {
				foreach (var prj in Projects)
				{
					prj.LoadSettings();
				}
				foreach (var prj in Projects)
				{
					prj.LoadMain();
				}
				foreach (var prj in Projects)
				{
					prj.LoadReference();
				}
				foreach(var prj in ProjectDependencies)
				{
					Project project = prj.Key;
					Console.WriteLine("  Project: {0}", project.Name);

					HashSet<string> dependencies = prj.Value;
					foreach (string dependency in dependencies)
					{
						if (Name2Project.TryGetValue(dependency, out Project depprj))
						{
							Console.WriteLine("     Dependency: {0}", depprj.Name);
							project.Instance.MergeProjectReference(depprj.Instance);
						}

						// Add the packages of this dependency to the project
						if (ProjectPackages.TryGetValue(dependency, out HashSet<string> packages))
						{
							foreach (string package in packages)
							{
								project.AddPackage(package);
							}
						}
					}
				}

				foreach (var prj in Projects)
				{
					prj.Merge();
				}

				foreach (var prj in Projects)
				{
					prj.CollectVars();
				}

				foreach (var prj in Projects)
				{
					prj.ResolveSource();
				}
			}

			public void Add(XNode node)
            {
                List<Platform> platforms = new List<Platform>();
                foreach (XNode pc in node.Children)
                {
                    if (pc.Name == "Platform")
                    {
                        platforms.Add(new Platform(pc.Value));
                    }
                }
                if (platforms.Count == 0)
                {
                    platforms.AddRange(SlnPlatforms);
                }
				Dictionary<string, string> configMapping = new Dictionary<string, string>();
				foreach (string config in SlnConfigs)
				{
					configMapping.Add(config, config);
				}
				foreach (XNode pc in node.Children)
				{
					if (pc.Name == "Configuration")
					{
						string[] mapping = pc.Value.Split('=');
						configMapping[mapping[0].Trim()] = mapping[1].Trim();
					}
				}

				string name = node.GetAttrValue("Name");
				string prjpath = Path.Combine(SlnDir, node.GetAttrValue("Path"));
				Project project = AddProject(name, prjpath, node, platforms, configMapping);

				HashSet<string> dependencies = new HashSet<string>();
				HashSet<string> packages = new HashSet<string>();
                foreach (XNode pc in node.Children)
                {
                    if (pc.Name == "Settings")
                    {
                        string filepath = Path.Combine(Environment.CurrentDirectory, pc.Value);
                        project.SetSettings(filepath);
						break;
                    }
                }
                foreach (XNode pc in node.Children)
                {
                    if (pc.Name == "Load")
                    {
                        string filepath = Path.Combine(Environment.CurrentDirectory, pc.Value);
                        project.SetMain(filepath);
						break;
					}
				}
				foreach (XNode pc in node.Children)
				{
					if (pc.Name == "Reference")
					{
						string filepath = Path.Combine(Environment.CurrentDirectory, pc.Value);
						project.SetReference(filepath);
						break;
					}
				}
				foreach (XNode pc in node.Children)
				{
					if (pc.Name == "Dependency")
					{
						dependencies.Add(pc.Value);
					}
				}
				foreach (XNode pc in node.Children)
                {
                    if (pc.Name == "Merge")
                    {
                        string filepath = Path.Combine(Environment.CurrentDirectory, pc.Value);
                        project.AddMerge(filepath);
                    }
                }
                foreach (XNode pc in node.Children)
                {
                    if (pc.Name == "Package")
                    {
                        string filepath = Path.Combine(Environment.CurrentDirectory, pc.Value);
						packages.Add(filepath);
                    }
                }

				ProjectDependencies.Add(project, dependencies);
				ProjectPackages.Add(name, packages);
            }

        }

        public MsDevSolution(string dirpath)
        {
            MsVcVersion = "15.00";
            SlnVersion = "12.00";
            MsVcName = "15";
            PlatformToolset = "v141";

            SlnDir = dirpath.EnsureEndsWith('\\');
            SlnPlatforms = new List<Platform>();
            SlnConfigs = new List<string>();
        }

        private void ForeachPlatformConfig(Action<Platform, string> action)
        {
            foreach (var platform in SlnPlatforms)
            {
                foreach (var config in SlnConfigs)
                {
                    action(platform, config);
                }
            }
        }

        public void Load(string filename)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(Path.Combine(Environment.CurrentDirectory, filename));
            if (xmldoc.HasChildNodes)
            {
                XXmlToXNode builder = new XXmlToXNode();
                Main = builder.FromXmlDoc(xmldoc);

                // Construct the projects
                Name = Main.GetAttrValue("Name");
                string slnfilepath = Main.GetAttrValue("Path");
                SlnDir = Path.Combine(SlnDir, Path.GetDirectoryName(slnfilepath)).EnsureEndsWith('\\');
                Filename = Path.GetFileName(slnfilepath);
                GUID = MsDevGUID.Generate(Name + slnfilepath);

				Console.WriteLine("Solution: {0}", Name);

				foreach (XNode c in Main.Children)
                {
                    if (c.Name == "Platform")
                    {
                        SlnPlatforms.Add(new Platform(c.Value));
						Console.WriteLine("  Platforms: {0}", c.Value);
					}
					else if (c.Name == "Configuration")
                    {
                        SlnConfigs.Add(c.Value);
						Console.WriteLine("  Configuration: {0}", c.Value);
					}
				}

				ListOfProjects = new ProjectList(SlnDir, SlnConfigs, SlnPlatforms);

				foreach (XNode c in Main.Children)
                {
                    if (c.Name == "Project")
                    {
                        ListOfProjects.Add(c);
                    }
                }

                ListOfProjects.Construct();
            }
        }


        public void Save()
        {
			Console.WriteLine("Saving projects ...");
			ListOfProjects.Save();

			Console.WriteLine("Saving solution ...");
			MsDevSolutionFile writer = new MsDevSolutionFile(Path.Combine(SlnDir, Filename));

            writer.AppendLine("Microsoft Visual Studio Solution File, Format Version " + SlnVersion);
            writer.AppendLine("# Visual Studio " + MsVcName);

			ListOfProjects.Foreach(delegate (string prjname, string prjfilepath, string prjguid)
			{
				writer.AppendLine("Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"", GUID, prjname, prjfilepath, prjguid);
				writer.AppendLine("EndProject");
			});

            writer.AppendLine("\nGlobal");
            writer.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            ForeachPlatformConfig(delegate (Platform platform, string config)
            {
                writer.AppendLine("\t\t{0}|{1} = {0}|{1}", config, platform.Name);
            });
            writer.AppendLine("\tEndGlobalSection");
            writer.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

            ForeachPlatformConfig(delegate (Platform platform, string config)
            {
				//writer.AppendLine("\t\t{0}.{1}|{2}.ActiveCfg = {1}|{2}", GUID, config, platform);
				//writer.AppendLine("\t\t{0}.{1}|{2}.Build.0 = {1}|{2}", GUID, config, platform);

				ListOfProjects.Foreach(config, platform, delegate (string prjname, string prjfilepath, string prjguid, string prjconfig, string prjplatform)
				{
					writer.AppendLine("\t\t{0}.{1}|{2}.ActiveCfg = {3}|{4}", prjguid, config, platform, prjconfig, prjplatform);
					writer.AppendLine("\t\t{0}.{1}|{2}.Build.0 = {3}|{4}", prjguid, config, platform, prjconfig, prjplatform);

					// Make sure the 'Deploy' checkbox under Configuration Manager is checked for Durango
					//if (platform.Durango)
					//	writer.AppendLine("\t\t{0}.{1}|{2}.Deploy.0 = {1}|{2}", GUID, config, platform);

				});
            });

            writer.AppendLine("\tEndGlobalSection");
            writer.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
            writer.AppendLine("\t\tHideSolutionNode = FALSE");
            writer.AppendLine("\tEndGlobalSection");
            writer.AppendLine("EndGlobal");

            writer.Close();
        }
    }

    public class MsDevSolutionFile
    {
        public string Filepath { get; set; }
        private List<string> Lines { get; set; }
        public MsDevSolutionFile(string filepath)
        {
            Filepath = Path.GetFullPath(filepath);
            Lines = new List<string>();
        }

        public void AppendLine(string line)
        {
            Lines.Add(line);
        }


        public void AppendLine(string format, params object[] arg)
        {
            Lines.Add(String.Format(format, arg));
        }

        public void Close()
        {
            // Open/Create file
            // Write out all lines
            // Close the file
            string[] lines = Lines.ToArray();
            File.WriteAllLines(Filepath, lines);
        }
    }
}
