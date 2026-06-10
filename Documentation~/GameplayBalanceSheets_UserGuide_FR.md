# Gameplay Balance Sheets for Unity — Guide utilisateur FR

## 1. Objectif de l’outil

**Gameplay Balance Sheets** est un outil Unity Editor conçu pour aider une équipe à créer, dupliquer, tester, appliquer et valider des fiches d’équilibrage gameplay directement dans Unity.

L’outil fonctionne comme des **fiches de personnage de JDR**, mais pour les valeurs gameplay.

Dans un JDR, une fiche décrit les caractéristiques d’un personnage.
Ici, une fiche décrit des valeurs gameplay applicables à une cible Unity, par exemple :

* un prefab joueur
* un prefab ennemi
* un prefab boss
* une caméra
* un objet de scène
* un contrôleur UI
* n’importe quel GameObject contenant des champs sérialisés dans des MonoBehaviours

L’objectif est de permettre aux développeurs d’exposer une fois les valeurs gameplay utiles, puis de permettre aux game designers de créer des variantes et de tester différents feelings sans devoir parcourir manuellement les composants techniques dans l’Inspector.

---

## 2. Concept principal

Le workflow repose sur trois notions principales :

### Fiche de référence

Une **fiche de référence** est la fiche de base créée par un développeur.

Elle définit :

* l’objet ou le prefab cible
* les sections gameplay
* les paramètres exposés
* les valeurs de référence, appelées `Baseline`

Par défaut, les designers ne doivent pas modifier directement une fiche de référence.
Ils doivent la dupliquer pour créer une variante.

### Variante GD

Une **variante GD** est une copie modifiable d’une fiche de référence.

Exemple :

```txt
Fiche de référence :
Player Balance Sheet

Variantes GD :
Player Balance Sheet - Test Fast Movement
Player Balance Sheet - Test Heavy Combat
Player Balance Sheet - Test Short Dash
```

Le designer modifie seulement le suffixe de la variante, par exemple :

```txt
Test Fast Movement
```

Le nom de base reste lié à la fiche de référence.

### Cible

Une **cible** est l’objet ou le prefab Unity sur lequel une fiche agit.

Exemple :

```txt
Cible : PFB_Player
Fiche de référence : Player Balance Sheet
Variantes :
  - Test Fast Movement
  - Test Heavy Combat
```

---

## 3. Les colonnes de valeurs

Chaque paramètre exposé contient trois valeurs principales.

### Baseline

`Baseline` est la valeur de référence.

Elle représente la valeur sûre issue de la fiche de référence.

Exemple :

```txt
Horizontal Speed
Baseline : 5.2
```

### Current

`Current` est la valeur actuellement écrite sur la cible Unity.

Elle représente ce que le prefab ou l’objet de scène utilise actuellement.

Exemple :

```txt
Current : 5.2
```

### Test

`Test` est la valeur préparée dans la fiche sélectionnée.

Modifier `Test` ne modifie pas immédiatement la cible.
La valeur est écrite sur la cible seulement quand la fiche ou les paramètres sélectionnés sont appliqués.

Exemple :

```txt
Test : 7.0
```

---

## 4. Les états d’un paramètre

Chaque paramètre possède un état.

### Référence

La cible correspond actuellement à la valeur de référence.

```txt
Current == Baseline
Test == Current
```

Cela signifie que la cible utilise actuellement la valeur de référence.

### Test en attente

La valeur `Test` est différente de `Current`.

```txt
Test != Current
```

Cela signifie que le designer a préparé une valeur, mais qu’elle n’a pas encore été appliquée.

### Modifié

La cible utilise une valeur différente de la référence.

```txt
Current == Test
Current != Baseline
```

Cela signifie que la cible a été modifiée par rapport à la référence.

### Introuvable

Le champ n’existe plus sur la cible.

Cela peut arriver si :

* un script a changé
* un champ a été renommé
* un composant a été supprimé
* la hiérarchie de la cible a changé

Un champ introuvable ne bloque pas l’application du reste de la fiche.
Les autres champs valides peuvent quand même être appliqués.

---

## 5. Workflow principal

### Étape 1 — Le développeur crée une fiche de référence

Ouvrir :

```txt
Tools > Gameplay Balance Sheets
```

Aller dans :

```txt
Configuration Dev
```

