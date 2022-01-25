using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class MainUI : MonoBehaviour
    {
    public class AmuletHolder : UIHolderInfo
            {
                public CompInfo comp;
            }
    
            public class CompInfo : UIHolderSubClassInfo
            {
                public GameObject[] go;
                public Image img;
            }
    

    
        public class MainUIHolder : UIHolderInfo
        {
            public List<GameObject> objs2;
            public C4[] c4s;
        }

        public class C1 : UIHolderSubClassInfo
        {
            public C2 c2;
        }

        public class C2 : UIHolderSubClassInfo
        {
            public C3 c3;
            public List<C4> objs;
        }

        public class C3 : UIHolderSubClassInfo
        {
            public C4 c4;
        }

        public class C4 : UIHolderSubClassInfo
        {
            public int c_4;
        }

        private AmuletHolder _holder => Get<AmuletHolder>();
  
        T Get<T>() where T : UIHolderInfo,new()
        {
            var holder = new T();
            var type = typeof(T);
            var holderScript = GetComponent<UIHolderSerialized>();

            ProcFields(holder,type,holderScript.ClassData.fields);
            ProcSubClass(holder,type,holderScript.ClassData.classes);

            return holder;
        }

        void ProcFields(object target,Type type,List<ClassField> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                var info = fields[i];
                var field = type.GetField(info.PropName);
                if (info.needUnityFields)
                {
                    if (info.unityFields != null)
                    {
                        var infoType = GetType(info.AssemblyType, info.ListItemType);
                        if (info.ListType == ClassField.TYPE_ARRAY)
                        {
                            var arr = Array.CreateInstance(infoType, info.unityFields.Count);

                            for (int j = 0; j < info.unityFields.Count; j++)
                            {
                                var item = info.unityFields[j];
                                if (item.obj != null)
                                    arr.SetValue(item.obj,j);
                                else
                                {
                                      var script = item.component.GetComponent(infoType);
                                                                        arr.SetValue(script, j);
                                }
                            }

                            field.SetValue(target, arr);
                        }
                        else if (info.ListType == ClassField.TYPE_LIST)
                        {
                            Type listType = typeof(List<>);
                            //指定泛型的具体类型
                            Type newType = listType.MakeGenericType(new Type[]{infoType});
                            //创建一个list返回
                            var list = (IList)Activator.CreateInstance(newType);
                            for (int j = 0; j < info.unityFields.Count; j++)
                            {
                                var item = info.unityFields[j];
                                if (item.obj != null)
                                    list.Add(item.obj);
                                else
                                {
                                    var script = item.component.GetComponent(infoType);
                                    list.Add(script);
                                }
                            }

                            field.SetValue(target, list); 
                        }
                    }
 
                }
                else if (info.needClasses)
                {
                    if (info.classes != null)
                    {
                        var infoType = GetType(info.AssemblyType,info.ListItemType);
                        if (info.ListType == ClassField.TYPE_ARRAY)
                        {
                            var arr = Array.CreateInstance(infoType, info.classes.Count);

                            for (int j = 0; j < info.classes.Count; j++)
                            {
                                var item = info.classes[j];
                                var cls = Activator.CreateInstance(infoType);
                                ProcFields(cls, infoType, item.fields);
                                ProcSubClass(cls, infoType, item.classes);
                                arr.SetValue(cls,j);
                            }

                            field.SetValue(target, arr);
                        }
                        else if (info.ListType == ClassField.TYPE_LIST)
                        {
                            Type listType = typeof(List<>);
                            //指定泛型的具体类型
                            Type newType = listType.MakeGenericType(new Type[]{infoType});
                            //创建一个list返回
                            var list = (IList)Activator.CreateInstance(newType);
                            for (int j = 0; j < info.classes.Count; j++)
                            {
                                var item = info.classes[j];
                                var cls = Activator.CreateInstance(infoType);
                                ProcFields(cls, infoType, item.fields);
                                ProcSubClass(cls, infoType, item.classes);
                                list.Add(cls);
                            }

                            field.SetValue(target, list); 
                        }
                    } 
                }
                else
                {
                    object obj = null;

                    if (info.ClassType == typeof(string).ToString())
                    {
                        obj = info.val;
                    }
                    else if (info.ClassType == typeof(short).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : short.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(ushort).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : ushort.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(int).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : int.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(uint).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : uint.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(long).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : long.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(ulong).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : ulong.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(float).ToString())
                    {
                        obj = string.IsNullOrEmpty(info.val) ? 0 : float.Parse(info.val);
                    }
                    else if (info.ClassType == typeof(bool).ToString())
                    {
                        obj = info.boolVal;
                    }
                   
                    else if (info.ClassType == typeof(GameObject).ToString())
                    {
                        obj = info.obj;
                    }
                    else
                    {
                        var infoType = GetType(info.AssemblyType,info.ClassType);
                        if (infoType != null)
                        {
                            if (typeof(Component).IsAssignableFrom(infoType))
                            {
                                obj = info.component.GetComponent(infoType);
                            }
                        }
                    }

                    field.SetValue(target, obj);
                }
            }
        }

        void ProcSubClass(object target,Type type,List<ClassData> subClasses)
        {
            for (int i = 0; i < subClasses.Count; i++)
            {
                var cls = subClasses[i];
                var field = type.GetField(cls.PropName);
                var subClsType = Type.GetType(cls.ClassName);
                var subCls = Activator.CreateInstance(subClsType);
                field.SetValue(target, subCls);
                
                ProcFields(subCls,subClsType ,cls.fields);
                ProcSubClass(subCls, subClsType,cls.classes);
            }
        }

        Type GetType(string assemblyType,string typeName)
        {
            Type result = null;
            if (assemblyType == ClassField.ASSAMBLE_UNITY)
            {
                result = assemblyUnity.GetType(typeName);
            }
            else if (assemblyType == ClassField.ASSAMBLE_UNITY_UI)
            {
                result = assemblyUI.GetType(typeName);
            }
            else if (assemblyType == ClassField.ASSAMBLE_SELF)
            {
                result = Type.GetType(typeName);
            }
            
            if(result == null)
            {
                result = assemblyCSharp.GetType(typeName);
            }

            return result;
        }


        private Assembly assemblyUI;
        private Assembly assemblyUnity;
        private Assembly assemblyCSharp;

        private void Awake()
        {
            assemblyUI = Assembly.Load("UnityEngine.UI");
            assemblyUnity = Assembly.Load("UnityEngine");
            assemblyCSharp = Assembly.Load("Assembly-CSharp");
            var holder = _holder;
            Debug.Log(holder.comp.img);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                
            }
        }
    }
}