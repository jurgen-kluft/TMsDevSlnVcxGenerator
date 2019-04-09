using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{

    public class XNode
    {
        private static Int64 sUniqueUUID = 1;

        [Flags]
        public enum XFlags : Int64
        {
            NONE = 0,
            GLOB_OFF = 1,
        }

        public Int64 UUID { get; private set; }
        public string Name { get; set; }
        public XFlags Flags { get; private set; }

        public string Value { get; private set; } = string.Empty;
        public List<int> Attrs { get; } = new List<int>();
        public List<string> AttrNames { get; } = new List<string>();
        public List<string> AttrValues { get; } = new List<string>();
        public List<XNode> Children { get; } = new List<XNode>();

        public XNode Parent { get; private set; } = null;

        public bool Globbing { get { return !Flags.HasFlag(XFlags.GLOB_OFF); } }

        public XNode(string name)
        {
            UUID = sUniqueUUID++;
            Name = name;
        }

        public XNode(XNode node_to_copy, XNode parent)
        {
            UUID = sUniqueUUID++;
            Name = node_to_copy.Name;
            Value = node_to_copy.Value;
            Parent = parent;
            Attrs.AddRange(node_to_copy.Attrs);
            AttrNames.AddRange(node_to_copy.AttrNames);
            AttrValues.AddRange(node_to_copy.AttrValues);
            foreach (XNode child_to_copy in node_to_copy.Children)
            {
                XNode child = new XNode(child_to_copy, this);
                AddChild(child);
            }
        }

        public XNode(string name, XNode parent)
        {
            UUID = sUniqueUUID++;
            Name = name;
            Parent = parent;
        }

        public bool HasValue { get { return !String.IsNullOrEmpty(Value); } }
        public bool HasChildren { get { return Children.Count > 0; } }

        public void AddChild(XNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public bool AddChildAfter(XNode child, XNode insertAfterThisChild)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].UUID == insertAfterThisChild.UUID)
                {
                    i = i + 1;
                    Children.Insert(i, child);
                    return true;
                }
            }
            return false;
        }

		public void TakeChildrenFrom(XNode otherparent)
		{
			List<XNode> children = otherparent.ReleaseChildren();
			foreach (XNode child in children)
			{
				AddChild(child);
			}
		}

		private List<XNode> ReleaseChildren()
		{
			List<XNode> children = new List<XNode>();
			children.AddRange(Children);
			Children.Clear();
			foreach(XNode child in children)
			{
				child.Parent = null;
			}
			return children;
		}

		public void RemoveChildren()
        {
			foreach (XNode child in Children)
			{
				child.Parent = null;
			}
			Children.Clear();
		}

		public void RemoveChild(XNode node)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].UUID == node.UUID)
                {
                    Children.RemoveAt(i);
                    break;
                }
            }
        }

        public void SetValue(string value)
        {
            Value = value;
        }

        public void RemoveAttrs()
        {
            Attrs.Clear();
            AttrNames.Clear();
            AttrValues.Clear();
        }

        public void RemoveMetaAttrs()
        {
            List<int> remove = new List<int>();
            foreach (int i in Attrs)
            {
                if (AttrNames[i].EndsWith("_"))
                {
                    remove.Add(i);
                }
            }
            foreach (int r in remove)
            {
                Attrs.RemoveAt(r);
                AttrNames.RemoveAt(r);
                AttrValues.RemoveAt(r);
            }
        }

        public void SetAttr(string name, string value)
        {
            // Some attributes are not real attributes but flags or options
            if (name == "Flags")
            {
                string[] flags = value.Split(';');
                foreach (string flag in flags)
                {
                    if (value == "Glob=false")
                    {
                        Flags |= XFlags.GLOB_OFF;
                    }
                }
            }
            else
            {
                // Find and update existing attribute
                foreach (int i in Attrs)
                {
                    if (AttrNames[i] == name)
                    {
                        AttrValues[i] = value;
                        return;
                    }
                }
                // Add new attribute
                Attrs.Add(Attrs.Count);
                AttrNames.Add(name);
                AttrValues.Add(value);
            }
        }

		public void RenameAttr(string oldname, string newname)
		{
			foreach (int i in Attrs)
			{
				if (AttrNames[i] == oldname)
				{
					AttrNames[i] = newname;
				}
			}
		}

		public string GetAttrValue(string name)
        {
            foreach (int i in Attrs)
            {
                if (AttrNames[i] == name)
                {
                    return AttrValues[i];
                }
            }
            return string.Empty;
        }

        public bool HasAttr(string attrname, out int outattr)
        {
            foreach (int attr in Attrs)
            {
                if (AttrNames[attr] == attrname)
                {
                    outattr = attr;
                    return true;
                }
            }
            outattr = -1;
            return false;
        }

        public bool HasAttr(string attrname)
        {
            foreach (int attr in Attrs)
            {
                if (attrname == AttrNames[attr])
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasAttrWithValue(string attrname, string attrvalue)
        {
            foreach (int attr in Attrs)
            {
                if (attrname == AttrNames[attr])
                {
                    return MsDevUtils.MatchString(AttrValues[attr], attrvalue);
                }
            }
            return false;
        }

        public void ReplaceNode(XNode child, List<XNode> nodes)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].UUID == child.UUID)
                {
                    Children.RemoveAt(i);
                    Children.InsertRange(i, nodes);
					foreach(XNode c in nodes)
					{
						c.Parent = this;
					}
					break;
                }
            }
        }

        public List<XNode> GetAllNodesBefore(XNode node)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].UUID == node.UUID)
                {
                    return Children.GetRange(0, i);
                }
            }
            return new List<XNode>();
        }

        public static string[] Indentation = new string[] { "", "\t", "\t\t", "\t\t\t", "\t\t\t\t", "\t\t\t\t\t", "\t\t\t\t\t\t", "\t\t\t\t\t\t\t", "\t\t\t\t\t\t\t\t", "\t\t\t\t\t\t\t\t\t" };
        public void ToXml(StringBuilder sb)
        {
            string indent = string.Empty;

            int indentCount = 0;
            XNode p = Parent;
            while (p != null)
            {
                indentCount += 1;
                p = p.Parent;
            }

            sb.Append(Indentation[indentCount]);
            sb.Append("<");
            sb.Append(Name);
            foreach (int attr in Attrs)
            {
                sb.Append(" ");
                sb.Append(AttrNames[attr]);
                sb.Append("=");
                sb.Append("\"");
                sb.Append(AttrValues[attr]);
                sb.Append("\"");
            }
            if (Children.Count > 0)
            {
                sb.Append(">");
                sb.Append(Environment.NewLine);
                foreach (XNode n in Children)
                {
                    n.ToXml(sb);
                }
                sb.Append(Indentation[indentCount]);
                sb.Append("</");
                sb.Append(Name);
                sb.Append(">");
                sb.Append(Environment.NewLine);
            }
            else
            {
                if (HasValue)
                {
                    sb.Append(">");
                    sb.Append(Value);
                    sb.Append("</");
                    sb.Append(Name);
                    sb.Append(">");
                    sb.Append(Environment.NewLine);
                }
                else
                {
                    sb.Append("/>");
                    sb.Append(Environment.NewLine);
                }
            }
        }
    }

    public class XVars
    {
        private List<int> Vars { get; set; }
        private List<bool> Active { get; set; }
        private List<string> Names { get; set; }
        private List<string> Values { get; set; }
        private Dictionary<string, int> NameToIndex { get; set; }
        public int Count { get { return Vars.Count; } }

        public XVars()
        {
            Vars = new List<int>();
            Active = new List<bool>();
            Names = new List<string>();
            Values = new List<string>();
            NameToIndex = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string Expand(string str)
        {
            foreach (int i in Vars)
            {
                if (Active[i])
                {
                    str = str.Replace(Names[i], Values[i]);
                }
            }
            return str;
        }
        public string ExpandLock(string str, out XVars usedvars)
        {
            usedvars = new XVars();
            foreach (int i in Vars)
            {
                if (Active[i])
                {
                    if (str.Contains(Names[i]))
                    {
                        usedvars.Set(Names[i], Values[i]);
                        str = str.Replace(Names[i], Values[i]);
                    }
                }
            }
            return str;
        }

        public string Collapse(string str)
        {
            foreach (int i in Vars)
            {
                if (Active[i])
                {
                    int pos = str.IndexOf(Values[i], StringComparison.OrdinalIgnoreCase);
                    if (pos >= 0)
                    {
                        str = str.Remove(pos, Values[i].Length);
                        str = str.Insert(pos, Names[i]);
                    }
                }
            }
            return str;
        }

        public void Remove(string name)
        {
            if (NameToIndex.TryGetValue(name, out int index))
            {
                Active[index] = false;
            }
        }


        public string Set(string name, string value)
        {
            if (!NameToIndex.TryGetValue(name, out int index))
            {
                index = Count;
                NameToIndex.Add(name, index);
                Vars.Add(index);
                Names.Add(name);
                Values.Add(value);
                Active.Add(true);
            }
            string oldvalue = Values[index];
            Values[index] = value;
            Active[index] = true;
            return oldvalue;
        }

        public bool Get(string name, out string value)
        {
            if (NameToIndex.TryGetValue(name, out int index))
            {
                if (Active[index])
                {
                    value = Values[index];
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }
    }

	public class XXmlToXNode
	{
		private Queue<XmlNode> XmlQueue { get; set; } = new Queue<XmlNode>();
		private Queue<XNode> XNodeQueue { get; set; } = new Queue<XNode>();

		private HashSet<string> XmlNodesToIgnore { get; set; }

		private int Count { get { return XmlQueue.Count; } }

		public XXmlToXNode()
		{
			XmlNodesToIgnore = new HashSet<string>();
			XmlNodesToIgnore.Add("Comment");
		}

		private void Dequeue(out XmlNode xmlnode, out XNode xnode)
		{
			xmlnode = XmlQueue.Dequeue();
			xnode = XNodeQueue.Dequeue();
		}

		private void Enqueue(XmlNode xmlnode, XNode xnode)
		{
			XmlQueue.Enqueue(xmlnode);
			XNodeQueue.Enqueue(xnode);
		}

		public XNode ParseXmlNode(XmlNode xmlnode, XNode xparent)
		{
			if (xmlnode.NodeType == XmlNodeType.Text)
			{
				string value = xmlnode.InnerText;
				xparent.SetValue(value);
				return null;
			}
			else
			{
				XNode xnode = new XNode(xmlnode.Name, xparent);

				foreach (XmlAttribute attr in xmlnode.Attributes)
				{
					xnode.SetAttr(attr.Name, attr.Value);
				}

				return xnode;
			}
		}

		private bool ShouldIgnore(XmlNode xmlnode)
		{
			return XmlNodesToIgnore.Contains(xmlnode.Name);
		}

		public XNode FromXmlDoc(XmlDocument xmldoc)
		{
			XmlNode root = xmldoc.FirstChild;
			XNode xroot = ParseXmlNode(root, null);
			if (xroot != null)
			{
				Enqueue(root, xroot);
				while (Count > 0)
				{
					Dequeue(out XmlNode xmlnode, out XNode xnode);
					foreach (XmlNode xmlchild in xmlnode.ChildNodes)
					{
						if (!ShouldIgnore(xmlchild))
						{
							XNode child = ParseXmlNode(xmlchild, xnode);
							if (child != null)
							{
								xnode.AddChild(child);
								Enqueue(xmlchild, child);
							}
						}
					}
				}
			}
			return xroot;
		}

		public static XNode FromFile(string filepath)
		{
			XNode root = null;
			if (File.Exists(filepath))
			{
				XmlDocument xmldoc = new XmlDocument();
				xmldoc.Load(filepath);
				if (xmldoc.HasChildNodes)
				{
					XXmlToXNode builder = new XXmlToXNode();
					root = builder.FromXmlDoc(xmldoc);

					XProcessInclude includes = new XProcessInclude(Path.GetDirectoryName(filepath));
					includes.Process(root);
				}
			}
			return root;
		}
	}

	public class XProcessInclude
	{
		private string DirPath { get; set; }

		public XProcessInclude( string path )
		{
			DirPath = path;
		}

		// Expand <include> 
		public void Process(XNode node)
		{
			Queue<XNode> queue = new Queue<XNode>();
			foreach (XNode child in node.Children)
			{
				if (String.CompareOrdinal(child.Name, "Include") == 0)
				{
					queue.Enqueue(child);
				}
			}

			while (queue.Count > 0)
			{
				node = queue.Dequeue();
				string filepath = node.Value;
				if (Path.IsPathRooted(filepath) == false)
					filepath = Path.Combine(DirPath, filepath);

				XNode root = XXmlToXNode.FromFile(filepath);
				List<XNode> included_nodes = new List<XNode>();
				foreach(XNode include_node in root.Children)
				{
					included_nodes.Add(include_node);
				}
				node.Parent.ReplaceNode(node, included_nodes);
			}
		}
	}


	// Expand variables into their value
	public class XReplaceVars
    {
        private XVars Vars { get; set; }

        public XReplaceVars(XVars vars)
        {
            Vars = vars;
        }

        public bool DoNodeValues { get; set; } = true;
        public bool DoAttrValues { get; set; } = true;

        // Expand the variables in every value of a XNode
        public void Action(XNode node)
        {
            Queue<XNode> queue = new Queue<XNode>();
            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                node = queue.Dequeue();

                if (DoNodeValues && node.HasValue)
                {
                    node.SetValue(Vars.Expand(node.Value));
                }
                if (DoAttrValues)
                {
                    foreach (int attr in node.Attrs)
                    {
                        node.AttrValues[attr] = Vars.Expand(node.AttrValues[attr]);
                    }
                }

                foreach (XNode child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }
    }

	public class XCollectVars
	{
		private XVars Vars { get; set; }

		public XCollectVars(XVars vars)
		{
			Vars = vars;
		}

		// Expand the variables in every value of a XNode
		public void Action(XNode node)
		{
			Queue<XNode> queue = new Queue<XNode>();
			foreach (XNode child in node.Children)
			{
				if (node.Name == "PropertyGroup")
				{
					queue.Enqueue(child);
				}
			}
			while (queue.Count > 0)
			{
				node = queue.Dequeue();
				if (!node.HasAttr("Condition"))
				{
					string property_name = node.Name;
					string property_value = node.Value;
					Vars.Set(property_name, property_value);
				}
			}
		}
	}


	public class XMergeIdenticalChildren
    {
        public MsDevProjectMergeSettings Settings { get; set; }

        public XMergeIdenticalChildren(MsDevProjectMergeSettings settings)
        {
            Settings = settings;
        }


        private bool MatchAttributes(XNode left, XNode right)
        {
            if (left.Attrs.Count != right.Attrs.Count)
                return false;

            foreach (int attr in right.Attrs)
            {
                if (!left.HasAttr(right.AttrNames[attr], out int lattr))
                    return false;
                if (left.AttrValues[lattr] != right.AttrValues[attr])
                    return false;
            }
            return true;
        }

        private bool FindMatches(XNode main, XNode lchild, out List<XNode> matches)
        {
            matches = new List<XNode>();
            foreach (XNode mchild in main.Children)
            {
                if (lchild.Name == mchild.Name && mchild.UUID != lchild.UUID)
                {
                    if (MatchAttributes(mchild, lchild))
                    {
                        matches.Add(mchild);
                    }
                }
            }
            return matches.Count > 0;
        }

        public void Merge(XNode main)
        {
            Queue<XNode> queue = new Queue<XNode>();
            foreach (XNode child in main.Children)
            {
                queue.Enqueue(child);
            }

            XVars vars = new XVars();
            XReplaceVars expandVarsInValues = new XReplaceVars(vars);

            while (queue.Count > 0)
            {
                XNode node = queue.Dequeue();
                Dictionary<Int64, XNode> childrenThatAreMerged = new Dictionary<long, XNode>();
                foreach (XNode child in node.Children)
                {
                    // Skip the children that are marked as 'merged'
                    if (!childrenThatAreMerged.ContainsKey(child.UUID))
                    {
                        if (FindMatches(node, child, out List<XNode> matching_children))
                        {
                            // First merge all their 'values' with our current child if it has value to begin with
                            if (child.HasValue)
                            {
                                var mergefunc = Settings.GetValueMergeFuncFor(child);
                                string value = child.Value;
                                foreach (XNode match in matching_children)
                                {
                                    value = mergefunc(value, match.Value);
                                }
                                child.SetValue(value);
                            }

                            // Merge children from matches into child
                            foreach (XNode match in matching_children)
                            {
                                foreach (XNode match_child in match.Children)
                                {
                                    child.AddChild(match_child);
                                }
                                match.RemoveChildren();
                            }

                            // Remove matches from node
                            foreach (XNode match in matching_children)
                            {
                                childrenThatAreMerged.Add(match.UUID, match);
                            }
                        }

                        // Continue on with the children of this child
                        queue.Enqueue(child);
                    }
                }

                // Remove the 'merged' children from node
                foreach (var merged_child in childrenThatAreMerged)
                {
                    node.RemoveChild(merged_child.Value);
                }
            }
        }
    }

    public class XPermutations
    {
        private List<Platform> Platforms { get; set; }
        private List<string> Configurations { get; set; }

        public XPermutations(List<Platform> platforms, List<string> configurations)
        {
            Platforms = platforms;
            Configurations = configurations;
        }

        private bool RequiresPermutating(XNode node, params string[] vars)
        {
            int n = 0;
            foreach (string var in vars)
            {
                foreach (string attrvalue in node.AttrValues)
                {
                    if (attrvalue.Contains(var))
                    {
                        n++;
                        break;
                    }
                }
            }
            return n == vars.Length;
        }

        private bool CloneNode(XNode current, XVars vars, out XNode node)
        {
            node = new XNode(current, current.Parent);
            foreach (int a in node.Attrs)
            {
                node.AttrValues[a] = vars.Expand(node.AttrValues[a]);
            }
            return true;
        }

        public void Action(XNode root)
        {
            Queue<XNode> queue = new Queue<XNode>();
            queue.Enqueue(root);

            XVars vars = new XVars();
            XReplaceVars expandVarsInValues = new XReplaceVars(vars);

            // Expand every XNode with attributes that contains ${PLATFORM} and/or ${CONFIGURATION}
            while (queue.Count > 0)
            {
                var xnode = queue.Dequeue();
                List<XNode> permutations = new List<XNode>();
                if (RequiresPermutating(xnode, "${CONFIGURATION}", "${PLATFORM}"))
                {
                    // Expand it into multiple nodes
                    foreach (Platform p in Platforms)
                    {
                        foreach (string c in Configurations)
                        {
                            vars.Set("${CONFIGURATION}", c);
                            vars.Set("${PLATFORM}", p.Name);
                            if (CloneNode(xnode, vars, out XNode pcnode))
                            {
                                expandVarsInValues.Action(pcnode);
                                permutations.Add(pcnode);
                            }
                        }
                    }
                }
                else if (RequiresPermutating(xnode, "${CONFIGURATION}"))
                {
                    vars.Remove("${PLATFORM}");

                    // Expand it into multiple nodes
                    foreach (string c in Configurations)
                    {
                        vars.Set("${CONFIGURATION}", c);
                        if (CloneNode(xnode, vars, out XNode cnode))
                        {
                            expandVarsInValues.Action(cnode);
                            permutations.Add(cnode);
                        }
                    }
                }
                else if (RequiresPermutating(xnode, "${PLATFORM}"))
                {
                    vars.Remove("${CONFIGURATION}");

                    // Expand it into multiple nodes
                    foreach (Platform p in Platforms)
                    {
                        vars.Set("${PLATFORM}", p.Name);
                        if (CloneNode(xnode, vars, out XNode pnode))
                        {
                            expandVarsInValues.Action(pnode);
                            permutations.Add(pnode);
                        }
                    }
                }

                if (permutations.Count > 0)
                {
                    // The single node that has been multiplicated into many replace the single node at the parent
                    // with the new permutations.
                    xnode.Parent.ReplaceNode(xnode, permutations);
                    foreach (XNode p in permutations)
                    {
                        queue.Enqueue(p);
                    }
                }
                else
                {
                    foreach (XNode child in xnode.Children)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }
    }

    public class XExtractSource
    {
        public bool Extract(XNode main, out XNode source)
        {
			source = null;

			// <ItemGroup Label="Source"> exist as children of <Project>
			List<XNode> donated = new List<XNode>();
            foreach (XNode node in main.Children)
            {
                if (node.Name == "ItemGroup" && node.HasAttrWithValue("Label", "Source"))
                {
					if (source == null)
					{
						source = node;
					}
					else
					{
						source.TakeChildrenFrom(node);
					}
					donated.Add(node);
                }
            }

			foreach(XNode node in donated)
			{
				main.RemoveChild(node);
			}

            return source != null;
        }
    }

    public class XRemoveElements
    {
        public bool Remove(XNode main, List<Platform> platforms, List<string> configs)
        {
            Queue<XNode> queue = new Queue<XNode>();
            foreach (XNode node in main.Children)
            {
                queue.Enqueue(node);
            }

            while (queue.Count > 0)
            {
                main = queue.Dequeue();

                foreach (XNode node in main.Children)
                {
                    if (node.HasAttr("Remove_"))

                        queue.Enqueue(node);
                }
            }
            return false;
        }
    }


    // XMatchAndMerge will process two XNode tree and merge @left to @right
    // An XNode will be searched in @right and it's Children merged.
    //
    // For some XNodes they need to be inserted from @left to @right if it
    // doesn't exist in @right. Those have a label named 'Merge' and can have
    // values like:
    // - Force
    public class XMatchAndMerge
    {
        public MsDevProjectMergeSettings Settings { get; set; }

        public XMatchAndMerge(MsDevProjectMergeSettings settings)
        {
            Settings = settings;
        }

        private bool MatchAttributes(XNode left, XNode right)
        {
            foreach (int attr in right.Attrs)
            {
                if (!left.HasAttr(right.AttrNames[attr], out int lattr))
                    return false;

                return MsDevUtils.MatchString(left.AttrValues[lattr], right.AttrValues[attr]);
            }
            return true;
        }

        private bool FindMatches(XNode main, XNode lchild, List<Tuple<XNode, XNode>> matches)
        {
            foreach (XNode mchild in main.Children)
            {
                if (lchild.Name == mchild.Name)
                {
                    if (MatchAttributes(mchild, lchild))
                    {
                        matches.Add(new Tuple<XNode, XNode>(mchild, lchild));
                    }
                }
            }
            return matches.Count > 0;
        }

        private void MergeValue(XNode main, XNode other)
        {
            Func<string, string, string> merging = Settings.GetValueMergeFuncFor(main);
            main.SetValue(merging(main.Value, other.Value));
        }

        public void Merge(XNode main, XNode other)
        {
            if (!main.HasChildren && !other.HasChildren)
            {
                if (main.HasValue && other.HasValue)
                {
                    MergeValue(main, other);
                }
            }
            else
            {
                List<Tuple<XNode, XNode>> matches = new List<Tuple<XNode, XNode>>();
                foreach (XNode ochild in other.Children)
                {
                    matches.Clear();

                    if (FindMatches(main, ochild, matches))
                    {
                        // Merge the children of this node into all matching candidates
                        // Item1 = Main
                        // Item2 = Other
                        foreach (Tuple<XNode, XNode> match in matches)
                        {
                            string merge = Settings.GetNodeMergerFor(match.Item1);
                            switch (merge)
                            {
                                case "Match":
                                case "MatchInsert":
                                    {
                                        // MatchInsert means that there is either a match or a copy of it needs to be inserted.
                                        // Here we had a match so we continue merging:
                                        Merge(match.Item1, match.Item2);
                                        break;
                                    }
                                case "MatchInsertAfter":
                                    {
                                        // Copy the node and insert it in the children of the parent after the matching node
                                        XNode child = new XNode(ochild, match.Item1.Parent);
                                        if (!match.Item1.Parent.AddChildAfter(child, match.Item1))
                                        {
                                            // Something is wrong here!, main is not a child of its parent?
                                            match.Item1.Parent.AddChild(child);
                                        }
                                        break;
                                    }
                                default:
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // The merge strategy defines what to do here
                        string merge = Settings.GetNodeMergerFor(ochild);
                        switch (merge)
                        {
                            case "MatchInsert":
                                {
                                    // MatchInsert means that there is either a match or a copy of it needs to be inserted.
                                    // Here we copy the node and insert it, also no need to 'merge' further on this ochild.
                                    XNode child = new XNode(ochild, main);
                                    main.AddChild(child);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }
            }
        }
    }

    public class XGenerateFilters
    {
        public XGenerateFilters()
        {

        }

        public void Generate(XNode main, MsDevProjectFilters filters)
        {
            foreach (XNode node in main.Children)
            {
                if (node.Name == "ItemGroup" && node.HasAttrWithValue("Label", "Source*"))
                {
                    foreach (XNode fileitem in node.Children)
                    {
                        string filetype = fileitem.Name;
                        string filename = fileitem.GetAttrValue("Include");
                        if (!String.IsNullOrEmpty(filetype) && !String.IsNullOrEmpty(filename))
                        {
                            filters.Add(filetype, filename);
                        }
                    }
                }
            }
        }
    }

    public class MsDevProjectMergeSettings
    {
        // Merge functions should be configurable (delegates ?)

        private XNode Root { get; set; }

        public MsDevProjectMergeSettings(XNode root)
        {
            Root = root;
        }

        private List<XNode> GetPath(XNode node)
        {
            List<XNode> path = new List<XNode>() { node };
            XNode pn = node;
            while (pn.Parent != null)
            {
                pn = pn.Parent;
                path.Insert(0, pn);
            }
            return path;
        }

        public bool Match(XNode main, XNode other)
        {
            if (main.Name == other.Name)
            {
                foreach (int attr in other.Attrs)
                {
                    if (!main.HasAttr(other.AttrNames[attr], out int mainattr))
                        return false;
                    return MsDevUtils.MatchString(main.AttrValues[mainattr], other.AttrValues[attr]);
                }
                return true;
            }
            return false;
        }

        private void FindNode(XNode node, out string NodeMerge, out string ValueMerge)
        {
            NodeMerge = "Match";
            ValueMerge = "Overwrite";

            List<XNode> path = GetPath(node);
            XNode other = Root;
            for (int i = 0; i < path.Count; ++i)
            {
                if (i == 0)
                {
                    XNode main = path[i];
                    if (!Match(main, other))
                    {
                        return;
                    }
                }
                else
                {
                    XNode main = path[i];
                    XNode match = null;
                    if (other.HasChildren)
                    {
                        foreach (XNode c in other.Children)
                        {
                            if (c.Name == "Merge")
                            {
                                NodeMerge = c.Value;
                            }
                            else if (Match(main, c))
                            {
                                match = c;
                                break;
                            }
                        }
                        if (match == null)
                            break;
                        other = match;
                    }
                    if (other.HasValue)
                    {
                        ValueMerge = other.Value;
                    }
                }
            }
        }

        public string GetNodeMergerFor(XNode node)
        {
            // How are we going to quickly figure out the Merge strategy for this specific node ?
            // Incoming node has a 'path', we could walk the settings xml tree to get the merging strategy
            FindNode(node, out string NodeMerge, out string ValueMerge);
            return NodeMerge;
        }

        public Func<string, string, string> GetValueMergeFuncFor(XNode node)
        {
            FindNode(node, out string NodeMerge, out string ValueMerge);
            switch (ValueMerge)
            {
                case "Overwrite": return Overwrite;
                case "JoinWithSemiColon": return JoinWithSemiColon;
                case "JoinWithSemiColonSorted": return JoinWithSemiColonSorted;
                case "JoinWithComma": return JoinWithComma;
                case "JoinWithSpace": return JoinWithSpace;
                case "JoinFrontWithSpace": return JoinFrontWithSpace;
                default: return Overwrite;
            }
        }

        public static string Overwrite(string oldstr, string newstr)
        {
            if (newstr == null)
            {
                return oldstr;
            }
            return newstr;
        }

        private static string JoinWithDelimiter(string oldstr, string newstr, char delimiter, bool sorted)
        {
            if (oldstr == null)
            {
                return newstr;
            }
            else if (newstr == null)
            {
                return oldstr;
            }

            string delimiterstr = string.Empty;
            delimiterstr += delimiter;

            // Remove duplicates
            string[] olditems = oldstr.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            string[] newitems = newstr.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

            List<string> allitems = new List<string>();
            allitems.AddRange(olditems);
            allitems.AddRange(newitems);
            if (sorted)
            {
                allitems.Sort();
            }

            HashSet<string> setofitems = new HashSet<string>();

            string result = string.Empty;
			string force_append_items = string.Empty;
            foreach (string item in allitems)
            {
                if (setofitems.Contains(item) == false)
                {
                    setofitems.Add(item);
					if (item.StartsWith("%"))
					{
						if (force_append_items == string.Empty)
						{
							force_append_items = item;
						}
						else
						{
							force_append_items = force_append_items + delimiterstr + item;
						}
					}
					else
					{
						if (result == string.Empty)
						{
							result = item;
						}
						else
						{
							result = result + delimiterstr + item;
						}
					}
                }
            }

			// Append the items that should always be at the end
			if (result == string.Empty)
			{
				result = force_append_items;
			}
			else if (force_append_items != string.Empty)
			{
				result = result + delimiterstr + force_append_items;
			}

			return result;
        }

        public static string JoinWithComma(string oldstr, string newstr)
        {
            return JoinWithDelimiter(oldstr, newstr, ',', false);
        }
        public static string JoinWithSpace(string oldstr, string newstr)
        {
            return JoinWithDelimiter(oldstr, newstr, ' ', false);
        }
        public static string JoinFrontWithSpace(string oldstr, string newstr)
        {
            return JoinWithDelimiter(newstr, oldstr, ' ', false);
        }

        public static string JoinWithSemiColon(string oldstr, string newstr)
        {
            return JoinWithDelimiter(oldstr, newstr, ';', false);
        }
        public static string JoinWithSemiColonSorted(string oldstr, string newstr)
        {
            return JoinWithDelimiter(oldstr, newstr, ';', true);
        }

        public void Initialize(string settings_filepath)
        {
			if (File.Exists(settings_filepath))
			{
				XmlDocument xmldoc = new XmlDocument();
				xmldoc.Load(settings_filepath);
				if (xmldoc.HasChildNodes)
				{
					XXmlToXNode builder = new XXmlToXNode();
					Root = builder.FromXmlDoc(xmldoc);
				}
			}
			else
			{
				Console.WriteLine("Error: Cannot find settings file '{0}'", settings_filepath);
			}
		}
    }

    public class MsDevProject
    {
        public string Name { get; private set; }
        public string PrjDir { get; private set; }
        public string GUID { get; private set; }

        public string SlnDir { get; private set; }

        private List<Platform> Platforms { get; set; }
        private List<string> Configurations { get; set; }
        private XVars Vars { get; set; }

        private XNode Main { get; set; }
        private XSource Source { get; set; }
        private XNode Reference { get; set; }
        private Dictionary<string, XNode> Packages { get; set; }
        private XNode MergeSettings { get; set; }

        public MsDevProject(string name, string prjdir, string guid, string slndir, List<Platform> platforms, List<string> configurations)
        {
            Name = name;
            PrjDir = prjdir;
            GUID = guid;
            SlnDir = slndir;

            Platforms = new List<Platform>();
            Platforms.AddRange(platforms);
            Configurations = new List<string>();
            Configurations.AddRange(configurations);

            Vars = new XVars();
            Main = null;
            Source = null;
            Reference = null;

            Packages = new Dictionary<string, XNode>(StringComparer.InvariantCultureIgnoreCase);

            MergeSettings = null;
        }

        public bool HasPlatform(Platform platform)
        {
            foreach (Platform p in Platforms)
            {
                if (platform.Index == p.Index)
                    return true;
            }
            return false;
        }

        public string MapConfig(string config)
        {
            return config;
        }

		//
		// The main (first) project will contain all the nodes for those platforms and 
		// configurations that are requested.
		// Any 'partial' project content that contains node that cannot be matched in the
		// main project will be rejected.
		// For example 'MsDevProject-3rdparty-FMOD' could contain nodes for many platforms,
		// but if the main project platforms are only Durango and Orbis then any other
		// nodes are rejected.
		//
		// Content of a project is processed and evaluated top to bottom.
		// Default behaviour for merging the value of a XmlNode is 'overwrite', any XmlNode
		// that needs to 'join' values with a specific delimiter need to be registered.
		// 
		public void LoadSettings(string filename)
		{
			MergeSettings = XXmlToXNode.FromFile(filename);
			if (MergeSettings == null)
			{
				Console.WriteLine("Error: Cannot find settings file '{0}'", filename);
			}
		}

		private void SetProjectGUID()
        {
            foreach (XNode node in Main.Children)
            {
                if (node.Name == "PropertyGroup" && node.HasAttrWithValue("Label", "Globals"))
                {
                    foreach (XNode prop in node.Children)
                    {
                        if (prop.Name == "ProjectGuid")
                        {
                            prop.SetValue(GUID);
                        }
                    }
                }
            }
        }

		public void LoadMain(string filename)
		{
			Main = XXmlToXNode.FromFile(filename);
			if (Main != null)
			{
				XPermutations expand = new XPermutations(Platforms, Configurations);
				expand.Action(Main);
				SetProjectGUID();
				Source = new XSource(Configurations, Platforms, new MsDevProjectMergeSettings(MergeSettings));
			}
			else
			{
				Console.WriteLine("Error: Cannot find main file '{0}'", filename);
			}
		}

		public void LoadReference(string filename)
		{
			Reference = XXmlToXNode.FromFile(filename);
			if (Reference != null)
			{
				XVars vars = new XVars();
				vars.Set("${PROJECT_PATH}", PrjDir);
				vars.Set("${PROJECT_GUID}", GUID);
				XReplaceVars replace = new XReplaceVars(vars);
				replace.Action(Reference);
			}
			else
			{
				Console.WriteLine("Error: Cannot find reference file '{0}'", filename);
			}
		}

		public void CollectVars()
		{
			// Project is fully merged, so we can scan all <PropertyGroup> children
			// and register them in XVars.
			XVars vars = new XVars();
			vars.Set("$(SolutionDir)", SlnDir);
			string projectFilepath = Path.Combine(SlnDir, PrjDir);
			vars.Set("$(ProjectDir)", Path.GetDirectoryName(projectFilepath).EnsureEndsWith('\\'));



			Vars = vars;
		}

		public void Merge(string filename)
        {
			if (Main != null)
			{
				XNode root = XXmlToXNode.FromFile(filename);
				if (root != null)
				{
					XPermutations expand = new XPermutations(Platforms, Configurations);
					expand.Action(root);

					XVars vars = new XVars();
					vars.Set("$(SolutionDir)", SlnDir);
					string projectFilepath = Path.Combine(SlnDir, PrjDir);
					vars.Set("$(ProjectDir)", Path.GetDirectoryName(projectFilepath).EnsureEndsWith('\\'));

					MsDevProjectMergeSettings mergeSettings = new MsDevProjectMergeSettings(MergeSettings);

					XMergeIdenticalChildren mergeIdenticalChildren = new XMergeIdenticalChildren(mergeSettings);
					mergeIdenticalChildren.Merge(root);

					XExtractSource extract = new XExtractSource();
					if (extract.Extract(root, out XNode sources))
					{
						Source.AddDefinition(sources, vars);
					}
					XMatchAndMerge matchmerge = new XMatchAndMerge(mergeSettings);
					matchmerge.Merge(Main, root);
				}
				else
				{
					Console.WriteLine("Error: Cannot find file '{0}'", filename);
				}
			}
		}

        public void ResolveSource()
        {
			if (Source != null)
			{
				Source.Process();
				Source.ConvertToXml(out XNode source);
				XMatchAndMerge matchmerge = new XMatchAndMerge(new MsDevProjectMergeSettings(MergeSettings));
				matchmerge.Merge(Main, source);
			}
        }

        public void MergeProjectReference(MsDevProject depprj)
        {
			if (Main != null)
			{
				XMatchAndMerge matchmerge = new XMatchAndMerge(new MsDevProjectMergeSettings(MergeSettings));
				matchmerge.Merge(Main, depprj.Reference);
			}
        }

        public void Save()
        {
			if (Main != null)
			{
				Main.SetAttr("DefaultTargets", "Build");
				Main.SetAttr("ToolsVersion", "15.0");
				Main.SetAttr("xmlns", "http://schemas.microsoft.com/developer/msbuild/2003");

				string projectFilepath = Path.Combine(SlnDir, PrjDir);

				StringBuilder sb = new StringBuilder();
				Main.ToXml(sb);
				File.WriteAllText(projectFilepath, sb.ToString());

				MsDevProjectFilters projectFilters = new MsDevProjectFilters(PrjDir + ".filters");
				XGenerateFilters generateFilters = new XGenerateFilters();
				generateFilters.Generate(Main, projectFilters);
				projectFilters.Save();
			}
        }
    }


}