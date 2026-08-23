# Eval Rubric — Brief 031 : le viewer montre les gisements photographiés (R1)

**Authored**: 2026-08-23T09:50:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : même rôle signataire et même acteur que `brief.md`
de ce répertoire (Fable, session Planificateur).

---

## Guide de lecture

Pour chaque condition du brief :

- **Vérification** : commandes rejouables depuis la racine avec
  `.venv/bin/python`. Jamais l'alias nu.
- **Reconstruction indépendante** : l'Évaluateur re-dérive chaque valeur
  depuis les fichiers (snapshot, SVG, code), sans reprendre un nombre du
  manifeste ni du journal du Générateur.
- **Contre-preuve disqualifiante** : sabotage dans une copie **hors du
  dépôt**. Si le contrôle reste vert, la condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire et décisions : uniquement dans `brief.md` de ce répertoire (et
dans les briefs 026, 028 et 030 qu'il cite). Ce fichier ne les reformule
pas.

**Où se lit un compteur.** `deliverables/manifest.json` › `counters[]`
(valeur, `sample_size`, commande).

---

## Condition 0 — Le lot 030 était fusionné avant la première écriture

**Vérification :** rejouer les trois commandes de SC0 : version `v0a-2`,
statut `present`. Vérifier dans `generator-log.md` que le constat a été fait
en premier.

**Contre-preuve :** sur un checkout du commit parent d'avant la fusion de
030, la première commande imprime `v0a-1` — le Générateur aurait dû
s'arrêter là.

**Résultat attendu :** `schema_version_est_v0a2 == 1` ;
`couche_r1_present == 1`.

---

## Condition 1 — La couche se montre, ou se déclare indisponible

**Vérification :** rejouer les deux commandes de SC1. Ouvrir la preuve SVG :
la couche gisements y est. Recompter depuis le snapshot les cellules à liste
`resources` non vide, puis recompter les marqueurs de cellules dotées dans
le SVG : égalité.

**Reconstruction indépendante :** le comptage côté snapshot se fait par
`json.loads`, jamais en important le code du viewer.

**Contre-preuve :** passer au viewer un snapshot dont la couche est
`absent` (la preuve `sans_r1` du lot 030 convient) : la couche doit être
désactivée avec la raison lue, sans dessin et sans erreur. Si le viewer la
dessine quand même, ou la masque sans raison, la condition est fausse.

**Résultat attendu :** `cellules_dotees_dessinees` égal au recomptage
indépendant ; comportement dégradé conforme.

---

## Condition 2 — Trois états, aucune grandeur

**Vérification :** rejouer les tests des familles 1 à 3 :

```
.venv/bin/python -m pytest viewer/tests/ -q
```

Balayer soi-même la preuve SVG : aucun attribut de taille, rayon ou opacité
corrélé à `richness_class`. Chercher les natures de ressource en chaînes
littérales dans le code du viewer (hors tests).

**Contre-preuve :** dans une copie hors dépôt, faire varier le rayon d'un
marqueur selon la classe — le test de la famille 3 doit rougir sur le SVG
produit. S'il ne regarde que le code et pas le document, la famille est
disqualifiée.

**Résultat attendu :** `etats_visuels_distincts == 3` ;
`attributs_de_grandeur_par_classe == 0` ;
`natures_en_dur_dans_le_viewer == 0`.

---

## Condition 3 — Rien de calculé, rien de lu ailleurs

**Vérification :** diff du code du viewer contre les instantanés
`deliverables/pre-edit/` : aucun nouveau chemin `pipeline/geo`, aucun score
ou densité dérivé des gisements. Vérifier que la sélection d'une cellule
liste `id`, `resource`, `richness_class` dans l'ordre du snapshot.

**Reconstruction indépendante :** juger sur le code publié et le rendu
produit, pas sur le journal.

**Contre-preuve :** passer un snapshot dont une cellule n'a pas la clé
`resources` (copie sabotée hors dépôt) — le chargeur doit refuser, jamais
compléter (famille 4).

**Résultat attendu :** `lectures_pipeline_geo_dans_viewer == 0` ;
`grandeurs_derivees_des_gisements == 0`.

---

## Condition 4 — Déterminisme visuel, suites, preuves, rouges qui mordent

**Vérification :**

```
git ls-files harness/queue/briefs/031-viewer-couche-gisements-r1/deliverables
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

Produire deux rendus SVG du même snapshot dans des fichiers temporaires :
byte-identiques. Comparer la preuve avec couche à la preuve sans couche :
différentes (couple `must_differ_from` du manifeste). Lire
`viewer/README.md`. Relire le journal des cinq familles : chaque sabotage
doit faire rougir son test, rejoué dans une copie hors dépôt. Regarder la
preuve SVG soi-même (règle n° 11) et confronter à la description du
journal : gisements aux endroits attendus de la fenêtre pilote, aucun
marqueur en mer.

**Contre-preuve :** un `red_proof` vide ou un rouge non reproduit
disqualifie la famille (règle n° 4).

**Résultat attendu :** suites vertes (SKIP Unity Linux déclarés) ;
`rendus_svg_identiques == 1` ; `preuves_svg_differentes == 1` ;
`controles_rouges_mordants == 5` ; preuves suivies par git.

---

## Ce que cette rubrique ne juge pas

- La forme de la couche `resources_r1` du snapshot (jugée par la rubrique du
  lot 030).
- La vérité historique des gisements et de leurs classes (bornée par le
  brief 026).
- La manière dont le tick se servira des gisements (décision propriétaire en
  attente).
- Le relief G6, le climat observé, Unity.
- Un compteur recopié à la main dans le manifeste : présence n'est pas
  fonction (règle n° 7) — seule la reconstruction compte.
