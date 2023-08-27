using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEngine;

namespace Brawl.Core
{
    public class AsmdefNode : Node
    {
        private AssembliesContext _assembliesContext;
        
        public Assembly Assembly { get; private set; }
        public string AsmdefName { get; private set; }
        
        public AsmdefNode(Assembly assembly, AssembliesContext assembliesContext, Vector2 position, float width, float height, GUIStyle nodeStyle, GUIStyle selectedStyle, GUIStyle inPointStyle,
            GUIStyle outPointStyle, Action<ConnectionPoint> OnClickInPoint, Action<ConnectionPoint> OnClickOutPoint, Action<Node> OnClickRemoveNode) 
            : base(position, width, height, nodeStyle, selectedStyle, inPointStyle, outPointStyle, OnClickInPoint, OnClickOutPoint, OnClickRemoveNode)
        {
            AsmdefName = assembly.name;
            Assembly = assembly;
            _assembliesContext = assembliesContext;
        }

        protected override void DrawInternal()
        {
            base.DrawInternal();
            var textRect = rect;
            textRect.min += Vector2.right * 20;
            GUI.Label(textRect, AsmdefName);
        }
    }
}
