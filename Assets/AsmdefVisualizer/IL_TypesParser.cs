using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

namespace AsmdefVisualizer
{
    /// <summary>
    /// https://stackoverflow.com/questions/5667816/get-types-used-inside-a-c-sharp-method-body
    /// </summary>
    public class IL_TypesParser 
    {
        public static List<string> DumpMethod(Delegate method)
        {
            // First we need to extract out the raw IL
            var mi = method.Method;
            return DumpMethod(mi);
        }

        public static List<string> DumpMethod(MethodInfo mi)
        {
            // For aggregating our response
            /*StringBuilder sb = new StringBuilder();*/
            var types = new HashSet<string>();
            
            var mb = mi.GetMethodBody();
            //Debug.Log($"scanning method: {mi.Name} {mi.DeclaringType.FullName}");
            if (mb == null)
            {
                return new List<string>();
            }

            var il = mb.GetILAsByteArray();
        
            // We'll also need a full set of the IL opcodes so we
            // can remap them over our method body
            var opCodes = typeof(System.Reflection.Emit.OpCodes)
                .GetFields()
                .Select(fi => (System.Reflection.Emit.OpCode)fi.GetValue(null));
        
            //opCodes.Dump();
        
            // For each byte in our method body, try to match it to an opcode
            var mappedIL = il.Select(op => 
                opCodes.FirstOrDefault(opCode => opCode.Value == op));
        
            // OpCode/Operand parsing: 
            //     Some opcodes have no operands, some use ints, etc. 
            //  let's try to cover all cases
            var ilWalker = mappedIL.GetEnumerator();
            while(ilWalker.MoveNext())
            {
                var mappedOp = ilWalker.Current;
                if(mappedOp.OperandType != OperandType.InlineNone)
                {
                    // For operand inference:
                    // MOST operands are 32 bit, 
                    // so we'll start there
                    var byteCount = 4;
                    long operand = 0;
                    string token = string.Empty;
        
                    // For metadata token resolution            
                    var module = mi.Module;
                    List<string> emptyList = new List<string>();
                    Func<int, List<string>> tokenResolver = tkn => new List<string>();
                    switch(mappedOp.OperandType)
                    {
                        // These are all 32bit metadata tokens
                        case OperandType.InlineMethod:        
                            tokenResolver = tkn =>
                            {
                                var resMethod = module.SafeResolveMethod((int)tkn);
                                if (resMethod == null)
                                {
                                    return emptyList;
                                }
                                var list = new List<string>();
                                if (!string.IsNullOrEmpty(resMethod.DeclaringType.FullName))
                                {
                                    list.Add(resMethod.DeclaringType.FullName.Replace("+", "."));
                                }

                                foreach (var p in resMethod.GetParameters())
                                {
                                    if (!string.IsNullOrEmpty(p.ParameterType.FullName))
                                    {
                                        list.Add(p.ParameterType.FullName);
                                    }
                                }

                                try
                                {
                                    foreach (var genericArgument in resMethod.GetGenericArguments())
                                    {
                                        if (!string.IsNullOrEmpty(genericArgument.FullName))
                                        {
                                            list.Add(genericArgument.FullName);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"{mi.MemberType}   {e}");
                                }

                                return list;
                                
                            };
                            break;
                        case OperandType.InlineField:
                            tokenResolver = tkn =>
                            {
                                var field = module.SafeResolveField((int)tkn);
                                if (field == null) return emptyList;
                                return new List<string>() { field.FieldType.FullName };
                            };
                            break;
                        case OperandType.InlineSig:
                            tokenResolver = tkn =>
                            {
                                var sigBytes = module.SafeResolveSignature((int)tkn);
                                var catSig = string
                                    .Join(",", sigBytes);
                                if (catSig == null) return emptyList;
                                return new List<string>() {catSig};
                            };
                            break;
                        case OperandType.InlineString:
                            tokenResolver = tkn =>
                            {
                                var str = module.SafeResolveString((int)tkn);
                                if (str == null) return emptyList;
                                return new List<string>() {str};
                            };
                            break;
                        case OperandType.InlineType:
                            tokenResolver = tkn =>
                            {
                                var type = module.SafeResolveType((int)tkn);
                                if (type == null) return emptyList;
                                return new List<string>() {type.Name};
                            };
                            break;
                        // These are plain old 32bit operands
                        case OperandType.InlineI:
                        case OperandType.InlineBrTarget:
                        case OperandType.InlineSwitch:
                        case OperandType.ShortInlineR:
                            break;
                        // These are 64bit operands
                        case OperandType.InlineI8:
                        case OperandType.InlineR:
                            byteCount = 8;
                            break;
                        // These are all 8bit values
                        case OperandType.ShortInlineBrTarget:
                        case OperandType.ShortInlineI:
                        case OperandType.ShortInlineVar:
                            byteCount = 1;
                            break;
                    }
                    // Based on byte count, pull out the full operand
                    for(int i=0; i < byteCount; i++)
                    {
                        ilWalker.MoveNext();
                        operand |= ((long)ilWalker.Current.Value) << (8 * i);
                    }
        
                    var resolvedList = tokenResolver((int)operand);

                    /*if (resolvedList.Count == 0)
                    {
                        var resolved = operand.ToString();
                        sb.AppendFormat("{0} {1}",
                                mappedOp.Name, 
                                resolved)
                            .AppendLine();  
                    }*/
                    foreach (var resolved in resolvedList)
                    {
                        if (string.IsNullOrEmpty(resolved))
                        {
                            continue;
                        }
                        types.Add(resolved);
                    }
                }
                else
                {
                    // sb.AppendLine(mappedOp.Name);
                }
            }
            return types.ToList();//.ToString();
        }
        
        public static void Demo()
        {
            var foo = new Foo();
            foo.GetFooName((int)Time.time);
            foreach (var mi in typeof(Foo).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Debug.Log($"Method: {mi.Name}");
                Debug.Log(DumpMethod(mi));
            }
        }

        public class Foo
        {
            public const string FooName = "Foo";

            public string GetFooName(int rot1)
            {
                Debug.Log($"Something");
                GenericMethod<GameObject>();
                GetTAsync<Transform>();

                return typeof(Foo).Name + ":" + FooName + $"{Quaternion.Euler(rot1, rot1, rot1).ToString()}";
            }

            private Vector2 GetVector()
            {
                return new Vector2(2, 2);
            }

            private void GenericMethod<T>(){}

            private async Task<T> GetTAsync<T>()
            {
                return default;
            }
        }
    }
    public static class Ext
    {
        public static FieldInfo SafeResolveField(this Module m, int token)
        {
            FieldInfo fi;
            m.TryResolveField(token, out fi);
            return fi;
        }
        public static bool TryResolveField(this Module m, int token, out FieldInfo fi)
        {
            var ok = false;
            try { fi = m.ResolveField(token); ok = true; }
            catch { fi = null; }    
            return ok;
        }
        public static MethodBase SafeResolveMethod(this Module m, int token)
        {
            MethodBase fi;
            m.TryResolveMethod(token, out fi);
            return fi;
        }
        public static bool TryResolveMethod(this Module m, int token, out MethodBase fi)
        {
            var ok = false;
            try { fi = m.ResolveMethod(token); ok = true; }
            catch { fi = null; }    
            return ok;
        }
        public static string SafeResolveString(this Module m, int token)
        {
            string fi;
            m.TryResolveString(token, out fi);
            return fi;
        }
        public static bool TryResolveString(this Module m, int token, out string fi)
        {
            var ok = false;
            try { fi = m.ResolveString(token); ok = true; }
            catch { fi = null; }    
            return ok;
        }
        public static byte[] SafeResolveSignature(this Module m, int token)
        {
            byte[] fi;
            m.TryResolveSignature(token, out fi);
            return fi;
        }
        public static bool TryResolveSignature(this Module m, int token, out byte[] fi)
        {
            var ok = false;
            try { fi = m.ResolveSignature(token); ok = true; }
            catch { fi = null; }    
            return ok;
        }
        public static Type SafeResolveType(this Module m, int token)
        {
            Type fi;
            m.TryResolveType(token, out fi);
            return fi;
        }
        public static bool TryResolveType(this Module m, int token, out Type fi)
        {
            var ok = false;
            try { fi = m.ResolveType(token); ok = true; }
            catch { fi = null; }    
            return ok;
        }
    }
}
