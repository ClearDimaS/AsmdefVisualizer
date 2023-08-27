using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AsmdefVisualizer;
using UnityEditor;
using UnityEngine;
using Assembly = UnityEditor.Compilation.Assembly;
using Debug = UnityEngine.Debug;
using Exception = System.Exception;

namespace Brawl.Core
{
    public class AsmdefConnection : Connection
    {
        private Color _buttonColor = new Color(1, 1, 1, 0.6f);
        
        private Assembly _assemblyIn;
        private Assembly _assemblyOut;
        
        private static AsmdefConnection _showInfoParent;
        Vector2 _scrollPosition = Vector2.zero;
        private Dictionary<Type, HashSet<Type>> _typeNames = new ();
        private bool _isDestroyed;
        
        public AsmdefConnection(Assembly assemblyIn, Assembly assemblyOut, ConnectionPoint inPoint, ConnectionPoint outPoint, Action<Connection> OnClickRemoveConnection) : base(inPoint, outPoint, OnClickRemoveConnection)
        {
            _assemblyIn = assemblyIn;
            _assemblyOut = assemblyOut;
        }
        
        public override void Draw()
        {
            if(_isDestroyed)
                return;
            base.Draw();
            var oldColor = Handles.color;
            Handles.color = _buttonColor;
            if (Handles.Button((inPoint.rect.center + outPoint.rect.center) * 0.5f, Quaternion.identity, 6, 8, Handles.RectangleHandleCap))
            {
                if (_showInfoParent == this)
                {
                    _showInfoParent = null;
                }
                else
                {
                    _showInfoParent = this;
                    _typeNames = FindUsedClasses(_assemblyOut, _assemblyIn);   
                }
            }

            Handles.color = oldColor;
            if (_showInfoParent == this)
            {
                if(GUI.Button(new Rect(600 + 10, 300 - 20, 20, 20), "X"))
                {
                    _showInfoParent = null;
                    return;
                }
                GUI.Label(new Rect(10, 300, 600, 20), $"{_assemblyOut.name}    ->    {_assemblyIn.name}");
                var heightElementsCount = _typeNames.Count + _typeNames.SelectMany(x => x.Value).Count();
                var desiredHeight = heightElementsCount * 30 + 70;
                
                GUI.Box(new Rect(10, 300, 600, desiredHeight), "");
                
                if (_typeNames.Count == 0)
                {
                    GUI.Label(new Rect(10, 300 + 20, 200, 20), $"No dependencies found! Delete?");
                    if(GUI.Button(new Rect(300 - 70, 300 + 50, 70, 20), "Delete"))
                    {
                        var removeGuidFilePath = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(_assemblyIn.name);
                        var fileMetaLines = File.ReadAllLines(removeGuidFilePath + ".meta");
                        var guid = fileMetaLines.FirstOrDefault(x => x.Contains("guid:")).
                            Replace("guid:", "").
                            Replace(" ", "");
                        
                        var modifyFilePath =  UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(_assemblyOut.name);
                        var fileLines = File.ReadAllLines(modifyFilePath);
                        for (int i = 1; i < fileLines.Length - 1; i++)
                        {
                            var line = fileLines[i];
                            if (line.Contains(guid))
                            {
                                var prevLine = fileLines[i - 1];
                                fileLines[i] = "";
                                if (prevLine.Contains("GUID"))
                                {
                                    fileLines[i - 1] = prevLine.Remove(prevLine.Length - 1, 1);
                                }
                            }
                        }

                        File.WriteAllLines(modifyFilePath, fileLines);
                        AssetDatabase.Refresh();
                        _isDestroyed = true;
                    }
                }
                
                _scrollPosition = GUI.BeginScrollView(new Rect(10, 300, 600, desiredHeight + 20), _scrollPosition, new Rect(0, 0, 600, Mathf.Min(desiredHeight, 300)));

                int index = 0;
                foreach (var userUsedPair in _typeNames)
                {
                    var user = userUsedPair.Key;
                    var usedList = userUsedPair.Value;
                    bool isFirstDependency = true;
                    foreach (var used in usedList)
                    {
                        var text = "";
                        if (isFirstDependency)
                        {
                            text = $"{user.Name}  ->";
                            GUI.Label(new Rect(0, index * (20 + 10) + 50, (user.Name.Length + used.Name.Length) * 100 / 10, 20), 
                                text);
                            index++;
                        }
                        var spaces = String.Concat(Enumerable.Repeat("  ", user.Name.Length));
                        text = $"{spaces}      {used.Name}";
                        GUI.Label(new Rect(0, index * (20 + 10) + 50, (user.Name.Length + used.Name.Length) * 100 / 10, 20), 
                            text);

                        index++;
                        isFirstDependency = false;
                    }
                }

                GUI.EndScrollView();
            }
        }

