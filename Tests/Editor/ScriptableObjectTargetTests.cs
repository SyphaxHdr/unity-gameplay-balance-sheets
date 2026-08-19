using System.Linq;
using GameplayBalanceSheets.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameplayBalanceSheets.Editor.Tests
{
    public sealed class ScriptableObjectTargetTests
    {
        private TestBalanceAsset target;
        private GameplayBalanceSheetProfile sheet;

        [SetUp]
        public void SetUp()
        {
            target = ScriptableObject.CreateInstance<TestBalanceAsset>();
            sheet = ScriptableObject.CreateInstance<GameplayBalanceSheetProfile>();
            sheet.target = target;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void ScanTarget_ReturnsSupportedSerializedScriptableObjectFields()
        {
            var scannedProperties = GameplayBalanceSheetUtility.ScanTarget(target);

            string[] propertyPaths = scannedProperties
                .Select(property => property.propertyPath)
                .ToArray();

            CollectionAssert.Contains(propertyPaths, nameof(TestBalanceAsset.maxHealth));
            CollectionAssert.Contains(propertyPaths, nameof(TestBalanceAsset.movementSpeed));
            CollectionAssert.Contains(propertyPaths, nameof(TestBalanceAsset.canDash));
            CollectionAssert.Contains(propertyPaths, nameof(TestBalanceAsset.displayName));
            CollectionAssert.Contains(propertyPaths, nameof(TestBalanceAsset.difficulty));
            CollectionAssert.Contains(propertyPaths, "cooldown");
            CollectionAssert.DoesNotContain(propertyPaths, nameof(TestBalanceAsset.spawnOffset));
            CollectionAssert.DoesNotContain(propertyPaths, "m_Script");

            ScannedBalanceProperty health = scannedProperties.Single(
                property => property.propertyPath == nameof(TestBalanceAsset.maxHealth)
            );

            Assert.That(health.componentName, Is.EqualTo(nameof(TestBalanceAsset)));
            Assert.That(health.currentValue, Is.EqualTo("120"));
            Assert.That(health.componentPath, Is.EqualTo("."));
        }

        [Test]
        public void ApplyAndRefresh_RoundTripScriptableObjectValue()
        {
            var scannedProperties = GameplayBalanceSheetUtility.ScanTarget(target);
            ScannedBalanceProperty health = scannedProperties.Single(
                property => property.propertyPath == nameof(TestBalanceAsset.maxHealth)
            );
            health.selected = true;

            GameplayBalanceSheetSection section = sheet.GetOrCreateSection("Stats");
            int addedCount = GameplayBalanceSheetUtility.AddSelectedScannedPropertiesToSection(
                sheet,
                section,
                scannedProperties
            );

            Assert.That(addedCount, Is.EqualTo(1));
            GameplayBalanceSheetEntry entry = section.entries.Single();

            entry.testValue = "275";

            Assert.That(
                GameplayBalanceSheetUtility.ApplyEntryTestValue(sheet, entry),
                Is.True
            );
            Assert.That(target.maxHealth, Is.EqualTo(275));
            Assert.That(entry.currentValue, Is.EqualTo("275"));

            target.maxHealth = 310;

            Assert.That(
                GameplayBalanceSheetUtility.RefreshCurrentValue(sheet, entry),
                Is.True
            );
            Assert.That(entry.currentValue, Is.EqualTo("310"));
            Assert.That(entry.testValue, Is.EqualTo("310"));
            Assert.That(entry.isMissing, Is.False);
        }

        [Test]
        public void ScanTarget_StillScansMonoBehavioursInGameObjectHierarchy()
        {
            GameObject root = new GameObject("Balance Root");

            try
            {
                TestBalanceBehaviour behaviour = root.AddComponent<TestBalanceBehaviour>();
                behaviour.damage = 42;

                var scannedProperties = GameplayBalanceSheetUtility.ScanTarget(root);
                ScannedBalanceProperty damage = scannedProperties.Single(
                    property => property.propertyPath == nameof(TestBalanceBehaviour.damage)
                );

                Assert.That(damage.currentValue, Is.EqualTo("42"));
                Assert.That(damage.componentName, Is.EqualTo(nameof(TestBalanceBehaviour)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    public enum TestDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public sealed class TestBalanceAsset : ScriptableObject
    {
        public int maxHealth = 120;
        public float movementSpeed = 4.5f;
        public bool canDash = true;
        public string displayName = "Ranger";
        public TestDifficulty difficulty = TestDifficulty.Hard;
        public Vector3 spawnOffset = Vector3.one;

        [SerializeField]
        private float cooldown = 0.75f;
    }

    public sealed class TestBalanceBehaviour : MonoBehaviour
    {
        public int damage = 10;
    }
}
