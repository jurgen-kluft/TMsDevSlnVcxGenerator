using System;
using System.Xml;
using System.Collections.Generic;

namespace MsDevSlnVcxGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			MsDevSolution sln = new MsDevSolution(@"F:\TR2\tr2_dev\");
			sln.Load("Generate-TRHD-Orbis.xml");
			sln.Save();
		}
	}
}
