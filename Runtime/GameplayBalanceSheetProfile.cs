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

        [Tooltip("Other GameObjects or ScriptableObject assets managed by this same sheet (e.g. the 3 attacks of a combo). The same field can be added once per target.")]
        public List<UnityEngine.Object> additionalTargets = new List<UnityEngine.Object>();

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

        public bool HasAdditionalTargets
        {
            get
            {
                if (additionalTargets == null)
                {
                    return false;
                }

                for (int i = 0; i < additionalTargets.Count; i++)
                {
                    if (additionalTargets[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Main target first, then every additional target (no null, no duplicate, only GameObjects
        /// and ScriptableObjects).
        /// </summary>
        public List<UnityEngine.Object> GetAllTargets()
        {
            List<UnityEngine.Object> targets = new List<UnityEngine.Object>();

            if (IsSupportedTargetObject(target))
            {
                targets.Add(target);
            }

            if (additionalTargets == null)
            {
                return targets;
            }

            for (int i = 0; i < additionalTargets.Count; i++)
            {
                UnityEngine.Object additionalTarget = additionalTargets[i];

                if (IsSupportedTargetObject(additionalTarget) && !targets.Contains(additionalTarget))
                {
                    targets.Add(additionalTarget);
                }
            }

            return targets;
        }

        /// <summary>
        /// Adds an extra target to this sheet. Returns false if it is null, unsupported, the main
        /// target, or already listed.
        /// </summary>
        public bool AddAdditionalTarget(UnityEngine.Object newTarget)
        {
            if (!IsSupportedTargetObject(newTarget) || newTarget == target)
            {
                return false;
            }

            if (additionalTargets == null)
            {
                additionalTargets = new List<UnityEngine.Object>();
            }

            if (additionalTargets.Contains(newTarget))
            {
                return false;
            }

            additionalTargets.Add(newTarget);
            return true;
        }

        /// <summary>
        /// The object an entry reads and writes: its own target when it was added from an additional
        /// target, otherwise the sheet's main target. Returns null when the entry's own target was
        /// deleted (the entry must then be reported as missing, never applied to the main target).
        /// </summary>
        public UnityEngine.Object ResolveEntryTarget(GameplayBalanceSheetEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (entry.hasTargetOverride)
            {
                return entry.targetOverride != null ? entry.targetOverride : null;
            }

            return target;
        }

        public static bool IsSupportedTargetObject(UnityEngine.Object candidate)
        {
            return candidate != null && (candidate is GameObject || candidate is ScriptableObject);
        }

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
                additionalTargets = source.additionalTargets != null
                    ? new List<UnityEngine.Object>(source.additionalTargets)
                    : new List<UnityEngine.Object>();
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
            if (additionalTargets == null)
            {
                additionalTargets = new List<UnityEngine.Object>();
            }

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

        [Tooltip("True when this entry belongs to one of the sheet's additional targets instead of its main target.")]
        public bool hasTargetOverride;

        [Tooltip("The additional target this entry reads and writes (only used when hasTargetOverride is true).")]
        public UnityEngine.Object targetOverride;

        [Header("Values")]
        public string baselineValue;
        public string currentValue;
        public string testValue;
        public bool isMissing;

        public string Id => BuildId(
            componentPath,
            componentAssemblyQualifiedTypeName,
            componentIndex,
            propertyPath,
            hasTargetOverride,
            targetOverride
        );

        /// <summary>
        /// Identity of a field inside a sheet. Entries of the main target keep the historical format
        /// (existing sheets stay valid); entries of an additional target also carry that target, so the
        /// same field can be added once per target (attack 1, attack 2, attack 3...).
        /// </summary>
        public static string BuildId(
            string componentPath,
            string componentAssemblyQualifiedTypeName,
            int componentIndex,
            string propertyPath,
            bool hasTargetOverride,
            UnityEngine.Object targetOverride
        )
        {
            string baseId = $"{componentPath}|{componentAssemblyQualifiedTypeName}|{componentIndex}|{propertyPath}";

            if (!hasTargetOverride)
            {
                return baseId;
            }

            string targetKey = targetOverride != null
                ? targetOverride.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "missing";

            return $"{baseId}|@{targetKey}";
        }

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
