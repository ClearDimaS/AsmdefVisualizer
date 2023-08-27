using System.Collections.Generic;
using UnityEditor.Compilation;

namespace Brawl.Core
{
    public class AssembliesContext
    {
        public Assembly[] allAssemblies;
        public HashSet<string> gatheredAssemblyNames;
        public List<string> nameStartsWith = new List<string>();
    }
}