using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIHolder))]
public class UIHolderEditor : OdinEditor
{
    private string[] typeNames = null;
    private int curIndex = -1;
    private SerializedProperty propTypeName;
    private bool needForceRefreshByCompile = false;
    private Dictionary<string,ClassField> stashFieldData = new Dictionary<string,ClassField>();
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        int selectedIndex = EditorGUILayout.Popup("ClassType", curIndex, typeNames);
        if (selectedIndex < 0)
            selectedIndex = 0;
        if (curIndex != selectedIndex)
        {
            stashFieldData.Clear();
            
            curIndex = selectedIndex;
            propTypeName.stringValue = typeNames[selectedIndex];
            OnClassNameChange();
            needForceRefreshByCompile = false;
        }

        if (needForceRefreshByCompile)
        {
            ForceRefreshByCompile();
        }

        base.OnInspectorGUI();
        needForceRefreshByCompile = false;
    }

    void StashField(ClassField field,Stack<string> path)
    {
        path.Push(field.Name);
        var name = string.Join(".", path);
        stashFieldData[name] = field;

        if (field.unityFields != null)
        {
            for (int i = 0; i < field.unityFields.Count; i++)
            {
                path.Push(i.ToString());
                StashField(field.unityFields[i], path);
                path.Pop();
            }
        }

        if (field.classes != null)
        {
            for (int i = 0; i < field.classes.Count; i++)
            {
                path.Push(i.ToString());
                StashSubClass(field.classes[i], path);
                path.Pop();
            }
        }

        path.Pop();
    }

    void StashSubClass(ClassData cls,Stack<string> path)
    {
        path.Push(cls.ClassName);
        for (int i = 0; i < cls.fields.Count; i++)
        {
            StashField(cls.fields[i], path);
        }
        for (int i = 0; i < cls.classes.Count; i++)
        {
            StashSubClass(cls.classes[i], path);
        }
        path.Pop();
    }
    public static T GetValue<T> (SerializedProperty property) where T : class {
        object obj = property.serializedObject.targetObject;
        string path = property.propertyPath.Replace (".Array.data", "");
        string[] fieldStructure = path.Split ('.');
        Regex rgx = new Regex (@"\[\d+\]");
        for (int i = 0; i < fieldStructure.Length; i++) {
            if (fieldStructure[i].Contains ("[")) {
                int index = System.Convert.ToInt32 (new string (fieldStructure[i].Where (c => char.IsDigit (c)).ToArray ()));
                obj = GetFieldValueWithIndex (rgx.Replace (fieldStructure[i], ""), obj, index);
            } else {
                obj = GetFieldValue (fieldStructure[i], obj);
            }
        }
        return (T) obj;
    }   
    
    private static object GetFieldValueWithIndex (string fieldName, object obj, int index, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) {
        FieldInfo field = obj.GetType ().GetField (fieldName, bindings);
        if (field != null) {
            object list = field.GetValue (obj);
            if (list.GetType ().IsArray) {
                return ((object[]) list)[index];
            } else if (list is IEnumerable) {
                return ((IList) list)[index];
            }
        }
        return default (object);
    }
    private static object GetFieldValue (string fieldName, object obj, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) {
        FieldInfo field = obj.GetType ().GetField (fieldName, bindings);
        if (field != null) {
            return field.GetValue (obj);
        }
        return default (object);
    }
    void ForceRefreshByCompile()
    {
        stashFieldData.Clear();
        
        var clsProp = serializedObject.FindProperty("ClassData");
        var oldBoxData = GetValue<object>(clsProp);
        var oldData = oldBoxData as ClassData;
        oldData = oldData.Copy();

        for (int i = 0; i < oldData.fields.Count; i++)
        {
            var stack = new Stack<string>();
            stack.Push(oldData.ClassName);
            StashField(oldData.fields[i], stack);
        }

        for (int i = 0; i < oldData.classes.Count; i++)
        {
            var stack = new Stack<string>();
            stack.Push(oldData.ClassName);
            StashSubClass(oldData.classes[i], stack);
        }
        
        var fieldsArr = serializedObject.FindProperty("ClassData.fields");
        var subClassArr = serializedObject.FindProperty("ClassData.classes"); 
        fieldsArr.ClearArray();
        subClassArr.ClearArray();
        
        OnClassNameChange();
    }

    

    void OnClassNameChange()
    {
        var assembly = Assembly.Load("Assembly-CSharp");
        Type type = assembly.GetType(propTypeName.stringValue);
        var fileds = type.GetFields();

        var fieldsArr = serializedObject.FindProperty("ClassData.fields");
        var subClassArr = serializedObject.FindProperty("ClassData.classes");
        
        
        var stack = new Stack<string>();
        stack.Push(type.ToString());
        ProcFields(fieldsArr, type,stack);
        
        var clsStack = new Stack<string>();
        clsStack.Push(type.ToString());
        ProcSubClass(subClassArr, type,clsStack);

        serializedObject.ApplyModifiedProperties();
    }

    void ProcFields(SerializedProperty fieldArr, Type type,Stack<string> path)
    {
        fieldArr.ClearArray();

        var arr = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        List<FieldInfo> fields = new List<FieldInfo>();
        for (int i = 0; i < arr.Length; i++)
        {
            var field = arr[i];
            if (field.FieldType.IsSubclassOf(typeof(UIHolderSubClassInfo)))
                continue;
            fields.Add(field);
        }

        for (int i = 0; i < fields.Count; i++)
        {
            fieldArr.InsertArrayElementAtIndex(i);

            var field = fieldArr.GetArrayElementAtIndex(i);
            var assemblyField = fields[i].FieldType.Assembly;
            path.Push(fields[i].Name);
            SetString(field,"Name",fields[i].Name);
            if (typeof(IList).IsAssignableFrom(fields[i].FieldType))
            {
                string typeName = string.Empty;
                bool isArray = false;
                bool isList = false;
                var match = ClassField.regArray.Match(fields[i].FieldType.ToString());
                if (match.Success)
                {
                    isArray = true;
                    var group = match.Groups[1];
                    typeName = group.Value;
                }
                else
                {
                    isList = true;
                    match = ClassField.regList.Match(fields[i].FieldType.ToString());
                    if (match.Success)
                    {
                        var group = match.Groups[1];
                        typeName = group.Value;
                    }
                }

                SetString(field,"ListItemType",typeName);
                if (!string.IsNullOrEmpty(typeName))
                {
                    SetString(field, "ClassType", fields[i].FieldType.ToString());
                    
                    var itemType = ClassField.GetType(typeName);
                    var assembly = itemType.Assembly;
                    var assemblyName = ClassField.GetAssemblyName(assembly);
                    SetBool(field, "needClasses", false);
                    SetBool(field, "needUnityFields", false);
                    if (assemblyName == ClassField.ASSAMBLE_SELF)
                    {
                        SetBool(field, "needClasses", true);
                    }
                    else
                    {
                        SetBool(field, "needUnityFields", true);
                    }
                    if (isArray)
                        SetString(field, "ListType", ClassField.TYPE_ARRAY);
                    if(isList)
                        SetString(field, "ListType", ClassField.TYPE_LIST);
                    
                    string name = string.Join(".", path);
                    if (stashFieldData.TryGetValue(name, out var stashData))
                    {
                        var curNeedClasses = GetBool(field, "needClasses");
                        if (curNeedClasses && stashData.classes != null)
                        {
                            if (stashData.ListItemType == typeName)
                            {
                                var classArr = field.FindPropertyRelative("classes");

                                for (int j = 0; j < stashData.classes.Count; j++)
                                {
                                    path.Push(j.ToString());
                                    path.Push(typeName);
                                    var data = stashData.classes[j];
                                    classArr.InsertArrayElementAtIndex(j);
                                    var clsItem = classArr.GetArrayElementAtIndex(j);
                                    var _type = ClassField.GetType(typeName);

                                    SetString(clsItem, "ClassName", typeName);
                                    SetString(clsItem, "propName", j.ToString());

                                    var _fieldArr = clsItem.FindPropertyRelative("fields");
                                    var _classArr = clsItem.FindPropertyRelative("classes");
                                    ProcFields(_fieldArr, _type, path);
                                    ProcSubClass(_classArr, _type, path);
                                    path.Pop();
                                    path.Pop();
                                }
                            }
                        }

                        var curNeedUnityFields = GetBool(field, "needUnityFields");
                        if (curNeedUnityFields && stashData.unityFields != null)
                        {
                            if (stashData.ListItemType == typeName)
                            {
                                var classArr = field.FindPropertyRelative("unityFields");
                                for (int j = 0; j < stashData.unityFields.Count; j++)
                                {
                                    var data = stashData.unityFields[j];
                                    classArr.InsertArrayElementAtIndex(j);
                                    var clsItem = classArr.GetArrayElementAtIndex(j);

                                    SetString(clsItem, "ClassType", stashData.ListItemType);
                                    SetString(clsItem, "ListType", ClassField.TYPE_NORMAL);
                                    SetString(clsItem, "Name", j.ToString());
                                    SetString(clsItem, "val", data.val);
                                    SetObject(clsItem, "obj", data.obj);
                                    SetObject(clsItem, "component", data.component);
                                    SetBool(clsItem, "boolVal", data.boolVal);
                                    SetColor(clsItem, "colorVal", data.colorVal);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                SetString(field, "ClassType", fields[i].FieldType.ToString());
                SetString(field, "ListType", ClassField.TYPE_NORMAL);
                string name = string.Join(".", path);
                if (stashFieldData.TryGetValue(name, out var stashData))
                {
                    if (stashData.ClassType == fields[i].FieldType.ToString())
                    {
                        SetString(field, "val", stashData.val);
                        SetObject(field, "obj", stashData.obj);
                        SetObject(field, "component", stashData.component);
                        SetBool(field, "boolVal", stashData.boolVal);
                        SetColor(field, "colorVal", stashData.colorVal);
                    }
                    else
                    {
                        //类型变了，重置
                        SetFieldToDefault(field);
                    }
                }
                else
                {
                    //新属性,重置
                    SetFieldToDefault(field);
                }
            }
            SetAssemblyType(assemblyField, field);
            path.Pop();
        }
    }
    

    void SetFieldToDefault(SerializedProperty field)
    {
        SetString(field,"val",string.Empty);
        SetObject(field, "obj", null);
        SetObject(field,"component",null);
        SetBool(field, "boolVal", false); 
        SetColor(field, "colorVal",Color.white);
    }

    void ProcSubClass(SerializedProperty classArr, Type type,Stack<string> path)
    {
        
        classArr.ClearArray();
        
        var arr = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        List<FieldInfo> fields = new List<FieldInfo>();
        for (int i = 0; i < arr.Length; i++)
        {
            var field = arr[i];
            if (!field.FieldType.IsSubclassOf(typeof(UIHolderSubClassInfo)))
                continue;
            fields.Add(field);
        }

        for (int i = 0; i < fields.Count; i++)
        {
            classArr.InsertArrayElementAtIndex(i);

            var clsItem = classArr.GetArrayElementAtIndex(i);
            SetString(clsItem,"propName",fields[i].Name);
            SetString(clsItem,"ClassName",fields[i].FieldType.ToString());

            var fieldArr = clsItem.FindPropertyRelative("fields");
            var subClassArr = clsItem.FindPropertyRelative("classes");

            path.Push(fields[i].FieldType.ToString());
            ProcFields(fieldArr, fields[i].FieldType,path);
            ProcSubClass(subClassArr, fields[i].FieldType,path);
            path.Pop();
        }
    }

    bool GetBool(SerializedProperty self, string propName)
    {
        var prop = self.FindPropertyRelative(propName); 
        return prop.boolValue;
    }
    
    void SetBool(SerializedProperty self, string propName,bool val)
    {
        var prop = self.FindPropertyRelative(propName);
        prop.boolValue = val;
    }
    
    void SetObject(SerializedProperty self, string propName,UnityEngine.Object val)
    {
        var prop = self.FindPropertyRelative(propName);
        prop.objectReferenceValue = val;
    }

    void SetString(SerializedProperty self,string propName,string value)
    {
        var prop = self.FindPropertyRelative(propName);
        prop.stringValue = value;
    }

    void SetColor(SerializedProperty self, string propName, Color val)
    {
        var prop = self.FindPropertyRelative(propName);
        prop.colorValue = val;
    }
    
    void SetAssemblyType(Assembly assembly, SerializedProperty field)
    {
        var name = GetAssemblyName(assembly);
        SetString(field, "AssemblyType", name);
    }
    
    string GetAssemblyName(Assembly assembly)
    {
        if (assembly.ManifestModule.Name == "UnityEngine.UI.dll")
        {
            return ClassField.ASSAMBLE_UNITY_UI;
        }
        else if (assembly.ManifestModule.Name == "UnityEngine.CoreModule.dll")
        {
            return ClassField.ASSAMBLE_UNITY;
        }
        else
        {
            return ClassField.ASSAMBLE_SELF;
        } 
    }

    string GetString(SerializedProperty self, string propName)
    {
        var prop = self.FindPropertyRelative(propName);
        return prop.stringValue;
    }
   
    void OnEnable()
    {
        base.OnEnable();
        propTypeName = serializedObject.FindProperty("ClassData.ClassName");
        OnCompileComplete();
    }

    void OnCompileComplete()
    {
        RefreshTypeNames();
        needForceRefreshByCompile = true;
    }

    private void RefreshTypeNames()
    {
        typeNames = GetTypeNames(typeof(UIHolderInfo));
        curIndex = -1;
        for (int i = 0; i < typeNames.Length; i++)
        {
            if (typeNames[i] == propTypeName.stringValue)
            {
                curIndex = i;
                break;
            }
        }
    }

    private static string[] GetTypeNames(System.Type typeBase)
    {
        List<string> typeNames = new List<string>();
        var assembly = Assembly.Load("Assembly-CSharp");

        System.Type[] types = assembly.GetTypes();
        foreach (System.Type type in types)
        {
            if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
            {
                typeNames.Add(type.FullName);
            }
        }

        typeNames.Sort();
        return typeNames.ToArray();
    }
}
