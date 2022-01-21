using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class UIHolder : MonoBehaviour
{
    public ClassData ClassData;
}

[Serializable]
public class UIHolderInfo
{
}

[Serializable]
public class UIHolderSubClassInfo
{
}

[Serializable]
public class ClassData
{
    [HideInInspector] public string propName;

    [HideInInspector] public string ClassName;

    [FoldoutGroup("$GetClassName")]
    [ListDrawerSettings(IsReadOnly = true)]
    [ShowIf("$ConditionFields"), LabelText("$GetFieldTitle")]
    [Indent]
    public List<ClassField> fields;

    [FoldoutGroup("$GetClassName")]
    [ListDrawerSettings(IsReadOnly = true)]
    [ShowIf("$ConditionClass"), LabelText("$GetClassTitle")]
    [Indent]
    public List<ClassData> classes;


    public ClassData Copy()
    {
        ClassData cls = new ClassData();

        cls.ClassName = ClassName;
        cls.propName = propName;

        cls.fields = new List<ClassField>(fields == null ? 0 : fields.Count);
        if (fields != null)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                cls.fields.Add(fields[i].Copy());
            }
        }

        cls.classes = new List<ClassData>(classes == null ? 0 : classes.Count);
        if (classes != null)
        {
            for (int i = 0; i < classes.Count; i++)
            {
                cls.classes.Add(classes[i].Copy());
            }
        }

        return cls;
    }

    string GetClassName()
    {
        if (!string.IsNullOrEmpty(ClassName))
        {
            var arr = ClassName.Split('+');
            if (arr.Length > 1)
            {
                return arr[arr.Length - 1];
            }
            else
            {
                arr = ClassName.Split('.');
                if (arr.Length > 1)
                    return arr[arr.Length - 1];
            }
        }

        return ClassName;
    }

    string GetClassTitle()
    {
        return $"Class({classes.Count})";
    }

    string GetFieldTitle()
    {
        return $"Fields({fields.Count})";
    }

    bool ConditionClass()
    {
        return classes != null && classes.Count > 0;
    }

    bool ConditionFields()
    {
        return fields != null && fields.Count > 0;
    }
}

