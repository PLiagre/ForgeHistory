**Author**: forge-evaluateur
**Authored**: 2026-08-15T10:15:00Z

# Feedback 001 — Brief 021 (fleuves G5)

> **Note de transparence.** L'acteur réel est Claude Code endossant le rôle natif
> `forge-evaluateur`, sans suffixe ajouté à la signature. Les défauts listés ici
> proviennent d'une relecture indépendante du diff de la PR #107, menée dans une
> invocation séparée de celle du Générateur (Cursor) ; chaque point décisif a été
> re-vérifié sur les artefacts committés avant d'être écrit ici.

Verdict associé : **REJECT**, rendu par `harness/verdict_audit.py` sur la PR #107.

**À lire d'abord.** Le travail n'est pas à refaire. Le module produit tourne, il
est déterministe (SHA256 identiques sur 8 fichiers en deux passes), il ne touche
aucun des neuf fichiers partagés interdits ni `constants.py`, `adjacency_g5.json`
est une copie fidèle de `adjacency_g4.json` (2085/2085 arêtes, zéro champ G4
modifié), et les identités de compteurs tiennent (36+92+29 = 157 tronçons ;
72+195+9 = 276 arêtes). Le crochet `pipeline.py --source rivers` est réellement
satisfait. Il y a **onze points à corriger**, dont un seul touche la substance.

**Un point ne vous concerne pas** : la sémantique d'« artère fluviale ». Le brief
se contredisait (World-Terms §3 promettait un fleuve qui *longe* la frontière,
D3 définissait une classification purement fondée sur la navigabilité). Vous avez
implémenté D3 fidèlement. C'est réparé par `../amendment-001-artere-sans-geometrie.md`,
qui maintient D3 et corrige le texte. Voir le point 11 ci-dessous pour ce que
cela vous demande concrètement.

---

## Point 1 — Substance : le contrôle G5-D ne peut pas rougir (`steps/05_rivers.py:544`)

**Le défaut.** `derive_mouths` écrit le littéral
`"sea_zone_adjacent_to_river_cells": True`. Le champ que
`g5d_mouth_on_adjacent_sea` valide est donc vrai par construction : le contrôle
G5-D ne peut jamais rougir sur des données produites. Aggravant : la fonction ne
considère que les zones déjà présentes dans `cell_to_zones` (l. 521-523) et
abandonne l'embouchure quand aucune n'est à portée de `snap_m` (l. 530-531) —
donc le cas que G5-D existe pour attraper (une embouchure sur une zone maritime
**non** adjacente aux cellules du fleuve) est silencieusement écarté au lieu
d'être émis avec le drapeau à faux, et aucun compteur n'enregistre l'abandon.

Règle durement acquise n° 4 : un contrôle qui ne peut pas rougir ne prouve rien.
La seule preuve rouge actuelle est une mutation forcée à la main
(`tests/test_qa_red_g5.py:128`), ce qui prouve le code du contrôle, pas le fait.

**Ce qu'il faut faire.** Appliquer D6 tel qu'il est écrit : retenir la zone
maritime **la plus proche** du point terminal (pas seulement parmi
`cell_to_zones`), puis **calculer** l'adjacence entre cette zone et les cellules
que le tronçon traverse, et écrire le booléen calculé. Une embouchure dont la
zone la plus proche n'est adjacente à aucune cellule du fleuve doit être
**émise** avec `sea_zone_adjacent_to_river_cells: false`, pas supprimée.

**Attendu mesuré.** Sur les données actuelles le filtre est sans effet numérique
(j'ai mesuré 57 candidats → 57 embouchures, zéro écart avec la zone la plus
proche) : `mouth_count` doit donc rester à **57**. La correction est
comportementale. Ajouter un compteur `embouchures_zone_non_adjacente` (valeur
attendue 0 aujourd'hui, mais **calculé**, jamais supposé — la sentinelle `-1`
ne doit pas apparaître pour un compteur effectivement calculable).

