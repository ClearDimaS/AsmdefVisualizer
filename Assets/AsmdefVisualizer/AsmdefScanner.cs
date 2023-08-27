using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace Brawl.Core
{
    public class AsmdefScanner
    {
        public AssembliesContext Scan(List<string> nameStartsWith)
        {
            Assembly[] assemblies =
                CompilationPipeline.GetAssemblies();
            var path = Application.dataPath;
            var sc = new AssembliesContext()
            {
                allAssemblies = assemblies,
                gatheredAssemblyNames = new HashSet<string>(),
                nameStartsWith = nameStartsWith
            };
            
            GetAsmdefNames(sc, path);
            return sc;
        }

        private void GetAsmdefNames(AssembliesContext sc, string path)
        {
            var filePaths = Directory.GetFiles(path);
            foreach (var file in filePaths)
            {
                if (file.EndsWith($".asmdef"))
                {
                    var nameWithExt = file.Split('\\', '/').Last();
                    var fileName = nameWithExt.Substring(0, nameWithExt.Length - ".asmdef".Length).Replace(" ", "");

                    var assembly = sc.allAssemblies.FirstOrDefault(x => x.name.StartsWith(fileName, StringComparison.InvariantCultureIgnoreCase));

                    if (assembly == null)
                    {
                        Debug.LogWarning($"{fileName}  not found! Does it have any scripts? Or maybe the name of the fil doesnt match assmbly name?");
                        continue;
                    }

                    var dependencyNames = assembly.assemblyReferences.Select(x => x.name).ToArray();
                    var filtered = FilterReferences(dependencyNames, sc);
                    if((IsAllowedAssembly(assembly, sc.nameStartsWith)))
                    {
                        sc.gatheredAssemblyNames.Add(assembly.name);   
                    }
                    foreach (var reference in filtered)
                    {
                        sc.gatheredAssemblyNames.Add(reference);
                    }
                }
            }
            
            var directories =  Directory.GetDirectories(path);
            foreach (var childDirectory in directories)
            {
                GetAsmdefNames(sc, childDirectory);   
            }
        }

        private string[] FilterReferences(string[] assemblyAllReferences, AssembliesContext sc)
        {
            List<string> filteredNames = new List<string>();
            
            foreach (var assemblyName in assemblyAllReferences)
            {
                var assembly = sc.allAssemblies.Where(x => x.name.StartsWith(assemblyName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (assembly == null)
                {
                    Debug.Log($"assembly: {assemblyName} is null!");
                    continue;
                }
                if (IsAllowedAssembly(assembly, sc.nameStartsWith))
                {
                    filteredNames.Add(assemblyName);
                }
            }

            return filteredNames.ToArray();
        }

        private bool IsAllowedAssembly(Assembly assembly, List<string> nameStartsWith)
        {
            return nameStartsWith.Any(x => string.IsNullOrEmpty(x) || assembly.name.StartsWith(x)) || nameStartsWith.Count == 0;
        }

    }
}