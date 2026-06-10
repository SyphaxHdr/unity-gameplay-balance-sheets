using System.Collections.Generic;
using System.Globalization;
using GameplayBalanceSheets;
using UnityEditor;
using UnityEngine;

namespace GameplayBalanceSheets.Editor
{
    public sealed class GameplayBalanceSheetsWindow : EditorWindow
    {
        private List<GameplayBalanceSheetProfile> sheets = new List<GameplayBalanceSheetProfile>();
        private GameplayBalanceSheetProfile selectedSheet;

        private List<ScannedBalanceProperty> scannedProperties = new List<ScannedBalanceProperty>();

        private Vector2 sidebarScroll;
        private Vector2 contentScroll;
        private Vector2 scanScroll;

        private string sheetSearchText = string.Empty;
        private string scanSearchText = string.Empty;

        private int selectedSectionIndex;
        private string newSectionName = "New Section";
        private bool showSetupPanel = true;

        [MenuItem("Tools/Gameplay Balance Sheets")]
        public static void Open()
        {
            GameplayBalanceSheetsWindow window = GetWindow<GameplayBalanceSheetsWindow>();
            window.titleContent = new GUIContent("Balance Sheets");
            window.minSize = new Vector2(1100f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSheets();
        }

        private void OnGUI()
        {
            DrawTopBar();

            EditorGUILayout.BeginHorizontal();

            DrawSidebar();
            DrawMainContent();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Gameplay Balance Sheets", EditorStyles.boldLabel, GUILayout.Width(220f));

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                RefreshSheets();
            }

            if (GUILayout.Button("Create Sheet", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                CreateSheet();
            }

            GUILayout.FlexibleSpace();

            sheetSearchText = GUILayout.TextField(
                sheetSearchText,
                GUI.skin.FindStyle("ToolbarSearchTextField"),
                GUILayout.Width(230f)
            );

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            {
                sheetSearchText = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Sheets", EditorStyles.boldLabel);

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll, "box");

            for (int i = 0; i < sheets.Count; i++)
            {
                GameplayBalanceSheetProfile sheet = sheets[i];

                if (sheet == null)
                {
                    continue;
                }

                if (!MatchesSheetSearch(sheet))
                {
                    continue;
                }

                Color previousColor = GUI.backgroundColor;

                if (sheet == selectedSheet)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                }

                string label = string.IsNullOrWhiteSpace(sheet.sheetTitle)
                    ? sheet.name
                    : sheet.sheetTitle;

                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(26f)))
                {
                    selectedSheet = sheet;
                    selectedSectionIndex = 0;
                    scannedProperties.Clear();
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = previousColor;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawMainContent()
        {
            EditorGUILayout.BeginVertical();

            if (selectedSheet == null)
            {
                DrawEmptyState();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSheetHeader();
            DrawSheetActions();

            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            DrawSectionsAndEntries();

            EditorGUILayout.Space(10f);
            DrawSetupPanel();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(12f);

            EditorGUILayout.HelpBox(
                "Select an existing balance sheet or create a new one.",
                MessageType.Info
            );
        }

        private void DrawSheetHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUI.BeginChangeCheck();

            selectedSheet.sheetTitle = EditorGUILayout.TextField("Title", selectedSheet.sheetTitle);
            selectedSheet.targetRoot = (GameObject)EditorGUILayout.ObjectField(
                "Target Root",
                selectedSheet.targetRoot,
                typeof(GameObject),
                true
            );

            EditorGUILayout.LabelField("Description");
            selectedSheet.sheetDescription = EditorGUILayout.TextArea(
                selectedSheet.sheetDescription,
                GUILayout.MinHeight(45f)
            );

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedSheet);
            }

            if (selectedSheet.targetRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Target Root before scanning or applying values.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSheetActions()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh Current Values", GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
            }

