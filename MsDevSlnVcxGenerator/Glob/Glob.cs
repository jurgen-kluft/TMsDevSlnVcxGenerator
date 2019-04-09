using System;
using System.Linq;
using System.Collections.Generic;

namespace Glob
{
	public class GlobCache
	{
		private Dictionary<string, Glob> Cache = new Dictionary<string, Glob>();

		public bool Find(string pattern, out Glob glob)
		{
			if (!Cache.TryGetValue(pattern, out glob))
			{
				glob = null;
				return false;
			}
			return true;
		}
		public void Add(string pattern, Glob glob)
		{
			if (!Cache.ContainsKey(pattern))
			{
				Cache.Add(pattern, glob);
			}
		}
	}

	public struct CachingGlob
	{
		private static GlobCache sCache = new GlobCache();

		private Glob Instance { get; set; }
		public CachingGlob(string pattern)
		{
			if (!sCache.Find(pattern, out Glob glob))
			{
				glob = new Glob(pattern);
				sCache.Add(pattern, glob);
			}
			Instance = glob;
		}

		public bool IsMatch(string input)
		{
			return Instance.IsMatch(input);
		}
	}

	public class Glob
    {
        public string Pattern { get; }
        public bool Inverted { get; }

        private Tree _root;
        private Segment[] _segments;


        public Glob(string pattern, GlobOptions options = GlobOptions.Compiled)
        {
            if (pattern.StartsWith("NOT("))
            {
                this.Pattern = pattern.Substring(4, pattern.Length - 4 - 1);
                this.Inverted = true;
            }
            else
            {
                this.Pattern = pattern;
            }

            if (options.HasFlag(GlobOptions.Compiled))
            {
                this.Compile();
            }
        }

        private bool Return(bool result)
        {
            if (Inverted)
            {
                return !result;
            }
            return result;
        }

        private void Compile()
        {
            if (_root != null)
                return;

            if (_segments != null)
                return;

            var parser = new Parser(this.Pattern);
            _root = parser.ParseTree();
            _segments = _root.Segments;
        }

        public bool IsMatch(string input)
        {
            var pathSegments = input.Split('/', '\\');
            // match filename only
            if (_segments.Length == 1)
            {
                var last = pathSegments.LastOrDefault();
                var tail = (last == null) ? new string[0] : new[] { last };

                if (IsMatch(_segments, 0, tail, 0))
                    return Return(true);
            }
            return Return(IsMatch(_segments, 0, pathSegments, 0));
        }

        static bool IsMatch(Segment[] pattern, int patternIndex, string[] input, int inputIndex)
        {
            while (true)
            {
                if (inputIndex == input.Length)
                    return patternIndex == pattern.Length;

                // we have a input to match but no pattern to match against so we are done.
                if (patternIndex == pattern.Length)
                    return false;

                var inputHead = input[inputIndex];
                var patternHead = pattern[patternIndex];


                switch (patternHead)
                {
                    case DirectoryWildcard _:
                        // return all consuming the wildcard
                        return IsMatch(pattern, patternIndex + 1, input, inputIndex) // 0 matches
                               || IsMatch(pattern, patternIndex + 1, input, inputIndex + 1) // 1 match
                               || IsMatch(pattern, patternIndex, input, inputIndex + 1); // more

                    case Root root when inputHead == root.Text:
                        patternIndex++;
                        inputIndex++;
                        continue;

                    case DirectorySegment dir when dir.MatchesSegment(inputHead):
                        patternIndex++;
                        inputIndex++;
                        continue;
                }

                return false;

            }
        }

        public static bool IsMatch(string input, string pattern, GlobOptions options = GlobOptions.None) =>
            new Glob(pattern, options).IsMatch(input);

    }
}
