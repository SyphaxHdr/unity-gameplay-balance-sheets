using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayBalanceSheets
{
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
        public GameObject targetRoot;

        [Header("Sections")]
        public List<GameplayBalanceSheetSection> sections = new List<GameplayBalanceSheetSection>
        {
            new GameplayBalanceSheetSection("General")
        };

        public bool HasTarget => targetRoot != null;

        public GameplayBalanceSheetSection GetOrCreateSection(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                sectionName = "General";
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i] != null && sections[i].sectionName == sectionName)
                {
                    return sections[i];
                }
            }

            GameplayBalanceSheetSection section = new GameplayBalanceSheetSection(sectionName);
            sections.Add(section);
            return section;
        }

        public bool ContainsEntry(string entryId)
        {
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                GameplayBalanceSheetSection section = sections[sectionIndex];

                if (section == null)
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

        public bool HasPendingTestValue =>
            !isMissing &&
            !StringEquals(testValue, currentValue);

        public bool HasChangedFromBaseline =>
            !isMissing &&
            !StringEquals(currentValue, baselineValue);

        public string StateLabel
        {
            get
            {
                if (isMissing)
                {
                    return "Missing";
                }

                if (HasPendingTestValue)
                {
                    return "Pending";
                }

                if (HasChangedFromBaseline)
                {
                    return "Changed";
                }

                return "OK";
            }
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