Cliquer sur :

```txt
Créer fiche de référence
```

Le développeur configure ensuite :

* le titre de référence
* la cible
* la description
* les sections
* les paramètres gameplay exposés

---

### Étape 2 — Le développeur crée des sections

Les sections servent à organiser les paramètres.

Exemples :

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

Les sections peuvent être réordonnées par glisser-déposer.

---

### Étape 3 — Le développeur scanne la cible

Dans la page `Configuration Dev`, il faut assigner une `Target Root`, puis cliquer sur :

```txt
Scanner la cible
```

L’outil scanne les champs sérialisés compatibles dans les MonoBehaviours.

Types supportés :

```txt
int
float
bool
string
enum
```

Le développeur sélectionne ensuite les champs utiles et les ajoute à une section.

---

### Étape 4 — Le GD crée une variante

Aller dans :

```txt
Équilibrage GD
```

Sélectionner une fiche de référence, puis créer une variante.

Exemple :

```txt
Référence :
Player Balance Sheet

Suffixe de variante :
Test Fast Movement

Résultat :
Player Balance Sheet - Test Fast Movement
```

Le GD peut ensuite modifier les valeurs `Test` dans la variante.

---

### Étape 5 — Le GD teste les valeurs

Le GD peut appliquer :

* toute la fiche
* des paramètres sélectionnés
* des paramètres sélectionnés à l’intérieur d’une section

Actions utiles dans une section :

```txt
Appliquer sélection
Current → Test
Baseline → Test
Restaurer sélection
```

### Current → Test

Copie la valeur actuellement présente sur la cible vers le champ `Test`.

À utiliser quand une valeur a été modifiée en dehors de l’outil et qu’elle doit devenir la nouvelle valeur de test.

### Baseline → Test

Copie la valeur de référence dans le champ `Test`.

À utiliser pour préparer un retour à la référence ou comparer une valeur testée avec la valeur de base.

### Restaurer sélection

Restaure les paramètres sélectionnés vers leurs valeurs de référence.

---

### Étape 6 — L’équipe valide une fiche

Aller dans :

```txt
Application des fiches
```

Cette page affiche les fiches sous forme d’arborescence :

```txt
Cible
  Fiche de référence
    Variantes GD
```

Exemple :

```txt
Cible : PFB_Player
  Player Balance Sheet
    Test Fast Movement
    Test Heavy Combat
```

Quand l’équipe décide qu’une fiche donne le meilleur feeling gameplay, cliquer sur :

```txt
Appliquer comme référence
```

Cette action :

1. applique la fiche sélectionnée sur la cible
2. définit ses valeurs actuelles comme nouvelles valeurs `Baseline`
3. transforme la fiche sélectionnée en nouvelle fiche de référence

Cette action doit être utilisée uniquement après validation de l’équipe.

---

## 6. Page Équilibrage GD

La page `Équilibrage GD` sert à :

* lire les fiches de référence
* créer des variantes
* modifier les valeurs `Test` des variantes
* appliquer des valeurs sélectionnées
* restaurer des valeurs sélectionnées
* supprimer des variantes GD

Un GD peut supprimer une variante GD, mais ne doit pas supprimer une fiche de référence depuis la page GD.

Les fiches de référence sont verrouillées par défaut.

---

## 7. Page Configuration Dev

La page `Configuration Dev` sert à :

* créer des fiches de référence
* configurer les cibles
* écrire les descriptions des fiches
* créer et réordonner les sections
* scanner les cibles
* ajouter les champs utiles
* retirer des champs d’une fiche
* écrire des descriptions pour les paramètres

La page `Configuration Dev` ne sert pas à tester l’équilibrage gameplay.

Les tests gameplay doivent se faire depuis la page `Équilibrage GD`.
La validation finale doit se faire depuis la page `Application des fiches`.

---

## 8. Page Application des fiches

La page `Application des fiches` sert à comparer et valider les fiches.

Elle est organisée comme ceci :

```txt
Cible
  Fiche de référence
    Variantes GD
```

Chaque fiche affiche :

* son nom
* son état d’application
* son nombre de sections
* son nombre de paramètres

Actions disponibles :

```txt
Appliquer comme référence
Actualiser
Ouvrir côté GD
Voir cible
```

### Appliquer comme référence

