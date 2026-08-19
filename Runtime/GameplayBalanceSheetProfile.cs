using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameplayBalanceSheets
{
    public enum GameplayBalanceSheetEntryState
    {
        Baseline,
        Pending,
        Modified,
        Missing
    }

    public enum GameplayBalanceSheetApplicationState
    {
        Reference,
        Modified,
        Partial,
        NotApplied,
        Broken
    }

    [CreateAssetMenu(
        fileName = "GD_BalanceSheet",
        menuName = "Gameplay Balance Sheets/Balance Sheet"
    )]
    public sealed class GameplayBalanceSheetProfile : ScriptableObject
    {
        [Header("Sheet")]
        public string sheetTitle = "New Balance Sheet";

        [TextArea(2, 6)]
        public string sheetDescription;

        [Header("Target")]
        [FormerlySerializedAs("targetRoot")]
        [Tooltip("GameObject root or ScriptableObject asset whose serialized gameplay values are managed by this sheet.")]
        public UnityEngine.Object target;

        [Obsolete("Use target instead. This compatibility property only exposes GameObject targets.")]
        public GameObject targetRoot
        {
            get => target as GameObject;
            set => target = value;
        }

        [Header("Designer Variant")]
        public bool isDesignerVariant;

        public GameplayBalanceSheetProfile sourceSheet;

        public string variantName = "New Variant";

        [Tooltip("If enabled, designers should not directly edit the reference sheet. They must duplicate it and edit the variant.")]
        public bool lockSourceSheetForDesigner = true;

        [Header("Sections")]
        public List<GameplayBalanceSheetSection> sections = new List<GameplayBalanceSheetSection>
        {
            new GameplayBalanceSheetSection("General")
        };

        public bool HasTarget => target != null;

        public bool HasSupportedTarget => target is GameObject || target is ScriptableObject;

        public bool IsReferenceSheet => !isDesignerVariant;

        public bool IsDesignerVariant => isDesignerVariant;

        public string ReferenceTitle
        {
            get
            {
                if (sourceSheet != null && !string.IsNullOrWhiteSpace(sourceSheet.sheetTitle))
                {
                    return sourceSheet.sheetTitle;
                }

                return string.IsNullOrWhiteSpace(sheetTitle)
                    ? name
                    : sheetTitle;
            }
        }

        public string DisplayTitle
        {
            get
            {
                if (!isDesignerVariant)
                {
                    return string.IsNullOrWhiteSpace(sheetTitle)
                        ? name
                        : sheetTitle;
                }

                string baseTitle = sourceSheet != null && !string.IsNullOrWhiteSpace(sourceSheet.sheetTitle)
                    ? sourceSheet.sheetTitle
                    : sheetTitle;

                if (string.IsNullOrWhiteSpace(baseTitle))
                {
                    baseTitle = name;
                }

                string cleanVariantName = string.IsNullOrWhiteSpace(variantName)
                    ? "Variant"
                    : variantName.Trim();

                return $"{baseTitle} - {cleanVariantName}";
            }
        }

        public string VariantSuffix
        {
            get
            {
                return string.IsNullOrWhiteSpace(variantName)
                    ? "Variant"
                    : variantName.Trim();
            }
        }

        public GameplayBalanceSheetSection GetOrCreateSection(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                sectionName = "General";
            }

            EnsureSections();

            for (int i = 0; i < sections.Count; i++)
            {
                GameplayBalanceSheetSection section = sections[i];

                if (section != null && section.sectionName == sectionName)
                {
                    return section;
                }
            }

            GameplayBalanceSheetSection newSection = new GameplayBalanceSheetSection(sectionName);
            sections.Add(newSection);

            return newSection;
        }

        public bool ContainsEntry(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return false;
            }

            EnsureSections();

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = sections[sectionIndex];

                if (section == null || section.entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.entries.Count; entryIndex++)
                {
                    GameplayBalanceSheetEntry entry = section.entries[entryIndex];

                    if (entry != null && entry.Id == entryId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int GetSectionCount()
        {
            EnsureSections();
            return sections.Count;
        }

        public int GetEntryCount()
        {
            int count = 0;

            ForEachEntry(_ => count++);

            return count;
        }

        public int GetEntryCountByState(GameplayBalanceSheetEntryState state)
        {
            int count = 0;

            ForEachEntry(entry =>
            {
                if (entry != null && entry.State == state)
                {
                    count++;
                }
            });

            return count;
        }

        public void ConfigureAsDesignerVariant(
            GameplayBalanceSheetProfile source,
            string newVariantName
        )
        {
            isDesignerVariant = true;
            sourceSheet = source;

            if (source != null)
            {
                sheetTitle = source.sheetTitle;
                sheetDescription = source.sheetDescription;
                target = source.target;
                lockSourceSheetForDesigner = source.lockSourceSheetForDesigner;
            }

            variantName = string.IsNullOrWhiteSpace(newVariantName)
                ? "New Variant"
                : newVariantName.Trim();
        }

        public void ConfigureAsReferenceSheet()
        {
            isDesignerVariant = false;
            sourceSheet = null;
            variantName = string.Empty;
        }

        public void ForEachEntry(Action<GameplayBalanceSheetEntry> action)
        {
            if (action == null)
            {
                return;
            }

            EnsureSections();

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = sections[sectionIndex];

                if (section == null || section.entries == null)
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

        public void EnsureSections()
        {
            if (sections == null)
            {
                sections = new List<GameplayBalanceSheetSection>();
            }

            if (sections.Count == 0)
            {
                sections.Add(new GameplayBalanceSheetSection("General"));
            }
        }

        private void OnValidate()
        {
            EnsureSections();

            if (isDesignerVariant && sourceSheet == this)
            {
                sourceSheet = null;
            }

            if (!isDesignerVariant)
            {
                sourceSheet = null;
                variantName = string.Empty;
            }

            if (isDesignerVariant && string.IsNullOrWhiteSpace(variantName))
            {
                variantName = "New Variant";
            }
        }
    }

    [Serializable]
    public sealed class GameplayBalanceSheetSection
    {
        public bool expanded = true;
        public string sectionName = "General";
        public List<GameplayBalanceSheetEntry> entries = new List<GameplayBalanceSheetEntry>();

        public GameplayBalanceSheetSection()
        {
        }

        public GameplayBalanceSheetSection(string sectionName)
        {
            this.sectionName = string.IsNullOrWhiteSpace(sectionName)
                ? "General"
                : sectionName;
        }
    }

    [Serializable]
    public sealed class GameplayBalanceSheetEntry
    {
        [Header("UI")]
        public bool selected;
        public string displayName;

        [TextArea(2, 5)]
        public string description;

        [Header("Target Property")]
        public string componentPath;
        public string componentName;
        public string componentTypeName;
        public string componentAssemblyQualifiedTypeName;
        public int componentIndex;
        public string propertyPath;
        public string propertyTypeName;
        public List<string> enumNames = new List<string>();

        [Header("Values")]
        public string baselineValue;
        public string currentValue;
        public string testValue;
        public bool isMissing;

        public string Id =>
            $"{componentPath}|{componentAssemblyQualifiedTypeName}|{componentIndex}|{propertyPath}";

        public GameplayBalanceSheetEntryState State
        {
            get
            {
                if (isMissing)
                {
                    return GameplayBalanceSheetEntryState.Missing;
                }

                if (!StringEquals(testValue, currentValue))
                {
                    return GameplayBalanceSheetEntryState.Pending;
                }

                if (StringEquals(currentValue, baselineValue))
                {
                    return GameplayBalanceSheetEntryState.Baseline;
                }

                return GameplayBalanceSheetEntryState.Modified;
            }
        }

        public bool HasPendingTestValue =>
            State == GameplayBalanceSheetEntryState.Pending;

        public bool IsMissing =>
            State == GameplayBalanceSheetEntryState.Missing;

        public bool IsOnBaseline =>
            State == GameplayBalanceSheetEntryState.Baseline;

        public bool IsModifiedOnTarget =>
            State == GameplayBalanceSheetEntryState.Modified;

        public bool IsAppliedToTarget =>
            State == GameplayBalanceSheetEntryState.Modified ||
            State == GameplayBalanceSheetEntryState.Baseline;

        public bool HasDifferentTestFromBaseline =>
            !StringEquals(testValue, baselineValue);

        public string StateLabel
        {
            get
            {
                switch (State)
                {
                    case GameplayBalanceSheetEntryState.Missing:
                        return "Missing";

                    case GameplayBalanceSheetEntryState.Pending:
                        return "Pending";

                    case GameplayBalanceSheetEntryState.Modified:
                        return "Modified";

                    case GameplayBalanceSheetEntryState.Baseline:
                        return "Baseline";

                    default:
                        return "Unknown";
                }
            }
        }

        public void CopyCurrentToTest()
        {
            if (isMissing)
            {
                return;
            }

            testValue = currentValue;
        }

        public void CopyBaselineToTest()
        {
            if (isMissing)
            {
                return;
            }

            testValue = baselineValue;
        }

        public void SetCurrentAsBaseline()
        {
            if (isMissing)
            {
                return;
            }

            baselineValue = currentValue;
            testValue = currentValue;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(
                left ?? string.Empty,
                right ?? string.Empty,
                StringComparison.Ordinal
            );
        }
    }
}
