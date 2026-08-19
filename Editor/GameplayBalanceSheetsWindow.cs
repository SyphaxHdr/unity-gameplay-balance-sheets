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
        private const string AllowReferenceEditingPrefKey = "GameplayBalanceSheets.AllowReferenceEditing";

        private const float SidebarWidth = 380f;
        private const float DevSheetConfigHeight = 330f;

        private enum MainPage
        {
            Designer,
            Application,
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
        private Vector2 applicationScroll;

        private string sheetSearchText = string.Empty;
        private string scanSearchText = string.Empty;
        private string newSectionName = "New Section";
        private string duplicateVariantName = "Test Fast Movement";
        private string defaultSheetsFolder = GameplayBalanceSheetUtility.DefaultSheetsFolder;

        private int selectedSectionIndex;

        private bool showTechnicalInfo;
        private bool showHelpBoxes = true;
        private bool showEntryDescriptions = true;
        private bool showDetailedSidebar = true;
        private bool allowReferenceSheetEditing;

        private readonly Dictionary<string, bool> applicationTargetFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> applicationReferenceFoldouts = new Dictionary<string, bool>();

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
            window.minSize = new Vector2(1320f, 780f);
            window.Show();
        }

        private void OnEnable()
        {
            language = (PluginLanguage)EditorPrefs.GetInt(LanguagePrefKey, (int)PluginLanguage.English);
            defaultSheetsFolder = EditorPrefs.GetString(SheetsFolderPrefKey, GameplayBalanceSheetUtility.DefaultSheetsFolder);
            showTechnicalInfo = EditorPrefs.GetBool(ShowTechnicalInfoPrefKey, false);
            showHelpBoxes = EditorPrefs.GetBool(ShowHelpBoxesPrefKey, true);
            showEntryDescriptions = EditorPrefs.GetBool(ShowEntryDescriptionsPrefKey, true);
            showDetailedSidebar = EditorPrefs.GetBool(ShowDetailedSidebarPrefKey, true);
            allowReferenceSheetEditing = EditorPrefs.GetBool(AllowReferenceEditingPrefKey, false);

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

            if (currentPage == MainPage.Application)
            {
                DrawApplicationPage();
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

            GUILayout.Label("Gameplay Balance Sheets", EditorStyles.boldLabel, GUILayout.Width(220f));

            string[] tabs =
            {
                T("Designer", "Équilibrage GD"),
                T("Sheet Application", "Application des fiches"),
                T("Developer", "Configuration Dev"),
                T("Tutorial", "Tutoriel"),
                T("Settings", "Paramètres")
            };

            currentPage = (MainPage)GUILayout.Toolbar(
                (int)currentPage,
                tabs,
                EditorStyles.toolbarButton,
                GUILayout.Width(760f)
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

            DrawSheetTreePanel(
                T("Balance Sheets", "Fiches d’équilibrage"),
                false
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawDeveloperSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));

            DrawSheetTreePanel(
                T("Sheet Instances", "Instances de fiches"),
                true
            );

            DrawDeveloperSheetConfigurationPanel();

            EditorGUILayout.EndVertical();
        }

        private void DrawSheetTreePanel(string title, bool developerMode)
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

            if (developerMode)
            {
                EditorGUILayout.Space(4f);

                if (DrawColoredButton(
                        T("Create Reference Sheet", "Créer fiche de référence"),
                        new Color(0.35f, 0.7f, 1f),
                        GUILayout.Height(30f)))
                {
                    CreateSheet();
                }
            }

            EditorGUILayout.Space(4f);

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

            List<GameplayBalanceSheetProfile> references = GetReferenceSheets();

            for (int i = 0; i < references.Count; i++)
            {
                GameplayBalanceSheetProfile referenceSheet = references[i];

                if (referenceSheet == null)
                {
                    continue;
                }

                if (!ShouldShowReferenceGroup(referenceSheet))
                {
                    continue;
                }

                DrawReferenceSheetCard(referenceSheet);

                List<GameplayBalanceSheetProfile> variants = GetVariantsForReference(referenceSheet);

                for (int variantIndex = 0; variantIndex < variants.Count; variantIndex++)
                {
                    GameplayBalanceSheetProfile variant = variants[variantIndex];

                    if (variant == null || !MatchesSheetSearch(variant))
                    {
                        continue;
                    }

                    DrawVariantSheetCard(variant);
                }

                EditorGUILayout.Space(6f);
            }

            DrawOrphanVariants();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawReferenceSheetCard(GameplayBalanceSheetProfile sheet)
        {
            SheetStats stats = GetSheetStats(sheet);

            Color previousColor = GUI.backgroundColor;

            if (sheet == selectedSheet)
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            }

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            if (GUILayout.Button(sheet.DisplayTitle, cardTitleStyle, GUILayout.Height(30f)))
            {
                SelectSheet(sheet);
            }

            DrawInlineInfo(
                T("Type", "Type"),
                T("Reference Sheet", "Fiche de référence"),
                new Color(0.3f, 0.85f, 0.45f)
            );

            string targetName = sheet.target != null
                ? sheet.target.name
                : T("No target", "Aucune cible");

            DrawInlineInfo(
                T("Target", "Cible"),
                targetName,
                sheet.target != null ? new Color(0.3f, 0.85f, 0.45f) : new Color(1f, 0.45f, 0.35f)
            );

            if (showDetailedSidebar)
            {
                EditorGUILayout.BeginHorizontal();

                DrawSmallStat(T("Pending", "Test"), stats.pending, new Color(0.35f, 0.65f, 1f));
                DrawSmallStat(T("Modified", "Modifié"), stats.modified, new Color(0.6f, 0.45f, 1f));
                DrawSmallStat(T("Missing", "Introuvable"), stats.missing, new Color(1f, 0.35f, 0.35f));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVariantSheetCard(GameplayBalanceSheetProfile sheet)
        {
            SheetStats stats = GetSheetStats(sheet);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(22f);

            Color previousColor = GUI.backgroundColor;

            if (sheet == selectedSheet)
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            }

            EditorGUILayout.BeginVertical("box");

            GUI.backgroundColor = previousColor;

            if (GUILayout.Button("↳ " + sheet.VariantSuffix, EditorStyles.miniButton, GUILayout.Height(26f)))
            {
                SelectSheet(sheet);
            }

            DrawInlineInfo(
                T("Type", "Type"),
                T("Designer Variant", "Variante GD"),
                new Color(0.35f, 0.65f, 1f)
            );

            if (showDetailedSidebar)
            {
                EditorGUILayout.BeginHorizontal();

                DrawSmallStat(T("Pending", "Test"), stats.pending, new Color(0.35f, 0.65f, 1f));
                DrawSmallStat(T("Modified", "Modifié"), stats.modified, new Color(0.6f, 0.45f, 1f));
                DrawSmallStat(T("Missing", "Introuvable"), stats.missing, new Color(1f, 0.35f, 0.35f));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOrphanVariants()
        {
            List<GameplayBalanceSheetProfile> orphans = new List<GameplayBalanceSheetProfile>();

            for (int i = 0; i < sheets.Count; i++)
            {
                GameplayBalanceSheetProfile sheet = sheets[i];

                if (sheet == null || !sheet.isDesignerVariant || sheet.sourceSheet != null)
                {
                    continue;
                }

                if (!MatchesSheetSearch(sheet))
                {
                    continue;
                }

                orphans.Add(sheet);
            }

            if (orphans.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            DrawColoredLabel(T("Orphan Variants", "Variantes sans référence"), new Color(1f, 0.65f, 0.25f), true);

            for (int i = 0; i < orphans.Count; i++)
            {
                DrawVariantSheetCard(orphans[i]);
            }
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
                    "Create reference sheets here. Designer variants are created from the Designer page.",
                    "Crée ici les fiches de référence. Les variantes GD sont créées depuis la page Équilibrage GD."
                ),
                MessageType.Info
            );

            if (selectedSheet == null)
            {
                EditorGUILayout.HelpBox(
                    T("No sheet selected.", "Aucune fiche sélectionnée."),
                    MessageType.Warning
                );

                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(selectedSheet.isDesignerVariant))
            {
                selectedSheet.sheetTitle = EditorGUILayout.TextField(
                    T("Reference Title", "Titre de référence"),
                    selectedSheet.sheetTitle
                );

                UnityEngine.Object previousTarget = selectedSheet.target;
                UnityEngine.Object selectedTarget = EditorGUILayout.ObjectField(
                    T("Target", "Cible"),
                    previousTarget,
                    typeof(UnityEngine.Object),
                    true
                );

                selectedTarget = NormalizeTarget(selectedTarget);

                if (IsSupportedTarget(selectedTarget))
                {
                    selectedSheet.target = selectedTarget;

                    if (previousTarget != selectedSheet.target)
                    {
                        scannedProperties.Clear();
                    }
                }

                if (!IsSupportedTarget(selectedTarget))
                {
                    EditorGUILayout.HelpBox(
                        T(
                            "The target must be a GameObject or a ScriptableObject asset.",
                            "La cible doit être un GameObject ou un asset ScriptableObject."
                        ),
                        MessageType.Error
                    );
                }
            }

            if (selectedSheet.isDesignerVariant)
            {
                EditorGUILayout.LabelField(
                    T("Variant Name", "Nom de variante"),
                    selectedSheet.variantName
                );

                DrawInfoBox(
                    T(
                        "This is a designer variant. Its structure comes from its reference sheet. Edit gameplay values from the Designer page.",
                        "Ceci est une variante GD. Sa structure vient de sa fiche de référence. Les valeurs gameplay se modifient depuis la page Équilibrage GD."
                    ),
                    MessageType.Info
                );
            }

            selectedSheet.lockSourceSheetForDesigner = EditorGUILayout.ToggleLeft(
                T(
                    "Lock reference sheet for designers",
                    "Verrouiller la fiche de référence pour les GD"
                ),
                selectedSheet.lockSourceSheetForDesigner
            );

            EditorGUILayout.LabelField(T("Description", "Description"));

            using (new EditorGUI.DisabledScope(selectedSheet.isDesignerVariant))
            {
                selectedSheet.sheetDescription = EditorGUILayout.TextArea(
                    selectedSheet.sheetDescription,
                    GUILayout.Height(55f)
                );
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(selectedSheet);
            }

            EditorGUILayout.Space(4f);

            if (DrawColoredButton(
                    T("Delete Sheet", "Supprimer fiche"),
                    new Color(1f, 0.45f, 0.35f),
                    GUILayout.Height(28f)))
            {
                ConfirmAndDeleteSheet(false);
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

            EditorGUILayout.LabelField(selectedSheet.DisplayTitle, titleStyle);

            DrawColoredLabel(
                selectedSheet.isDesignerVariant
                    ? T("Designer Variant", "Variante GD")
                    : T("Reference Sheet", "Fiche de référence"),
                selectedSheet.isDesignerVariant
                    ? new Color(0.35f, 0.65f, 1f)
                    : new Color(0.3f, 0.85f, 0.45f),
                true
            );

            if (selectedSheet.target != null)
            {
                DrawColoredLabel(
                    T("Target", "Cible") + ": " + selectedSheet.target.name,
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

            if (selectedSheet.isDesignerVariant)
            {
                EditorGUI.BeginChangeCheck();

                selectedSheet.variantName = EditorGUILayout.TextField(
                    T("Variant suffix", "Suffixe de variante"),
                    selectedSheet.variantName
                );

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(selectedSheet);
                }
            }
            else if (!CanDesignerEditSelectedSheet())
            {
                DrawInfoBox(
                    T(
                        "This reference sheet is locked for designers. Duplicate it to create an editable variant.",
                        "Cette fiche de référence est verrouillée pour les GD. Duplique-la pour créer une variante modifiable."
                    ),
                    MessageType.Info
                );
            }

            if (!string.IsNullOrWhiteSpace(selectedSheet.sheetDescription))
            {
                EditorGUILayout.LabelField(selectedSheet.sheetDescription, descriptionStyle);
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(590f));

            EditorGUILayout.BeginHorizontal();

            duplicateVariantName = EditorGUILayout.TextField(
                duplicateVariantName,
                GUILayout.Height(28f),
                GUILayout.MinWidth(230f)
            );

            if (DrawColoredButton(
                    T("Create Variant", "Créer une variante"),
                    new Color(0.35f, 0.7f, 1f),
                    GUILayout.Width(170f),
                    GUILayout.Height(28f)))
            {
                DuplicateSelectedSheetForDesigner();
            }

            using (new EditorGUI.DisabledScope(!selectedSheet.isDesignerVariant))
            {
                if (DrawColoredButton(
                        T("Delete Variant", "Supprimer variante"),
                        new Color(1f, 0.45f, 0.35f),
                        GUILayout.Width(155f),
                        GUILayout.Height(28f)))
                {
                    ConfirmAndDeleteSheet(true);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();

            if (DrawColoredButton(
                    T("Refresh Current", "Actualiser Current"),
                    new Color(0.55f, 0.75f, 1f),
                    GUILayout.Height(30f)))
            {
                GameplayBalanceSheetUtility.RefreshCurrentValues(selectedSheet);
            }

            if (DrawColoredButton(
                    T("Apply Whole Sheet", "Appliquer toute la fiche"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Height(30f)))
            {
                GameplayBalanceSheetUtility.ApplyAllValues(selectedSheet);
            }

            if (DrawColoredButton(
                    T("Restore Reference", "Restaurer la référence"),
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

            DrawStatusExplanation(
                T("Reference", "Référence"),
                T(
                    "The target currently matches the reference value.",
                    "La cible correspond actuellement à la valeur de référence."
                ),
                new Color(0.3f, 0.85f, 0.45f)
            );

            DrawStatusExplanation(
                T("Pending", "Test en attente"),
                T(
                    "Test is different from Current. The value is ready to be applied.",
                    "Test est différent de Current. La valeur est prête à être appliquée."
                ),
                new Color(0.35f, 0.65f, 1f)
            );

            DrawStatusExplanation(
                T("Modified", "Modifié"),
                T(
                    "The target uses a value different from the reference.",
                    "La cible utilise une valeur différente de la référence."
                ),
                new Color(0.6f, 0.45f, 1f)
            );

            DrawStatusExplanation(
                T("Missing", "Introuvable"),
                T(
                    "The field no longer exists. Other valid fields can still be applied.",
                    "Le champ n’existe plus. Les autres champs valides peuvent quand même être appliqués."
                ),
                new Color(1f, 0.35f, 0.35f)
            );

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

            if (GUILayout.Button(T("Select", "Sélectionner"), GUILayout.Width(95f)))
            {
                SetSectionEntriesSelected(section, true);
            }

            if (GUILayout.Button(T("Clear", "Désélectionner"), GUILayout.Width(120f)))
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

            using (new EditorGUI.DisabledScope(!CanDesignerEditSelectedSheet()))
            {
                if (DrawColoredButton(
                        "Current → Test",
                        new Color(0.55f, 0.75f, 1f),
                        GUILayout.Width(120f)))
                {
                    CopyCurrentToTestInSection(section);
                }

                if (DrawColoredButton(
                        "Baseline → Test",
                        new Color(0.55f, 0.75f, 1f),
                        GUILayout.Width(125f)))
                {
                    CopyBaselineToTestInSection(section);
                }
            }

            if (DrawColoredButton(
                    T("Restore Selected", "Restaurer sélection"),
                    new Color(1f, 0.65f, 0.25f),
                    GUILayout.Width(135f)))
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

            EditorGUILayout.LabelField(entry.displayName, compactBoldLabelStyle, GUILayout.MinWidth(190f));

            DrawValueCell("Baseline", entry.baselineValue, 145f);
            DrawValueCell("Current", entry.currentValue, 145f);

            EditorGUILayout.LabelField("Test", compactBoldLabelStyle, GUILayout.Width(35f));

            using (new EditorGUI.DisabledScope(!CanDesignerEditSelectedSheet()))
            {
                string newTestValue = DrawValueField(entry, entry.testValue, 130f);

                if (newTestValue != entry.testValue)
                {
                    entry.testValue = newTestValue;
                    EditorUtility.SetDirty(selectedSheet);
                }
            }

            DrawStateBadge(entry, 140f);

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

            if (!selectedSheet.HasSupportedTarget)
            {
                DrawInfoBox(
                    T(
                        "Assign a GameObject or ScriptableObject target in the left configuration panel before scanning.",
                        "Assigne une cible GameObject ou ScriptableObject dans le panneau de configuration à gauche avant de scanner."
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
            entry.displayName = EditorGUILayout.TextField(entry.displayName, compactBoldLabelStyle, GUILayout.MinWidth(200f));

            DrawStateBadge(entry, 135f);

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

            EditorGUILayout.EndVertical();
        }

        // --------------------------------------------------------------------
        // Application Page
        // --------------------------------------------------------------------

        private void DrawApplicationPage()
        {
            applicationScroll = EditorGUILayout.BeginScrollView(applicationScroll);

            EditorGUILayout.Space(12f);

            EditorGUILayout.LabelField(
                T("Sheet Application", "Application des fiches"),
                tutorialTitleStyle
            );

            DrawInfoBox(
                T(
                    "This page is organized as Target > Reference Sheet > Designer Variants. Applying a sheet here promotes it as the new reference.",
                    "Cette page est organisée ainsi : Cible > Fiche de référence > Variantes GD. Appliquer une fiche ici la transforme en nouvelle référence."
                ),
                MessageType.Warning
            );

            List<TargetGroup> targetGroups = BuildTargetGroups();

            if (targetGroups.Count == 0)
            {
                DrawInfoBox(
                    T("No reference sheet found.", "Aucune fiche de référence trouvée."),
                    MessageType.Info
                );

                EditorGUILayout.EndScrollView();
                return;
            }

            for (int targetIndex = 0; targetIndex < targetGroups.Count; targetIndex++)
            {
                TargetGroup group = targetGroups[targetIndex];

                string targetKey = GetTargetKey(group.target, targetIndex);
                bool targetExpanded = GetFoldout(applicationTargetFoldouts, targetKey, true);

                EditorGUILayout.Space(10f);
                EditorGUILayout.BeginVertical("box");

                string targetName = group.target != null
                    ? group.target.name
                    : T("No Target", "Aucune cible");

                EditorGUILayout.BeginHorizontal();

                targetExpanded = EditorGUILayout.Foldout(
                    targetExpanded,
                    "Target: " + targetName,
                    true,
                    EditorStyles.foldoutHeader
                );

                SetFoldout(applicationTargetFoldouts, targetKey, targetExpanded);

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    T("Reference sheets", "Fiches de référence") + $": {group.references.Count}",
                    GUILayout.Width(180f)
                );

                EditorGUILayout.EndHorizontal();

                if (targetExpanded)
                {
                    for (int referenceIndex = 0; referenceIndex < group.references.Count; referenceIndex++)
                    {
                        GameplayBalanceSheetProfile referenceSheet = group.references[referenceIndex];

                        if (referenceSheet == null)
                        {
                            continue;
                        }

                        DrawApplicationReferenceGroup(referenceSheet);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(20f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawApplicationReferenceGroup(GameplayBalanceSheetProfile referenceSheet)
        {
            string referenceKey = GetSheetKey(referenceSheet);
            bool referenceExpanded = GetFoldout(applicationReferenceFoldouts, referenceKey, true);

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            referenceExpanded = EditorGUILayout.Foldout(
                referenceExpanded,
                referenceSheet.DisplayTitle,
                true,
                EditorStyles.foldoutHeader
            );

            SetFoldout(applicationReferenceFoldouts, referenceKey, referenceExpanded);

            GUILayout.FlexibleSpace();

            List<GameplayBalanceSheetProfile> variants = GetVariantsForReference(referenceSheet);

            EditorGUILayout.LabelField(
                T("Variants", "Variantes") + $": {variants.Count}",
                GUILayout.Width(110f)
            );

            EditorGUILayout.EndHorizontal();

            if (referenceExpanded)
            {
                DrawApplicationSheetCard(referenceSheet, true);

                if (variants.Count > 0)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(T("Designer Variants", "Variantes GD"), compactBoldLabelStyle);

                    for (int variantIndex = 0; variantIndex < variants.Count; variantIndex++)
                    {
                        DrawApplicationVariantCard(variants[variantIndex]);
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawApplicationVariantCard(GameplayBalanceSheetProfile sheet)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);

            DrawApplicationSheetCard(sheet, false);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawApplicationSheetCard(GameplayBalanceSheetProfile sheet, bool isReference)
        {
            if (sheet == null)
            {
                return;
            }

            GameplayBalanceSheetApplicationState state =
                GameplayBalanceSheetUtility.GetApplicationState(sheet);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                isReference
                    ? sheet.DisplayTitle
                    : "↳ " + sheet.VariantSuffix,
                cardTitleStyle,
                GUILayout.MinWidth(260f)
            );

            DrawApplicationStateBadge(state, 130f);

            EditorGUILayout.LabelField(
                T("Sections", "Sections") + $": {sheet.GetSectionCount()}",
                GUILayout.Width(100f)
            );

            EditorGUILayout.LabelField(
                T("Entries", "Paramètres") + $": {sheet.GetEntryCount()}",
                GUILayout.Width(130f)
            );

            GUILayout.FlexibleSpace();

            if (DrawColoredButton(
                    T("Apply As Reference", "Appliquer comme référence"),
                    new Color(0.35f, 0.85f, 0.45f),
                    GUILayout.Width(175f)))
            {
                ConfirmAndApplySheetAsReference(sheet);
            }

            if (GUILayout.Button(T("Refresh", "Actualiser"), GUILayout.Width(95f)))
            {
                GameplayBalanceSheetUtility.RefreshCurrentValues(sheet);
            }

            if (GUILayout.Button(T("Open in Designer", "Ouvrir côté GD"), GUILayout.Width(130f)))
            {
                selectedSheet = sheet;
                currentPage = MainPage.Designer;
            }

            if (GUILayout.Button(T("Ping Target", "Voir cible"), GUILayout.Width(95f)))
            {
                GameplayBalanceSheetUtility.PingTarget(sheet);
            }

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
                    "Gameplay Balance Sheets works like tabletop RPG character sheets, but for gameplay balancing. A sheet describes gameplay values that can be applied to a GameObject hierarchy or a ScriptableObject asset.",
                    "Gameplay Balance Sheets fonctionne comme des fiches de personnage de JDR, mais pour l’équilibrage gameplay. Une fiche décrit des valeurs gameplay applicables à une hiérarchie de GameObjects ou à un asset ScriptableObject."
                )
            );

            DrawLargeTutorialParagraph(
                T(
                    "The developer creates a reference sheet. The designer duplicates it into variants, changes Test values, applies a variant, and compares which one gives the best gameplay flow.",
                    "Le développeur crée une fiche de référence. Le GD la duplique en variantes, modifie les valeurs Test, applique une variante, puis compare celle qui donne le meilleur flow gameplay."
                )
            );

            DrawTutorialSectionTitle(T("Main workflow", "Workflow principal"));

            DrawTutorialBlock(
                T("1. Developer creates the reference sheet", "1. Le développeur crée la fiche de référence"),
                T(
                    "Go to Developer. Create a reference sheet, assign a GameObject or ScriptableObject target, create sections, scan the target, and add useful serialized fields.",
                    "Va dans Configuration Dev. Crée une fiche de référence, assigne une cible GameObject ou ScriptableObject, crée des sections, scanne la cible et ajoute les champs sérialisés utiles."
                )
            );

            DrawTutorialBlock(
                T("2. Designer creates variants", "2. Le GD crée des variantes"),
                T(
                    "Go to Designer. Select a reference sheet and create a variant. Example: Player Balance Sheet becomes Player Balance Sheet - Test Fast Movement.",
                    "Va dans Équilibrage GD. Sélectionne une fiche de référence et crée une variante. Exemple : Player Balance Sheet devient Player Balance Sheet - Test Fast Movement."
                )
            );

            DrawTutorialBlock(
                T("3. Designer edits Test values", "3. Le GD modifie les valeurs Test"),
                T(
                    "Baseline is the reference value. Current is the value currently on the Unity target. Test is the value prepared inside the selected sheet.",
                    "Baseline est la valeur de référence. Current est la valeur actuellement présente sur la cible Unity. Test est la valeur préparée dans la fiche sélectionnée."
                )
            );

            DrawTutorialBlock(
                T("4. Designer applies values", "4. Le GD applique les valeurs"),
                T(
                    "Use Apply Whole Sheet to apply the selected sheet to the target, or use section selection to apply only selected entries.",
                    "Utilise Appliquer toute la fiche pour appliquer la fiche sélectionnée à la cible, ou utilise la sélection par section pour appliquer uniquement certains paramètres."
                )
            );

            DrawTutorialBlock(
                T("5. Team validates the best sheet", "5. L’équipe valide la meilleure fiche"),
                T(
                    "Go to Sheet Application. Sheets are grouped as Target > Reference Sheet > Designer Variants. Use Apply As Reference only when the team decides that this sheet becomes the new reference.",
                    "Va dans Application des fiches. Les fiches sont regroupées ainsi : Cible > Fiche de référence > Variantes GD. Utilise Appliquer comme référence uniquement quand l’équipe décide que cette fiche devient la nouvelle référence."
                )
            );

            DrawTutorialSectionTitle(T("Designer Tutorial", "Tutoriel GD"));

            DrawTutorialBlock(
                T("Reference sheet", "Fiche de référence"),
                T(
                    "A reference sheet is the base version created by a developer. By default, designers cannot edit it directly. They must create a variant.",
                    "Une fiche de référence est la version de base créée par un développeur. Par défaut, les GD ne peuvent pas la modifier directement. Ils doivent créer une variante."
                )
            );

            DrawTutorialBlock(
                T("Designer variant", "Variante GD"),
                T(
                    "A designer variant is an editable copy of a reference sheet. Only the suffix is edited by the designer. Example: Test Fast Movement.",
                    "Une variante GD est une copie modifiable d’une fiche de référence. Seul le suffixe est modifié par le GD. Exemple : Test Fast Movement."
                )
            );

            DrawTutorialBlock(
                T("Section actions", "Actions de section"),
                T(
                    "Select entries in a section, then use Apply Selected, Current → Test, Baseline → Test, or Restore Selected. These actions affect only selected entries in that section.",
                    "Sélectionne des paramètres dans une section, puis utilise Appliquer sélection, Current → Test, Baseline → Test ou Restaurer sélection. Ces actions ne touchent que les paramètres sélectionnés de cette section."
                )
            );

            DrawTutorialBlock(
                T("Status meanings", "Signification des états"),
                T(
                    "Reference means the target matches the baseline. Pending means Test is different from Current. Modified means the target uses a value different from the reference. Missing means the field no longer exists.",
                    "Référence signifie que la cible correspond à la baseline. Test en attente signifie que Test est différent de Current. Modifié signifie que la cible utilise une valeur différente de la référence. Introuvable signifie que le champ n’existe plus."
                )
            );

            DrawTutorialSectionTitle(T("Developer Tutorial", "Tutoriel Dev"));

            DrawTutorialBlock(
                T("Create and configure", "Créer et configurer"),
                T(
                    "Create the reference sheet from Developer. Configure its title, target and description in the lower-left panel.",
                    "Crée la fiche de référence depuis Configuration Dev. Configure son titre, sa cible et sa description dans le panneau inférieur gauche."
                )
            );

            DrawTutorialBlock(
                T("Sections and scan", "Sections et scan"),
                T(
                    "Create sections such as Movement, Jump, Combat, Camera or AI. Scan the target and add only fields that are useful for balancing.",
                    "Crée des sections comme Movement, Jump, Combat, Camera ou AI. Scanne la cible et ajoute uniquement les champs utiles à l’équilibrage."
                )
            );

            DrawTutorialBlock(
                T("Developer page is not for testing", "La page Dev ne sert pas aux tests"),
                T(
                    "The Developer page builds the sheet structure. Gameplay testing happens from Designer. Final validation happens from Sheet Application.",
                    "La page Dev construit la structure de la fiche. Les tests gameplay se font depuis Équilibrage GD. La validation finale se fait depuis Application des fiches."
                )
            );

            DrawTutorialSectionTitle(T("Settings and Git", "Paramètres et Git"));

            DrawTutorialBlock(
                T("Where preferences are stored", "Où sont enregistrées les préférences"),
                T(
                    "Language, display options and default folder are stored in Unity EditorPrefs on your local machine. They are not stored in the package files.",
                    "La langue, les options d’affichage et le dossier par défaut sont enregistrés dans Unity EditorPrefs sur ta machine locale. Ils ne sont pas stockés dans les fichiers du package."
                )
            );

            DrawTutorialBlock(
                T("Do you need to add them to .gitignore?", "Faut-il les ajouter au .gitignore ?"),
                T(
                    "No. EditorPrefs are outside the project folder, so they are not committed to Git. The balance sheet assets created inside Assets are project data and should usually be committed.",
                    "Non. Les EditorPrefs sont en dehors du dossier projet, donc ils ne sont pas commit dans Git. Les assets de fiches créés dans Assets sont des données projet et doivent généralement être commit."
                )
            );

            DrawTutorialSectionTitle(T("Generic Example", "Exemple générique"));

            DrawLargeTutorialParagraph(
                T(
                    "A Player prefab has movementSpeed, jumpHeight and attackDamage. The developer creates Player Balance Sheet, assigns the Player prefab, creates Movement, Jump and Combat sections, then adds these fields. The designer creates Player Balance Sheet - Test Fast Movement, changes movementSpeed, applies it, and tests the gameplay flow. If the team validates the result, the sheet can be applied as the new reference from Sheet Application.",
                    "Un prefab Player possède movementSpeed, jumpHeight et attackDamage. Le développeur crée Player Balance Sheet, assigne le prefab Player, crée les sections Movement, Jump et Combat, puis ajoute ces champs. Le GD crée Player Balance Sheet - Test Fast Movement, modifie movementSpeed, applique la fiche et teste le flow gameplay. Si l’équipe valide le résultat, la fiche peut être appliquée comme nouvelle référence depuis Application des fiches."
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

            DrawInfoBox(
                T(
                    "The language choice is saved locally with Unity EditorPrefs. It is not written into the Git repository and does not need to be added to .gitignore.",
                    "Le choix de la langue est sauvegardé localement avec Unity EditorPrefs. Il n’est pas écrit dans le dépôt Git et n’a pas besoin d’être ajouté au .gitignore."
                ),
                MessageType.Info
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
                    "This folder is used when creating new balance sheet assets. It must be inside the Unity project Assets folder. Assets created there are project data and should usually be committed to Git.",
                    "Ce dossier est utilisé lors de la création de nouvelles fiches d’équilibrage. Il doit être dans le dossier Assets du projet Unity. Les assets créés dedans sont des données projet et doivent généralement être commit dans Git."
                ),
                MessageType.Info
            );

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(T("Display Options", "Options d’affichage"), subtitleStyle);

            allowReferenceSheetEditing = EditorGUILayout.ToggleLeft(
                T(
                    "Allow designers to edit reference sheets",
                    "Autoriser les GD à modifier les fiches de référence"
                ),
                allowReferenceSheetEditing
            );

            DrawInfoBox(
                T(
                    "Recommended: keep this disabled. Designers should usually duplicate a reference sheet and edit the variant instead of editing the reference directly.",
                    "Recommandé : garder cette option désactivée. Les GD doivent généralement dupliquer une fiche de référence et modifier la variante plutôt que modifier directement la référence."
                ),
                MessageType.Warning
            );

            showTechnicalInfo = EditorGUILayout.ToggleLeft(
                T(
                    "Show technical information in Designer page",
                    "Afficher les informations techniques dans la page GD"
                ),
                showTechnicalInfo
            );

            DrawInfoBox(
                T(
                    "Technical information shows component names and property paths. Useful for developers, usually unnecessary for designers.",
                    "Les informations techniques affichent les noms de composants et les chemins de propriétés. Utile pour les développeurs, généralement inutile pour les GD."
                ),
                MessageType.Info
            );

            showHelpBoxes = EditorGUILayout.ToggleLeft(
                T("Show help boxes", "Afficher les bulles d’aide"),
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

            DrawInfoBox(
                T(
                    "These display settings are also stored locally in Unity EditorPrefs. They are personal editor preferences, not shared project configuration.",
                    "Ces options d’affichage sont aussi enregistrées localement dans Unity EditorPrefs. Ce sont des préférences personnelles de l’éditeur, pas une configuration projet partagée."
                ),
                MessageType.Info
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
                bool boolValue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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

            GUIStyle style = new GUIStyle(compactBoldLabelStyle);
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleLeft;

            EditorGUILayout.LabelField(GetLocalizedState(entry), style, GUILayout.Width(width));
        }

        private void DrawApplicationStateBadge(GameplayBalanceSheetApplicationState state, float width)
        {
            Color color = GetApplicationStateColor(state);

            GUIStyle style = new GUIStyle(compactBoldLabelStyle);
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleLeft;

            EditorGUILayout.LabelField(GetLocalizedApplicationState(state), style, GUILayout.Width(width));
        }

        private void DrawColoredLabel(string text, Color color, bool bold)
        {
            GUIStyle style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label);
            style.normal.textColor = color;
            style.wordWrap = true;

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
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.normal.textColor = color;

            EditorGUILayout.LabelField($"{label}: {value}", style, GUILayout.Width(115f));
        }

        private void DrawStatusExplanation(string label, string explanation, Color color)
        {
            EditorGUILayout.BeginVertical("box");

            DrawColoredLabel(label, color, true);
            EditorGUILayout.LabelField(explanation, descriptionStyle);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
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

        private void SelectSheet(GameplayBalanceSheetProfile sheet)
        {
            selectedSheet = sheet;
            selectedSectionIndex = 0;
            scannedProperties.Clear();
            ResetSectionReorderList();
            GUI.FocusControl(null);
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

        private void DuplicateSelectedSheetForDesigner()
        {
            if (selectedSheet == null)
            {
                return;
            }

            GameplayBalanceSheetProfile duplicate =
                GameplayBalanceSheetUtility.DuplicateSheetForDesigner(
                    selectedSheet,
                    duplicateVariantName,
                    defaultSheetsFolder
                );

            RefreshSheets();

            if (duplicate != null)
            {
                selectedSheet = duplicate;
                currentPage = MainPage.Designer;
            }
        }

        private void ConfirmAndDeleteSheet(bool designerVariantOnly)
        {
            if (selectedSheet == null)
            {
                return;
            }

            if (designerVariantOnly && !selectedSheet.isDesignerVariant)
            {
                EditorUtility.DisplayDialog(
                    T("Delete Blocked", "Suppression bloquée"),
                    T(
                        "Designers can only delete designer variants.",
                        "Les GD peuvent seulement supprimer les variantes GD."
                    ),
                    "OK"
                );

                return;
            }

            string title = selectedSheet.DisplayTitle;

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
            if (selectedSheet == null || !selectedSheet.HasSupportedTarget)
            {
                return;
            }

            scannedProperties = GameplayBalanceSheetUtility.ScanTarget(selectedSheet.target);
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
                T("Restore Reference", "Restaurer la référence"),
                T(
                    "This will restore all entries to their baseline values.",
                    "Tous les paramètres vont revenir à leur valeur de référence."
                ),
                T("Restore", "Restaurer"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.RevertAllToBaseline(selectedSheet);
        }

        private void ConfirmAndApplySheetAsReference(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                T("Apply As Reference", "Appliquer comme référence"),
                T(
                    "This will apply this sheet to the target and turn this sheet into a new reference. Use this only when the team has validated the gameplay result.",
                    "Cela va appliquer cette fiche à la cible et transformer cette fiche en nouvelle référence. À utiliser uniquement quand l’équipe a validé le résultat gameplay."
                ),
                T("Apply As Reference", "Appliquer comme référence"),
                T("Cancel", "Annuler")
            );

            if (!confirmed)
            {
                return;
            }

            GameplayBalanceSheetUtility.ApplySheetAsNewReference(sheet);

            RefreshSheets();
            selectedSheet = sheet;
        }

        private void SavePreferences()
        {
            EditorPrefs.SetInt(LanguagePrefKey, (int)language);
            EditorPrefs.SetString(SheetsFolderPrefKey, defaultSheetsFolder);
            EditorPrefs.SetBool(ShowTechnicalInfoPrefKey, showTechnicalInfo);
            EditorPrefs.SetBool(ShowHelpBoxesPrefKey, showHelpBoxes);
            EditorPrefs.SetBool(ShowEntryDescriptionsPrefKey, showEntryDescriptions);
            EditorPrefs.SetBool(ShowDetailedSidebarPrefKey, showDetailedSidebar);
            EditorPrefs.SetBool(AllowReferenceEditingPrefKey, allowReferenceSheetEditing);

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

                if (entry != null && entry.selected)
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
                T("Restore Section Selection", "Restaurer la sélection de section"),
                T(
                    "Selected entries in this section will restore their reference value.",
                    "Les paramètres sélectionnés dans cette section vont retrouver leur valeur de référence."
                ),
                T("Restore", "Restaurer"),
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

        private void CopyCurrentToTestInSection(GameplayBalanceSheetSection section)
        {
            if (section == null || section.entries == null || !CanDesignerEditSelectedSheet())
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry != null && entry.selected)
                {
                    entry.CopyCurrentToTest();
                }
            }

            EditorUtility.SetDirty(selectedSheet);
        }

        private void CopyBaselineToTestInSection(GameplayBalanceSheetSection section)
        {
            if (section == null || section.entries == null || !CanDesignerEditSelectedSheet())
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
            {
                GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                if (entry != null && entry.selected)
                {
                    entry.CopyBaselineToTest();
                }
            }

            EditorUtility.SetDirty(selectedSheet);
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

        private List<GameplayBalanceSheetProfile> GetReferenceSheets()
        {
            List<GameplayBalanceSheetProfile> references = new List<GameplayBalanceSheetProfile>();

            for (int i = 0; i < sheets.Count; i++)
            {
                GameplayBalanceSheetProfile sheet = sheets[i];

                if (sheet != null && !sheet.isDesignerVariant)
                {
                    references.Add(sheet);
                }
            }

            references.Sort((left, right) =>
                string.Compare(left.DisplayTitle, right.DisplayTitle, StringComparison.Ordinal)
            );

            return references;
        }

        private List<GameplayBalanceSheetProfile> GetVariantsForReference(
            GameplayBalanceSheetProfile referenceSheet
        )
        {
            List<GameplayBalanceSheetProfile> variants = new List<GameplayBalanceSheetProfile>();

            if (referenceSheet == null)
            {
                return variants;
            }

            for (int i = 0; i < sheets.Count; i++)
            {
                GameplayBalanceSheetProfile sheet = sheets[i];

                if (sheet != null &&
                    sheet.isDesignerVariant &&
                    sheet.sourceSheet == referenceSheet)
                {
                    variants.Add(sheet);
                }
            }

            variants.Sort((left, right) =>
                string.Compare(left.VariantSuffix, right.VariantSuffix, StringComparison.Ordinal)
            );

            return variants;
        }

        private bool ShouldShowReferenceGroup(GameplayBalanceSheetProfile referenceSheet)
        {
            if (referenceSheet == null)
            {
                return false;
            }

            if (MatchesSheetSearch(referenceSheet))
            {
                return true;
            }

            List<GameplayBalanceSheetProfile> variants = GetVariantsForReference(referenceSheet);

            for (int i = 0; i < variants.Count; i++)
            {
                if (MatchesSheetSearch(variants[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private List<TargetGroup> BuildTargetGroups()
        {
            List<TargetGroup> groups = new List<TargetGroup>();
            List<GameplayBalanceSheetProfile> references = GetReferenceSheets();

            for (int i = 0; i < references.Count; i++)
            {
                GameplayBalanceSheetProfile reference = references[i];

                if (reference == null)
                {
                    continue;
                }

                TargetGroup group = FindTargetGroup(groups, reference.target);

                if (group == null)
                {
                    group = new TargetGroup(reference.target);
                    groups.Add(group);
                }

                group.references.Add(reference);
            }

            groups.Sort((left, right) =>
            {
                string leftName = left.target != null ? left.target.name : string.Empty;
                string rightName = right.target != null ? right.target.name : string.Empty;
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });

            return groups;
        }

        private TargetGroup FindTargetGroup(List<TargetGroup> groups, UnityEngine.Object target)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].target == target)
                {
                    return groups[i];
                }
            }

            return null;
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
            if (sheet == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sheetSearchText))
            {
                return true;
            }

            string search = sheetSearchText.ToLowerInvariant();

            return SafeLower(sheet.DisplayTitle).Contains(search) ||
                   SafeLower(sheet.sheetDescription).Contains(search) ||
                   SafeLower(sheet.name).Contains(search) ||
                   SafeLower(sheet.target != null ? sheet.target.name : string.Empty).Contains(search);
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

        private bool CanDesignerEditSelectedSheet()
        {
            if (selectedSheet == null)
            {
                return false;
            }

            if (selectedSheet.isDesignerVariant)
            {
                return true;
            }

            if (allowReferenceSheetEditing)
            {
                return true;
            }

            return !selectedSheet.lockSourceSheetForDesigner;
        }

        private static UnityEngine.Object NormalizeTarget(UnityEngine.Object target)
        {
            return target is Component component
                ? component.gameObject
                : target;
        }

        private static bool IsSupportedTarget(UnityEngine.Object target)
        {
            return target == null || target is GameObject || target is ScriptableObject;
        }

        private bool GetFoldout(Dictionary<string, bool> foldouts, string key, bool defaultValue)
        {
            if (!foldouts.ContainsKey(key))
            {
                foldouts.Add(key, defaultValue);
            }

            return foldouts[key];
        }

        private void SetFoldout(Dictionary<string, bool> foldouts, string key, bool value)
        {
            if (foldouts.ContainsKey(key))
            {
                foldouts[key] = value;
            }
            else
            {
                foldouts.Add(key, value);
            }
        }

        private string GetTargetKey(UnityEngine.Object target, int index)
        {
            return target != null
                ? "target_" + target.GetInstanceID()
                : "target_null_" + index;
        }

        private string GetSheetKey(GameplayBalanceSheetProfile sheet)
        {
            if (sheet == null)
            {
                return "null";
            }

            string path = AssetDatabase.GetAssetPath(sheet);

            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return "sheet_" + sheet.GetInstanceID();
        }

        private Color GetEntryPanelColor(GameplayBalanceSheetEntry entry)
        {
            switch (entry.State)
            {
                case GameplayBalanceSheetEntryState.Missing:
                    return new Color(1f, 0.55f, 0.55f);

                case GameplayBalanceSheetEntryState.Pending:
                    return new Color(0.55f, 0.75f, 1f);

                case GameplayBalanceSheetEntryState.Modified:
                    return new Color(0.68f, 0.58f, 1f);

                case GameplayBalanceSheetEntryState.Baseline:
                    return Color.white;

                default:
                    return Color.white;
            }
        }

        private Color GetEntryStateColor(GameplayBalanceSheetEntry entry)
        {
            switch (entry.State)
            {
                case GameplayBalanceSheetEntryState.Missing:
                    return new Color(1f, 0.35f, 0.35f);

                case GameplayBalanceSheetEntryState.Pending:
                    return new Color(0.35f, 0.65f, 1f);

                case GameplayBalanceSheetEntryState.Modified:
                    return new Color(0.6f, 0.45f, 1f);

                case GameplayBalanceSheetEntryState.Baseline:
                    return new Color(0.3f, 0.85f, 0.45f);

                default:
                    return Color.white;
            }
        }

        private Color GetApplicationStateColor(GameplayBalanceSheetApplicationState state)
        {
            switch (state)
            {
                case GameplayBalanceSheetApplicationState.Broken:
                    return new Color(1f, 0.35f, 0.35f);

                case GameplayBalanceSheetApplicationState.Partial:
                    return new Color(1f, 0.65f, 0.25f);

                case GameplayBalanceSheetApplicationState.NotApplied:
                    return new Color(0.35f, 0.65f, 1f);

                case GameplayBalanceSheetApplicationState.Modified:
                    return new Color(0.6f, 0.45f, 1f);

                case GameplayBalanceSheetApplicationState.Reference:
                    return new Color(0.3f, 0.85f, 0.45f);

                default:
                    return Color.white;
            }
        }

        private string GetLocalizedState(GameplayBalanceSheetEntry entry)
        {
            switch (entry.State)
            {
                case GameplayBalanceSheetEntryState.Missing:
                    return T("Missing", "Introuvable");

                case GameplayBalanceSheetEntryState.Pending:
                    return T("Pending", "Test en attente");

                case GameplayBalanceSheetEntryState.Modified:
                    return T("Modified", "Modifié");

                case GameplayBalanceSheetEntryState.Baseline:
                    return T("Reference", "Référence");

                default:
                    return T("Unknown", "Inconnu");
            }
        }

        private string GetLocalizedApplicationState(GameplayBalanceSheetApplicationState state)
        {
            switch (state)
            {
                case GameplayBalanceSheetApplicationState.Broken:
                    return T("Broken", "Cassée");

                case GameplayBalanceSheetApplicationState.Partial:
                    return T("Partial", "Partielle");

                case GameplayBalanceSheetApplicationState.NotApplied:
                    return T("Not Applied", "Non appliquée");

                case GameplayBalanceSheetApplicationState.Modified:
                    return T("Modified", "Modifiée");

                case GameplayBalanceSheetApplicationState.Reference:
                    return T("Reference", "Référence");

                default:
                    return T("Unknown", "Inconnu");
            }
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

                    switch (entry.State)
                    {
                        case GameplayBalanceSheetEntryState.Missing:
                            stats.missing++;
                            break;

                        case GameplayBalanceSheetEntryState.Pending:
                            stats.pending++;
                            break;

                        case GameplayBalanceSheetEntryState.Modified:
                            stats.modified++;
                            break;

                        case GameplayBalanceSheetEntryState.Baseline:
                            stats.baseline++;
                            break;
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

        private sealed class TargetGroup
        {
            public readonly UnityEngine.Object target;
            public readonly List<GameplayBalanceSheetProfile> references = new List<GameplayBalanceSheetProfile>();

            public TargetGroup(UnityEngine.Object target)
            {
                this.target = target;
            }
        }

        private struct SheetStats
        {
            public int baseline;
            public int pending;
            public int modified;
            public int missing;
        }
    }
}