C’est une action de validation.

Elle doit être utilisée uniquement quand l’équipe a testé une fiche et a décidé qu’elle doit devenir la nouvelle référence.

Cette action est plus forte qu’une simple application de valeurs.

---

## 9. Paramètres

La page `Paramètres` contient des préférences personnelles de l’éditeur.

### Langue

L’outil supporte :

```txt
Anglais
Français
```

La langue sélectionnée est enregistrée localement avec Unity `EditorPrefs`.

Cela signifie que :

* la langue est stockée sur la machine locale
* elle n’est pas écrite dans le dépôt Git
* elle n’a pas besoin d’être ajoutée au `.gitignore`

### Dossier par défaut des fiches

C’est le dossier utilisé par défaut lors de la création de nouveaux assets de fiches.

Exemple :

```txt
Assets/GameplayBalanceSheets/Sheets
```

Les fichiers `.asset` créés dans ce dossier sont des données projet.

Ils doivent généralement être commit dans Git, car ils contiennent les fiches d’équilibrage utilisées par l’équipe.

### Autoriser les GD à modifier les fiches de référence

Valeur recommandée :

```txt
Désactivé
```

Quand l’option est désactivée, les GD ne peuvent pas modifier directement les fiches de référence.
Ils doivent les dupliquer pour créer des variantes GD.

Cela protège les valeurs de référence.

### Afficher les informations techniques

Affiche les noms des composants et les chemins des propriétés sérialisées.

Utile pour les développeurs.
Généralement inutile pour les GD.

### Afficher les bulles d’aide

Affiche ou masque les messages d’aide dans l’outil.

### Afficher les descriptions des paramètres

Affiche ou masque les descriptions sous chaque paramètre dans la page `Équilibrage GD`.

### Afficher les fiches détaillées dans la barre latérale

Affiche ou masque les statistiques détaillées dans les cartes de fiches.

---

## 10. Notes Git

L’outil utilise deux types de données.

### Préférences locales de l’éditeur

Elles sont stockées dans Unity `EditorPrefs`.

Exemples :

```txt
langue
options d’affichage
dossier par défaut
```

Ces préférences sont personnelles et ne sont pas commit dans Git.

Aucune règle `.gitignore` n’est nécessaire pour ces préférences.

### Assets de fiches d’équilibrage

Ils sont stockés dans le projet Unity, généralement ici :

```txt
Assets/GameplayBalanceSheets/Sheets
```

Ces fichiers `.asset` sont des données projet.

Ils doivent généralement être commit dans Git, car ils définissent les fiches d’équilibrage utilisées par l’équipe.

---

## 11. Workflow recommandé pour l’équipe

### Développeur

1. Créer la fiche de référence.
2. Assigner la cible.
3. Créer les sections.
4. Scanner la cible.
5. Ajouter les champs sérialisés utiles.
6. Écrire des descriptions lisibles.
7. Commit la fiche de référence.

### Game Designer

1. Sélectionner une fiche de référence.
2. Créer une variante GD.
3. Modifier les valeurs `Test`.
4. Appliquer la fiche ou des paramètres sélectionnés.
5. Tester le gameplay en Play Mode.
6. Créer plusieurs variantes si nécessaire.
7. Comparer le feeling de chaque variante.

### Validation équipe

1. Ouvrir `Application des fiches`.
2. Comparer les variantes par cible.
3. Choisir la meilleure variante.
4. L’appliquer comme nouvelle référence.
5. Commit la fiche de référence validée.

---

## 12. Exemple complet

Un projet contient un prefab joueur :

```txt
PFB_Player
```

Le développeur crée :

```txt
Player Balance Sheet
```

Sections :

```txt
Movement
Jump
Combat
Camera
```

Champs exposés :

```txt
movementSpeed
jumpHeight
attackDamage
cameraFollowSpeed
```

Le GD crée :

```txt
Player Balance Sheet - Test Fast Movement
Player Balance Sheet - Test Heavy Combat
Player Balance Sheet - Test Short Jump
```

Après test, l’équipe valide :

```txt
Player Balance Sheet - Test Fast Movement
```

Dans la page `Application des fiches`, l’équipe clique sur :

```txt
Appliquer comme référence
```

La variante sélectionnée devient la nouvelle référence pour les futurs équilibrages.