[Serializable]
public class ClassField
{
    public static HashSet<Type> PrimitiveTypes = new HashSet<Type>()
    {
        typeof(string), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float)
    };

    private static HashSet<string> PrimitiveTypesStr;
    public static bool IsPrimitiveType(string clsType)
    {
        if (PrimitiveTypesStr == null)
        {
            PrimitiveTypesStr = new HashSet<string>();
            foreach (Type t in PrimitiveTypes)
            {
                PrimitiveTypesStr.Add(t.ToString());
            }
        }

        return PrimitiveTypesStr.Contains(clsType);
    }

    public static Regex regArray = new Regex(@"(.*)\[\]");
    public static Regex regList = new Regex(@"System.Collections.Generic.List`1\[(.*)\]");


    public const string ASSAMBLE_UNITY = "UnityEngine";
    public const string ASSAMBLE_UNITY_UI = "UnityEngine.UI";
    public const string ASSAMBLE_SELF = "Self";

    public const string TYPE_NORMAL = "TYPE_NORMAL";
    public const string TYPE_ARRAY = "TYPE_ARRAY";
    public const string TYPE_LIST = "TYPE_LIST";

    [HideInInspector] public string Name;
    [HideInInspector] public string ClassType;

    [ShowIf("$ConditionVal"), LabelText("$GetName")]
    public string val;

    [Required, ChildGameObjectsOnly, ShowIf("$ConditionObj"), LabelText("$Name")]
    public GameObject obj;

    [ValidateInput("ComponentValid", "$ComponentStr")]
    [Required("$ComponentStr"), ChildGameObjectsOnly, ShowIf("$ConditionComponent"), LabelText("$GetName")]
    public Component component;

    [ShowIf("$ConditionBool"), LabelText("$Name")]
    public bool boolVal;

    [ShowIf("$ConditionColor"), LabelText("$Name")]
    public Color colorVal;

    #region Array logic

    [HideInInspector] public string ListType;
    [HideInInspector] public string ListItemType;

    [HideInInspector] public bool needUnityFields;

    [Indent]
    [ListDrawerSettings(HideAddButton = true)]
    [ShowIf("ConditionUnityFieldArray"), LabelText("$GetListName")]
    [InlineButton("Add")]
    public List<ClassField> unityFields;

    [HideInInspector] public bool needClasses;

    [Indent]
    [ListDrawerSettings(HideAddButton = true)]
    [ShowIf("$ConditionClassList"), LabelText("$GetListName")]
    [InlineButton("add")]
    public List<ClassData> classes;

    [HideInInspector] public bool needPrimitiveField;

    [Indent]
    [ListDrawerSettings(HideAddButton = true)]
    [ShowIf("$ConditionPrimitiveList"), LabelText("$GetListName")]
    [InlineButton("ADD")]
    public List<ClassField> PrimitiveFields;

    #endregion

    string ComponentStr()
    {
        var typeArr = ClassType.Split('.');
        var type = typeArr[typeArr.Length - 1];
        return $"{type} is required";
    }

    void Add()
    {
        var field = new ClassField();
        field.Name = unityFields.Count.ToString();
        field.ClassType = ListItemType;
        unityFields.Add(field);
    }

    void add()
    {
        var cls = new ClassData();
        cls.propName = classes.Count.ToString();
        cls.ClassName = ListItemType;

        cls.fields = new List<ClassField>();
        cls.classes = new List<ClassData>();

        var assembly = Assembly.Load("Assembly-CSharp");
        var type = assembly.GetType(ListItemType);
        ProcFields(cls, type);
        ProcSubClasses(cls, type);

        classes.Add(cls);
    }

    void ADD()
    {
        var fi = new ClassField();
        fi.Name = classes.Count.ToString();
        fi.ClassType = ListItemType;
        PrimitiveFields.Add(fi);
    }

    void AddVal()
    {
        var cls = new ClassData();
        cls.propName = classes.Count.ToString();
        cls.ClassName = ListItemType;
        classes.Add(cls);
    }

    public static Type GetType(string typeName)
    {
        var infoType = Type.GetType(typeName);
        if (infoType == null)
            infoType = Type.GetType(typeName + ",UnityEngine");
        if (infoType == null)
            infoType = Type.GetType(typeName + ",UnityEngine.UI");
        return infoType;
    }

    public static string GetAssemblyName(Assembly assembly)
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

    void ProcFields(ClassData cls, Type type)
    {
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
            var field = new ClassField();

            field.Name = fields[i].Name;

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

                field.ListItemType = typeName;
                if (!string.IsNullOrEmpty(typeName))
                {
                    field.ClassType = fields[i].FieldType.ToString();

                    var itemType = GetType(typeName);
                    var assembly = itemType.Assembly;
                    var assemblyName = GetAssemblyName(assembly);
                    field.needClasses = false;
                    field.needUnityFields = false;
                    if (assemblyName == ClassField.ASSAMBLE_SELF)
                    {
                        field.needClasses = true;
                    }
                    else
                    {
                        field.needUnityFields = true;
                    }

                    if (isArray)
                        field.ListType = ClassField.TYPE_ARRAY;
                    if (isList)
                        field.ListType = ClassField.TYPE_LIST;
                }
            }
            else
            {
                field.ClassType = fields[i].FieldType.ToString();
                field.ListType = ClassField.TYPE_NORMAL;
            }

            cls.fields.Add(field);
        }
    }

    void ProcSubClasses(ClassData cls, Type type)
    {
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
            var item = new ClassData();
            item.classes = new List<ClassData>();
            item.fields = new List<ClassField>();

            item.propName = fields[i].Name;
            item.ClassName = fields[i].FieldType.ToString();

            ProcFields(item, fields[i].FieldType);
            ProcSubClasses(item, fields[i].FieldType);

            cls.classes.Add(item);
        }
    }

    string GetListName()
    {
        var typeArr = ListItemType.Split('.');
        var type = typeArr[typeArr.Length - 1];
        if (ListType == TYPE_LIST)
        {
            var str = type.Replace("+", ".");
            return $"{Name} (List<{str}>)";
        }
        else if (ListType == TYPE_ARRAY)
        {
            var str = type.Replace("+", ".") + "[]";
            return $"{Name} ({str})";
        }
        else
        {
            return Name;
        }
    }

    bool ConditionUnityFieldArray()
    {
        return needUnityFields;
    }

    bool ConditionClassList()
    {
        return needClasses;
    }

    bool ConditionPrimitiveList()
    {
        return needPrimitiveField;
    }

    bool ConditionColor()
    {
        return ClassType == typeof(Color).ToString();
    }

    public ClassField Copy()
    {
        var clsField = new ClassField();

        clsField.Name = Name;
        clsField.ClassType = ClassType;
        clsField.val = val;
        clsField.obj = obj;
        clsField.component = component;
        clsField.boolVal = boolVal;
        clsField.colorVal = colorVal;

        clsField.ListType = ListType;
        clsField.ListItemType = ListItemType;
        clsField.needUnityFields = needUnityFields;
        clsField.unityFields = new List<ClassField>(unityFields == null ? 0 : unityFields.Count);
        if (unityFields != null)
        {
            for (int i = 0; i < unityFields.Count; i++)
            {
                clsField.unityFields.Add(unityFields[i].Copy());
            }
        }

        clsField.needClasses = needClasses;
        clsField.classes = new List<ClassData>(classes == null ? 0 : classes.Count);
        if (classes != null)
        {
            for (int i = 0; i < classes.Count; i++)
            {
                clsField.classes.Add(classes[i].Copy());
            }
        }

        clsField.needPrimitiveField = needPrimitiveField;
        clsField.PrimitiveFields = new List<ClassField>(PrimitiveFields == null ? 0 : PrimitiveFields.Count);
        if (PrimitiveFields != null)
        {
            for (int i = 0; i < PrimitiveFields.Count; i++)
            {
                clsField.PrimitiveFields.Add(PrimitiveFields[i].Copy());
            }
        }

        return clsField;
    }

    string GetName()
    {
        var typeArr = ClassType.Split('.');
        var type = typeArr[typeArr.Length - 1];
        return $"{Name} ({type})";
    }

    bool ComponentValid()
    {
        if (component != null)
        {
            var type = Type.GetType(ClassType);
            if (type == null)
                type = Type.GetType(ClassType + ",UnityEngine");
            if (type == null)
                type = Type.GetType(ClassType + ",UnityEngine.UI");
            return component.gameObject.GetComponent(type) != null;
        }

        return true;
    }

    bool ConditionVal()
    {
        return IsPrimitiveType(ClassType);
    }

    bool ConditionObj()
    {
        return ClassType == typeof(GameObject).ToString();
    }

    bool ConditionComponent()
    {
        var type = Type.GetType(ClassType);
        if (type == null)
            type = Type.GetType(ClassType + ",UnityEngine");
        if (type == null)
            type = Type.GetType(ClassType + ",UnityEngine.UI");
        if (type == null)
            return false;
        return type.IsSubclassOf(typeof(Component));
    }

    bool ConditionBool()
    {
        return ClassType == typeof(bool).ToString();
    }
}