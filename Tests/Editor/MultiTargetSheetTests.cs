using System.Collections.Generic;
using System.Linq;
using GameplayBalanceSheets.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameplayBalanceSheets.Editor.Tests
{
    /// <summary>
    /// One sheet managing several targets of the same type (e.g. the 3 attacks of a combo).
    /// </summary>
    public sealed class MultiTargetSheetTests
    {
        private TestBalanceAsset attack1;
        private TestBalanceAsset attack2;
        private TestBalanceAsset attack3;
        private GameplayBalanceSheetProfile sheet;

        [SetUp]
        public void SetUp()
        {
            attack1 = CreateAsset("Attack_1", 10);
            attack2 = CreateAsset("Attack_2", 20);
            attack3 = CreateAsset("Attack_3", 30);

            sheet = ScriptableObject.CreateInstance<GameplayBalanceSheetProfile>();
            sheet.target = attack1;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sheet);

            if (attack1 != null) Object.DestroyImmediate(attack1);
            if (attack2 != null) Object.DestroyImmediate(attack2);
            if (attack3 != null) Object.DestroyImmediate(attack3);
        }

        [Test]
        public void AddAdditionalTarget_RejectsNullMainTargetAndDuplicates()
        {
            Assert.That(sheet.AddAdditionalTarget(null), Is.False);
            Assert.That(sheet.AddAdditionalTarget(attack1), Is.False, "the main target is not an additional target");
            Assert.That(sheet.AddAdditionalTarget(attack2), Is.True);
            Assert.That(sheet.AddAdditionalTarget(attack2), Is.False, "no duplicate");

            CollectionAssert.AreEqual(
                new Object[] { attack1, attack2 },
                sheet.GetAllTargets()
            );
            Assert.That(sheet.HasAdditionalTargets, Is.True);
        }

        [Test]
        public void SameField_CanBeAddedOncePerTarget()
        {
            sheet.AddAdditionalTarget(attack2);
            sheet.AddAdditionalTarget(attack3);

            GameplayBalanceSheetSection section = sheet.GetOrCreateSection("Combo");

            Assert.That(AddField(section, attack1, nameof(TestBalanceAsset.maxHealth)), Is.EqualTo(1));
            Assert.That(AddField(section, attack2, nameof(TestBalanceAsset.maxHealth)), Is.EqualTo(1));
            Assert.That(AddField(section, attack3, nameof(TestBalanceAsset.maxHealth)), Is.EqualTo(1));

            // A second time on the same target is still refused.
            Assert.That(AddField(section, attack2, nameof(TestBalanceAsset.maxHealth)), Is.EqualTo(0));

            Assert.That(section.entries.Count, Is.EqualTo(3));
            Assert.That(section.entries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(3));

            Assert.That(section.entries[0].hasTargetOverride, Is.False, "main target entries keep the old format");
            Assert.That(section.entries[1].targetOverride, Is.SameAs(attack2));
            Assert.That(section.entries[2].targetOverride, Is.SameAs(attack3));

            StringAssert.StartsWith("Attack_2 > ", section.entries[1].displayName);
        }

        [Test]
        public void ScannedPropertiesOfAnotherTarget_AreNotHiddenByMainTargetEntries()
        {
            sheet.AddAdditionalTarget(attack2);
            GameplayBalanceSheetSection section = sheet.GetOrCreateSection("Combo");
            AddField(section, attack1, nameof(TestBalanceAsset.maxHealth));

            ScannedBalanceProperty mainHealth = FindScanned(attack1, nameof(TestBalanceAsset.maxHealth));
            ScannedBalanceProperty otherHealth = FindScanned(attack2, nameof(TestBalanceAsset.maxHealth));

            Assert.That(sheet.ContainsEntry(mainHealth.id), Is.True);
            Assert.That(sheet.ContainsEntry(otherHealth.id), Is.False);
        }

        [Test]
        public void ApplyAndRefresh_EachEntryUsesItsOwnTarget()
        {
            sheet.AddAdditionalTarget(attack2);
            sheet.AddAdditionalTarget(attack3);

            GameplayBalanceSheetSection section = sheet.GetOrCreateSection("Combo");
            AddField(section, attack1, nameof(TestBalanceAsset.maxHealth));
            AddField(section, attack2, nameof(TestBalanceAsset.maxHealth));
            AddField(section, attack3, nameof(TestBalanceAsset.maxHealth));

            Assert.That(section.entries.Select(entry => entry.baselineValue), Is.EqualTo(new[] { "10", "20", "30" }));

            section.entries[0].testValue = "100";
            section.entries[1].testValue = "200";
            section.entries[2].testValue = "300";

            GameplayBalanceSheetUtility.ApplyAllValues(sheet);

            Assert.That(attack1.maxHealth, Is.EqualTo(100));
            Assert.That(attack2.maxHealth, Is.EqualTo(200));
            Assert.That(attack3.maxHealth, Is.EqualTo(300));

            attack2.maxHealth = 250;
            Assert.That(GameplayBalanceSheetUtility.RefreshCurrentValue(sheet, section.entries[1]), Is.True);
            Assert.That(section.entries[1].currentValue, Is.EqualTo("250"));
            Assert.That(section.entries[0].currentValue, Is.EqualTo("100"));
        }

        [Test]
        public void DeletedAdditionalTarget_IsReportedMissing_AndNeverAppliedToMainTarget()
        {
            sheet.AddAdditionalTarget(attack2);

            GameplayBalanceSheetSection section = sheet.GetOrCreateSection("Combo");
            AddField(section, attack2, nameof(TestBalanceAsset.maxHealth));
            GameplayBalanceSheetEntry entry = section.entries.Single();

            Object.DestroyImmediate(attack2);
            attack2 = null;

            entry.testValue = "999";

            Assert.That(GameplayBalanceSheetUtility.ApplyEntryTestValue(sheet, entry), Is.False);
            Assert.That(entry.isMissing, Is.True);
            Assert.That(attack1.maxHealth, Is.EqualTo(10), "the main target must stay untouched");
        }

        [Test]
        public void DesignerVariant_KeepsAdditionalTargets()
        {
            sheet.AddAdditionalTarget(attack2);

            GameplayBalanceSheetProfile variant = ScriptableObject.CreateInstance<GameplayBalanceSheetProfile>();

            try
            {
                variant.ConfigureAsDesignerVariant(sheet, "Test");

                CollectionAssert.AreEqual(sheet.GetAllTargets(), variant.GetAllTargets());
                Assert.That(variant.additionalTargets, Is.Not.SameAs(sheet.additionalTargets), "the list is copied, not shared");
            }
            finally
            {
                Object.DestroyImmediate(variant);
            }
        }

        [Test]
        public void MainTargetEntryId_KeepsTheHistoricalFormat()
        {
            GameplayBalanceSheetEntry entry = new GameplayBalanceSheetEntry
            {
                componentPath = ".",
                componentAssemblyQualifiedTypeName = "Type",
                componentIndex = 0,
                propertyPath = "maxHealth"
            };

            Assert.That(entry.Id, Is.EqualTo(".|Type|0|maxHealth"));
        }

        private static TestBalanceAsset CreateAsset(string assetName, int maxHealth)
        {
            TestBalanceAsset asset = ScriptableObject.CreateInstance<TestBalanceAsset>();
            asset.name = assetName;
            asset.maxHealth = maxHealth;
            return asset;
        }

        private ScannedBalanceProperty FindScanned(Object scanTarget, string propertyPath)
        {
            List<ScannedBalanceProperty> scanned = GameplayBalanceSheetUtility.ScanTarget(sheet, scanTarget);
            return scanned.Single(property => property.propertyPath == propertyPath);
        }

        private int AddField(GameplayBalanceSheetSection section, Object scanTarget, string propertyPath)
        {
            List<ScannedBalanceProperty> scanned = GameplayBalanceSheetUtility.ScanTarget(sheet, scanTarget);
            scanned.Single(property => property.propertyPath == propertyPath).selected = true;

            return GameplayBalanceSheetUtility.AddSelectedScannedPropertiesToSection(sheet, section, scanned);
        }
    }
}
