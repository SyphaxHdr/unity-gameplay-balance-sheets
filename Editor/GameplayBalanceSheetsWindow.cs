using System;
using System.Collections.Generic;
using System.Globalization;
using GameplayBalanceSheets;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameplayBalanceSheets.Editor
{
    public sealed class GameplayBalanceSheetsWindow : EditorWindow
    {
        private const string LanguagePrefKey = "GameplayBalanceSheets.Language";
        private const string SheetsFolderPrefKey = "GameplayBalanceSheets.DefaultSheetsFolder";
        private const string ShowTechnicalInfoPrefKey = "GameplayBalanceSheets.ShowTechnicalInfo";
        private const string ShowHelpBoxesPrefKey = "GameplayBalanceSheets.ShowHelpBoxes";
        private const string ShowEntryDescriptionsPrefKey = "GameplayBalanceSheets.ShowEntryDescriptions";
        private const string ShowDetailedSidebarPrefKey = "GameplayBalanceSheets.ShowDetailedSidebar";

        private const float SidebarWidth = 360f;
        private const float DevSheetConfigHeight = 330f;

        private enum MainPage
        {
            Designer,
            Developer,
            Tutorial,
            Settings
        }

        private enum PluginLanguage
        {
            English,
            French
        }

        private MainPage currentPage = MainPage.Designer;
        private PluginLanguage language = PluginLanguage.English;

        private List<GameplayBalanceSheetProfile> sheets = new List<GameplayBalanceSheetProfile>();
        private GameplayBalanceSheetProfile selectedSheet;

        private List<ScannedBalanceProperty> scannedProperties = new List<ScannedBalanceProperty>();

        private Vector2 sidebarScroll;
        private Vector2 contentScroll;
        private Vector2 scanScroll;
        private Vector2 tutorialScroll;
        private Vector2 settingsScroll;

        private string sheetSearchText = string.Empty;
        private string scanSearchText = string.Empty;

        private int selectedSectionIndex;
        private string newSectionName = "New Section";
        private string defaultSheetsFolder = GameplayBalanceSheetUtility.DefaultSheetsFolder;

        private bool showTechnicalInfo;
        private bool showHelpBoxes = true;
        private bool showEntryDescriptions = true;
        private bool showDetailedSidebar = true;

        private SerializedObject sectionSerializedObject;
        private ReorderableList sectionReorderableList;
        private GameplayBalanceSheetProfile sectionListTarget;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle tutorialTitleStyle;
        private GUIStyle tutorialBodyStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle compactLabelStyle;
        private GUIStyle compactBoldLabelStyle;
        private GUIStyle descriptionStyle;

        [MenuItem("Tools/Gameplay Balance Sheets")]
        public static void Open()
        {
            GameplayBalanceSheetsWindow window = GetWindow<GameplayBalanceSheetsWindow>();
            window.titleContent = new GUIContent("Balance Sheets");
            window.minSize = new Vector2(1280f, 760f);
            window.Show();
        }

        private void OnEnable()
        {
            language = (PluginLanguage)EditorPrefs.GetInt(
                LanguagePrefKey,
                (int)PluginLanguage.English
            );

            defaultSheetsFolder = EditorPrefs.GetString(
                SheetsFolderPrefKey,
                GameplayBalanceSheetUtility.DefaultSheetsFolder
            );

            showTechnicalInfo = EditorPrefs.GetBool(ShowTechnicalInfoPrefKey, false);
            showHelpBoxes = EditorPrefs.GetBool(ShowHelpBoxesPrefKey, true);
            showEntryDescriptions = EditorPrefs.GetBool(ShowEntryDescriptionsPrefKey, true);
            showDetailedSidebar = EditorPrefs.GetBool(ShowDetailedSidebarPrefKey, true);

            RefreshSheets();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawTopBar();

            if (currentPage == MainPage.Tutorial)
            {
                DrawTutorialPage();
                return;
            }

            if (currentPage == MainPage.Settings)
            {
                DrawSettingsPage();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            if (currentPage == MainPage.Developer)
            {
                DrawDeveloperSidebar();
            }
            else
            {
                DrawDesignerSidebar();
            }

            EditorGUILayout.BeginVertical();

            if (selectedSheet == null)
            {
                DrawEmptyState();
            }
            else if (currentPage == MainPage.Designer)
            {
                DrawDesignerPage();
            }
            else
            {
                DrawDeveloperPage();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                wordWrap = true
            };

            subtitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                wordWrap = true
            };

            tutorialTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                wordWrap = true
            };

            tutorialBodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = true
            };

            cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                wordWrap = true
            };

            compactLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = false
            };

            compactBoldLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                wordWrap = false
            };

            descriptionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true
            };
        }

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Gameplay Balance Sheets", EditorStyles.boldLabel, GUILayout.Width(230f));

            string[] tabs =
            {
                T("Designer", "Équilibrage GD"),
                T("Developer", "Configuration Dev"),
                T("Tutorial", "Tutoriel"),
                T("Settings", "Paramètres")
            };

            currentPage = (MainPage)GUILayout.Toolbar(
                (int)currentPage,
                tabs,
                EditorStyles.toolbarButton,
                GUILayout.Width(560f)
            );

            GUILayout.FlexibleSpace();

            language = (PluginLanguage)EditorGUILayout.EnumPopup(
                language,
                EditorStyles.toolbarPopup,
                GUILayout.Width(95f)
            );

            if (GUILayout.Button(T("Refresh", "Actualiser"), EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                RefreshSheets();
            }

            EditorGUILayout.EndHorizontal();

            EditorPrefs.SetInt(LanguagePrefKey, (int)language);
        }

        // --------------------------------------------------------------------
        // Sidebars
        // --------------------------------------------------------------------

        private void DrawDesignerSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));

            DrawSheetListPanel(
                T("Balance Sheets", "Fiches d’équilibrage"),
                false
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));

            DrawSheetListPanel(
                T("Sheet Instances", "Instances de fiches"),
                true
            );

            DrawDeveloperSheetConfigurationPanel();

            EditorGUILayout.EndVertical();
        }

        private void DrawSheetListPanel(string title, bool developerMode)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField(title, subtitleStyle);

            EditorGUILayout.BeginHorizontal();

            sheetSearchText = EditorGUILayout.TextField(sheetSearchText, GUILayout.Height(24f));

            if (GUILayout.Button(T("Clear", "Effacer"), GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                sheetSearchText = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

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

                DrawSheetCard(sheet, developerMode);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawSheetCard(GameplayBalanceSheetProfile sheet, bool developerMode)
        {
            SheetStats stats = GetSheetStats(sheet);

            Color previousColor = GUI.backgroundColor;

            if (sheet == selectedSheet)
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            }

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            string title = string.IsNullOrWhiteSpace(sheet.sheetTitle)
                ? sheet.name
                : sheet.sheetTitle;

            if (GUILayout.Button(title, cardTitleStyle, GUILayout.Height(26f)))
            {
                selectedSheet = sheet;
                selectedSectionIndex = 0;
                scannedProperties.Clear();
                ResetSectionReorderList();
                GUI.FocusControl(null);
            }

            string targetName = sheet.targetRoot != null
                ? sheet.targetRoot.name
                : T("No target", "Aucune cible");

            DrawInlineInfo(
                T("Target", "Cible"),
                targetName,
                sheet.targetRoot != null ? new Color(0.3f, 0.85f, 0.45f) : new Color(1f, 0.45f, 0.35f)
            );

            if (showDetailedSidebar)
            {
                EditorGUILayout.BeginHorizontal();

                DrawSmallStat(T("Pending", "Test"), stats.pending, new Color(0.35f, 0.65f, 1f));
                DrawSmallStat(T("Changed", "Modifié"), stats.changed, new Color(1f, 0.65f, 0.25f));
                DrawSmallStat(T("Missing", "Introuvable"), stats.missing, new Color(1f, 0.35f, 0.35f));

                EditorGUILayout.EndHorizontal();
            }

            if (developerMode && showTechnicalInfo)
            {
                EditorGUILayout.LabelField(
                    AssetDatabase.GetAssetPath(sheet),
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(3f);
        }

        private void DrawDeveloperSheetConfigurationPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(DevSheetConfigHeight));

            EditorGUILayout.LabelField(
                T("Selected Sheet Configuration", "Configuration de la fiche sélectionnée"),
                subtitleStyle
            );

            DrawInfoBox(
                T(
                    "Create or delete balance sheets here. Select a sheet above before editing or deleting it.",
                    "Crée ou supprime les fiches ici. Sélectionne une fiche au-dessus avant de la modifier ou de la supprimer."
                ),
                MessageType.Info
            );

            EditorGUILayout.BeginHorizontal();

            if (DrawColoredButton(
                    T("Create Sheet", "Créer fiche"),
                    new Color(0.35f, 0.7f, 1f),
                    GUILayout.Height(28f)))
            {
                CreateSheet();
            }

            using (new EditorGUI.DisabledScope(selectedSheet == null))
            {
                if (DrawColoredButton(
                        T("Delete Sheet", "Supprimer fiche"),
                        new Color(1f, 0.45f, 0.35f),
                        GUILayout.Height(28f)))
                {
                    ConfirmAndDeleteSheet();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);

            if (selectedSheet == null)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "No sheet selected.",
                        "Aucune fiche sélectionnée."
                    ),
                    MessageType.Warning
                );

                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();

            selectedSheet.sheetTitle = EditorGUILayout.TextField(
                T("Title", "Titre"),
                selectedSheet.sheetTitle
            );

            selectedSheet.targetRoot = (GameObject)EditorGUILayout.ObjectField(
                T("Target Root", "Cible"),
                selectedSheet.targetRoot,
                typeof(GameObject),
                true
            );

            EditorGUILayout.LabelField(T("Description", "Description"));
            selectedSheet.sheetDescription = EditorGUILayout.TextArea(
                selectedSheet.sheetDescription,
                GUILayout.Height(55f)
            );

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.Space(4f);

            if (DrawColoredButton(
                    T("Save Sheet", "Sauvegarder fiche"),
                    new Color(0.75f, 0.75f, 0.75f),
                    GUILayout.Height(28f)))
            {
                GameplayBalanceSheetUtility.SaveSheet(selectedSheet);
            }

            EditorGUILayout.EndVertical();
        }

        // --------------------------------------------------------------------
        // Designer Page
        // --------------------------------------------------------------------

        private void DrawDesignerPage()
        {
            DrawDesignerHeader();

            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            DrawStatusLegend();
            DrawDesignerSections();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDesignerHeader()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(selectedSheet.sheetTitle, titleStyle);

            if (selectedSheet.targetRoot != null)
            {
                DrawColoredLabel(
                    T("Target", "Cible") + ": " + selectedSheet.targetRoot.name,
                    new Color(0.3f, 0.85f, 0.45f),
                    true
                );
            }
            else
            {
                DrawColoredLabel(
                    T("No target assigned.", "Aucune cible assignée."),
                    new Color(1f, 0.35f, 0.35f),
                    true
                );
            }

            if (!string.IsNullOrWhiteSpace(selectedSheet.sheetDescription))
            {
                EditorGUILayout.LabelField(selectedSheet.sheetDescription, descriptionStyle);
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(520f));

            EditorGUILayout.BeginHorizontal();

            if (DrawColoredButton(
                    T("Refresh Values", "Actualiser valeurs"),
                    new Color(0.55f, 0.75f, 1f),
                    GUILayout.Height(30f)))
            {
                GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
            }

            if (DrawColoredButton(
                    T("Apply All Tests", "Appliquer tous les tests"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Height(30f)))
            {
                GameplayBalanceSheetUtility.ApplyAllPendingValues(selectedSheet);
            }

            if (DrawColoredButton(
                    T("Revert All", "Tout réinitialiser"),
                    new Color(1f, 0.65f, 0.25f),
                    GUILayout.Height(30f)))
            {
                ConfirmAndRevertAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(T("Select All", "Tout sélectionner"), GUILayout.Height(25f)))
            {
                SetAllEntriesSelected(true);
            }

            if (GUILayout.Button(T("Clear Selection", "Vider sélection"), GUILayout.Height(25f)))
            {
                SetAllEntriesSelected(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusLegend()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(
                T("Status meaning", "Signification des états"),
                subtitleStyle
            );

            EditorGUILayout.BeginHorizontal();

            DrawStatusExplanation(
                "OK",
                T("Current equals baseline.", "Current est identique à la baseline."),
                new Color(0.3f, 0.85f, 0.45f)
            );

            DrawStatusExplanation(
                T("Pending", "Test en attente"),
                T("Test differs from Current and is ready to apply.", "Test est différent de Current et peut être appliqué."),
                new Color(0.35f, 0.65f, 1f)
            );

            DrawStatusExplanation(
                T("Changed", "Modifié"),
                T("Current differs from baseline.", "Current est différent de la baseline."),
                new Color(1f, 0.65f, 0.25f)
            );

            DrawStatusExplanation(
                T("Missing", "Introuvable"),
                T("The field or target cannot be found.", "Le champ ou la cible est introuvable."),
                new Color(1f, 0.35f, 0.35f)
            );

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDesignerSections()
        {
            EnsureSections();

            for (int sectionIndex = 0; sectionIndex < selectedSheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = selectedSheet.sections[sectionIndex];

                if (section == null)
                {
                    continue;
                }

                DrawDesignerSection(section);
            }
        }

        private void DrawDesignerSection(GameplayBalanceSheetSection section)
        {
            EditorGUILayout.Space(10f);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            section.expanded = EditorGUILayout.Foldout(
                section.expanded,
                section.sectionName,
                true,
                EditorStyles.foldoutHeader
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(T("Select Section", "Sélectionner section"), GUILayout.Width(130f)))
            {
                SetSectionEntriesSelected(section, true);
            }

            if (GUILayout.Button(T("Clear Section", "Vider section"), GUILayout.Width(110f)))
            {
                SetSectionEntriesSelected(section, false);
            }

            if (DrawColoredButton(
                    T("Apply Selected", "Appliquer sélection"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Width(145f)))
            {
                ApplySelectedInSection(section);
            }

            if (DrawColoredButton(
                    T("Revert Selected", "Réinitialiser sélection"),
                    new Color(1f, 0.65f, 0.25f),
                    GUILayout.Width(160f)))
            {
                RevertSelectedInSection(section);
            }

            EditorGUILayout.EndHorizontal();

            if (!section.expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (section.entries == null || section.entries.Count == 0)
            {
                DrawInfoBox(
                    T("No parameter in this section.", "Aucun paramètre dans cette section."),
                    MessageType.Info
                );

                EditorGUILayout.EndVertical();
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry == null)
                {
                    continue;
                }

                DrawDesignerEntryCompact(entry);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDesignerEntryCompact(GameplayBalanceSheetEntry entry)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetEntryPanelColor(entry);

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            EditorGUILayout.BeginHorizontal();

            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(22f));

            EditorGUILayout.LabelField(entry.displayName, compactBoldLabelStyle, GUILayout.MinWidth(180f));

            DrawValueCell("Baseline", entry.baselineValue, 140f);
            DrawValueCell("Current", entry.currentValue, 140f);

            EditorGUILayout.LabelField("Test", compactBoldLabelStyle, GUILayout.Width(35f));
            string newTestValue = DrawValueField(entry, entry.testValue, 120f);

            if (newTestValue != entry.testValue)
            {
                entry.testValue = newTestValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            DrawStateBadge(entry, 135f);

            EditorGUILayout.EndHorizontal();

            if (showEntryDescriptions && !string.IsNullOrWhiteSpace(entry.description))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(entry.description, descriptionStyle);
            }

            if (showTechnicalInfo)
            {
                EditorGUILayout.LabelField(
                    "Source: " + entry.componentName + " / " + entry.propertyPath,
                    EditorStyles.miniLabel
                );
            }

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(T("Apply", "Appliquer"), GUILayout.Width(95f)))
            {
                GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Revert Baseline", "Revenir baseline"), GUILayout.Width(130f)))
            {
                GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
            }

            if (GUILayout.Button("Current → Test", GUILayout.Width(120f)))
            {
                entry.testValue = entry.currentValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            if (GUILayout.Button("Baseline → Test", GUILayout.Width(125f)))
            {
                entry.testValue = entry.baselineValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // --------------------------------------------------------------------
        // Developer Page
        // --------------------------------------------------------------------

        private void DrawDeveloperPage()
        {
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            DrawDeveloperSectionTools();
            DrawDeveloperScanTools();
            DrawDeveloperEntries();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDeveloperSectionTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Sections", "Sections"), subtitleStyle);

            DrawInfoBox(
                T(
                    "Create project-specific sections. Drag them to change their order.",
                    "Crée des sections propres au projet. Déplace-les à la souris pour changer leur ordre."
                ),
                MessageType.Info
            );

            EnsureSectionReorderList();

            if (sectionReorderableList != null)
            {
                sectionSerializedObject.Update();
                sectionReorderableList.DoLayoutList();
                sectionSerializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();

            newSectionName = EditorGUILayout.TextField(
                T("New Section", "Nouvelle section"),
                newSectionName,
                GUILayout.Height(26f)
            );

            if (DrawColoredButton(
                    T("Add Section", "Ajouter section"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Width(145f),
                    GUILayout.Height(26f)))
            {
                AddSection(newSectionName);
                newSectionName = "New Section";
                ResetSectionReorderList();
            }

            if (DrawColoredButton(
                    T("Remove Selected Section", "Supprimer section"),
                    new Color(1f, 0.45f, 0.35f),
                    GUILayout.Width(190f),
                    GUILayout.Height(26f)))
            {
                RemoveSelectedSection();
                ResetSectionReorderList();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperScanTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Scan Target", "Scanner la cible"), subtitleStyle);

            if (selectedSheet.targetRoot == null)
            {
                DrawInfoBox(
                    T(
                        "Assign a Target Root in the left configuration panel before scanning.",
                        "Assigne une cible dans le panneau de configuration à gauche avant de scanner."
                    ),
                    MessageType.Warning
                );

                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            if (DrawColoredButton(
                    T("Scan Target", "Scanner la cible"),
                    new Color(0.35f, 0.65f, 1f),
                    GUILayout.Width(170f),
                    GUILayout.Height(34f)))
            {
                ScanTarget();
            }

            if (GUILayout.Button(T("Select All Visible", "Sélectionner visibles"), GUILayout.Width(170f), GUILayout.Height(34f)))
            {
                SetVisibleScannedPropertiesSelected(true);
            }

            if (GUILayout.Button(T("Clear Scan Selection", "Vider sélection scan"), GUILayout.Width(170f), GUILayout.Height(34f)))
            {
                SetVisibleScannedPropertiesSelected(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(T("Search", "Recherche"), GUILayout.Width(80f));
            scanSearchText = EditorGUILayout.TextField(scanSearchText, GUILayout.Height(30f), GUILayout.MinWidth(340f));

            string[] sectionNames = GetSectionNames();

            if (sectionNames.Length == 0)
            {
                AddSection("General");
                sectionNames = GetSectionNames();
            }

            selectedSectionIndex = Mathf.Clamp(selectedSectionIndex, 0, sectionNames.Length - 1);

            EditorGUILayout.LabelField(T("Target Section", "Section cible"), GUILayout.Width(105f));

            selectedSectionIndex = EditorGUILayout.Popup(
                selectedSectionIndex,
                sectionNames,
                GUILayout.Height(30f),
                GUILayout.Width(230f)
            );

            if (DrawColoredButton(
                    T("Add Selected To Section", "Ajouter sélection à la section"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Width(230f),
                    GUILayout.Height(30f)))
            {
                AddSelectedScannedPropertiesToCurrentSection();
            }

            EditorGUILayout.EndHorizontal();

            scanScroll = EditorGUILayout.BeginScrollView(scanScroll, GUILayout.MinHeight(210f));

            DrawScannedProperties();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawScannedProperties()
        {
            if (scannedProperties == null || scannedProperties.Count == 0)
            {
                DrawInfoBox(
                    T(
                        "No scan result yet. Click Scan Target.",
                        "Aucun résultat de scan. Clique sur Scanner la cible."
                    ),
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
                    EditorGUILayout.Space(8f);
                    DrawColoredLabel(group, new Color(0.35f, 0.65f, 1f), true);
                }

                EditorGUILayout.BeginHorizontal("box");

                property.selected = EditorGUILayout.Toggle(property.selected, GUILayout.Width(22f));
                EditorGUILayout.LabelField(property.displayName, GUILayout.Width(250f));
                EditorGUILayout.LabelField(property.propertyPath, GUILayout.Width(360f));
                EditorGUILayout.LabelField(property.propertyTypeName, GUILayout.Width(80f));
                EditorGUILayout.SelectableLabel(property.currentValue, GUILayout.Height(18f));

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDeveloperEntries()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Entries Already Added", "Paramètres déjà ajoutés"), subtitleStyle);

            EnsureSections();

            for (int sectionIndex = 0; sectionIndex < selectedSheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = selectedSheet.sections[sectionIndex];

                if (section == null)
                {
                    continue;
                }

                DrawDeveloperSectionEntries(section);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperSectionEntries(GameplayBalanceSheetSection section)
        {
            EditorGUILayout.Space(6f);

            section.expanded = EditorGUILayout.Foldout(
                section.expanded,
                section.sectionName,
                true,
                EditorStyles.foldoutHeader
            );

            if (!section.expanded)
            {
                return;
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

                DrawDeveloperEntry(section, entry, entryIndex);
            }
        }

        private void DrawDeveloperEntry(
            GameplayBalanceSheetSection section,
            GameplayBalanceSheetEntry entry,
            int entryIndex
        )
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetEntryPanelColor(entry);

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            EditorGUILayout.BeginHorizontal();

            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(22f));
            entry.displayName = EditorGUILayout.TextField(entry.displayName, compactBoldLabelStyle, GUILayout.MinWidth(180f));

            DrawStateBadge(entry, 130f);

            if (DrawColoredButton(
                    T("Remove", "Supprimer"),
                    new Color(1f, 0.45f, 0.35f),
                    GUILayout.Width(95f)))
            {
                section.entries.RemoveAt(entryIndex);
                EditorUtility.SetDirty(selectedSheet);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Source: " + entry.componentName + " / " + entry.propertyPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Type: " + entry.propertyTypeName, EditorStyles.miniLabel);

            EditorGUILayout.LabelField(T("Description", "Description"));
            entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight(38f));

            DrawEntryValueRow(entry);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(T("Apply Test", "Appliquer Test"), GUILayout.Width(120f)))
            {
                GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Revert To Baseline", "Revenir à la baseline"), GUILayout.Width(160f)))
            {
                GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
            }

            if (DrawColoredButton(
                    T("Current As Baseline", "Current devient baseline"),
                    new Color(1f, 0.65f, 0.25f),
                    GUILayout.Width(185f)))
            {
                entry.baselineValue = entry.currentValue;
                entry.testValue = entry.currentValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // --------------------------------------------------------------------
        // Tutorial Page
        // --------------------------------------------------------------------

        private void DrawTutorialPage()
        {
            tutorialScroll = EditorGUILayout.BeginScrollView(tutorialScroll);

            EditorGUILayout.Space(14f);

            EditorGUILayout.LabelField(
                T("Gameplay Balance Sheets Tutorial", "Tutoriel Gameplay Balance Sheets"),
                tutorialTitleStyle
            );

            DrawLargeTutorialParagraph(
                T(
                    "Gameplay Balance Sheets is a generic Unity editor tool. It lets developers expose only the gameplay parameters that are useful for balancing, then lets game designers tune these values from a clean interface without browsing technical components.",
                    "Gameplay Balance Sheets est un outil Unity Editor générique. Il permet aux développeurs d’exposer uniquement les paramètres gameplay utiles à l’équilibrage, puis aux game designers de les modifier depuis une interface propre sans parcourir les composants techniques."
                )
            );

            DrawTutorialSectionTitle(T("What the tool is for", "À quoi sert l’outil"));

            DrawLargeTutorialParagraph(
                T(
                    "The tool creates balance sheets. A balance sheet is a readable list of selected values from a target prefab or scene object. The tool does not require gameplay scripts to use special tuning assets. It works with existing serialized fields.",
                    "L’outil crée des fiches d’équilibrage. Une fiche est une liste lisible de valeurs sélectionnées depuis un prefab ou un objet de scène cible. L’outil n’oblige pas les scripts gameplay à utiliser des ScriptableObjects de tuning spécifiques. Il fonctionne avec les champs sérialisés existants."
                )
            );

            DrawTutorialSectionTitle(T("Designer Tutorial", "Tutoriel GD"));

            DrawTutorialBlock(
                T("1. Select a balance sheet", "1. Sélectionner une fiche"),
                T(
                    "On the Designer page, select the balance sheet in the left panel. Each sheet is linked to a target object such as a player, enemy, boss, camera or UI controller.",
                    "Dans la page Équilibrage GD, sélectionne une fiche dans le panneau de gauche. Chaque fiche est liée à une cible comme un joueur, un ennemi, un boss, une caméra ou un contrôleur UI."
                )
            );

            DrawTutorialBlock(
                T("2. Understand Baseline", "2. Comprendre Baseline"),
                T(
                    "Baseline is the official validated value. It is the reference value you can safely return to. If a test becomes bad or breaks the gameplay feeling, Revert To Baseline restores that reference.",
                    "Baseline est la valeur officielle validée. C’est la valeur de référence à laquelle on peut revenir. Si un test devient mauvais ou casse le feeling gameplay, Revenir à la baseline restaure cette référence."
                )
            );

            DrawTutorialBlock(
                T("3. Understand Current", "3. Comprendre Current"),
                T(
                    "Current is the value currently written on the target component. It represents what the game object is using right now in Unity.",
                    "Current est la valeur actuellement écrite sur le composant cible. Elle représente ce que l’objet de jeu utilise réellement dans Unity à cet instant."
                )
            );

            DrawTutorialBlock(
                T("4. Understand Test", "4. Comprendre Test"),
                T(
                    "Test is the value you want to try next. Changing Test alone does not immediately modify the target. You must click Apply to write Test into Current.",
                    "Test est la valeur que tu veux essayer. Modifier Test ne modifie pas immédiatement la cible. Il faut cliquer sur Appliquer pour écrire Test dans Current."
                )
            );

            DrawTutorialBlock(
                "Current → Test",
                T(
                    "Use this when you changed a value directly in the Inspector or when the target already has a good temporary value. It copies the current target value into the Test field so you can keep editing from there.",
                    "À utiliser quand une valeur a été modifiée directement dans l’Inspector ou quand la cible possède déjà une bonne valeur temporaire. Cela copie la valeur actuelle de la cible dans Test pour continuer à travailler à partir de cette valeur."
                )
            );

            DrawTutorialBlock(
                "Baseline → Test",
                T(
                    "Use this when you want to prepare a return to the official reference without applying it immediately. It copies the baseline into Test, then you can Apply when ready.",
                    "À utiliser quand tu veux préparer un retour à la valeur officielle sans l’appliquer immédiatement. Cela copie la baseline dans Test, puis tu peux appliquer quand tu es prêt."
                )
            );

            DrawTutorialBlock(
                T("Apply and Revert", "Appliquer et revenir"),
                T(
                    "Apply writes Test into the target component. Revert To Baseline puts the baseline value into Test and applies it. Section buttons only affect selected entries in that section.",
                    "Appliquer écrit Test dans le composant cible. Revenir à la baseline met la baseline dans Test puis l’applique. Les boutons de section n’affectent que les paramètres sélectionnés dans cette section."
                )
            );

            DrawTutorialSectionTitle(T("Developer Tutorial", "Tutoriel Dev"));

            DrawTutorialBlock(
                T("1. Create a sheet", "1. Créer une fiche"),
                T(
                    "Go to the Developer page. Use Create Sheet in the lower-left configuration panel. Save the asset in your project folder, for example Assets/Design/BalanceSheets.",
                    "Va dans Configuration Dev. Utilise Créer fiche dans le panneau de configuration en bas à gauche. Sauvegarde l’asset dans le dossier du projet, par exemple Assets/Design/BalanceSheets."
                )
            );

            DrawTutorialBlock(
                T("2. Configure the sheet", "2. Configurer la fiche"),
                T(
                    "Select the sheet in the left panel, then configure its title, target root and description in the lower-left panel.",
                    "Sélectionne la fiche dans le panneau de gauche, puis configure son titre, sa cible et sa description dans le panneau inférieur gauche."
                )
            );

            DrawTutorialBlock(
                T("3. Create sections", "3. Créer des sections"),
                T(
                    "Create sections such as Movement, Jump, Combat, Camera, Economy or AI. Sections are fully project-specific and can be reordered with drag and drop.",
                    "Crée des sections comme Movement, Jump, Combat, Camera, Economy ou AI. Les sections sont entièrement propres au projet et peuvent être réordonnées par glisser-déposer."
                )
            );

            DrawTutorialBlock(
                T("4. Scan the target", "4. Scanner la cible"),
                T(
                    "Click Scan Target. The tool lists supported serialized values from MonoBehaviours: int, float, bool, string and enum.",
                    "Clique sur Scanner la cible. L’outil liste les valeurs sérialisées compatibles des MonoBehaviours : int, float, bool, string et enum."
                )
            );

            DrawTutorialBlock(
                T("5. Add useful fields", "5. Ajouter les champs utiles"),
                T(
                    "Use the search bar to find useful gameplay values such as speed, damage, cooldown, jump height, health or range. Select fields, choose a target section, then click Add Selected To Section.",
                    "Utilise la barre de recherche pour trouver les valeurs gameplay utiles comme speed, damage, cooldown, jump height, health ou range. Sélectionne les champs, choisis une section cible, puis clique sur Ajouter sélection à la section."
                )
            );

            DrawTutorialBlock(
                T("6. Validate baseline", "6. Valider la baseline"),
                T(
                    "When the current target values are validated by the team, use Set Baseline. This makes Current the new official reference.",
                    "Quand les valeurs actuelles de la cible sont validées par l’équipe, utilise Définir baseline. Cela transforme Current en nouvelle référence officielle."
                )
            );

            DrawTutorialSectionTitle(T("Generic Example", "Exemple générique"));

            DrawLargeTutorialParagraph(
                T(
                    "A project has a Player prefab with movementSpeed, jumpHeight and attackDamage fields. The developer creates a sheet called Player Balance, assigns the Player prefab as Target Root, creates Movement, Jump and Combat sections, scans the target, then adds movementSpeed to Movement, jumpHeight to Jump and attackDamage to Combat. Designers can then tune these values from the Designer page.",
                    "Un projet possède un prefab Player avec les champs movementSpeed, jumpHeight et attackDamage. Le développeur crée une fiche Player Balance, assigne le prefab Player comme cible, crée les sections Movement, Jump et Combat, scanne la cible, puis ajoute movementSpeed à Movement, jumpHeight à Jump et attackDamage à Combat. Les GD peuvent ensuite équilibrer ces valeurs depuis la page Équilibrage GD."
                )
            );

            EditorGUILayout.Space(20f);

            EditorGUILayout.EndScrollView();
        }

        // --------------------------------------------------------------------
        // Settings Page
        // --------------------------------------------------------------------

        private void DrawSettingsPage()
        {
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            EditorGUILayout.Space(14f);

            EditorGUILayout.LabelField(
                T("Plugin Settings", "Paramètres du plugin"),
                tutorialTitleStyle
            );

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Language", "Langue"), subtitleStyle);

            language = (PluginLanguage)EditorGUILayout.EnumPopup(
                language,
                GUILayout.Height(26f)
            );

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(
                T("Default Balance Sheets Folder", "Dossier par défaut des fiches"),
                subtitleStyle
            );

            EditorGUILayout.BeginHorizontal();

            defaultSheetsFolder = EditorGUILayout.TextField(
                defaultSheetsFolder,
                GUILayout.Height(28f),
                GUILayout.MinWidth(500f)
            );

            if (GUILayout.Button(T("Browse", "Parcourir"), GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                defaultSheetsFolder = GameplayBalanceSheetUtility.BrowseAssetsFolder(defaultSheetsFolder);
            }

            EditorGUILayout.EndHorizontal();

            DrawInfoBox(
                T(
                    "This folder is used when creating new balance sheet assets. It must be inside the Unity project Assets folder.",
                    "Ce dossier est utilisé lors de la création de nouvelles fiches d’équilibrage. Il doit être dans le dossier Assets du projet Unity."
                ),
                MessageType.Info
            );

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(T("Display Options", "Options d’affichage"), subtitleStyle);

            showTechnicalInfo = EditorGUILayout.ToggleLeft(
                T(
                    "Show technical information in Designer page",
                    "Afficher les informations techniques dans la page GD"
                ),
                showTechnicalInfo
            );

            showHelpBoxes = EditorGUILayout.ToggleLeft(
                T(
                    "Show help boxes",
                    "Afficher les bulles d’aide"
                ),
                showHelpBoxes
            );

            showEntryDescriptions = EditorGUILayout.ToggleLeft(
                T(
                    "Show entry descriptions in Designer page",
                    "Afficher les descriptions des paramètres en page GD"
                ),
                showEntryDescriptions
            );

            showDetailedSidebar = EditorGUILayout.ToggleLeft(
                T(
                    "Show detailed sheet cards in sidebar",
                    "Afficher les fiches détaillées dans la barre latérale"
                ),
                showDetailedSidebar
            );

            EditorGUILayout.Space(12f);

            if (DrawColoredButton(
                    T("Save Preferences", "Sauvegarder préférences"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Width(210f),
                    GUILayout.Height(34f)))
            {
                SavePreferences();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20f);

            EditorGUILayout.EndScrollView();
        }

        // --------------------------------------------------------------------
        // Shared Drawing
        // --------------------------------------------------------------------

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(12f);

            DrawInfoBox(
                T(
                    "No balance sheet selected. Select a sheet from the left panel, or create one from the Developer page.",
                    "Aucune fiche sélectionnée. Sélectionne une fiche dans le panneau de gauche ou crée-en une depuis la page Configuration Dev."
                ),
                MessageType.Info
            );
        }

        private void DrawEntryValueRow(GameplayBalanceSheetEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            DrawValueCell("Baseline", entry.baselineValue, 140f);
            DrawValueCell("Current", entry.currentValue, 140f);

            EditorGUILayout.LabelField("Test", compactBoldLabelStyle, GUILayout.Width(35f));

            string newTestValue = DrawValueField(entry, entry.testValue, 120f);

            if (newTestValue != entry.testValue)
            {
                entry.testValue = newTestValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            DrawStateBadge(entry, 135f);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawValueCell(string label, string value, float width)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));

            EditorGUILayout.LabelField(label, compactBoldLabelStyle, GUILayout.Width(58f));
            EditorGUILayout.SelectableLabel(value, GUILayout.Height(18f), GUILayout.Width(width - 60f));

            EditorGUILayout.EndHorizontal();
        }

        private string DrawValueField(GameplayBalanceSheetEntry entry, string value, float width)
        {
            if (entry.propertyTypeName == "Boolean")
            {
                bool boolValue = string.Equals(
                    value,
                    "true",
                    StringComparison.OrdinalIgnoreCase
                );

                bool newBoolValue = EditorGUILayout.Toggle(boolValue, GUILayout.Width(width));
                return newBoolValue ? "true" : "false";
            }

            if (entry.propertyTypeName == "Integer")
            {
                int intValue = 0;
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue);

                int newIntValue = EditorGUILayout.IntField(intValue, GUILayout.Width(width));
                return newIntValue.ToString(CultureInfo.InvariantCulture);
            }

            if (entry.propertyTypeName == "Float")
            {
                float floatValue = 0f;
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue);

                float newFloatValue = EditorGUILayout.FloatField(floatValue, GUILayout.Width(width));
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

                int newIndex = EditorGUILayout.Popup(currentIndex, entry.enumNames.ToArray(), GUILayout.Width(width));
                return entry.enumNames[newIndex];
            }

            return EditorGUILayout.TextField(value, GUILayout.Width(width));
        }

        private void DrawStateBadge(GameplayBalanceSheetEntry entry, float width)
        {
            Color color = GetEntryStateColor(entry);
            GUIStyle style = new GUIStyle(compactBoldLabelStyle)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleLeft
            };

            EditorGUILayout.LabelField(GetLocalizedState(entry), style, GUILayout.Width(width));
        }

        private void DrawColoredLabel(string text, Color color, bool bold)
        {
            GUIStyle style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label)
            {
                normal = { textColor = color },
                wordWrap = true
            };

            EditorGUILayout.LabelField(text, style);
        }

        private bool DrawColoredButton(string label, Color color, params GUILayoutOption[] options)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            bool clicked = GUILayout.Button(label, options);

            GUI.backgroundColor = previousColor;

            return clicked;
        }

        private void DrawInlineInfo(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(label + ":", GUILayout.Width(65f));
            DrawColoredLabel(value, valueColor, true);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSmallStat(string label, int value, Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = color }
            };

            EditorGUILayout.LabelField($"{label}: {value}", style, GUILayout.Width(105f));
        }

        private void DrawStatusExplanation(string label, string explanation, Color color)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.MinWidth(220f));

            DrawColoredLabel(label, color, true);
            EditorGUILayout.LabelField(explanation, descriptionStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawTutorialSectionTitle(string title)
        {
            EditorGUILayout.Space(14f);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                wordWrap = true
            };

            EditorGUILayout.LabelField(title, style);
        }

        private void DrawLargeTutorialParagraph(string text)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(text, tutorialBodyStyle);
        }

        private void DrawTutorialBlock(string title, string body)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(title, subtitleStyle);
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(body, tutorialBodyStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawInfoBox(string message, MessageType messageType)
        {
            if (!showHelpBoxes)
            {
                return;
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        // --------------------------------------------------------------------
        // Reorderable Sections
        // --------------------------------------------------------------------

        private void EnsureSectionReorderList()
        {
            if (selectedSheet == null)
            {
                sectionReorderableList = null;
                sectionSerializedObject = null;
                sectionListTarget = null;
                return;
            }

            if (sectionReorderableList != null && sectionListTarget == selectedSheet)
            {
                return;
            }

            sectionListTarget = selectedSheet;
            sectionSerializedObject = new SerializedObject(selectedSheet);

            SerializedProperty sectionsProperty = sectionSerializedObject.FindProperty("sections");

            sectionReorderableList = new ReorderableList(
                sectionSerializedObject,
                sectionsProperty,
                true,
                true,
                false,
                false
            );

            sectionReorderableList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, T("Sections order", "Ordre des sections"));
            };

            sectionReorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = sectionsProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty = element.FindPropertyRelative("sectionName");

                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.PropertyField(rect, nameProperty, GUIContent.none);
            };

            sectionReorderableList.onSelectCallback = list =>
            {
                selectedSectionIndex = list.index;
            };

            sectionReorderableList.onReorderCallback = list =>
            {
                selectedSectionIndex = list.index;
                EditorUtility.SetDirty(selectedSheet);
            };
        }

        private void ResetSectionReorderList()
        {
            sectionReorderableList = null;
            sectionSerializedObject = null;
            sectionListTarget = null;
        }

        // --------------------------------------------------------------------
        // Actions
        // --------------------------------------------------------------------

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

            ResetSectionReorderList();
        }

        private void CreateSheet()
        {
            GameplayBalanceSheetProfile sheet =
                GameplayBalanceSheetUtility.CreateSheet(defaultSheetsFolder);

            RefreshSheets();

            if (sheet != null)
            {
                selectedSheet = sheet;
                currentPage = MainPage.Developer;
            }
        }

        private void ConfirmAndDeleteSheet()
        {
            if (selectedSheet == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(selectedSheet.sheetTitle)
                ? selectedSheet.name
                : selectedSheet.sheetTitle;

            bool confirmed = EditorUtility.DisplayDialog(
                T("Delete Balance Sheet", "Supprimer la fiche"),
                T(
                    $"Delete balance sheet \"{title}\"?\n\nThis will only delete the balance sheet asset.\nIt will not delete the target, prefab or gameplay scripts.",
                    $"Supprimer la fiche \"{title}\" ?\n\nCela supprimera uniquement l’asset de fiche d’équilibrage.\nLa cible, le prefab et les scripts gameplay ne seront pas supprimés."
                ),
                T("Delete", "Supprimer"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetProfile sheetToDelete = selectedSheet;
            selectedSheet = null;

            bool deleted = GameplayBalanceSheetUtility.DeleteSheet(sheetToDelete);

            if (!deleted)
            {
                EditorUtility.DisplayDialog(
                    T("Delete Failed", "Suppression échouée"),
                    T("Could not delete the selected sheet.", "Impossible de supprimer la fiche sélectionnée."),
                    "OK"
                );
            }

            RefreshSheets();
        }

        private void ScanTarget()
        {
            if (selectedSheet == null || selectedSheet.targetRoot == null)
            {
                return;
            }

            scannedProperties = GameplayBalanceSheetUtility.ScanTarget(selectedSheet.targetRoot);
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

        private void RemoveSelectedSection()
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

            GameplayBalanceSheetSection section = selectedSheet.sections[selectedSectionIndex];

            bool confirmed = EditorUtility.DisplayDialog(
                T("Remove Section", "Supprimer section"),
                T(
                    $"Remove section \"{section.sectionName}\" and all its entries?",
                    $"Supprimer la section \"{section.sectionName}\" et tous ses paramètres ?"
                ),
                T("Remove", "Supprimer"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            selectedSheet.sections.RemoveAt(selectedSectionIndex);

            selectedSectionIndex = Mathf.Clamp(
                selectedSectionIndex,
                0,
                Mathf.Max(0, selectedSheet.sections.Count - 1)
            );

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

            Debug.Log(
                $"Gameplay Balance Sheets: added {addedCount} field(s) to section '{targetSection.sectionName}'."
            );
        }

        private void ConfirmAndRevertAll()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                T("Revert All", "Tout réinitialiser"),
                T(
                    "This will revert all entries to their baseline values.",
                    "Tous les paramètres vont revenir à leur valeur baseline."
                ),
                T("Revert All", "Tout réinitialiser"),
                T("Cancel", "Annuler")
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
                T("Set Current As Baseline", "Définir Current comme baseline"),
                T(
                    "This will replace the official baseline of every entry with the current values from the target. Continue only if these values are validated.",
                    "Cela remplacera la baseline officielle de tous les paramètres par les valeurs actuelles de la cible. Continue uniquement si ces valeurs sont validées."
                ),
                T("Set Baseline", "Définir baseline"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.SetCurrentAsBaseline(selectedSheet);
        }

        private void SavePreferences()
        {
            EditorPrefs.SetInt(LanguagePrefKey, (int)language);
            EditorPrefs.SetString(SheetsFolderPrefKey, defaultSheetsFolder);
            EditorPrefs.SetBool(ShowTechnicalInfoPrefKey, showTechnicalInfo);
            EditorPrefs.SetBool(ShowHelpBoxesPrefKey, showHelpBoxes);
            EditorPrefs.SetBool(ShowEntryDescriptionsPrefKey, showEntryDescriptions);
            EditorPrefs.SetBool(ShowDetailedSidebarPrefKey, showDetailedSidebar);

            EditorUtility.DisplayDialog(
                T("Preferences Saved", "Préférences sauvegardées"),
                T("Preferences have been saved.", "Les préférences ont été sauvegardées."),
                "OK"
            );
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

        private void SetAllEntriesSelected(bool selected)
        {
            if (selectedSheet == null || selectedSheet.sections == null)
            {
                return;
            }

            for (int sectionIndex = 0; sectionIndex < selectedSheet.sections.Count; sectionIndex++)
            {
                SetSectionEntriesSelected(selectedSheet.sections[sectionIndex], selected);
            }

            EditorUtility.SetDirty(selectedSheet);
        }

        private void SetSectionEntriesSelected(GameplayBalanceSheetSection section, bool selected)
        {
            if (section == null || section.entries == null)
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry != null)
                {
                    entry.selected = selected;
                }
            }

            EditorUtility.SetDirty(selectedSheet);
        }

        private void ApplySelectedInSection(GameplayBalanceSheetSection section)
        {
            if (section == null || section.entries == null)
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry != null && entry.selected && entry.HasPendingTestValue)
                {
                    GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
                }
            }

            GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
        }

        private void RevertSelectedInSection(GameplayBalanceSheetSection section)
        {
            if (section == null || section.entries == null)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                T("Revert Section Selection", "Réinitialiser la sélection de section"),
                T(
                    "Selected entries in this section will revert to baseline.",
                    "Les paramètres sélectionnés dans cette section vont revenir à leur baseline."
                ),
                T("Revert", "Réinitialiser"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry != null && entry.selected)
                {
                    GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
                }
            }

            GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
        }

        private void EnsureSections()
        {
            if (selectedSheet.sections == null)
            {
                selectedSheet.sections = new List<GameplayBalanceSheetSection>();
            }

            if (selectedSheet.sections.Count == 0)
            {
                selectedSheet.sections.Add(new GameplayBalanceSheetSection("General"));
                EditorUtility.SetDirty(selectedSheet);
            }
        }

        // --------------------------------------------------------------------
        // Queries / State
        // --------------------------------------------------------------------

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
                   SafeLower(sheet.name).Contains(search) ||
                   SafeLower(sheet.targetRoot != null ? sheet.targetRoot.name : string.Empty).Contains(search);
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

        private Color GetEntryPanelColor(GameplayBalanceSheetEntry entry)
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

        private Color GetEntryStateColor(GameplayBalanceSheetEntry entry)
        {
            if (entry.isMissing)
            {
                return new Color(1f, 0.35f, 0.35f);
            }

            if (entry.HasPendingTestValue)
            {
                return new Color(0.35f, 0.65f, 1f);
            }

            if (entry.HasChangedFromBaseline)
            {
                return new Color(1f, 0.65f, 0.25f);
            }

            return new Color(0.3f, 0.85f, 0.45f);
        }

        private string GetLocalizedState(GameplayBalanceSheetEntry entry)
        {
            if (entry.isMissing)
            {
                return T("Missing", "Introuvable");
            }

            if (entry.HasPendingTestValue)
            {
                return T("Pending", "Test en attente");
            }

            if (entry.HasChangedFromBaseline)
            {
                return T("Changed", "Modifié");
            }

            return "OK";
        }

        private SheetStats GetSheetStats(GameplayBalanceSheetProfile sheet)
        {
            SheetStats stats = new SheetStats();

            if (sheet == null || sheet.sections == null)
            {
                return stats;
            }

            for (int sectionIndex = 0; sectionIndex < sheet.sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = sheet.sections[sectionIndex];

                if (section == null || section.entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
                {
                    GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                    if (entry == null)
                    {
                        continue;
                    }

                    if (entry.isMissing)
                    {
                        stats.missing++;
                    }
                    else if (entry.HasPendingTestValue)
                    {
                        stats.pending++;
                    }
                    else if (entry.HasChangedFromBaseline)
                    {
                        stats.changed++;
                    }
                }
            }

            return stats;
        }

        private string T(string english, string french)
        {
            return language == PluginLanguage.French ? french : english;
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        private struct SheetStats
        {
            public int pending;
            public int changed;
            public int missing;
        }
    }
}