            if (GUILayout.Button("Apply Selected", GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.ApplySelectedPendingValues(selectedSheet);
            }

            if (GUILayout.Button("Apply All Pending", GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.ApplyAllPendingValues(selectedSheet);
            }

            if (GUILayout.Button("Revert Selected", GUILayout.Height(28f)))
            {
                ConfirmAndRevertSelected();
            }

            if (GUILayout.Button("Revert All", GUILayout.Height(28f)))
            {
                ConfirmAndRevertAll();
            }

            if (GUILayout.Button("Ping Target", GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.PingTarget(selectedSheet);
            }

            if (GUILayout.Button("Save Sheet", GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.SaveSheet(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Set Current As Baseline", GUILayout.Height(24f)))
            {
                ConfirmAndSetCurrentAsBaseline();
            }

            if (GUILayout.Button("Select All Entries", GUILayout.Height(24f)))
            {
                SetAllEntriesSelected(true);
            }

            if (GUILayout.Button("Clear Entry Selection", GUILayout.Height(24f)))
            {
                SetAllEntriesSelected(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSectionsAndEntries()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Balance Sheet", EditorStyles.boldLabel);

            if (selectedSheet.sections == null)
            {
                selectedSheet.sections = new List<GameplayBalanceSheetSection>();
            }

            for (int sectionIndex = 0; sectionIndex < selectedSheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = selectedSheet.sections[sectionIndex];

                if (section == null)
                {
                    continue;
                }

                DrawSection(section, sectionIndex);
            }
        }

        private void DrawSection(GameplayBalanceSheetSection section, int sectionIndex)
        {
            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            section.expanded = EditorGUILayout.Foldout(
                section.expanded,
                section.sectionName,
                true,
                EditorStyles.foldoutHeader
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Up", GUILayout.Width(45f)) && sectionIndex > 0)
            {
                SwapSections(sectionIndex, sectionIndex - 1);
            }

            if (GUILayout.Button("Down", GUILayout.Width(55f)) &&
                sectionIndex < selectedSheet.sections.Count - 1)
            {
                SwapSections(sectionIndex, sectionIndex + 1);
            }

            if (GUILayout.Button("Remove Section", GUILayout.Width(110f)))
            {
                if (ConfirmRemoveSection(section))
                {
                    selectedSheet.sections.RemoveAt(sectionIndex);
                    EditorUtility.SetDirty(selectedSheet);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!section.expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();

            section.sectionName = EditorGUILayout.TextField("Section Name", section.sectionName);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedSheet);
            }

            if (section.entries == null)
            {
                section.entries = new List<GameplayBalanceSheetEntry>();
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry == null)
                {
                    continue;
                }

                DrawEntry(section, entry, entryIndex);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEntry(
            GameplayBalanceSheetSection section,
            GameplayBalanceSheetEntry entry,
            int entryIndex
        )
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetEntryColor(entry);

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            EditorGUILayout.BeginHorizontal();

            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(22f));
            entry.displayName = EditorGUILayout.TextField(entry.displayName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(entry.StateLabel, GUILayout.Width(75f));

            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                section.entries.RemoveAt(entryIndex);
                EditorUtility.SetDirty(selectedSheet);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Source", $"{entry.componentName} / {entry.propertyPath}");

            EditorGUILayout.LabelField("Description");
            entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight(38f));

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Baseline", GUILayout.Width(65f));
            EditorGUILayout.SelectableLabel(entry.baselineValue, GUILayout.Height(18f), GUILayout.Width(120f));

            EditorGUILayout.LabelField("Current", GUILayout.Width(58f));
            EditorGUILayout.SelectableLabel(entry.currentValue, GUILayout.Height(18f), GUILayout.Width(120f));

            EditorGUILayout.LabelField("Test", GUILayout.Width(35f));
            string newTestValue = DrawValueField(entry, entry.testValue);

            if (newTestValue != entry.testValue)
            {
                entry.testValue = newTestValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply"))
            {
                GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
            }

            if (GUILayout.Button("Revert To Baseline"))
            {
                GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
            }

            if (GUILayout.Button("Copy Current To Test"))
            {
                entry.testValue = entry.currentValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            if (GUILayout.Button("Copy Baseline To Test"))
            {
                entry.testValue = entry.baselineValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupPanel()
        {
            EditorGUILayout.Space(12f);

            showSetupPanel = EditorGUILayout.Foldout(
                showSetupPanel,
                "Developer Setup",
                true,
                EditorStyles.foldoutHeader
            );

            if (!showSetupPanel)
            {
                return;
            }

            EditorGUILayout.BeginVertical("box");

            DrawSectionCreationTools();
            DrawScanTools();

            EditorGUILayout.EndVertical();
        }

        private void DrawSectionCreationTools()
        {
            EditorGUILayout.LabelField("Sections", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            newSectionName = EditorGUILayout.TextField("New Section", newSectionName);

            if (GUILayout.Button("Add Section", GUILayout.Width(110f)))
            {
                AddSection(newSectionName);
                newSectionName = "New Section";
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawScanTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Scan Target", EditorStyles.boldLabel);

            if (selectedSheet.targetRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Target Root to scan serialized fields.",
                    MessageType.Warning
                );
                return;
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Scan Target", GUILayout.Height(28f)))
            {
                scannedProperties = GameplayBalanceSheetUtility.ScanTarget(selectedSheet.targetRoot);
            }

            if (GUILayout.Button("Select All Visible", GUILayout.Height(28f)))
            {
                SetVisibleScannedPropertiesSelected(true);
            }

            if (GUILayout.Button("Clear Scan Selection", GUILayout.Height(28f)))
            {
                SetVisibleScannedPropertiesSelected(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            scanSearchText = EditorGUILayout.TextField("Search", scanSearchText);

            string[] sectionNames = GetSectionNames();

            if (sectionNames.Length == 0)
            {
                AddSection("General");
                sectionNames = GetSectionNames();
            }

            selectedSectionIndex = Mathf.Clamp(selectedSectionIndex, 0, sectionNames.Length - 1);

            selectedSectionIndex = EditorGUILayout.Popup(
                "Target Section",
                selectedSectionIndex,
                sectionNames
            );

            if (GUILayout.Button("Add Selected To Section", GUILayout.Width(180f)))
            {
                AddSelectedScannedPropertiesToCurrentSection();
            }

            EditorGUILayout.EndHorizontal();

            scanScroll = EditorGUILayout.BeginScrollView(scanScroll, GUILayout.MinHeight(160f));

            DrawScannedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawScannedProperties()
        {
            if (scannedProperties == null || scannedProperties.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No scan result yet. Click Scan Target.",
                    MessageType.Info
                );
                return;
            }

            string currentGroup = null;

            for (int i = 0; i < scannedProperties.Count; i++)
            {
                ScannedBalanceProperty property = scannedProperties[i];

                if (property == null)
                {
                    continue;
                }

                if (!MatchesScanSearch(property))
                {
                    continue;
                }

                if (selectedSheet.ContainsEntry(property.id))
                {
                    continue;
                }

                string group = $"{property.componentPath} / {property.componentName}";

                if (group != currentGroup)
                {
                    currentGroup = group;
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }

                EditorGUILayout.BeginHorizontal();

                property.selected = EditorGUILayout.Toggle(property.selected, GUILayout.Width(22f));
                EditorGUILayout.LabelField(property.displayName, GUILayout.Width(220f));
                EditorGUILayout.LabelField(property.propertyPath, GUILayout.Width(260f));
                EditorGUILayout.LabelField(property.currentValue);

                EditorGUILayout.EndHorizontal();
            }
        }

        private string DrawValueField(GameplayBalanceSheetEntry entry, string value)
        {
            if (entry.propertyTypeName == "Boolean")
            {
                bool boolValue = string.Equals(
                    value,
                    "true",
                    System.StringComparison.OrdinalIgnoreCase
                );

                bool newBoolValue = EditorGUILayout.Toggle(boolValue);
                return newBoolValue ? "true" : "false";
            }

            if (entry.propertyTypeName == "Integer")
            {
                int intValue = 0;
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue);

                int newIntValue = EditorGUILayout.IntField(intValue);
                return newIntValue.ToString(CultureInfo.InvariantCulture);
            }

            if (entry.propertyTypeName == "Float")
            {
                float floatValue = 0f;
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue);

                float newFloatValue = EditorGUILayout.FloatField(floatValue);
                return newFloatValue.ToString("R", CultureInfo.InvariantCulture);
            }

            if (entry.propertyTypeName == "Enum" &&
                entry.enumNames != null &&
                entry.enumNames.Count > 0)
            {
                int currentIndex = entry.enumNames.IndexOf(value);

                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                int newIndex = EditorGUILayout.Popup(currentIndex, entry.enumNames.ToArray());
                return entry.enumNames[newIndex];
            }

            return EditorGUILayout.TextField(value);
        }

        private void RefreshSheets()
        {
            sheets = GameplayBalanceSheetUtility.LoadAllSheets();

            if (selectedSheet == null && sheets.Count > 0)
            {
                selectedSheet = sheets[0];
            }

            if (selectedSheet != null && !sheets.Contains(selectedSheet))
            {
                selectedSheet = sheets.Count > 0 ? sheets[0] : null;
            }
        }

        private void CreateSheet()
        {
            GameplayBalanceSheetProfile sheet = GameplayBalanceSheetUtility.CreateSheet();

            RefreshSheets();

            if (sheet != null)
            {
                selectedSheet = sheet;
            }
        }

        private void AddSection(string sectionName)
        {
            if (selectedSheet == null)
            {
                return;
            }

            selectedSheet.GetOrCreateSection(sectionName);
            EditorUtility.SetDirty(selectedSheet);
            AssetDatabase.SaveAssets();
        }

        private void AddSelectedScannedPropertiesToCurrentSection()
        {
            if (selectedSheet == null ||
                selectedSheet.sections == null ||
                selectedSheet.sections.Count == 0)
            {
                return;
            }

            selectedSectionIndex = Mathf.Clamp(
                selectedSectionIndex,
                0,
                selectedSheet.sections.Count - 1
            );

            GameplayBalanceSheetSection targetSection =
                selectedSheet.sections[selectedSectionIndex];

            int addedCount = GameplayBalanceSheetUtility.AddSelectedScannedPropertiesToSection(
                selectedSheet,
                targetSection,
                scannedProperties
            );

            Debug.Log($"Gameplay Balance Sheets: added {addedCount} field(s) to section '{targetSection.sectionName}'.");
        }

        private void SwapSections(int firstIndex, int secondIndex)
        {
            GameplayBalanceSheetSection temp = selectedSheet.sections[firstIndex];
            selectedSheet.sections[firstIndex] = selectedSheet.sections[secondIndex];
            selectedSheet.sections[secondIndex] = temp;

            EditorUtility.SetDirty(selectedSheet);
        }

        private bool ConfirmRemoveSection(GameplayBalanceSheetSection section)
        {
            return EditorUtility.DisplayDialog(
                "Remove Section",
                $"Remove section '{section.sectionName}' and all its entries?",
                "Remove",
                "Cancel"
            );
        }

        private void ConfirmAndRevertSelected()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Revert Selected",
                "This will revert selected entries to their baseline values.",
                "Revert",
                "Cancel"
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.RevertSelectedToBaseline(selectedSheet);
        }

        private void ConfirmAndRevertAll()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Revert All",
                "This will revert all entries to their baseline values.",
                "Revert All",
                "Cancel"
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.RevertAllToBaseline(selectedSheet);
        }

        private void ConfirmAndSetCurrentAsBaseline()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Set Current As Baseline",
                "This will replace the official baseline of every entry with the current values from the target. Continue only if these values are validated.",
                "Set Baseline",
                "Cancel"
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.SetCurrentAsBaseline(selectedSheet);
        }

        private void SetAllEntriesSelected(bool selected)
        {
            if (selectedSheet == null)
            {
                return;
            }

            for (int sectionIndex = 0; sectionIndex < selectedSheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = selectedSheet.sections[sectionIndex];

                if (section == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
                {
                    GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                    if (entry != null)
                    {
                        entry.selected = selected;
                    }
                }
            }

            EditorUtility.SetDirty(selectedSheet);
        }

        private void SetVisibleScannedPropertiesSelected(bool selected)
        {
            if (scannedProperties == null)
            {
                return;
            }

            for (int i = 0; i < scannedProperties.Count; i++)
            {
                ScannedBalanceProperty property = scannedProperties[i];

                if (property == null)
                {
                    continue;
                }

                if (!MatchesScanSearch(property))
                {
                    continue;
                }

                if (selectedSheet != null && selectedSheet.ContainsEntry(property.id))
                {
                    continue;
                }

                property.selected = selected;
            }
        }

        private string[] GetSectionNames()
        {
            if (selectedSheet == null || selectedSheet.sections == null)
            {
                return new string[0];
            }

            List<string> names = new List<string>();

            for (int i = 0; i < selectedSheet.sections.Count; i++)
            {
                GameplayBalanceSheetSection section = selectedSheet.sections[i];

                if (section == null)
                {
                    continue;
                }

                names.Add(string.IsNullOrWhiteSpace(section.sectionName)
                    ? $"Section {i + 1}"
                    : section.sectionName
                );
            }

            return names.ToArray();
        }

        private bool MatchesSheetSearch(GameplayBalanceSheetProfile sheet)
        {
            if (string.IsNullOrWhiteSpace(sheetSearchText))
            {
                return true;
            }

            string search = sheetSearchText.ToLowerInvariant();

            return SafeLower(sheet.sheetTitle).Contains(search) ||
                   SafeLower(sheet.sheetDescription).Contains(search) ||
                   SafeLower(sheet.name).Contains(search);
        }

        private bool MatchesScanSearch(ScannedBalanceProperty property)
        {
            if (string.IsNullOrWhiteSpace(scanSearchText))
            {
                return true;
            }

            string search = scanSearchText.ToLowerInvariant();

            return SafeLower(property.componentName).Contains(search) ||
                   SafeLower(property.propertyPath).Contains(search) ||
                   SafeLower(property.displayName).Contains(search) ||
                   SafeLower(property.currentValue).Contains(search);
        }

        private Color GetEntryColor(GameplayBalanceSheetEntry entry)
        {
            if (entry.isMissing)
            {
                return new Color(1f, 0.55f, 0.55f);
            }

            if (entry.HasPendingTestValue)
            {
                return new Color(0.55f, 0.75f, 1f);
            }

            if (entry.HasChangedFromBaseline)
            {
                return new Color(1f, 0.75f, 0.35f);
            }

            return Color.white;
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }
    }
}