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

        private string sheetSearchText = string.Empty;
        private string scanSearchText = string.Empty;

        private int selectedSectionIndex;
        private string newSectionName = "New Section";
        private string defaultSheetsFolder = GameplayBalanceSheetUtility.DefaultSheetsFolder;
        private bool showTechnicalInfo;

        private SerializedObject sectionSerializedObject;
        private ReorderableList sectionReorderableList;
        private GameplayBalanceSheetProfile sectionListTarget;

        [MenuItem("Tools/Gameplay Balance Sheets")]
        public static void Open()
        {
            GameplayBalanceSheetsWindow window = GetWindow<GameplayBalanceSheetsWindow>();
            window.titleContent = new GUIContent("Balance Sheets");
            window.minSize = new Vector2(1180f, 720f);
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

            RefreshSheets();
        }

        private void OnGUI()
        {
            DrawTopBar();

            EditorGUILayout.BeginHorizontal();

            DrawSidebar();

            EditorGUILayout.BeginVertical();
            DrawCurrentPage();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            DrawBottomBar();
        }

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Gameplay Balance Sheets", EditorStyles.boldLabel, GUILayout.Width(220f));

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
                GUILayout.Width(520f)
            );

            GUILayout.FlexibleSpace();

            language = (PluginLanguage)EditorGUILayout.EnumPopup(
                language,
                EditorStyles.toolbarPopup,
                GUILayout.Width(90f)
            );

            if (GUILayout.Button(T("Refresh", "Actualiser"), EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                RefreshSheets();
            }

            EditorGUILayout.EndHorizontal();

            EditorPrefs.SetInt(LanguagePrefKey, (int)language);
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300f));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(T("Balance Sheets", "Fiches d’équilibrage"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            sheetSearchText = EditorGUILayout.TextField(sheetSearchText);
            if (GUILayout.Button(T("Clear", "Effacer"), GUILayout.Width(70f)))
            {
                sheetSearchText = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

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

                DrawSheetButton(sheet);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawSheetButton(GameplayBalanceSheetProfile sheet)
        {
            Color previousColor = GUI.backgroundColor;

            if (sheet == selectedSheet)
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            }

            string label = string.IsNullOrWhiteSpace(sheet.sheetTitle)
                ? sheet.name
                : sheet.sheetTitle;

            SheetStats stats = GetSheetStats(sheet);

            string suffix = $"  P:{stats.pending} C:{stats.changed} M:{stats.missing}";

            if (GUILayout.Button(label + suffix, EditorStyles.miniButton, GUILayout.Height(28f)))
            {
                selectedSheet = sheet;
                selectedSectionIndex = 0;
                scannedProperties.Clear();
                ResetSectionReorderList();
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawCurrentPage()
        {
            if (currentPage != MainPage.Tutorial &&
                currentPage != MainPage.Settings &&
                selectedSheet == null)
            {
                DrawEmptyState();
                return;
            }

            switch (currentPage)
            {
                case MainPage.Designer:
                    DrawDesignerPage();
                    break;

                case MainPage.Developer:
                    DrawDeveloperPage();
                    break;

                case MainPage.Tutorial:
                    DrawTutorialPage();
                    break;

                case MainPage.Settings:
                    DrawSettingsPage();
                    break;
            }
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(12f);

            EditorGUILayout.HelpBox(
                T(
                    "Select an existing balance sheet or create a new one from the Developer page.",
                    "Sélectionne une fiche existante ou crée une nouvelle fiche depuis la page Configuration Dev."
                ),
                MessageType.Info
            );
        }

        private void DrawDesignerPage()
        {
            DrawDesignerHeader();

            DrawStatusLegend();

            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            DrawDesignerEntries();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDesignerHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(
                selectedSheet.sheetTitle,
                EditorStyles.boldLabel
            );

            if (selectedSheet.targetRoot != null)
            {
                DrawColoredLabel(
                    T("Target assigned", "Cible assignée") + $": {selectedSheet.targetRoot.name}",
                    new Color(0.3f, 0.8f, 0.45f),
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
                EditorGUILayout.HelpBox(selectedSheet.sheetDescription, MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDesignerEntries()
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
            EditorGUILayout.Space(8f);

            EditorGUILayout.BeginVertical("box");

            section.expanded = EditorGUILayout.Foldout(
                section.expanded,
                section.sectionName,
                true,
                EditorStyles.foldoutHeader
            );

            if (!section.expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            if (section.entries == null || section.entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    T("No parameter in this section.", "Aucun paramètre dans cette section."),
                    MessageType.Info
                );

                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < section.entries.Count; i++)
            {
                GameplayBalanceSheetEntry entry = section.entries[i];

                if (entry == null)
                {
                    continue;
                }

                DrawDesignerEntry(entry);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDesignerEntry(GameplayBalanceSheetEntry entry)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = GetEntryPanelColor(entry);

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            EditorGUILayout.BeginHorizontal();

            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(22f));
            EditorGUILayout.LabelField(entry.displayName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            DrawStateBadge(entry);

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(entry.description))
            {
                EditorGUILayout.HelpBox(entry.description, MessageType.None);
            }

            if (showTechnicalInfo)
            {
                EditorGUILayout.LabelField("Source", $"{entry.componentName} / {entry.propertyPath}");
            }

            DrawEntryValueRow(entry);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(T("Apply", "Appliquer")))
            {
                GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Revert To Baseline", "Revenir à la baseline")))
            {
                GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Copy Current To Test", "Current vers Test")))
            {
                entry.testValue = entry.currentValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            if (GUILayout.Button(T("Copy Baseline To Test", "Baseline vers Test")))
            {
                entry.testValue = entry.baselineValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperPage()
        {
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            DrawDeveloperSheetSettings();
            DrawDeveloperSectionTools();
            DrawDeveloperEntries();
            DrawDeveloperScanTools();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDeveloperSheetSettings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Sheet Setup", "Configuration de la fiche"), EditorStyles.boldLabel);

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
                GUILayout.MinHeight(45f)
            );

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedSheet);
            }

            if (selectedSheet.targetRoot == null)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "Assign a Target Root before scanning or applying values.",
                        "Assigne une cible avant de scanner ou d’appliquer des valeurs."
                    ),
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperSectionTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Sections", "Sections"), EditorStyles.boldLabel);

            DrawColoredLabel(
                T(
                    "Drag sections to reorder them.",
                    "Déplace les sections à la souris pour changer leur ordre."
                ),
                new Color(0.35f, 0.65f, 1f),
                false
            );

            EnsureSectionReorderList();

            if (sectionReorderableList != null)
            {
                sectionSerializedObject.Update();
                sectionReorderableList.DoLayoutList();
                sectionSerializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();

            newSectionName = EditorGUILayout.TextField(
                T("New Section", "Nouvelle section"),
                newSectionName
            );

            if (GUILayout.Button(T("Add Section", "Ajouter section"), GUILayout.Width(130f)))
            {
                AddSection(newSectionName);
                newSectionName = "New Section";
                ResetSectionReorderList();
            }

            if (GUILayout.Button(T("Remove Selected Section", "Supprimer section"), GUILayout.Width(180f)))
            {
                RemoveSelectedSection();
                ResetSectionReorderList();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperEntries()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Sheet Entries", "Paramètres de la fiche"), EditorStyles.boldLabel);

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
            entry.displayName = EditorGUILayout.TextField(entry.displayName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            DrawStateBadge(entry);

            if (GUILayout.Button(T("Remove", "Supprimer"), GUILayout.Width(90f)))
            {
                section.entries.RemoveAt(entryIndex);
                EditorUtility.SetDirty(selectedSheet);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Source", $"{entry.componentName} / {entry.propertyPath}");
            EditorGUILayout.LabelField("Type", entry.propertyTypeName);

            EditorGUILayout.LabelField(T("Description", "Description"));
            entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight(40f));

            DrawEntryValueRow(entry);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(T("Apply Test", "Appliquer Test")))
            {
                GameplayBalanceSheetUtility.ApplyEntryTestValue(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Revert To Baseline", "Revenir à la baseline")))
            {
                GameplayBalanceSheetUtility.RevertEntryToBaseline(selectedSheet, entry);
            }

            if (GUILayout.Button(T("Current As Baseline", "Current devient baseline")))
            {
                entry.baselineValue = entry.currentValue;
                entry.testValue = entry.currentValue;
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperScanTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Scan Target", "Scanner la cible"), EditorStyles.boldLabel);

            if (selectedSheet.targetRoot == null)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "Assign a Target Root to scan serialized fields.",
                        "Assigne une cible pour scanner ses champs sérialisés."
                    ),
                    MessageType.Warning
                );

                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(T("Scan Target", "Scanner la cible"), GUILayout.Height(28f)))
            {
                ScanTarget();
            }

            if (GUILayout.Button(T("Select All Visible", "Sélectionner visibles"), GUILayout.Height(28f)))
            {
                SetVisibleScannedPropertiesSelected(true);
            }

            if (GUILayout.Button(T("Clear Scan Selection", "Vider sélection scan"), GUILayout.Height(28f)))
            {
                SetVisibleScannedPropertiesSelected(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();

            scanSearchText = EditorGUILayout.TextField(T("Search", "Recherche"), scanSearchText);

            string[] sectionNames = GetSectionNames();

            if (sectionNames.Length == 0)
            {
                AddSection("General");
                sectionNames = GetSectionNames();
            }

            selectedSectionIndex = Mathf.Clamp(selectedSectionIndex, 0, sectionNames.Length - 1);

            selectedSectionIndex = EditorGUILayout.Popup(
                T("Target Section", "Section cible"),
                selectedSectionIndex,
                sectionNames
            );

            if (GUILayout.Button(T("Add Selected To Section", "Ajouter à la section"), GUILayout.Width(190f)))
            {
                AddSelectedScannedPropertiesToCurrentSection();
            }

            EditorGUILayout.EndHorizontal();

            scanScroll = EditorGUILayout.BeginScrollView(scanScroll, GUILayout.MinHeight(180f));

            DrawScannedProperties();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawScannedProperties()
        {
            if (scannedProperties == null || scannedProperties.Count == 0)
            {
                EditorGUILayout.HelpBox(
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
                    EditorGUILayout.Space(6f);
                    DrawColoredLabel(group, new Color(0.35f, 0.65f, 1f), true);
                }

                EditorGUILayout.BeginHorizontal();

                property.selected = EditorGUILayout.Toggle(property.selected, GUILayout.Width(22f));
                EditorGUILayout.LabelField(property.displayName, GUILayout.Width(230f));
                EditorGUILayout.LabelField(property.propertyPath, GUILayout.Width(320f));
                DrawColoredLabel(property.currentValue, Color.white, false);

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTutorialPage()
        {
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(
                T("Gameplay Balance Sheets Tutorial", "Tutoriel Gameplay Balance Sheets"),
                EditorStyles.boldLabel
            );

            EditorGUILayout.HelpBox(
                T(
                    "This tool lets developers expose selected serialized gameplay values in clean balance sheets for game designers.",
                    "Cet outil permet aux développeurs d’exposer certains champs gameplay sérialisés dans des fiches lisibles pour les game designers."
                ),
                MessageType.Info
            );

            DrawTutorialBlock(
                T("1. Create a sheet", "1. Créer une fiche"),
                T(
                    "Go to the Developer page and click Create Sheet. Save it inside your project, for example Assets/Design/BalanceSheets.",
                    "Va dans Configuration Dev et clique sur Créer une fiche. Sauvegarde-la dans ton projet, par exemple Assets/Design/BalanceSheets."
                )
            );

            DrawTutorialBlock(
                T("2. Assign a target", "2. Assigner une cible"),
                T(
                    "Drag a prefab or scene object into Target Root. This can be a player, enemy, boss, camera, UI controller, or any gameplay object.",
                    "Glisse un prefab ou un objet de scène dans Cible. Cela peut être un joueur, un ennemi, un boss, une caméra, une UI ou n’importe quel objet gameplay."
                )
            );

            DrawTutorialBlock(
                T("3. Create sections", "3. Créer des sections"),
                T(
                    "Create sections such as Movement, Jump, Combat, Camera, Economy or AI. Sections are project-specific and can be reordered by drag and drop.",
                    "Crée des sections comme Movement, Jump, Combat, Camera, Economy ou AI. Les sections sont propres au projet et peuvent être réordonnées à la souris."
                )
            );

            DrawTutorialBlock(
                T("4. Scan the target", "4. Scanner la cible"),
                T(
                    "Click Scan Target. The tool lists editable serialized values such as float, int, bool, string and enum fields.",
                    "Clique sur Scanner la cible. L’outil liste les valeurs sérialisées modifiables comme les float, int, bool, string et enum."
                )
            );

            DrawTutorialBlock(
                T("5. Add fields to sections", "5. Ajouter des champs aux sections"),
                T(
                    "Search for values like speed, damage, cooldown, jump, health or range. Select useful fields and add them to the chosen section.",
                    "Recherche des valeurs comme speed, damage, cooldown, jump, health ou range. Sélectionne les champs utiles et ajoute-les à la section choisie."
                )
            );

            DrawTutorialBlock(
                T("6. Tune as a designer", "6. Équilibrer côté GD"),
                T(
                    "Go to the Designer page. Change Test Value, then Apply. If the result is bad, Revert To Baseline. If the team validates the value, a developer can set Current As Baseline.",
                    "Va dans Équilibrage GD. Modifie Test Value, puis applique. Si le résultat n’est pas bon, reviens à la baseline. Si l’équipe valide la valeur, un développeur peut définir Current comme baseline."
                )
            );

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(T("Generic Example", "Exemple générique"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                T(
                    "Example: A project has a Player prefab with movementSpeed, jumpHeight and attackDamage fields. Create sections Movement, Jump and Combat. Add movementSpeed to Movement, jumpHeight to Jump and attackDamage to Combat. Designers can then tune these values without opening the technical components.",
                    "Exemple : un projet possède un prefab Player avec les champs movementSpeed, jumpHeight et attackDamage. Crée les sections Movement, Jump et Combat. Ajoute movementSpeed à Movement, jumpHeight à Jump et attackDamage à Combat. Les GD peuvent ensuite équilibrer ces valeurs sans ouvrir les composants techniques."
                ),
                MessageType.None
            );

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsPage()
        {
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            EditorGUILayout.Space(10f);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(T("Plugin Settings", "Paramètres du plugin"), EditorStyles.boldLabel);

            language = (PluginLanguage)EditorGUILayout.EnumPopup(
                T("Language", "Langue"),
                language
            );

            defaultSheetsFolder = EditorGUILayout.TextField(
                T("Default Sheets Folder", "Dossier par défaut des fiches"),
                defaultSheetsFolder
            );

            showTechnicalInfo = EditorGUILayout.ToggleLeft(
                T(
                    "Show technical information in Designer page",
                    "Afficher les informations techniques dans la page GD"
                ),
                showTechnicalInfo
            );

            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                T(
                    "These settings are stored locally in Unity EditorPrefs. Each user can choose their own language and display preferences.",
                    "Ces paramètres sont stockés localement dans Unity EditorPrefs. Chaque utilisateur peut choisir sa langue et ses préférences d’affichage."
                ),
                MessageType.Info
            );

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBottomBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.FlexibleSpace();

            switch (currentPage)
            {
                case MainPage.Designer:
                    DrawDesignerBottomBar();
                    break;

                case MainPage.Developer:
                    DrawDeveloperBottomBar();
                    break;

                case MainPage.Settings:
                    DrawSettingsBottomBar();
                    break;

                case MainPage.Tutorial:
                    DrawTutorialBottomBar();
                    break;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDesignerBottomBar()
        {
            using (new EditorGUI.DisabledScope(selectedSheet == null))
            {
                if (GUILayout.Button(T("Refresh Values", "Actualiser valeurs"), EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
                }

                if (GUILayout.Button(T("Apply Selected", "Appliquer sélection"), EditorStyles.toolbarButton, GUILayout.Width(130f)))
                {
                    GameplayBalanceSheetUtility.ApplySelectedPendingValues(selectedSheet);
                }

                if (GUILayout.Button(T("Apply All Pending", "Appliquer tous les tests"), EditorStyles.toolbarButton, GUILayout.Width(155f)))
                {
                    GameplayBalanceSheetUtility.ApplyAllPendingValues(selectedSheet);
                }

                if (GUILayout.Button(T("Revert Selected", "Réinitialiser sélection"), EditorStyles.toolbarButton, GUILayout.Width(145f)))
                {
                    ConfirmAndRevertSelected();
                }

                if (GUILayout.Button(T("Revert All", "Tout réinitialiser"), EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    ConfirmAndRevertAll();
                }

                if (GUILayout.Button(T("Save", "Sauvegarder"), EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    GameplayBalanceSheetUtility.SaveSheet(selectedSheet);
                }
            }
        }

        private void DrawDeveloperBottomBar()
        {
            if (GUILayout.Button(T("Create Sheet", "Créer fiche"), EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                CreateSheet();
            }

            using (new EditorGUI.DisabledScope(selectedSheet == null))
            {
                if (GUILayout.Button(T("Delete Sheet", "Supprimer fiche"), EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    ConfirmAndDeleteSheet();
                }

                if (GUILayout.Button(T("Scan Target", "Scanner cible"), EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    ScanTarget();
                }

                if (GUILayout.Button(T("Add Selected", "Ajouter sélection"), EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    AddSelectedScannedPropertiesToCurrentSection();
                }

                if (GUILayout.Button(T("Set Baseline", "Définir baseline"), EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    ConfirmAndSetCurrentAsBaseline();
                }

                if (GUILayout.Button(T("Ping Target", "Localiser cible"), EditorStyles.toolbarButton, GUILayout.Width(115f)))
                {
                    GameplayBalanceSheetUtility.PingTarget(selectedSheet);
                }

                if (GUILayout.Button(T("Save", "Sauvegarder"), EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    GameplayBalanceSheetUtility.SaveSheet(selectedSheet);
                }
            }
        }

        private void DrawSettingsBottomBar()
        {
            if (GUILayout.Button(T("Save Preferences", "Sauvegarder préférences"), EditorStyles.toolbarButton, GUILayout.Width(170f)))
            {
                SavePreferences();
            }
        }

        private void DrawTutorialBottomBar()
        {
            GUILayout.Label(
                T(
                    "Tutorial page only explains the workflow. No project data is modified here.",
                    "La page tutoriel explique seulement le workflow. Aucune donnée projet n’est modifiée ici."
                ),
                EditorStyles.miniLabel
            );
        }

        private void DrawEntryValueRow(GameplayBalanceSheetEntry entry)
        {
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

        private void DrawStatusLegend()
        {
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            DrawColoredLabel("OK", new Color(0.3f, 0.8f, 0.45f), true);
            GUILayout.Space(16f);
            DrawColoredLabel(T("Pending", "Test en attente"), new Color(0.35f, 0.65f, 1f), true);
            GUILayout.Space(16f);
            DrawColoredLabel(T("Changed", "Modifié"), new Color(1f, 0.65f, 0.25f), true);
            GUILayout.Space(16f);
            DrawColoredLabel(T("Missing", "Introuvable"), new Color(1f, 0.35f, 0.35f), true);

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStateBadge(GameplayBalanceSheetEntry entry)
        {
            Color color = GetEntryStateColor(entry);
            DrawColoredLabel(GetLocalizedState(entry), color, true);
        }

        private void DrawColoredLabel(string text, Color color, bool bold)
        {
            GUIStyle style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label);
            style.normal.textColor = color;
            EditorGUILayout.LabelField(text, style);
        }

        private void DrawTutorialBlock(string title, string body)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(body, MessageType.None);
            EditorGUILayout.EndVertical();
        }

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
                EditorGUI.LabelField(
                    rect,
                    T("Sections order", "Ordre des sections")
                );
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
                    $"Delete balance sheet \"{title}\"?\n\nThis will only delete the balance sheet asset. It will not delete the target prefab or any gameplay component.",
                    $"Supprimer la fiche \"{title}\" ?\n\nCela supprimera uniquement l’asset de fiche d’équilibrage. Le prefab cible et les composants gameplay ne seront pas supprimés."
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
                selectedSheet.sections.Count - 1
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

            Debug.Log($"Gameplay Balance Sheets: added {addedCount} field(s) to section '{targetSection.sectionName}'.");
        }

        private void ConfirmAndRevertSelected()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                T("Revert Selected", "Réinitialiser la sélection"),
                T(
                    "This will revert selected entries to their baseline values.",
                    "Les paramètres sélectionnés vont revenir à leur valeur baseline."
                ),
                T("Revert", "Réinitialiser"),
                T("Cancel", "Annuler")
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

        private void EnsureSections()
        {
            if (selectedSheet.sections == null)
            {
                selectedSheet.sections = new List<GameplayBalanceSheetSection>();
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

            return new Color(0.3f, 0.8f, 0.45f);
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