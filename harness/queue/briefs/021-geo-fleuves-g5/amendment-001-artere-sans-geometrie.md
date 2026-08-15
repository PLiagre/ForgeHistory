# Amendement 001 — ce que « artère fluviale » dit, et ce qu'il ne dit pas (D3, World-Terms §3)

**Authored**: 2026-08-15T10:05:00Z
**Author**: forge-planificateur

> **Note de transparence.** L'acteur réel de cet amendement est Claude Code
> endossant le rôle natif `forge-planificateur`, sans suffixe ajouté à la
> signature, pour que le contrôle mécanique `verdict_is_not_self_authored`
> puisse comparer les acteurs de part et d'autre du lot. Le défaut réparé ici
> est un défaut **du brief**, pas du travail du Générateur.

**Répond à** : la relecture indépendante de la PR #107 (point 2), et à la
décision du propriétaire prise le 2026-08-15 après mesure.

**Cet amendement est postérieur au code.** C'est assumé et daté. Le Générateur a
implémenté D3 fidèlement ; c'est le brief qui se contredisait. L'amendement ne
requalifie pas le travail livré, il corrige le texte qui l'instruisait.

---

## 1. La contradiction

Deux passages du brief 021 disaient deux choses incompatibles du même objet :

- **World-Terms §3** : « Un fleuve navigable qui **longe** une frontière
  terrestre est un axe de circulation continu (une "artère fluviale"). Un fleuve
  qui **traverse ponctuellement** une frontière terrestre sans être navigable est
  un obstacle local (un "croisement"). » — une définition **géométrique** (longer
  vs traverser) croisée avec la navigabilité.
- **D3** : « **artery** = tous les tronçons touchant l'arête sont `navigable` ;
  **crossing** = aucun n'est navigable ; **both** = mélange. » — une définition
  **purement fondée sur la navigabilité**, sans aucune géométrie.

D3 était la décision opérationnelle, et le Générateur l'a suivie. Le README et
`logs/v1_060_rivers.log` ont repris la formulation de World-Terms. Résultat : les
artefacts livrés portent un nom qui promet un corridor, et le code produit autre
chose.

## 2. Ce que la mesure établit

Mesuré sur les artefacts committés de la PR #107, sur les 81 arêtes `land-land`
portant au moins un tronçon navigable (72 `artery` + 9 `both`), longueur de
fleuve dans un tampon de 50 m autour de la frontière partagée :

| fait mesuré | valeur |
|---|---|
| longueur longée maximale | 1937 m |
| médiane (arêtes `artery`) | 109 m |
| arêtes sous 500 m longés | 97 % |
| **proportion de frontière longée, maximum** | **3 %** |
| longueurs de frontière typiques | 23 km à 130 km |

La raison est structurelle : le maillage G3 est un semis de Voronoï/Poisson
construit à partir de graines de villes. Ses frontières n'ont aucun rapport de
construction avec le tracé des fleuves. Dans le monde réel une frontière suit
souvent un fleuve ; **ces cellules-ci, non**.

## 3. Ce qui est tranché

**D3 est maintenu tel quel.** La classification reste fondée sur la seule
navigabilité. Aucun seuil géométrique n'est introduit.

**Aucune borne n'est calibrée après mesure.** Un seuil de 250 m aurait rendu
12 arêtes « artères » et un seuil de 500 m en aurait rendu 3 : ces nombres
existent, ils sont écrits ici, et ils sont **refusés** précisément parce qu'ils
n'ont été trouvés qu'en cherchant une valeur qui empêche un compteur d'être nul.
C'est ce que les règles durement acquises interdisent, et le brief 019 l'avait
déjà payé (« une borne déplacée après mesure n'est plus une borne »).

**C'est World-Terms §3 qui est faux, pas le code.** Le passage est remplacé par :

> **Il permet d'embarquer, ou il faut le franchir.** Une arête terrestre touchée
> par un fleuve **navigable** est une arête où une cargaison peut entrer dans le
> réseau fluvial (`artery`) : le fleuve y est praticable en bateau. Une arête
> touchée seulement par des fleuves non navigables est un obstacle local
> (`crossing`) : on le franchit à gué ou par un pont. Une arête touchée par les
> deux (`both`) porte les deux faits.
>
> Ce que cette classification **ne dit pas** : elle ne dit pas que le fleuve
> longe la frontière, ni qu'il constitue un corridor de transport continu le long
> de cette arête. Mesuré sur le maillage actuel, un fleuve `artery` longe la
> frontière partagée sur 3 % de sa longueur au maximum — il la touche, il ne la
> suit pas. Un consommateur de `fluvial_artery` peut en conclure « on peut
> embarquer ici » ; il ne peut **pas** en conclure « on circule le long de cette
> frontière ».

## 4. Ce qui est reporté, et pourquoi

Le propriétaire a tranché le 2026-08-15 que le bon modèle de monde est
**l'artère comme attribut du fleuve et de la chaîne de cellules qu'il traverse**,
pas de l'arête : un fleuve navigable est un axe de transport le long de son
propre cours, comme on remonte la Seine de Rouen à Paris.

Cette décision est enregistrée et **n'est pas mise en œuvre par ce lot**, pour
une raison mécanique et non par prudence :

- `qa/checks.py::g5c_artery_has_navigable_river` parcourt les arêtes et fait
  `continue` sur toute arête sans `fluvial_artery`. Si plus aucune arête n'en
  porte, la boucle ne retient rien et le contrôle passe **à vide** — le contrôle
  creux exact que la relecture vient de reprocher au lot.
- `pipeline.py:1112-1113` impose `artery_count`, `crossing_count` et
  `both_count` dans les métriques.

Ces deux fichiers sont en lecture seule par D12, et toute la prémisse du brief
021 est de satisfaire un contrat déjà câblé sans y toucher. Le modèle retenu par
le propriétaire exige de **réécrire ce contrat**, ce qui relève d'un brief dédié
et probablement d'un ADR, puisque cela change ce que `pipeline/geo/` exporte vers
`sim/`.

**Non-objectif ajouté au brief 021** : redéfinir l'artère comme attribut du
fleuve ou d'une chaîne de cellules. Ce sera l'objet d'un brief ultérieur.

## 5. Effet sur les conditions de succès

- **SC3 est inchangée.** `artery_count > 0` reste exigé et reste satisfait
  (72 arêtes), puisque D3 est maintenu.
- **SC6 est précisée.** La mise à jour de `pipeline/geo/README.md` doit reprendre
  la formulation du §3 ci-dessus, y compris la phrase « ce que cette
  classification ne dit pas » et le fait mesuré des 3 %, **avant** toute citation
  de compteur. Une description qui promet un corridor est désormais un échec de
  SC6, pas une approximation.
- `logs/v1_060_rivers.log` est régénéré avec la même formulation.
