# Gameplay Balance Sheets for Unity — User Guide

## 1. Purpose of the Tool

**Gameplay Balance Sheets** is a Unity Editor tool designed to help teams create, duplicate, test, apply and validate gameplay balancing sheets directly inside Unity.

The tool works like **tabletop RPG character sheets**, but for gameplay values.

In a tabletop RPG, a character sheet stores the stats of a character.
In this tool, a balance sheet stores gameplay values that can be applied to a Unity target, such as:

* a player prefab
* an enemy prefab
* a boss prefab
* a camera object
* a scene object
* a UI controller
* any GameObject with serialized MonoBehaviour fields

The goal is to let developers expose useful gameplay values once, then let game designers create variants and test different gameplay feelings without manually browsing technical components in the Inspector.

---

## 2. Main Concept

The workflow is based on three important ideas:

### Reference Sheet

A **Reference Sheet** is the base sheet created by a developer.

It defines:

* the target object or prefab
* the gameplay sections
* the exposed parameters
* the baseline reference values

By default, designers should not directly edit a reference sheet.
They should duplicate it into a variant.

### Designer Variant

A **Designer Variant** is an editable copy of a reference sheet.

Example:

```txt
Reference Sheet:
Player Balance Sheet

Designer Variants:
Player Balance Sheet - Test Fast Movement
Player Balance Sheet - Test Heavy Combat
Player Balance Sheet - Test Short Dash
```

The designer only edits the variant suffix, for example:

```txt
Test Fast Movement
```

The base name stays linked to the reference sheet.

### Target

A **Target** is the Unity object or prefab affected by a sheet.

Example:

```txt
Target: PFB_Player
Reference Sheet: Player Balance Sheet
Variants:
  - Test Fast Movement
  - Test Heavy Combat
```

---

## 3. Value Columns

Each exposed parameter contains three main values.

### Baseline

`Baseline` is the reference value.

It represents the safe value coming from the reference sheet.

Example:

```txt
Horizontal Speed
Baseline: 5.2
```

### Current

`Current` is the value currently written on the Unity target.

It represents what the prefab or scene object is using right now.

Example:

```txt
Current: 5.2
```

### Test

`Test` is the value prepared in the selected sheet.

Changing `Test` does not immediately change the target.
The value is only written to the target when the sheet or selected parameters are applied.

Example:

```txt
Test: 7.0
```

---

## 4. Entry States

Each parameter has a state.

### Reference

The target currently matches the reference value.

```txt
Current == Baseline
Test == Current
```

This means the target is currently using the reference value.

### Pending

The Test value is different from Current.

```txt
Test != Current
```

This means the designer prepared a value, but it has not been applied yet.

### Modified

The target is using a value different from the reference.

```txt
Current == Test
Current != Baseline
```

This means the target has been modified compared to the reference.

### Missing

The field no longer exists on the target.

This can happen if:

* the script changed
* the field was renamed
* the component was removed
* the target hierarchy changed

A missing field does **not** block the application of the rest of the sheet.
Valid fields can still be applied.

---

## 5. Main Workflow

### Step 1 — Developer Creates a Reference Sheet

Open:

```txt
Tools > Gameplay Balance Sheets
```

Go to:

```txt
Configuration Dev
```

Click:

```txt
Create Reference Sheet
```

The developer then configures:

* the reference title
* the target root
* the description
* the sections
* the exposed gameplay fields

---

### Step 2 — Developer Creates Sections

Sections are used to organize parameters.

Examples:

```txt
Movement
Jump
Combat
Camera
AI
Economy
Boss Phase 1
Boss Phase 2
```

Sections can be reordered using drag and drop.

---

### Step 3 — Developer Scans the Target

In the Developer page, assign a `Target Root`, then click:

```txt
Scan Target
```

The tool scans supported serialized fields from MonoBehaviours.

Supported types:

```txt
int
float
bool
string
enum
```

The developer selects useful fields and adds them to a section.

---

### Step 4 — Designer Creates a Variant

Go to:

```txt
Équilibrage GD
```

Select a reference sheet, then create a variant.

Example:

```txt
Reference:
Player Balance Sheet

Variant suffix:
Test Fast Movement

Result:
Player Balance Sheet - Test Fast Movement
```

The designer can then edit the `Test` values in the variant.

---

### Step 5 — Designer Tests Values

The designer can apply:

* the whole sheet
* selected parameters
* selected parameters inside a section

Useful section actions:

```txt
Apply Selected
Current → Test
Baseline → Test
Restore Selected
```

### Current → Test

Copies the value currently on the target into the Test field.

Use this when a value was changed outside the tool and should become the new test value.

### Baseline → Test

Copies the reference value into the Test field.

Use this when preparing a rollback or comparison.

