using System;
using System.Collections.Generic;
using System.Linq;
using PlasticGui.WorkspaceWindow.PendingChanges;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Brawl.Core
{
    public class AsmdefVisualizer : NodeBasedEditor
    {
        private Vector2 nodeSpacing = new Vector2(50, 20);
        private Vector2 nodeSize = new Vector2(300, 50);

        private static AsmdefScanner _asmdefScanner;
        private List<string> _nameFilters = new List<string>() {"Brawl"};
        
        [MenuItem("Window/AsmdefVisualizer")]
        private static void OpenWindow()
        {
            var window = GetWindow<AsmdefVisualizer>();
            window.titleContent = new GUIContent("AsmdefVisualizer");
        }

        protected override void OnGUI_Inetrnal()
        {
            base.OnGUI_Inetrnal();
            
            GUI.Label(new Rect(0, 0, 70, 20), "Name Filters:");
            if (GUI.Button(new Rect(80, 0, 20, 20), "+"))
            {
                _nameFilters.Add("StartsWith");
            }

            for (var i = 0; i < _nameFilters.Count; i++)
            {
                _nameFilters[i] = GUI.TextField(new Rect(0, (i + 1) * 30, 200, 20), _nameFilters[i]);
                if (GUI.Button(new Rect(200 + 10, (i + 1) * 30, 20, 20), "-"))
                {
                    _nameFilters.RemoveAt(i);
                    i--;
                }
            }

            if (GUI.Button(new Rect(350, 0, 200, 20), "Refresh"))
            {
                _asmdefScanner = new AsmdefScanner();
                var context = _asmdefScanner.Scan(_nameFilters);
                BuildTree(context);
            }
        }

        private void BuildTree(AssembliesContext context)
        {
            nodes?.Clear();
            connections?.Clear();

            var assemblies = context.allAssemblies.Where(x => context.gatheredAssemblyNames.Contains(x.name)).ToArray();
            var asmdefNames = context.gatheredAssemblyNames.ToArray();
            
            Dictionary<Assembly, int> referencesDict = new Dictionary<Assembly, int>();
            foreach (var assembly in assemblies)
            {
                referencesDict[assembly] = assemblies.Where(x => asmdefNames.Contains(x.name)).Count();
            }
            
            var sortedAssemblies = referencesDict.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
            var assemblyColumns = new List<NodeColumn>() {new NodeColumn()};
            
            foreach (var assembly in sortedAssemblies)
            {
                var column = assemblyColumns[0];
                column.Assemblies.Add(assembly);
            }
            
            /// add second pass to deal with same column dependencies
            /*var newNode = SpawnAssemblyNode(assembly.name, columnIndex, column.Assemblies.Count);
            spawnedNodes.Add(newNode);*/

            var moved = true;
            var maxMoves = 200;
            var moves = 0;
            while (moved && moves < maxMoves)
            {
                moved = false;
                for (var columnIndex1 = 0; columnIndex1 < assemblyColumns.Count; columnIndex1++)
                {
                    for (int columnIndex2 = columnIndex1; columnIndex2 < assemblyColumns.Count; columnIndex2++)
                    {
                        var column1 = assemblyColumns[columnIndex1];
                        for (var i = 0; i < column1.Assemblies.Count; i++)
                        {
                            var assembly1 = column1.Assemblies[i];

                            var column2 = assemblyColumns[columnIndex2];
                            for (var j = 0; j < column2.Assemblies.Count; j++)
                            {
                                var assembly2 = column2.Assemblies[j];
                                if (assembly1.assemblyReferences.Contains(assembly2))
                                {
                                    column1.Assemblies.RemoveAt(i);
                                    if (assemblyColumns.Count <= columnIndex1 + 1)
                                    {
                                        assemblyColumns.Add(new NodeColumn());
                                    }
                                    assemblyColumns[columnIndex1 + 1].Assemblies.Add(assembly1);
                                    moved = true;
                                    break;
                                }
                            }
                        }   
                    }
                }

                moves++;
            }

            List<AsmdefNode> spawnedNodes = new List<AsmdefNode>();

            int rowsOffset = 0;
            for (var i = 0; i < assemblyColumns.Count; i++)
            {
                var column = assemblyColumns[i];
                for (var j = 0; j < column.Assemblies.Count; j++)
                {
                    var row = column.Assemblies[j];
                    var newNode = SpawnAssemblyNode(row, context, i, j, rowsOffset);
                    spawnedNodes.Add(newNode);
                }

                rowsOffset += column.Assemblies.Count;
            }

            foreach (var node in spawnedNodes)
            {
                var assembly = sortedAssemblies.Where(x => x.name == node.AsmdefName).FirstOrDefault();
                SpawnAssemblyConnections(node, assembly, spawnedNodes);
            }
        }
        
        private AsmdefNode SpawnAssemblyNode(Assembly assembly, AssembliesContext context, int column, int row, int rowsOffset)
        {
            var pos = position.center + new Vector2(-column * (nodeSpacing.x + nodeSize.x), (row + rowsOffset) * (nodeSpacing.y + nodeSize.y));
            pos -= position.min;
            
            var node = new AsmdefNode(assembly, context, pos, 300, 50, nodeStyle, selectedNodeStyle, inPointStyle, outPointStyle,
                OnClickInPoint, OnClickOutPoint, OnClickRemoveNode);
            AddNode(node);
            return node;
        }
        
        private void SpawnAssemblyConnections(AsmdefNode node, Assembly assembly, List<AsmdefNode> spawnedNodes)
        {
            foreach (var dependencyNode in spawnedNodes)
            {
                if (assembly.assemblyReferences.Select(x => x.name).Contains(dependencyNode.AsmdefName))
                {
                    var connection = new AsmdefConnection(dependencyNode.Assembly, node.Assembly, dependencyNode.inPoint, node.outPoint, OnClickRemoveConnection);
                    AddConnection(connection);
                }   
            }
        }
    }

    public class NodeColumn
    {
        public List<Assembly> Assemblies = new List<Assembly>();
    }
}