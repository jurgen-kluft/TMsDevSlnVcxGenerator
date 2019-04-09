using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Glob;

namespace MsDevSlnVcxGenerator
{
    public static class ArrayExtensions
    {
        public static T[] Slice<T>(this T[] arr, int indexFrom, int indexTo)
        {
            if (indexFrom > indexTo)
            {
                throw new ArgumentOutOfRangeException("indexFrom is bigger than indexTo!");
            }

            int length = indexTo - indexFrom;
            T[] result = new T[length];
            Array.Copy(arr, indexFrom, result, 0, length);

            return result;
        }
        public static string Slice(this char[] arr, int indexFrom, int indexTo)
        {
            if (indexFrom > indexTo)
            {
                throw new ArgumentOutOfRangeException("indexFrom is bigger than indexTo!");
            }
            return new string(arr, indexFrom, (indexTo - indexFrom));
        }
    }

    public static class StringExtensions
    {
        public static string EnsureEndsWith(this string str, char endchar)
        {
            if (str.Length == 0)
                return str;

            if (str[str.Length - 1] == endchar)
                return str;

            return str + endchar;
        }

        public static string Between(this string str, char begchar, char endchar)
        {
            int startpos = str.IndexOf(begchar);
            if (startpos >= 0)
            {
                startpos += 1;
                int endpos = str.IndexOf(endchar, startpos);
                if (endpos >= 0)
                {
                    return str.Substring(startpos, endpos - startpos);
                }
            }
            return str;
        }

        public static string Between(this string str, char beginchar, char[] endchars)
        {
            int startpos = str.IndexOf(beginchar);
            if (startpos >= 0)
            {
                startpos = startpos + 1;
                int endpos = str.IndexOfAny(endchars, startpos);
                if (endpos >= 0)
                {
                    return str.Substring(startpos, endpos - startpos);
                }
            }
            return str;
        }

        public static string Until(this string str, params char[] endchars)
        {
            int index = str.IndexOfAny(endchars);
            if (index >= 0)
            {
                return str.Substring(0, index);
            }
            return str;
        }
    }

    public static class MsDevUtils
    {
        public static List<Tuple<string, string>> SplitIntoNameValue(string str)
        {
            List<Tuple<string, string>> namevalues = new List<Tuple<string, string>>();

            string[] parts = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string[] namevalue = part.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (namevalue.Length == 1)
                {
                    namevalues.Add(new Tuple<string, string>("Self", namevalue[0]));
                }
                else if (namevalue.Length == 2)
                {
                    namevalues.Add(new Tuple<string, string>(namevalue[0], namevalue[1]));
                }
            }
            return namevalues;
        }

        public static List<string> GlobFiles(string pathglob)
        {
            List<string> globbed = new List<string>();

			string dirpath = Path.GetDirectoryName(pathglob);
			string filename = Path.GetFileName(pathglob);
			int dirpathglobpos = dirpath.IndexOf('*');
			int filenameglobpos = filename.IndexOfAny(new char[] { '*', '{', '?', '[' });

			if (dirpathglobpos >= 0 || filenameglobpos >= 0)
			{
				string basepath = string.Empty;
				string glob = string.Empty;

				if (dirpathglobpos >= 0)
				{
					while (dirpath[dirpathglobpos] != '\\' && dirpathglobpos > 0)
						dirpathglobpos -= 1;
					basepath = dirpath.Substring(0, dirpathglobpos);

					if (pathglob[dirpathglobpos] == '\\')
					{
						dirpathglobpos += 1;
					}
					glob = pathglob.Substring(dirpathglobpos);
				}
				else
				{
					basepath = dirpath;
					glob = filename;
				}

				if (!basepath.EndsWith("\\"))
					basepath += "\\";

				DirectoryInfo basedir = new DirectoryInfo(basepath);
				if (basedir.Exists)
				{
					foreach (FileInfo fi in basedir.GlobFiles(glob))
					{
						globbed.Add(fi.FullName);
					}
				}
			}
			else
			{
				globbed.Add(pathglob);
			}
            return globbed;
        }

        public static bool MatchString(string main, string other)
        {
            bool matching = true;
            if (main != "*" && other != "*")
            {
                // The rule here is that @other should contain the glob pattern.
                Glob.CachingGlob glob = new Glob.CachingGlob(other);
                matching = glob.IsMatch(main);
            }
            return matching;
        }
    }

    public static class MsDevGUID
    {
        private static MD5 sMD5 = MD5.Create();
        private static char[] sHashStr = new char[32];

        public static string Generate(string key)
        {
            byte[] data = Encoding.Unicode.GetBytes(key);
            byte[] hash = sMD5.ComputeHash(data);
            for (int i = 0; i < hash.Length; ++i)
            {
                byte b0 = (byte)((hash[i] >> 4) & 0xF);
                char c0 = (b0 < 10) ? (char)('0' + b0) : (char)('A' + (b0 - 10));
                byte b1 = (byte)((hash[i] >> 0) & 0xF);
                char c1 = (b1 < 10) ? (char)('0' + b1) : (char)('A' + (b1 - 10));
                sHashStr[i * 2 + 0] = c0;
                sHashStr[i * 2 + 1] = c1;
            }

            // Convert to GUID form!
            string guid = "{" + sHashStr.Slice(0, 8) + "-" + sHashStr.Slice(8, 12) + "-" + sHashStr.Slice(12, 16) + "-" + sHashStr.Slice(16, 20) + "-" + sHashStr.Slice(20, 32) + "}";
            return guid;
        }
    }
}