### Restore Selected

Restores selected entries back to their baseline values.

---

### Step 6 — Team Validates a Sheet

Go to:

```txt
Application des fiches
```

This page displays sheets as:

```txt
Target
  Reference Sheet
    Designer Variants
```

Example:

```txt
Target: PFB_Player
  Player Balance Sheet
    Test Fast Movement
    Test Heavy Combat
```

When the team decides that a sheet gives the best gameplay feeling, click:

```txt
Apply As Reference
```

This action:

1. applies the selected sheet to the target
2. sets its current values as baseline
3. turns the selected sheet into a new reference sheet

Use this only after team validation.

---

## 6. Designer Page

The Designer page is used to:

* read reference sheets
* create variants
* edit variant test values
* apply selected values
* restore selected values
* delete designer variants

A designer can delete a designer variant, but should not delete reference sheets from the Designer page.

Reference sheets are locked by default.

---

## 7. Developer Page

The Developer page is used to:

* create reference sheets
* configure target roots
* write sheet descriptions
* create and reorder sections
* scan targets
* add useful fields
* remove fields from a sheet
* write descriptions for parameters

The Developer page is **not** meant for gameplay testing.

Gameplay testing should happen from the Designer page.
Final validation should happen from the Sheet Application page.

---

## 8. Sheet Application Page

The Sheet Application page is used to compare and validate sheets.

It is organized as:

```txt
Target
  Reference Sheet
    Designer Variants
```

Each sheet displays:

* sheet name
* application state
* number of sections
* number of parameters

Available actions:

```txt
Apply As Reference
Refresh
Open in Designer
Ping Target
```

### Apply As Reference

This is a validation action.

It should only be used when the team has tested a sheet and decided it should become the new reference.

This action is stronger than a normal apply.

---

## 9. Settings

The Settings page contains personal editor preferences.

### Language

The tool supports:

```txt
English
French
```

The selected language is saved locally using Unity `EditorPrefs`.

This means:

* it is stored on the local machine
* it is not written into the Git repository
* it does not need to be added to `.gitignore`

### Default Balance Sheets Folder

This is the default folder used when creating new balance sheet assets.

Example:

```txt
Assets/GameplayBalanceSheets/Sheets
```

The created `.asset` files are project data.

They should usually be committed to Git because they contain the actual balancing sheets used by the team.

### Allow Designers to Edit Reference Sheets

Recommended value:

```txt
Disabled
```

When disabled, designers cannot directly edit reference sheets.
They must duplicate them into designer variants.

This protects the base reference values.

### Show Technical Information

Shows component names and serialized property paths.

Useful for developers.
Usually unnecessary for designers.

### Show Help Boxes

Shows or hides help messages inside the tool.

### Show Entry Descriptions

Shows or hides descriptions under each parameter in the Designer page.

### Show Detailed Sidebar

Shows or hides detailed statistics inside sheet cards.

---

## 10. Git Notes

The tool uses two kinds of data:

### Local Editor Preferences

Stored in Unity `EditorPrefs`.

Examples:

```txt
language
display options
default folder path
```

These are personal settings and are not committed to Git.

No `.gitignore` rule is required for them.

### Balance Sheet Assets

Stored inside the Unity project, usually in:

```txt
Assets/GameplayBalanceSheets/Sheets
```

These `.asset` files are project data.

They should usually be committed to Git because they define the actual balancing sheets.

---

## 11. Recommended Team Workflow

### Developer

1. Create the reference sheet.
2. Assign the target root.
3. Create sections.
4. Scan the target.
5. Add useful serialized fields.
6. Write readable descriptions.
7. Commit the reference sheet.

### Game Designer

1. Select a reference sheet.
2. Create a designer variant.
3. Change Test values.
4. Apply the sheet or selected entries.
5. Test the gameplay in Play Mode.
6. Create multiple variants if needed.
7. Compare the feeling of each variant.

### Team Validation

1. Open Sheet Application.
2. Compare variants by target.
3. Choose the best variant.
4. Apply it as reference.
5. Commit the validated reference sheet.

---

## 12. Example

A project contains a player prefab:

```txt
PFB_Player
```

The developer creates:

```txt
Player Balance Sheet
```

Sections:

```txt
Movement
Jump
Combat
Camera
```

Exposed fields:

```txt
movementSpeed
jumpHeight
attackDamage
cameraFollowSpeed
```

The designer creates:

```txt
Player Balance Sheet - Test Fast Movement
Player Balance Sheet - Test Heavy Combat
Player Balance Sheet - Test Short Jump
```

After testing, the team validates:

```txt
Player Balance Sheet - Test Fast Movement
```

Then, in the Application page, the team clicks:

```txt
Apply As Reference
```

The selected variant becomes the new reference for future balancing.
