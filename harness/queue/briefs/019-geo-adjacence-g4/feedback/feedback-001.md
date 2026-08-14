**Author**: forge-evaluateur
**Authored**: 2026-08-14T10:35:00Z

# Feedback 001 — Brief 019 (adjacence maritime G4)

Premier feedback de ce brief. Verdict associé : REJECT, motivé dans
`../verdict.md`. Rédigé pour être consommé tel quel à l'itération suivante.

**À lire d'abord :** sur les dix conditions de succès, huit passent, et elles
passent solidement — j'ai re-dérivé `42` compteurs de façon indépendante et je
n'ai trouvé **aucun** écart avec le manifeste. Le travail n'est pas à refaire.
Il y a exactement deux choses à corriger, dont **une seule** est dans les mains
du Générateur.

---

## Point 1 — Pour le Planificateur, pas pour le Générateur : l'empreinte du littoral (SC7)

**Le défaut.** `empreinte_terre_g4_egale_entree_g3` vaut `0` ; SC7 exige `1` ;
la rubrique range cette inégalité parmi les échecs disqualifiants.

**Pourquoi ce n'est pas au Générateur de le corriger.** J'ai recalculé les
trois empreintes moi-même : celle du `artifacts/coastline_1400.json` régénéré
est égale à la sortie que `MANIFEST_g2b.json` déclare pour ce même fichier, et
différente de l'entrée que `MANIFEST_g3.json` déclare. Les deux manifestes
préexistent au lot. Le périmètre de D16 met `MANIFEST_g3.json` et les artefacts
G3 en lecture seule. **Aucune action du Générateur dans le périmètre autorisé
ne peut faire passer ce compteur à `1`.** Relancer la génération à l'identique
ne changerait rien : c'est le cas typique où la règle du plateau s'applique
avant même la deuxième itération.

**Ce qu'il ne faut surtout pas faire** (et que je refuserai si je le vois) :

- régénérer ou réécrire `MANIFEST_g3.json`, `cells_g3.json`, `stats_g3.json`
  ou `adjacency_g3.json` pour aligner l'empreinte ;
- appeler `run_corrections` avec d'autres paramètres jusqu'à retomber sur
  l'empreinte que G3 déclare ;
- déclarer le compteur à `1` en comparant à `MANIFEST_g2b.json` au lieu de
  `MANIFEST_g3.json` — ce serait renommer la cible pour la toucher ;
- transformer le constat en dérogation auto-accordée : la table des dérogations
  recevables du brief ne contient pas cette affirmation, et elle se termine par
  « aucune autre dérogation n'est recevable ».

**Ce qui est attendu, et de qui.** Le Planificateur doit trancher lequel des
deux artefacts committés est faux, puis ouvrir soit un lot de réparation de la
provenance G3, soit un amendement de SC7 qui dise ce que le lot G4 doit faire
quand la chaîne amont est incohérente. Deux hypothèses à départager, mesurables
l'une comme l'autre : `MANIFEST_g3.json` décrit un littoral que la chaîne ne
produit plus, ou bien `steps/02b_corrections_1400.py` a changé de sortie depuis
que G3 a été joué sans que G3 soit rejoué. Élément utile trouvé en chemin : le
`coastline_1400.json` que G3 avait sous la main n'est pas suivi par git et
n'existe plus dans le dépôt, donc seule la relecture de la chaîne peut trancher.

À ce stade, la seule action utile du Générateur est de **ne rien y toucher** et
de laisser le constat tel qu'il l'a écrit — il l'a bien écrit.

---

## Point 2 — À corriger par le Générateur : une empreinte citée par sa valeur

**Le défaut, précisément localisé.** Dans
`deliverables/generator-log.md`, la section qui compare les deux points d'entrée
écrit la valeur hexadécimale complète de l'empreinte de `adjacency_g4.json`
(`1aba2adc…`, `64` caractères) pour affirmer que le crochet et la preuve
produisent « les mêmes empreintes ». C'est une empreinte de parité citée par sa
**valeur** dans un document livré : règle durement acquise n° `12`,
non-objectif n° `16` du brief, et ligne explicite de la table des échecs
disqualifiants de la rubrique.

**La correction demandée, concrètement.** Remplacer la valeur par une citation
**par nom** et par une commande rejouable. Par exemple : « la branche
`--source adjacency` produit les mêmes empreintes que la preuve ; les paires
sont dans le bloc `determinism.sha256` de `logs/v1_050_qa.json`, et l'égalité se
revérifie en relançant les deux points d'entrée puis en constatant que
`git status --porcelain -- pipeline/geo/artifacts pipeline/geo/registry` ne
renvoie rien. » Aucun chiffre hexadécimal ne doit rester dans le texte. Rien
n'est perdu en force de preuve : c'est ainsi que j'ai vérifié la parité de mon
côté, et cela a marché.

