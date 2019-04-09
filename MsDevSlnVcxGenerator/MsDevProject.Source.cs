using System;
using System.IO;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
    //
    // Problem:
    // - UnityBuild (Congo) files exclude items that are not yet in the list of included files
    // 
    // Solution:
    //  - UnityBuild items are processed last and will apply to the final state of included files
    // 

    //
    // Problem:
    // - Exclude files from a build
    //
    // Solution:
    // - More explicit control and export of Xml to exclude an item:
    //       
    //       This can be accomplished by using Glob (Config|Platform), here an example:
    //       
    //       ""                              - Not excluded
    //       "*|*"                           - Excluded in all configurations and for all platforms 
    //       "{Debug}|{ORBIS}"               - Excluded in Debug for Orbis
    //       "{QA,Final}|{ORBIS,Durango}"    - Excluded in QA and Final for Orbis and Durango

    public class XSource
    {

		public class XState
		{
			private int StateId { get; set; }

			public bool IsIncluded { get { return StateId == 0; }  }
			public bool IsRemoved { get { return StateId == 2; }  }

			private XState(int state)
			{
				StateId = state;
			}

			public XState(XState o) : this(o.StateId)
			{
			}

			public void Set(XState state)
			{
				StateId = state.StateId;
			}

			public static readonly XState Included = new XState(0);
			public static readonly XState Removed = new XState(2);
		}

		public class XExclude
		{
			private HashSet<string> Items { get; set; }

			public XExclude()
			{
				Items = new HashSet<string>();
			}
			public XExclude(string exclude) : this()
			{
				Join(exclude);
			}

			public void Join(string exclude)
			{
				if (String.IsNullOrEmpty(exclude))
					return;

				Items.Add(exclude);
			}

			public bool IsEmpty()
			{
				return Items.Count == 0;
			}

			public bool IsExcluded(string configuration, string platform)
			{
				string config = String.Format("{0}|{1}", configuration, platform);
				foreach(string ex in Items)
				{
					if (ex == "*")
						return true;
					Glob.CachingGlob glob = new Glob.CachingGlob(ex);
					if (glob.IsMatch(config))
						return true;
				}
				return false;
			}
		}

		private class Group
        {
			public string Name { get; set; }
            private List<XState> States { get; set; }
            private List<XExclude> Excludes { get; set; }
			private List<XNode> Nodes { get; set; }
			private Dictionary<string, int> Items { get; set; }


			public Group(string name)
			{
				Name = name;
				States = new List<XState>();
				Excludes = new List<XExclude>();
				Nodes = new List<XNode>();
				Items = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
			}

			public void Include(string filename, XNode node, string exclude)
			{
				if (!Items.TryGetValue(filename, out int index))
				{
					index = States.Count;
					Items.Add(filename, index);
					Nodes.Add(node);
					States.Add(new XState(XState.Included));
					Excludes.Add(new XExclude(exclude));
				}
				else
				{
					Excludes[index].Join(exclude);
					Nodes[index] = node;
				}
			}

			public void Update(string filename, string exclude)
			{
				if (Items.TryGetValue(filename, out int index))
				{
					Excludes[index].Join(exclude);
				}
			}

			public void Remove(string filename)
			{
				if (!Items.TryGetValue(filename, out int index))
				{
					index = States.Count;
					Items.Add(filename, index);

					XNode node = new XNode(Name, null);
					node.SetAttr("Include", filename);
					Nodes.Add(node);

					States.Add(new XState(XState.Removed));
					Excludes.Add(new XExclude());
				}
				else
				{
					States[index].Set(XState.Removed);
				}
			}

			public void Foreach(Action<string, XNode, XState, XExclude> action)
			{
				foreach (var e in Items)
				{
					string item = e.Key;
					int index = e.Value;
					XState excluded = States[index];
					XExclude exclude = Excludes[index];
					XNode node = Nodes[index];
					action(item, node, excluded, exclude);
				}
			}

			public void Foreach(List<string> items, Action<string, XNode, XState, XExclude> action)
			{
				foreach (var e in items)
				{
					if (Items.TryGetValue(e, out int index))
					{
						string item = e;
						XState excluded = States[index];
						XExclude exclude = Excludes[index];
						XNode node = Nodes[index];
						action(item, node, excluded, exclude);
					}
				}
			}
		}

		private List<string> Configurations { get; set; }
		private List<Platform> Platforms { get; set; }
		private List<XVars> Vars { get; set; }

        // ClCompile, ClInclude, NXShader, NXShaderHdr, DtpCompile, CustomBuild
        private Dictionary<string, Group> Groups { get; set; }
		private List<XNode> Definitions { get; set; }
		private MsDevProjectMergeSettings MergeSettings { get; set; }

		public XSource(List<string> configurations, List<Platform> platforms, MsDevProjectMergeSettings mergesettings)
		{
			Configurations = configurations;
			Platforms = platforms;
			Vars = new List<XVars>();
			Groups = new Dictionary<string, Group>(StringComparer.InvariantCultureIgnoreCase);
			Definitions = new List<XNode>();
			MergeSettings = mergesettings;
		}

		private void SetIncluded(string groupname, string filename, XNode node, string excluded)
		{
			if (!Groups.TryGetValue(groupname, out Group group))
			{
				group = new Group(groupname);
				Groups.Add(groupname, group);
			}
			group.Include(filename, node, excluded);
		}

		private void UpdateIncluded(string groupname, string filename, string excluded)
		{
			if (Groups.TryGetValue(groupname, out Group group))
			{
				group.Update(filename, excluded);
			}
		}

		private void SetRemoved(string groupname, string filename)
        {
			if (!Groups.TryGetValue(groupname, out Group group))
			{
				group = new Group(groupname);
				Groups.Add(groupname, group);
			}
			group.Remove(filename);
		}

        public void AddDefinition(XNode node, XVars vars)
        {
            Definitions.Add(node);
			Vars.Add(vars);
        }

		private XNode ProjectNode { get; set; }
		private XNode SourceNode { get; set; }

        public bool ConvertToXml(out XNode xml)
        {

			foreach(var group in Groups)
			{
				group.Value.Foreach(delegate (string filename, XNode node, XState state, XExclude exclude)
				{
					if (state.IsIncluded)
					{
						if (!exclude.IsEmpty())
						{
							foreach (string c in Configurations)
							{
								foreach (Platform p in Platforms)
								{
									if (exclude.IsExcluded(c, p.ToString()))

									{   // Add 'ExcludedFromBuild' for this Config | Platform
										XNode exclude_node = new XNode("ExcludedFromBuild", node);
										string config = String.Format("{0}|{1}", c, p.ToString());
										exclude_node.SetAttr("Condition", String.Format("'$(Configuration)|$(Platform)'=='{0}'", config));
										exclude_node.SetValue("true");
										node.AddChild(exclude_node);
									}
								}
							}
						}
						SourceNode.AddChild(node);
					}
				});
			}
			xml = ProjectNode;
			return true;
        }


		public void Process()
		{
			ProjectNode = new XNode("Project", null);
			SourceNode = new XNode("ItemGroup", ProjectNode);
			SourceNode.SetAttr("Label", "Source");
			ProjectNode.AddChild(SourceNode);

			XMatchAndMerge matchandmerge = new XMatchAndMerge(MergeSettings);

			for (int i = 0; i < Definitions.Count; ++i)
			{
				XNode root = Definitions[i];
				XVars vars = Vars[i];

				foreach (XNode node in root.Children)
				{
					if (node.HasAttr("Include"))
					{
						string groupname = node.Name;
						string include_descr = node.GetAttrValue("Include");
						if (include_descr.StartsWith("SCAN->"))
						{
							include_descr = include_descr.Remove(0, 6);

							List<string> globbed_includes = DoGlobbing(include_descr, vars);
							foreach (string globbed_include in globbed_includes)
							{
								XNode item_node = new XNode(node, SourceNode);
								item_node.SetAttr("Include", globbed_include);
								item_node.RemoveMetaAttrs();
								string excluded = node.GetAttrValue("Exclude_");
								SetIncluded(groupname, globbed_include, item_node, excluded);
							}
						}
						if (include_descr.StartsWith("SCAN-NDFN->"))
						{
							// SCAN2 = SCAN with no duplicate filenames

							// First build database of existing items by filename
							Dictionary<string, string> existing_items = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
							Group group = GetGroup(groupname);
							group.Foreach(delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
							{
								existing_items.Add(Path.GetFileName(item_filename), item_filename);
							});

							// Now scan the folder and add any file item with a filename that hasn't been seen before
							include_descr = include_descr.Remove(0, 11);
							List<string> globbed_includes = DoGlobbing(include_descr, vars);
							foreach (string globbed_include in globbed_includes)
							{
								if (!existing_items.ContainsKey(Path.GetFileName(globbed_include)))
								{
									XNode item_node = new XNode(node, SourceNode);
									item_node.SetAttr("Include", globbed_include);
									item_node.RemoveMetaAttrs();
									string excluded = node.GetAttrValue("Exclude_");
									SetIncluded(groupname, globbed_include, item_node, excluded);
								}
							}
						}
						else if (include_descr.IndexOf("->") >= 0)
						{
							include_descr = include_descr.Remove(0, 2);

							XNode item_node = new XNode(node, SourceNode);
							item_node.SetAttr("Include", include_descr);
							item_node.RemoveMetaAttrs();

							string excluded = node.GetAttrValue("Exclude_");
							SetIncluded(groupname, include_descr, item_node, excluded);

							// Merge children of node to item_node
							matchandmerge.Merge(item_node, node);
						}
						else if (include_descr.IndexOf('*') >= 0)
						{
							Group group = GetGroup(groupname);
							var glob = new Glob.CachingGlob(include_descr);
							group.Foreach(delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
							{
								if (glob.IsMatch(item_filename))
								{
									if (item_state.IsIncluded)
									{
										string exclude = node.GetAttrValue("Exclude_");
										item_exclude.Join(exclude);

										// Merge children of node to item_node
										matchandmerge.Merge(item_node, node);
									}
								}
							});
						}
						else
						{
							bool item_exists = false;
							Group group = GetGroup(groupname);
							List<string> items = new List<string>() { include_descr };
							group.Foreach(items, delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
							{
								item_exists = true;
								if (item_state.IsIncluded)
								{
									string exclude = node.GetAttrValue("Exclude_");
									item_exclude.Join(exclude);

									// Merge children of node to item_node
									matchandmerge.Merge(item_node, node);
								}
							});

							if (!item_exists)
							{
								XNode item_node = new XNode(node, SourceNode);
								item_node.SetAttr("Include", include_descr);
								item_node.RemoveMetaAttrs();

								string excluded = node.GetAttrValue("Exclude_");
								SetIncluded(groupname, include_descr, item_node, excluded);

								// Merge children of node to item_node
								matchandmerge.Merge(item_node, node);
							}
						}
					}
					else if (node.HasAttr("Create"))
					{
						string groupname = node.Name;
						string include_descr = node.GetAttrValue("Create");
						node.RenameAttr("Create", "Include");
						string content = node.Value;
						node.SetValue(string.Empty);

						bool item_exists = false;
						Group group = GetGroup(groupname);
						List<string> items = new List<string>() { include_descr };
						group.Foreach(items, delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
						{
							item_exists = true;
							if (item_state.IsIncluded)
							{
								string exclude = node.GetAttrValue("Exclude_");
								item_exclude.Join(exclude);

								// Merge children of node to item_node
								matchandmerge.Merge(item_node, node);
							}
						});
						if (!item_exists)
						{
							XNode item_node = new XNode(node, SourceNode);
							item_node.SetAttr("Include", include_descr);
							item_node.RemoveMetaAttrs();

							string excluded = node.GetAttrValue("Exclude_");
							SetIncluded(groupname, include_descr, item_node, excluded);

							// Merge children of node to item_node
							matchandmerge.Merge(item_node, node);
						}


						// Create the file with content from Value
						vars.Get("$(ProjectDir)", out string basepath);
						basepath = basepath.EnsureEndsWith('\\');
						FileInfo fi = new FileInfo(Path.Combine(basepath, include_descr));
						StreamWriter sw = fi.CreateText();
						sw.WriteLine(content);
						sw.Close();
					}
					else if (node.HasAttr("Remove"))
					{
						string groupname = node.Name;
						Group group = GetGroup(groupname);
						string remove_descr = node.GetAttrValue("Remove");
						if (remove_descr.IndexOf('*') >= 0)
						{
							var glob = new Glob.CachingGlob(remove_descr);
							group.Foreach(delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
							{
								if (glob.IsMatch(item_filename))
								{
									if (item_state.IsIncluded)
									{
										// Mark item as removed
										item_state.Set(XState.Removed);
									}
								}
							});
						}
						else
						{
							// No globbing, just find it
							group.Foreach(delegate (string item_filename, XNode item_node, XState item_state, XExclude item_exclude)
							{
								if (String.Compare(remove_descr, item_filename, StringComparison.OrdinalIgnoreCase) == 0)
								{
									if (item_state.IsIncluded)
									{
										// Mark item as removed
										item_state.Set(XState.Removed);
									}
								}
							});
						}
					}
				}
			}

			for (int i=0; i<Definitions.Count; ++i)
			{
				XNode root = Definitions[i];
				XVars vars = Vars[i];

				// Expand every XNode with 'Include'/'Exclude'/'Remove' attribute that contains a globbing pattern
				// These nodes only exists as children of <Project>.
				foreach (XNode node in root.Children)
				{
					if (node.HasAttr("UnityBuild"))
					{
						// UnityBuild source files contain #include directives that include source code, all those included source files
						// have to be marked as 'excluded'.

						// Collect all nodes that have a filename indicated by the glob pattern.
						// e.g.: Conglomerate_*.cpp

						List<string> unitybuild_items = DoGlobbing(node.GetAttrValue("UnityBuild"), vars);
						if (unitybuild_items.Count > 0)
						{
							// Collect the include directories that should be used
							List<string> includeDirectories = new List<string>();
							includeDirectories.Add("");
							foreach (XNode c in node.Children)
							{
								if (c.Name == "IncludeDirectories")
								{
									string dirpath = vars.Expand(c.Value);
									includeDirectories.Add(dirpath);
								}
							}

							// Scan every unity build source file for include directives
							// Make sure path is absolute and collapse $(SolutionDir) var
							// Matching items in @include_nodes_map should get a XNode child with    <ExcludedFromBuild>True</ExcludedFromBuild>
							foreach (var unitybuild_item in unitybuild_items)
							{
								string unitybuildfilepath = vars.Expand(unitybuild_item);
								includeDirectories[0] = Path.GetDirectoryName(unitybuildfilepath);
								MsDevProjectUnityBuild.ScanCongoFile(unitybuildfilepath, includeDirectories, out List<string> includedfiles);
								foreach (string includedfile in includedfiles)
								{
									string removefile = vars.Collapse(includedfile);
									UpdateIncluded(node.Name, removefile, "*");
								}
							}
						}
					}
				}
			}
		}

		private List<string> DoGlobbing(string attrvalue, XVars vars)
		{
			attrvalue = vars.ExpandLock(attrvalue, out XVars usedvars);

			List<string> filepaths = new List<string>();

			if (usedvars.Count > 0)
			{
				filepaths = MsDevUtils.GlobFiles(attrvalue);
				for (int i = 0; i < filepaths.Count; ++i)
				{
					string filepath = filepaths[i];
					filepaths[i] = usedvars.Collapse(filepath);
				}
			}
			else
			{
				if (Path.IsPathRooted(attrvalue))
				{
					filepaths = MsDevUtils.GlobFiles(attrvalue);
					for (int i = 0; i < filepaths.Count; ++i)
					{
						string filepath = filepaths[i];
					}
				}
				else
				{
					// Everything specified purely 'relative' is relative to the project directory
					vars.Get("$(ProjectDir)", out string basepath);
					basepath = basepath.EnsureEndsWith('\\');

					filepaths = MsDevUtils.GlobFiles(Path.Combine(basepath, attrvalue));
					for (int i = 0; i < filepaths.Count; ++i)
					{
						string filepath = filepaths[i];
						filepaths[i] = filepath.Substring(basepath.Length);
					}
				}
			}
			return filepaths;
		}

		private Group GetGroup(string groupname)
		{
			if (!Groups.TryGetValue(groupname, out Group group))
			{
				group = new Group(groupname);
				Groups.Add(groupname, group);
			}
			return group;
		}
	}
}