Le cas rouge de G5-D dans `test_qa_red_g5.py` doit alors devenir **naturel** :
construire une embouchure dont la zone la plus proche n'est adjacente à aucune
cellule traversée, et constater que G5-D rougit — plus une mutation.

## Point 2 — `sea_zone_name` publie des faits faux (`steps/05_rivers.py:534`)

`sea_zone_name` recopie les étiquettes grossières de G4 et les publie sans
réserve dans `artifacts/mouths_g5.json`. G4 porte 40 zones pour bien moins de
noms : une composante connexe entière porte une seule étiquette. La zone 5008
s'étend de lon 10,7 à 21,9 et de lat 38,5 à 45,2 (Adriatique + Ionienne +
Tyrrhénienne) sous le nom « Mer Tyrrhenienne ». L'artefact affirme donc que le
**Pô** (12,53 ; 44,97) et l'**Ofanto** (16,20 ; 41,36) se jettent dans la
Tyrrhénienne, et le **Strymnas** (mer Égée) dans la « Mer de Marmara ».

`navigability` a reçu une mise en garde de proxy explicite (l. 603-606) ;
`sea_zone_name` n'en a aucune. Deux issues acceptables, au choix : retirer le
champ de l'artefact, ou le conserver **avec** une déclaration de proxy hérité de
G4 dans le `comment` de l'artefact **et** dans `README.md`, sur le modèle exact
de ce qui a été fait pour `scalerank`. Ne pas laisser un nom faux passer pour un
fait.

## Point 3 — `generator-log.md` : frontmatter `Author` absent

`harness/verdict_audit.py` échoue sur `verdict_is_not_self_authored` :
« Author frontmatter missing on generator-log.md ». Le journal ouvre sur
`**Rôle :** Générateur (Cursor…)` ; la porte sait lire `**Author**: <rôle>`,
comme dans `019-geo-adjacence-g4/deliverables/generator-log.md`. Ajouter la ligne
`**Author**: forge-generateur` en tête, sans suffixe d'acteur (un suffixe
casserait la comparaison des acteurs de part et d'autre du lot).

## Point 4 — `manifest.json` : `"counters": []`

La table « Required Counters » du brief impose 15 compteurs nommés ;
`measure_g5_021.py` en calcule 24 correctement, mais aucun n'est déclaré dans le
manifeste. Conséquence mécanique : `no_empty_sample_pass` passe **à vide** et
l'Évaluateur n'a rien à reconstruire. Déclarer chaque compteur avec un
`sample_size` réel — non nul et différent de la sentinelle — sur le modèle du
manifeste du brief 019 (48 compteurs déclarés). Déclarer aussi
`deliverables/measure_g5_021.py` dans `files` : il est produit mais absent de la
liste.

## Point 5 — Trois compteurs « fichier intact » incapables de rougir (`measure_g5_021.py:214, 232, 239`)

`constantes_g5_inchangees`, `fichiers_partages_modifies` et
`adjacency_g4_inchange` dérivent tous de `git status --porcelain`, qui est vide
pour tout changement **committé** — précisément l'état dans lequel la PR est
relue. Vérifié : ils rapportent 1 / 0 / 1 sur cette branche, et rapporteraient
exactement la même chose si `constants.py` avait été édité **puis committé**.
Règles n° 3 et 4.

Dériver de la référence de base à la place, par exemple
`git diff origin/master...HEAD --name-only -- <chemin>`, et prouver que le
compteur rougit en le rejouant contre une modification committée factice.

## Point 6 — `git()` avale les erreurs (`measure_g5_021.py:53`)

`subprocess.run(...).stdout` sur un échec (dépôt absent, chemin invalide, git
indisponible) rend `""`, que tous les appelants lisent comme « propre ». Un appel
git cassé rend donc verts tous les compteurs du point 5. Vérifier `returncode`,
et en cas d'échec lever ou rapporter la sentinelle `-1` (« non calculé »),
jamais un silence qui ressemble à un succès.

## Point 7 — Paramètre mort `rebuild_land` (`steps/05_rivers.py:255`)