        private Dictionary<Type, HashSet<Type>> FindUsedClasses(Assembly usedIn, Assembly usedFrom)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            
            var assemblyIn = assemblies.FirstOrDefault(x => x.GetName().Name == usedIn.name);
            var assemblyFrom = assemblies.FirstOrDefault(x => x.GetName().Name == usedFrom.name);

            var assemblyTypes = assemblyIn.GetTypes();
            var usedClasses = GetTypesUsedIn_From(assemblyTypes, assemblyFrom);

            return usedClasses;
        }

        private Dictionary<Type, HashSet<Type>> GetTypesUsedIn_From(Type[] types, System.Reflection.Assembly from)
        {
            var allTypes = from.GetTypes().ToHashSet();

            var usedTypes = new Dictionary<Type, HashSet<Type>>();
            foreach (var type in types)
            {
                var typesUsedByType = GetTypesUsedByType(type, allTypes);
                if (!usedTypes.ContainsKey(type))
                {
                    usedTypes[type] = new HashSet<Type>();
                }

                foreach (var used in typesUsedByType)
                {
                    usedTypes[type].Add(used);      
                }
            }
            return usedTypes;
        }

        private List<Type> GetTypesUsedByType(Type type, HashSet<Type> allTypes)
        {
            var usedTypes = new HashSet<Type>();

            var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            
            var methodInfos = type.GetMethods(flags).Where(m => !typeof(object)
                    .GetMethods(flags)
                    .Select(me => me.Name)
                    .Contains(m.Name));
            methodInfos = methodInfos.Where(m => !typeof(MonoBehaviour)
                    .GetMethods(flags)
                    .Select(me => me.Name)
                    .Contains(m.Name));
            methodInfos = methodInfos.Where(m => !typeof(ScriptableObject)
                    .GetMethods(flags)
                    .Select(me => me.Name)
                    .Contains(m.Name));
            
            var fields = type.GetFields(flags).Select(fi => fi.FieldType)
                .Where(x => allTypes.Contains(x));
            var properties = type.GetProperties(flags).Select(fi => fi.PropertyType)
                .Where(x => allTypes.Contains(x));
            var methodParameters = methodInfos.SelectMany(mi => mi.GetParameters())
                .Select(pa => pa.ParameterType)
                .Where(x => allTypes.Contains(x));
            var methodLocalVariables = methodInfos.Select(mi => mi.GetMethodBody())
                .Where(mb => mb != null).SelectMany(mb => mb.LocalVariables)
                .Select(lv => lv.LocalType)
                .Where(x => allTypes.Contains(x));
            var methodGenericVariables = methodInfos.SelectMany(mi => mi.GetGenericArguments())
                .Select(lv => lv.ReflectedType)
                .Where(x => allTypes.Contains(x));
            var methodReturns = methodInfos.Select(mi => mi.ReturnType).Where(r => r != null)
                .Select(r => r)
                .Where(x => allTypes.Contains(x));

            foreach (var t in fields)
            {
                usedTypes.Add(t);
            }          
            foreach (var t in properties)
            {
                usedTypes.Add(t);
            }
            foreach (var t in methodParameters)
            {
                usedTypes.Add(t);
            }
            foreach (var t in methodLocalVariables)
            {
                usedTypes.Add(t);
            }
            foreach (var t in methodGenericVariables)
            {
                usedTypes.Add(t);
            }
            foreach (var t in methodReturns)
            {
                usedTypes.Add(t);
            }

            foreach (var mi in methodInfos)
            {
                var typeNames = IL_TypesParser.DumpMethod(mi);
                foreach (var typeName in typeNames)
                {
                    var t = allTypes.FirstOrDefault(x => x.FullName == typeName);
                    if (t == null)
                    {
                        continue;
                    }
                    usedTypes.Add(t);   
                }
            }

            return usedTypes.ToList();
        }
    }
}
