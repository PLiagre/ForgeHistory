# Verdict propriétaire — galerie du brief 005 (passe Générateur interrompue)

**Date**: 2026-08-02
**Source**: message du propriétaire, session 2026-08-02, retranscrit verbatim
par l'orchestrateur, en réponse à cinq images de la galerie 005 que
l'orchestrateur lui a soumises.

## Ce qui a été montré, dans l'ordre

1. `deliverables/evidence/gallery/v005_orientation_before_fix.png`
2. `deliverables/evidence/gallery/v005_orientation_after_fix.png`
3. `deliverables/evidence/gallery/v005_after_01_world_neutral.png`
4. `deliverables/evidence/gallery/v005_after_02_country_selected.png`
5. `deliverables/evidence/gallery/v005_after_03_province_selected_default.png`

## Verbatim

> il y a un probleme avec la carte 1 montré. la seul carte correct qui
> ressemble a quelque chose est la carte 3. tout le reste est inutilisable

## Lecture par l'orchestrateur

Verdict artistique : **NON ADOPTÉ**, pour la deuxième fois consécutive
(après `004-polish-visuel/owner-verdict-2026-08-01.md`).

Deux des cinq images n'auraient pas dû être soumises au jugement
artistique — erreur de l'orchestrateur, pas du Générateur :

- L'image 1 est le côté *avant-correction* d'une paire de preuve : elle est
  délibérément renversée avec les libellés en miroir. C'est l'artefact qui
  démontre le défaut, pas un livrable.
- Les images 1 et 2 proviennent toutes deux du chemin d'export diagnostic
  (`MapSnapshotExporter`) sur un monde de test à 50 provinces synthétiques.
  Leur aspect en aplats polygonaux grossiers vient de ce monde de test, pas
  du rendu du jeu. Elles prouvent une convention d'orientation ; elles ne
  montrent pas ce que le joueur voit.

Les images 3, 4 et 5 viennent en revanche du vrai chemin joueur
(framebuffer standalone, données géo réelles). Le jugement du propriétaire
sur celles-ci est un constat de rendu, pas une préférence :

1. **La carte n'occupe qu'une fraction du viewport aux zooms Pays et
   Province.** Dans les images 4 et 5, la surface cartographiée s'arrête à
   environ 43 % de la largeur ; tout le reste de l'écran est le fond vide.
   **Défaut préexistant, pas une régression du brief 005** — vérifié en
   ouvrant `unity/game_unity/Captures/v004_after_default/02_country_selected.png`,
   qui présente exactement le même bandeau étroit et la même moitié droite
   vide.
2. **Les libellés de provinces sont surdimensionnés et s'empilent les uns
   sur les autres.** Image 5 : `YPRES`, `BEAUVAIS`, `ARRAS`, `CHARTRES`,
   `SENS`, `PARIS`, `FRANCE` se chevauchent en un bloc illisible, et une
   partie est masquée par le panneau de gauche. Préexistant lui aussi
   (même empilement dans la capture 004 citée ci-dessus).
3. **Le bandeau de crédits en haut à gauche déborde et se fait couper** par
   la barre supérieure, sur les trois captures du chemin joueur.

## Ce que le brief 005 a réellement corrigé, et qui tient

Comparaison directe avec `v004_after_default/02_country_selected.png` :

- Les libellés de provinces sont désormais à l'endroit et lisibles (ils
  étaient en miroir renversé).
- Les panneaux `Lois` et `Impôt` ne se chevauchent plus.
- Le bloc `Investir` affiche du français lisible
  (`Développement : Fiscalité 5 · Production 4 · Main-d'œuvre 3`) là où
  004 laissait `LAWMOD 0 EFF 0,002 %` et les jetons bruts.

## Suite

Les trois défauts constatés ci-dessus ne figurent dans aucune des Success
Conditions 1–7 du brief 005, dont les Non-Goals imposent explicitement de
les rapporter comme findings plutôt que de les corriger au passage. Ils
sont donc l'intrant d'un brief suivant, et non un motif de rejet du travail
mesuré du brief 005.