`load_context(*, rebuild_land: bool = True)` ne lit jamais `rebuild_land`. La
signature est reprise de `steps/04_adjacency.py:169`, où le drapeau garde
réellement l'appel à `run_corrections` (l. 179-180). Ici, passer `False`
reconstruit quand même toute la terre G2b — avec, en effet de bord, la
réécriture des artefacts et captures G2b. Honorer le paramètre ou le retirer.

## Point 8 — Code mort (`steps/05_rivers.py:272-280` et `:562`)

- `ctx_g4` est assemblé avec `cell_geoms: []` / `cell_ids: []` puis jamais
  utilisé ; `derive_sea` est appelé sur un littéral distinct (l. 282-289). Se lit
  comme si c'était l'argument de l'appel.
- `land_land_total = -1  # renseigné par l'appelant si besoin` dans
  `compute_metrics` n'est ni retourné ni lu ; la vraie valeur est calculée par
  l'appelant (l. 860). Employer la sentinelle « non calculé » là où rien ne peut
  la faire surgir prête à confusion à côté de la règle n° 8.

## Point 9 — Embouchures fantômes au bord de la fenêtre (`steps/05_rivers.py:510-511`)

`_line_endpoints` rend les extrémités de **chaque partie** d'un
`MultiLineString`, y compris les extrémités artificielles créées par `gpd.clip`
au bord de la fenêtre pilote. Comme `sea_xy` vaut `fenêtre − terre`, un fleuve
coupé par le bord au-dessus de l'eau produit une extrémité à moins de `snap_m`
de « la mer » et serait émis comme embouchure. Latent aujourd'hui (vérifié :
0 des 57 embouchures ne se trouve à moins de 0,05° du bord), mais se déclenchera
au premier déplacement de la fenêtre ou changement de couche source. Exclure les
extrémités posées sur `window_ll.exterior`.

## Point 10 — Les captures peignent les lacs en terre (`steps/05_rivers.py:729-738`)

`draw_land` ne rend que `poly.exterior` : les anneaux intérieurs sont ignorés,
donc les lacs et bassins fermés que le pipeline exclut soigneusement de la terre
apparaissent remplis en terre dans `v1_060_rivers_window.png` et
`v1_060_artery_crossing_both.png`. La règle n° 11 demande que ces captures soient
regardées et décrites ; la description du journal (« aucun trait ne traverse la
pleine mer hors centre-lignes de lac ») est donc lue sur une image qui situe mal
l'eau. Rendre les anneaux intérieurs, puis **re-regarder** les deux captures et
réécrire leur description à partir de ce qu'elles montrent réellement.

## Point 11 — Appliquer l'amendement 001 (documentation)

`../amendment-001-artere-sans-geometrie.md` maintient D3 et corrige le texte.
Concrètement, pour vous :

- Reprendre dans `pipeline/geo/README.md` la formulation du §3 de l'amendement,
  **y compris** la phrase « ce que cette classification ne dit pas » et le fait
  mesuré (un fleuve `artery` longe la frontière partagée sur 3 % de sa longueur
  au maximum), **avant** toute citation de compteur.
- Régénérer `logs/v1_060_rivers.log` avec la même formulation : le journal décrit
  actuellement `artery` comme « un axe de circulation continu », ce qui est faux.
- Ne changer **aucune ligne** de la classification dans `steps/05_rivers.py` :
  D3 est maintenu, `artery_count` doit rester à 72.

---

## Ce qui n'est pas demandé

- Ne pas introduire de seuil géométrique pour `artery`. L'amendement 001 §3
  explique pourquoi les valeurs de 250 m et 500 m sont refusées.
- Ne pas toucher `qa/checks.py`, `pipeline.py`, `constants.py` ni aucun des neuf
  fichiers de D12 : ils sont restés intacts, c'est bien, cela doit le rester.
- Ne pas rédiger de `verdict.md` : c'est le rôle de l'Évaluateur, pas le vôtre.
