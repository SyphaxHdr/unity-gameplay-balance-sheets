# Gameplay Balance Sheets for Unity

**Gameplay Balance Sheets** is a Unity Editor tool for creating, testing and validating gameplay balancing sheets directly inside Unity.

The tool works like **tabletop RPG character sheets**, but for gameplay values.

In an RPG, a character sheet stores a character’s stats.
Here, a balance sheet stores gameplay values that can be applied to a Unity target such as a player prefab, enemy prefab, boss, camera, UI controller or scene object.

---

## Core Workflow

```txt
Developer creates a reference sheet.
Designer duplicates it into variants.
Designer tests different gameplay values.
The team validates the best variant as the new reference.
```

Example:

```txt
Target: PFB_Player

Reference Sheet:
  Player Balance Sheet

Designer Variants:
  Player Balance Sheet - Test Fast Movement
  Player Balance Sheet - Test Heavy Combat
  Player Balance Sheet - Test Short Dash
```

---

## Main Concepts

### Reference Sheet

A **Reference Sheet** is the base sheet created by a developer.

It defines:

* the Unity target
* the gameplay sections
* the exposed fields
* the baseline values

Reference sheets are locked for designers by default.

### Designer Variant

A **Designer Variant** is an editable copy of a reference sheet.

Designers use variants to test different gameplay feelings without modifying the original reference sheet.

### Target

A **Target** is the prefab or scene object affected by a sheet.

Examples:

```txt
PFB_Player
PFB_Boss_01
CameraRig
EnemySpawner
UI_CombatHUD
```

---

## Value Columns

| Column     | Meaning                                               |
| ---------- | ----------------------------------------------------- |
| `Baseline` | Reference value from the sheet.                       |
| `Current`  | Value currently written on the Unity target.          |
| `Test`     | Value prepared in the selected sheet before applying. |

---

## Entry States

| State       | Meaning                                             |
| ----------- | --------------------------------------------------- |
| `Reference` | Target matches the baseline value.                  |
| `Pending`   | `Test` is different from `Current`; ready to apply. |
| `Modified`  | Target uses a value different from the reference.   |
| `Missing`   | The field no longer exists on the target.           |

Missing fields do not block the rest of the sheet. Valid fields can still be applied.

---

## Editor Pages

### Designer

Used by game designers to:

* create variants
* edit `Test` values
* apply selected values
* restore values to the reference
* delete designer variants

### Sheet Application

Used to compare and validate sheets.

Sheets are displayed as:

```txt
Target
  Reference Sheet
    Designer Variants
```

The action **Apply As Reference** applies a validated sheet and turns it into the new reference.

### Developer

Used by developers to:

* create reference sheets
* assign targets
* create sections
* scan targets
* expose serialized fields
* write parameter descriptions

### Settings

Contains local editor preferences such as language, default folder and display options.

These preferences are stored with Unity `EditorPrefs`, not in Git.

---

## Supported Field Types

The scanner supports serialized MonoBehaviour fields of these types:

```txt
int
float
bool
string
enum
```

---

## Installation

Use Unity Package Manager:

```txt
Window > Package Manager > Add package from git URL
```

Recommended repository URL format:

```txt
https://github.com/<owner>/unity-gameplay-balance-sheets.git
```

---

## Recommended Repository Name

Recommended GitHub repository name:

```txt
unity-gameplay-balance-sheets
```

Unity package name:

```txt
com.gameplaybalancesheets.unity
```

Display name:

```txt
Gameplay Balance Sheets
```

---

## Documentation

Full user guides are available here:

```txt
Documentation~/GameplayBalanceSheets_UserGuide.md
Documentation~/GameplayBalanceSheets_UserGuide_FR.md
```

---

## License

To be defined.