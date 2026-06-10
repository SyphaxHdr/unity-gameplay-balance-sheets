using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GameplayBalanceSheets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameplayBalanceSheets.Editor
{
    public sealed class ScannedBalanceProperty
    {
        public bool selected;
        public string id;
        public string componentPath;
        public string componentName;
        public string componentTypeName;
        public string componentAssemblyQualifiedTypeName;
        public int componentIndex;
        public string propertyPath;
        public string propertyTypeName;
        public string displayName;
        public string currentValue;
        public List<string> enumNames = new List<string>();
    }

    public static class GameplayBalanceSheetUtility
    {
        public const string DefaultSheetsFolder = "Assets/GameplayBalanceSheets/Sheets";

        private static readonly HashSet<SerializedPropertyType> SupportedTypes =
            new HashSet<SerializedPropertyType>
            {
                SerializedPropertyType.Integer,
                SerializedPropertyType.Boolean,
                SerializedPropertyType.Float,
                SerializedPropertyType.String,
                SerializedPropertyType.Enum
            };

        public static List<GameplayBalanceSheetProfile> LoadAllSheets()
        {
            List<GameplayBalanceSheetProfile> sheets = new List<GameplayBalanceSheetProfile>();

            string[] guids = AssetDatabase.FindAssets("t:GameplayBalanceSheetProfile");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameplayBalanceSheetProfile sheet =
                    AssetDatabase.LoadAssetAtPath<GameplayBalanceSheetProfile>(path);

                if (sheet != null)
                {
                    sheets.Add(sheet);
                }
            }

            sheets.Sort((left, right) =>
                string.Compare(left.sheetTitle, right.sheetTitle, StringComparison.Ordinal)
            );

            return sheets;
        }

        public static GameplayBalanceSheetProfile CreateSheet()
        {
            EnsureFolder(DefaultSheetsFolder);

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Balance Sheet",
                "GD_New_BalanceSheet",
                "asset",
                "Choose where to save this balance sheet.",
                DefaultSheetsFolder
            );

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            GameplayBalanceSheetProfile sheet =
                ScriptableObject.CreateInstance<GameplayBalanceSheetProfile>();

            AssetDatabase.CreateAsset(sheet, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = sheet;
            EditorGUIUtility.PingObject(sheet);

            return sheet;
        }

        public static List<ScannedBalanceProperty> ScanTarget(GameObject targetRoot)
        {
            List<ScannedBalanceProperty> results = new List<ScannedBalanceProperty>();

            if (targetRoot == null)
            {
                return results;
            }

            MonoBehaviour[] components = targetRoot.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];

                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();

                string componentPath = GetRelativeTransformPath(targetRoot.transform, component.transform);
                int componentIndex = GetComponentIndex(component);

                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();

                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    if (!ShouldIncludeProperty(property))
                    {
                        continue;
                    }

                    string propertyPath = property.propertyPath;
                    string componentAssemblyName = componentType.AssemblyQualifiedName;
                    string id = BuildEntryId(
                        componentPath,
                        componentAssemblyName,
                        componentIndex,
                        propertyPath
                    );

                    ScannedBalanceProperty scannedProperty = new ScannedBalanceProperty
                    {
                        id = id,
                        componentPath = componentPath,
                        componentName = componentType.Name,
                        componentTypeName = componentType.FullName,
                        componentAssemblyQualifiedTypeName = componentAssemblyName,
                        componentIndex = componentIndex,
                        propertyPath = propertyPath,
                        propertyTypeName = property.propertyType.ToString(),
                        displayName = BuildDisplayName(propertyPath),
                        currentValue = GetPropertyValueAsString(property)
                    };

                    if (property.propertyType == SerializedPropertyType.Enum)
                    {
                        scannedProperty.enumNames.AddRange(property.enumNames);
                    }

                    results.Add(scannedProperty);
                }
            }

            return results;
        }

        public static int AddSelectedScannedPropertiesToSection(
            GameplayBalanceSheetProfile sheet,
            GameplayBalanceSheetSection section,
            List<ScannedBalanceProperty> scannedProperties
        )
        {
            if (sheet == null || section == null || scannedProperties == null)
            {
                return 0;
            }

            int addedCount = 0;

            for (int i = 0; i < scannedProperties.Count; i++)
            {
                ScannedBalanceProperty scannedProperty = scannedProperties[i];

                if (scannedProperty == null || !scannedProperty.selected)
                {
                    continue;
                }

                if (sheet.ContainsEntry(scannedProperty.id))
                {
                    continue;
                }

                GameplayBalanceSheetEntry entry = new GameplayBalanceSheetEntry
                {
                    selected = false,
                    displayName = scannedProperty.displayName,
                    description = string.Empty,

                    componentPath = scannedProperty.componentPath,
                    componentName = scannedProperty.componentName,
                    componentTypeName = scannedProperty.componentTypeName,
                    componentAssemblyQualifiedTypeName = scannedProperty.componentAssemblyQualifiedTypeName,
                    componentIndex = scannedProperty.componentIndex,
                    propertyPath = scannedProperty.propertyPath,
                    propertyTypeName = scannedProperty.propertyTypeName,
                    enumNames = new List<string>(scannedProperty.enumNames),

                    baselineValue = scannedProperty.currentValue,
                    currentValue = scannedProperty.currentValue,
                    testValue = scannedProperty.currentValue,
                    isMissing = false
                };

                section.entries.Add(entry);
                addedCount++;
            }

            EditorUtility.SetDirty(sheet);
            AssetDatabase.SaveAssets();

            return addedCount;
        }

        public static void RefreshCurrentValues(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null || sheet.targetRoot == null)
            {
                return;
            }

            ForEachEntry(sheet, entry =>
            {
                RefreshCurrentValue(sheet, entry);
            });

            EditorUtility.SetDirty(sheet);
            AssetDatabase.SaveAssets();
        }

        public static bool RefreshCurrentValue(
            GameplayBalanceSheetProfile sheet,
            GameplayBalanceSheetEntry entry
        )
        {
            if (sheet == null || sheet.targetRoot == null || entry == null)
            {
                return false;
            }

            MonoBehaviour component = FindComponent(sheet.targetRoot, entry);

            if (component == null)
            {
                MarkEntryMissing(entry);
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(entry.propertyPath);

            if (property == null)
            {
                MarkEntryMissing(entry);
                return false;
            }

            string oldCurrentValue = entry.currentValue;
            string newCurrentValue = GetPropertyValueAsString(property);

            entry.currentValue = newCurrentValue;
            entry.isMissing = false;

            if (string.IsNullOrWhiteSpace(entry.testValue) ||
                string.Equals(entry.testValue, oldCurrentValue, StringComparison.Ordinal))
            {
                entry.testValue = newCurrentValue;
            }

            return true;
        }

        public static bool ApplyEntryTestValue(
            GameplayBalanceSheetProfile sheet,
            GameplayBalanceSheetEntry entry
        )
        {
            if (sheet == null || sheet.targetRoot == null || entry == null)
            {
                return false;
            }

            MonoBehaviour component = FindComponent(sheet.targetRoot, entry);

            if (component == null)
            {
                MarkEntryMissing(entry);
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(entry.propertyPath);

            if (property == null)
            {
                MarkEntryMissing(entry);
                return false;
            }

            Undo.RecordObject(component, "Apply Balance Sheet Value");

            bool valueApplied = SetPropertyValueFromString(property, entry.testValue);

            if (!valueApplied)
            {
                Debug.LogWarning(
                    $"Could not apply value '{entry.testValue}' to '{entry.displayName}'."
                );
                return false;
            }

            serializedObject.ApplyModifiedProperties();

            if (PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            entry.currentValue = GetPropertyValueAsString(property);
            entry.testValue = entry.currentValue;
            entry.isMissing = false;

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(sheet);

            SaveTarget(sheet.targetRoot);
            return true;
        }

        public static void ApplyAllPendingValues(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            ForEachEntry(sheet, entry =>
            {
                if (entry.HasPendingTestValue)
                {
                    ApplyEntryTestValue(sheet, entry);
                }
            });

            RefreshCurrentValues(sheet);
        }

        public static void ApplySelectedPendingValues(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            ForEachEntry(sheet, entry =>
            {
                if (entry.selected && entry.HasPendingTestValue)
                {
                    ApplyEntryTestValue(sheet, entry);
                }
            });

            RefreshCurrentValues(sheet);
        }

        public static void RevertEntryToBaseline(
            GameplayBalanceSheetProfile sheet,
            GameplayBalanceSheetEntry entry
        )
        {
            if (entry == null)
            {
                return;
            }

            entry.testValue = entry.baselineValue;
            ApplyEntryTestValue(sheet, entry);
        }

        public static void RevertAllToBaseline(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            ForEachEntry(sheet, entry =>
            {
                RevertEntryToBaseline(sheet, entry);
            });

            RefreshCurrentValues(sheet);
        }

        public static void RevertSelectedToBaseline(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            ForEachEntry(sheet, entry =>
            {
                if (entry.selected)
                {
                    RevertEntryToBaseline(sheet, entry);
                }
            });

            RefreshCurrentValues(sheet);
        }

        public static void SetCurrentAsBaseline(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            RefreshCurrentValues(sheet);

            ForEachEntry(sheet, entry =>
            {
                if (!entry.isMissing)
                {
                    entry.baselineValue = entry.currentValue;
                    entry.testValue = entry.currentValue;
                }
            });

            EditorUtility.SetDirty(sheet);
            AssetDatabase.SaveAssets();
        }

        public static void PingTarget(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null || sheet.targetRoot == null)
            {
                return;
            }

            Selection.activeObject = sheet.targetRoot;
            EditorGUIUtility.PingObject(sheet.targetRoot);
        }

        public static void SaveSheet(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            EditorUtility.SetDirty(sheet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            folderPath = folderPath.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void ForEachEntry(
            GameplayBalanceSheetProfile sheet,
            Action<GameplayBalanceSheetEntry> action
        )
        {
            if (sheet == null || action == null)
            {
                return;
            }

            for (int sectionIndex = 0; sectionIndex < sheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = sheet.sections[sectionIndex];

                if (section == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
                {
                    GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                    if (entry != null)
                    {
                        action(entry);
                    }
                }
            }
        }

        private static bool ShouldIncludeProperty(SerializedProperty property)
        {
            if (property.propertyPath == "m_Script")
            {
                return false;
            }

            if (property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
            {
                return false;
            }

            if (!SupportedTypes.Contains(property.propertyType))
            {
                return false;
            }

            return true;
        }

        private static string BuildEntryId(
            string componentPath,
            string componentAssemblyQualifiedName,
            int componentIndex,
            string propertyPath
        )
        {
            return $"{componentPath}|{componentAssemblyQualifiedName}|{componentIndex}|{propertyPath}";
        }

        private static string BuildDisplayName(string propertyPath)
        {
            string cleanPath = propertyPath.Replace(".Array.data[", "[");
            string[] parts = cleanPath.Split('.');

            List<string> displayParts = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (part.Contains("["))
                {
                    int bracketIndex = part.IndexOf("[", StringComparison.Ordinal);
                    string fieldName = part.Substring(0, bracketIndex);
                    string indexText = part
                        .Substring(bracketIndex)
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty);

                    if (int.TryParse(indexText, out int index))
                    {
                        displayParts.Add($"{ObjectNames.NicifyVariableName(fieldName)} {index + 1}");
                    }
                    else
                    {
                        displayParts.Add(ObjectNames.NicifyVariableName(fieldName));
                    }

                    continue;
                }

                displayParts.Add(ObjectNames.NicifyVariableName(part));
            }

            return string.Join(" > ", displayParts);
        }

        private static string GetPropertyValueAsString(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";

                case SerializedPropertyType.Float:
                    return property.floatValue.ToString("R", CultureInfo.InvariantCulture);

                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;

                case SerializedPropertyType.Enum:
                    if (property.enumValueIndex >= 0 &&
                        property.enumValueIndex < property.enumNames.Length)
                    {
                        return property.enumNames[property.enumValueIndex];
                    }

                    return property.enumValueIndex.ToString(CultureInfo.InvariantCulture);

                default:
                    return string.Empty;
            }
        }

        private static bool SetPropertyValueFromString(SerializedProperty property, string value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    {
                        property.intValue = intValue;
                        return true;
                    }

                    return false;

                case SerializedPropertyType.Boolean:
                    if (bool.TryParse(value, out bool boolValue))
                    {
                        property.boolValue = boolValue;
                        return true;
                    }

                    return false;

                case SerializedPropertyType.Float:
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                    {
                        property.floatValue = floatValue;
                        return true;
                    }

                    return false;

                case SerializedPropertyType.String:
                    property.stringValue = value ?? string.Empty;
                    return true;

                case SerializedPropertyType.Enum:
                    int enumIndex = Array.IndexOf(property.enumNames, value);

                    if (enumIndex >= 0)
                    {
                        property.enumValueIndex = enumIndex;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private static MonoBehaviour FindComponent(
            GameObject targetRoot,
            GameplayBalanceSheetEntry entry
        )
        {
            if (targetRoot == null || entry == null)
            {
                return null;
            }

            Transform targetTransform = FindTransformByRelativePath(
                targetRoot.transform,
                entry.componentPath
            );

            if (targetTransform == null)
            {
                return null;
            }

            Type targetType = Type.GetType(entry.componentAssemblyQualifiedTypeName);

            MonoBehaviour[] components = targetTransform.GetComponents<MonoBehaviour>();
            int currentIndex = 0;

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];

                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();

                bool typeMatches = targetType != null
                    ? componentType == targetType
                    : componentType.FullName == entry.componentTypeName;

                if (!typeMatches)
                {
                    continue;
                }

                if (currentIndex == entry.componentIndex)
                {
                    return component;
                }

                currentIndex++;
            }

            return null;
        }

        private static int GetComponentIndex(MonoBehaviour component)
        {
            MonoBehaviour[] components = component.transform.GetComponents<MonoBehaviour>();
            int index = 0;

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour current = components[i];

                if (current == null)
                {
                    continue;
                }

                if (current == component)
                {
                    return index;
                }

                if (current.GetType() == component.GetType())
                {
                    index++;
                }
            }

            return 0;
        }

        private static string GetRelativeTransformPath(Transform root, Transform target)
        {
            if (root == target)
            {
                return ".";
            }

            List<string> parts = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Transform FindTransformByRelativePath(Transform root, string path)
        {
            if (root == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(path) || path == ".")
            {
                return root;
            }

            return root.Find(path);
        }

        private static void SaveTarget(GameObject targetRoot)
        {
            if (targetRoot == null)
            {
                return;
            }

            EditorUtility.SetDirty(targetRoot);

            if (PrefabUtility.IsPartOfPrefabAsset(targetRoot))
            {
                PrefabUtility.SavePrefabAsset(targetRoot);
            }
            else if (targetRoot.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetRoot.scene);
            }

            AssetDatabase.SaveAssets();
        }

        private static void MarkEntryMissing(GameplayBalanceSheetEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.isMissing = true;
            entry.currentValue = "Missing";
        }
    }
}