**Vérification à faire après correction**, depuis la racine :

```
grep -rnoE '\b[0-9a-f]{64}\b' harness/queue/briefs/019-geo-adjacence-g4/deliverables pipeline/geo/README.md pipeline/geo/steps/04_adjacency.py pipeline/geo/tests/run_proof_g4.py pipeline/geo/tests/test_qa_red_g4.py
```

Attendu après correction : plus aucune ligne dans `generator-log.md`. Le code et
`README.md` sont déjà propres, je l'ai vérifié — ne les touche pas pour ça.

---

## Point 3 — À trancher, puis à corriger : les deux empreintes du champ `error`

**Le défaut.** La première dérogation de `deliverables/manifest.json` porte,
dans son champ `error`, les deux valeurs hexadécimales complètes du constat A,
parce que c'est la sortie littérale de l'`AssertionError` de la commande
déclarée. Il y a une tension réelle entre la règle n° `9` (une impossibilité
s'éprouve par une commande **et** son message d'erreur) et la règle n° `12`.
Elle est aggravée par une contradiction interne : la phrase de la dérogation
affirme « aucune empreinte recopiée » alors que son propre champ `error` en
contient deux.

**La correction que je recommande** — elle satisfait les deux règles à la fois :
faire porter à la commande déclarée une comparaison qui **nomme ses deux
sources** et n'imprime que le résultat, puis consigner cette sortie-là dans
`error`. Par exemple, une commande qui calcule les deux empreintes, les compare,
et échoue en imprimant « écart entre `artifacts/coastline_1400.json` calculé et
`MANIFEST_g3.json` `inputs.coastline_1400` » avec le code de sortie, sans jamais
imprimer les valeurs. La commande reste rejouable, l'impossibilité reste
éprouvée, et aucune constante morte n'entre dans le dépôt.

Au minimum, et dans tous les cas : retirer de la phrase de la dérogation
l'affirmation « aucune empreinte recopiée », qui est fausse en l'état.

---

## Points à ne pas corriger — je les ai vérifiés et ils vont bien

Consignés pour éviter qu'une itération suivante « répare » ce qui n'est pas
cassé, ou déplace une borne par excès de zèle.

- **Les `24` zones hors bornes d'intention ne sont pas un défaut.** J'ai
  reconstruit le compteur : `24` sur `40`, exemption de bassin entier appliquée
  (`2` zones). D13 déclare ces bornes non bloquantes avant toute mesure, la
  rubrique n'en fait qu'un compteur à inscrire comme constat ouvert, et le brief
  dit explicitement que leur non-respect « n'est pas une dérogation ». Le
  constat est inscrit au bon endroit, dans le journal **et** dans `README.md`,
  avec sa cause mesurée. **Ne déplace aucune valeur de `constants.py`** pour
  faire tomber ce `24`, et n'ajuste pas l'algorithme de découpe pour l'améliorer :
  ce serait une recalibration après mesure, c'est-à-dire la faute même que la
  rubrique sanctionne.
- La saturation du semis sur `SEA_ZONE_COUNT_MAX` est une observation que
  j'adresse au Planificateur, pas une correction à faire ici.
- L'attribution des noms, les `668` largeurs de détroit, la reconstruction de
  l'atteignabilité, la frontière ADR-`0003`, le suivi git des `14` preuves, le
  déterminisme sur `9` paires : tout cela, je l'ai refait de mon côté et cela
  tombe juste. N'y touche pas.
- Les trois captures montrent bien ce que le journal en dit, y compris le détail
  gênant du nom de proxy mal placé au nord de la Baltique, que le journal
  signale de lui-même. C'est le bon réflexe ; garde-le.

---

## Suggestions facultatives, sans effet sur la recevabilité

Aucune des deux n'est un motif de rejet ; je les note parce qu'elles coûteront
plus cher plus tard qu'aujourd'hui.

1. **Le cas rouge de `Q4` gagnerait à être resserré.** Il obtient son rouge en
   passant une liste d'arêtes vide, donc en isolant tout le graphe. Isoler
   **une seule** entité — retirer toutes les arêtes d'une seule cellule, sur une
   copie en mémoire — prouverait ce que le contrôle doit repérer, au lieu de
   prouver qu'il repère un monde entièrement déconnecté (règle n° `6`).
2. **`logs/v1_050_adjacency.log` embarque une durée d'horloge murale** et des
   chemins absolus de la machine. Ce journal n'est pas un artefact, donc D11
   n'est pas violé, mais c'est le seul fichier que ma ré-exécution a fait
   diverger : arrondir la durée ou la sortir du fichier suivi rendrait la
   propriété « rejouer ne produit aucune différence » vraie sur l'ensemble des
   preuves committées, et non sur les seuls artefacts.
