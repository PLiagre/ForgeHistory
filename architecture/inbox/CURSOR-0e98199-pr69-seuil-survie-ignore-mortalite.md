---
audit_id:                CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
auditor:                 cursor-cloud
target_branch:           master
target_commit:           0e98199dac39a4a5a9a5f9d62f206c40d442d3f5
created_at:              2026-08-13T11:05:00Z
audit_type:              post-merge-architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit du commit de fusion `0e98199` — brief 013, « le tick nourrit une fois »

Audit du merge de la PR [#69](https://github.com/PLiagre/ForgeHistory/pull/69)
sur `master` (parents `538be56` côté `master`, `29913c0` côté lot ; 22 fichiers,
+4011 / −114). Méthode : `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Rôle : auditeur en lecture
seule ; cet audit **n'instruit rien** et ne vaut pas décision
(`architecture/README.md`). La veille externe du § 9 est produite par le rôle
compagnon `cursor-qa-scout` (`architecture/agents/cursor-qa-scout.md`).

Ce lot est la réponse à l'audit `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois`,
dont le constat P0 était que la nourriture échangée nourrissait deux fois. La
première question de cet audit est donc simple : **la correction tient-elle ?**
Réponse mesurée : oui, complètement. Toutes les mesures ci-dessous ont été
rejouées par l'auditeur avec ses propres sondes, écrites hors du dépôt et
recopiées en clair au § 8.

## 0. Synthèse

| # | Sévérité | Constat en une phrase |
|---|---|---|
| 1 | **P1** | Le seuil qui certifie « le monde vit » n'est pas un modèle de la survie : il **ignore** `HUNGER_DEATH_SCALE`, la constante qui pilote la mortalité, et il varie **en sens inverse** de `DEFICIT_RECOVERY_RATE_PER_TICK`. Le seul terme qui fait passer le test est aussi celui dont le signe est faux. |
| 2 | **P2** | La mortalité ne tue plus personne tant que le déficit cumulé reste sous 200 kg, **quelle que soit la population** — cinq ticks de famine totale pour une cellule de 20 habitants. Sur le monde réel, 48,6 % des cellules-ticks en déficit ne tuent personne et 24 346 morts fractionnaires sont jetées. |
| 3 | **P3** | Le plafond `MAX_DEATH_RATE_PER_TICK` n'est **jamais** atteint (0 cellule-tick sur 76 932) : la protection documentée contre « l'effondrement instantané » est inerte sur le monde réel. |
| 4 | **P3** | `_update_hunger` compte comme « affamée » une cellule qui a mangé sa ration entière : le garde-manger vide est confondu avec la faim. Effet mesuré sur le compteur publié `cellules_affamees_monde_reel_re` : **nul** (les 536 ont toutes connu un déficit réel). Piège latent, pas défaut actif. |
| 5 | **P3** | Classification CI : les 5 workflows `push` du commit sont verts ; sur la PR, 14 vérifications passent, 3 sont ignorées et 1 (`Reconcile local Hermes state`) reste en attente. Déjà consigné, non ré-instruit. |

Aucun constat P0. Ce qui tient — et c'est l'essentiel de ce lot — est au § 4.

## 1. Intention avant diff (lentille 1)

L'intention est traçable de bout en bout, ce qui reste rare : l'audit
`CURSOR-a4de4bb` a été contre-audité, décidé, converti en brief 013, exécuté en
deux itérations (`REJECT` puis `PASS`), puis fusionné. Le brief énonce 8
conditions de succès et route explicitement les points retenus qu'il ne traite
pas (`harness/queue/briefs/013-sim-tick-nourrit-une-fois/brief.md`, § Provenance
et § Non-Goals) : points 1, 2, 5, 6, 7 et 10 traités ici ; points 3 et 9 renvoyés
au brief `014` ; point 4 renvoyé au propriétaire comme question de gouvernance.
Ce routage écrit est une amélioration réelle par rapport au lot précédent : un
lecteur peut vérifier qu'aucun point retenu n'a été perdu en silence.

La critique ci-dessous porte donc sur l'écart entre ce que SC3 et SC4
**exigent** et ce que le code **fait** — pas sur une absence d'intention.

## 2. Portes mécaniques d'abord (lentille 3)

Toutes rejouées par l'auditeur (sorties complètes au § 8.1) :

| Porte | Résultat rejoué | Affirmation du lot |
|---|---|---|
| `verdict_audit.py` brief 013 | `VERDICT: ACCEPT` | conforme |
| `pytest sim/tests/` | `35 passed` | conforme |
| `pytest harness/tests/` | `314 passed, 16 skipped` | conforme |
| `harness_audit.py` | `SCORE: 20/24` | identique à l'état antérieur, pas de régression |
| `measure_sc6_013.py` (4 compteurs) | reproduits au chiffre près | conforme |

Deux limites des portes, utiles pour lire la suite :

- aucune porte ne teste la **sensibilité** d'un seuil dérivé. Le gate vérifie
  que `SEUIL_SURVIE_POPULATION_FRACTION` n'est plus un littéral et que le test
  peut rougir ; il ne vérifie pas que le seuil bouge dans le bon sens quand on
  change une constante du modèle. C'est exactement l'espace du constat 1 ;
- les 35 tests `sim/` vérifient la conservation de la masse, l'atomicité du
  transport et l'absence de plancher de mortalité. Aucun ne vérifie qu'une
  famine finit par tuer. C'est l'espace du constat 2.

## 3. Constats

### Constat 1 — P1 — Le seuil qui certifie « le monde vit » n'est pas un modèle de la survie

Rappel de la chronologie, parce qu'elle est le cœur du sujet. À l'itération 1, le
Générateur avait fixé la marge à `0.15` **après** avoir constaté que la mesure
(`0.766`) tombait hors de la fenêtre — et l'Évaluateur a prononcé `REJECT` pour
ce motif précis (`verdict.md`, § « SC3 en détail — pourquoi c'est un échec
disqualifiant »). À l'itération 2, la marge est remplacée par une **expression
calculée depuis les constantes** (`sim/constants.py`) :

```python
SURVIE_MARGE_DERIVEE = (
    _depassement_initial * _fraction_predite          # 0.10 × 0.9  = 0.0900
    + _p_tick_deficitaire * DEFICIT_RECOVERY_RATE_PER_TICK  # 0.611 × 0.10 = 0.0611
)                                                     # total ≈ 0.1511
```

L'Évaluateur a accepté, en déclarant lui-même un conflit d'intérêt (il avait
nommé les trois ingrédients de la formule dans son feedback) et en posant une
réserve explicite, `R9` : « Cela ne prouve pas que la composition des deux termes
soit la bonne physique » (`verdict.md`, ligne 641). Il a cassé quatre régimes
pour tester la falsifiabilité — densité doublée, production doublée, production
divisée par deux, consommation doublée — et le test rougit dans les quatre cas.

**Ce que cet audit apporte de neuf : ces quatre perturbations portent toutes sur
l'approvisionnement, jamais sur la mortalité.** Or la fenêtre est centrée sur
`_fraction_predite`, qui est justement construite depuis l'approvisionnement :
quand on déplace la production ou la densité, le centre de la fenêtre **et** la
mesure bougent ensemble, et le test réagit. Le côté mortalité n'a jamais été
touché. L'auditeur l'a fait, sur les deux constantes concernées.

Premier résultat — la constante qui pilote réellement la mortalité,
`HUNGER_DEATH_SCALE`, n'apparaît nulle part dans la formule du seuil (sonde 3,
§ 8.3) :

```
 HUNGER_DEATH_SCALE | survie mesuree | seuil derive | test
--------------------------------------------------------------
              0.001 |       0.869657 |     0.748889 | PASSE
              0.005 |       0.765706 |     0.748889 | PASSE <-- valeur livree
              0.010 |       0.680871 |     0.748889 | ECHOUE
              0.020 |       0.551459 |     0.748889 | ECHOUE
              0.050 |       0.338088 |     0.748889 | ECHOUE
```

La survie s'effondre de `0.87` à `0.34` pendant que le seuil ne bouge pas d'un
millième. Le critère rougit — donc il est falsifiable, c'est vrai — mais il
rougit **sans rien savoir** de ce qui a changé : il ne modélise pas la mortalité,
il l'ignore.

Second résultat, plus lourd — la seule constante de mortalité présente dans la
formule y entre **avec le mauvais signe** (sonde 2, § 8.2). Augmenter
`DEFICIT_RECOVERY_RATE_PER_TICK` veut dire pardonner le déficit plus vite, donc
tuer moins, donc survivre davantage ; la formule, elle, en déduit qu'il faut
**abaisser** le seuil :

```
taux recup | seuil derive | survie mesuree |    marge | test
----------------------------------------------------------------
      0.00 |       0.8100 |       0.150687 |  -0.6593 | ECHOUE
      0.05 |       0.7794 |       0.620905 |  -0.1585 | ECHOUE
      0.10 |       0.7489 |       0.765706 |   0.0168 | PASSE <-- valeur livree
      0.25 |       0.6572 |       0.846542 |   0.1893 | PASSE
      0.50 |       0.5044 |       0.869985 |   0.3655 | PASSE
      1.00 |       0.1989 |       0.886762 |   0.6879 | PASSE
```

Les deux colonnes centrales varient en sens opposés sur toute la plage. Une
grandeur qui prétend borner la survie et qui décroît quand la survie croît n'est
pas une dérivation de la survie.

Le point se resserre encore quand on regarde **quel terme** fait passer le test.
Le premier terme (`0.09`, le dépassement initial de la capacité de charge) est le
seul qui ait un sens physique clair ; à lui seul, il donne un seuil de `0.81` et
la mesure de `0.7657` **échoue**. C'est le second terme — la probabilité de tick
déficitaire multipliée par la vitesse de pardon, `0.0611`, un produit dont
l'homogénéité n'est démontrée nulle part — qui apporte exactement ce qu'il faut
pour passer. Et c'est ce même terme dont le signe est faux.

Enfin, l'argument de non-copie avancé par le journal du Générateur
(`deliverables/generator-log.md`, ligne 394) : « La valeur dérivée `0.1511`
diffère de la valeur calibrée de l'itération 1 (`0.15`), preuve qu'elle n'a pas
été copiée de la mesure. » Un écart de 0,7 % n'est pas une preuve d'indépendance :
c'est le résultat attendu si l'on cherche une composition de constantes qui
retombe sur un nombre déjà connu. La marge finale reste à `0.0168` de la mesure,
soit le point le plus serré de toute la plage explorée ci-dessus.

Portée honnête : ce constat **ne remet en cause aucun chiffre du lot**. La
physique du moteur est correcte (§ 4), les compteurs sont exacts et reproduits.
Ce qui est en cause est le critère qui certifie que la couche F2 « vit », et lui
seul. La sévérité est P1 et non P0 pour cette raison. Il ne s'agit pas non plus
de rejouer le motif du `REJECT` de l'itération 1 : la calibration après mesure
a bien été retirée, l'aveu est conservé dans `sim/SEEDING.md`, et l'élément neuf
est une **mesure de sensibilité** que personne n'avait faite.

### Constat 2 — P2 — La troncature `int()` remplace le plancher par une immunité

Le plancher `max(1, …)` du lot 012 — qui tuait quelqu'un pour tout déficit non
nul — a bien disparu. La formule est maintenant
`deaths = int(population × death_rate)` (`sim/engine.py`, `_apply_mortality`).
Le défaut symétrique apparaît : sous le plafond, `death_rate` vaut
`(deficit / population) × HUNGER_DEATH_SCALE`, donc
`deaths = int(deficit × 0.005)`, et le nombre de morts est **nul tant que le
déficit cumulé reste sous 200 kg — indépendamment de la population** (sonde 4,
§ 8.4) :

```
  deficit= 199.0 kg  population=     20  morts=0
  deficit= 199.0 kg  population= 200000  morts=0
  deficit= 200.0 kg  population=     20  morts=1
  deficit= 200.0 kg  population= 200000  morts=1
```

200 kg est une quantité **absolue** appliquée à des cellules dont la population
s'étale sur cinq ordres de grandeur. Pour une cellule d'un million d'habitants,
c'est une poussière. Pour une cellule de 20 habitants (besoin 40 kg par tick),
c'est **cinq ticks de famine totale sans un seul mort**. Cas limite : une
population de 9 habitants ou moins est structurellement immortelle, quel que
soit le déficit, parce que le plafond ramène le taux à 0,1 et que `int(0.9)` vaut
zéro (sonde 1 § D, § 8.1).

Portée mesurée sur le monde réel, et elle nuance le constat (sonde 5, § 8.5) :

```
cellules-ticks en deficit                       = 76932
  ... dont la troncature int() donne 0 mort     = 37384  (48.6 %)
morts fractionnaires perdues par la troncature  = 24345.7
cellules-ticks population < 10 avec deficit > 0 = 0
cellules a population < 10 au tick 200          = 0
```

Presque une cellule-tick affamée sur deux ne tue personne, et 24 346 morts sont
jetées à la virgule — mais cela ne représente que **0,16 %** des 15 666 208 morts
du monde réel, et aucune cellule ne descend sous 10 habitants sur cette
simulation. Le défaut est donc logique et structurel, pas numérique aujourd'hui :
il compte parce que la couche F2 est censée fournir une démographie utilisable
par les couches supérieures, et parce qu'une famine qui ne tue jamais dans les
petites cellules est un mécanisme de survie parasite qui apparaîtra dès que les
populations locales baisseront. D'où P2, pas P1.

### Constat 3 — P3 — Le plafond de mortalité ne protège rien aujourd'hui

`MAX_DEATH_RATE_PER_TICK = 0.10` est documenté comme empêchant « l'effondrement
instantané ». Sur les 200 ticks du monde réel, il n'est **jamais** atteint
(sonde 4, § 8.4) :

```
cellules-ticks en deficit sous le plafond = 76932 / 76932 (100.0 %)
cellules-ticks au plafond                 = 0 / 76932
```

Corollaire utile : faire varier ce plafond de `0.02` à `0.30` ne change la
fraction de survie que d'un cheveu au premier palier et pas du tout ensuite
(§ 8.3). Ce n'est pas un défaut du lot — c'est une réserve à connaître avant de
présenter ce plafond comme une garantie de stabilité. C'est aussi ce qui rend
caduc le sous-constat « plafond dépassé pour les petites populations » du lot
précédent : il ne peut plus l'être.

### Constat 4 — P3 — « Cellule affamée » désigne un garde-manger vide, pas un manque

`_update_hunger` incrémente `hunger_ticks` dès que `food_stock_kg <= 0`. Or, avec
le nouvel ordre du tick, une cellule que le commerce a nourrie **exactement** à
son besoin termine à un stock de zéro. Elle est donc comptée affamée alors
qu'elle a mangé sa ration entière (sonde 1 § A et § E, § 8.1) :

```
temoin   : stock=0.0  deficit=0.0  hunger_ticks=1
receveuse: stock=0.0  deficit=0.0  hunger_ticks=1  recu=200.0 kg
```

Cadrage adverse, et le résultat est négatif : l'auditeur a cherché si le
compteur publié `cellules_affamees_monde_reel_re = 536` était gonflé par cet
effet. **Il ne l'est pas.** Sur les 536 cellules comptées affamées, **zéro** a
traversé les 200 ticks avec un déficit constamment nul (§ 8.5) — toutes ont
réellement manqué de nourriture à un moment. Le chiffre publié est honnête ; le
piège reste latent dans la définition. D'où P3.

### Constat 5 — P3 — Classification CI du commit audité

Les cinq workflows déclenchés par le `push` sur `master` pour `0e98199` sont
verts : `security`, `audit-guard`, `hermes-dashboard`, `pipeline-audit`,
`harness-ci` (§ 8.6). Sur la PR #69 : 14 vérifications `pass` (dont `tests`,
`sim-tests`, `schema`, `actionlint`, `gitleaks`, `f0-demo`,
`invoke-cursor-auditor`), 3 `skipping` (`cursor-scope`, qui ne s'applique qu'aux
branches `cursor/*`, et `check-and-automerge`), et **1 en attente** :
`Reconcile local Hermes state`, sur exécuteur Windows auto-hébergé hors ligne.
Cette dernière réserve est déjà consignée par l'audit `CURSOR-a4de4bb` (constat
10) ; elle n'est pas ré-instruite, elle est seulement classée, comme la « Preuve
de fin » du contrat l'exige.

Même remarque pour l'état de la boucle : le registre
`architecture/audit-ledger.jsonl` s'arrête à `AUDIT_CONVERTED` pour
`CURSOR-a4de4bb` (dernière ligne, 08:40:34Z), alors que le brief issu de cette
conversion est fusionné depuis 12:48. Les transitions terminales
(`AUDIT_IMPLEMENTED`, `AUDIT_VERIFIED`) restent à poser à la main. Ce motif est
déjà l'objet des audits `CURSOR-7e5244b-ledger-post-fusion-poussee-master` et
`CURSOR-4c45718-pr65-ledger-recupere-a-la-main` : consigné, non ré-instruit,
aucun brief proposé ici pour ce point.

## 4. Ce qui tient (cadrage adverse, résultats négatifs)

La lentille 4 demande de chercher où les affirmations sont fausses. Sur ce lot,
la plupart ne le sont pas, et c'est le résultat principal de cet audit.

- **Le P0 du lot précédent est réellement fermé.** La sonde qui l'avait établi a
  été rejouée telle quelle : une cellule nourrie par le commerce et une cellule
  qui possédait déjà sa ration finissent maintenant dans **exactement** le même
  état — écart de stock `0.0` (§ 8.1 § A), contre 200 kg d'avance auparavant.
  Le maillon commerce ne touche plus `food_deficit_kg` du tout.
- **Le transport ne franchit plus qu'une arête.** Sur la chaîne `1—2—3` où la
  cellule 3 n'est pas adjacente à la cellule 1, la cellule 3 reçoit `0.0` kg, et
  l'état final est identique sous les deux ordres d'arêtes (§ 8.1 § B). Sur le
  monde réel, trois mélanges aléatoires de `world.adjacency` au tick 200 donnent
  le même état à la sixième décimale et les mêmes 1 200 kg transportés (§ 8.5).
  Le calcul sur snapshot en deux passes tient ce qu'il annonce.
- **Le compteur de transport mesure enfin des kilogrammes arrivés.** Kg comptés
  et kg réellement arrivés (somme des variations positives de stock pendant
  l'étape de commerce) sont **le même nombre** : 2 676 487 contre 2 676 487,
  écart `0.0` (§ 8.5). L'écart de 8,82 % du lot précédent a disparu.
- **Les quatre compteurs du monde réel se reproduisent au chiffre près** :
  15 666 208 morts, 2 676 487 kg, survie `0.765706`, 536 cellules affamées
  (§ 8.5), identiques au manifeste. Aucune correction hallucinée.
- **Le déficit a maintenant une mémoire.** Un tick d'excédent ne l'efface plus :
  il en retire 10 %, avec une coupure epsilon pour éviter les résidus non
  physiques. Le compteur `deficit_non_efface_en_1_tick = 9000.0` (sur 10 000)
  du manifeste est cohérent avec le code lu.
- **L'Évaluateur a déclaré son conflit d'intérêt de lui-même.** Il écrit qu'ayant
  nommé les trois ingrédients de la formule dans son feedback, son jugement sur
  SC3 « n'est pas pleinement indépendant » (`verdict.md`, ligne 512), et il pose
  la réserve `R9` qui laisse ouverte la question de la physique de la formule.
  Le constat 1 ci-dessus est la mesure de cette réserve, pas sa contradiction.
  Cette honnêteté déclarative est exactement ce que l'état de l'art réclame
  quand producteur et vérificateur partagent une infrastructure [S1, S2].
- **Le périmètre de la PR s'est resserré.** 22 fichiers contre 30, et surtout un
  seul objet (le lot 013) plus la clôture de session, au lieu des cinq objets
  distincts du lot précédent. Sur les +4011 lignes, environ 2 300 sont de la
  prose de traçabilité (verdict 867, journal 539, brief 347, rubrique 275,
  feedback 276) et environ 1 100 du code et des tests. La charge de relecture
  réelle reste sous la limite des ~400 lignes de code que la lentille 5 vise.

## 5. Limite de cet audit (à lire avant de s'en servir)

Les sept commits du lot portent une seule identité git :
`git log --format='%an' 538be56..29913c0 | sort | uniq -c` → `7 Cursor Agent`
(§ 8.6). Cet audit est lui aussi produit par un agent Cursor. L'indépendance
revendiquée par `architecture/agents/cursor-auditor.md` repose donc, sur ce
commit encore, sur la séparation des sessions et des contextes, pas sur celle
des infrastructures. Ce point est le constat 4 déjà retenu de l'audit
`CURSOR-a4de4bb`, explicitement renvoyé au propriétaire par le non-goal 2 du
brief 013 : il n'est pas ré-instruit ici. La compensation offerte est la même que
la fois précédente, et elle a été tenue : aucun constat n'est énoncé sans une
mesure rejouée par l'auditeur, et toutes les sondes sont publiées en clair au
§ 8 pour qu'un tiers puisse les contredire.

Une hypothèse de l'auditeur a d'ailleurs été **abandonnée en cours de route**
faute de preuve : le fait que le nombre absolu de morts soit indépendant de la
population sous le plafond n'est pas un défaut, c'est l'algèbre correcte d'un
taux proportionnel au déficit par tête. Elle n'apparaît pas dans les constats.

## 6. Briefs atomiques proposés (2 — proposition, pas instruction)

1. **Un seuil de survie qui modélise la mortalité, et une preuve de sensibilité
   qui l'accompagne.** Faire entrer dans la dérivation du seuil les constantes
   qui décident réellement de la survie (`HUNGER_DEATH_SCALE`,
   `MAX_DEATH_RATE_PER_TICK`, `DEFICIT_RECOVERY_RATE_PER_TICK`), et exiger que la
   dérivation soit **prouvée par sa sensibilité** et non par sa seule
   falsifiabilité : un test qui échoue si, en faisant varier une constante du
   modèle, la survie mesurée et le seuil dérivé bougent en sens contraires. La
   plage du § 8.2 fournit directement les cas de test. Le remède minimal, si la
   dérivation complète est trop coûteuse, est de remplacer le seuil par une
   comparaison entre survie mesurée et survie **prédite par un modèle de
   mortalité** — c'est-à-dire de juger un écart, pas un plancher.
2. **Une mortalité qui n'oublie pas ses morts, et des indicateurs de famine qui
   disent ce qu'ils comptent.** Reporter la fraction de mort non appliquée d'un
   tick au suivant (accumulateur par cellule) plutôt que de la jeter, de sorte
   qu'une famine prolongée finisse toujours par tuer et qu'aucune population ne
   soit structurellement immortelle ; tests exigés : une cellule de 5 habitants
   en famine totale perd des habitants en un nombre borné de ticks, et la somme
   des morts sur N ticks ne s'écarte pas de la valeur exacte de plus d'une unité
   par cellule. Dans le même lot, distinguer « garde-manger vide » de « en
   manque » dans `_update_hunger` et dans le compteur
   `cellules_affamees_monde_reel_re`, puis re-mesurer — le chiffre changera peu
   ou pas (§ 8.5 le montre), mais il voudra enfin dire ce que son nom annonce.

Aucun troisième brief n'est proposé : les constats 3, 4 et 5 ne le justifient
pas, et le contrat plafonne à trois sans jamais en exiger trois.

## 7. Risques par sévérité

| Sévérité | Constats | Risque si rien n'est fait |
|---|---|---|
| P0 | — | Aucun. Le défaut bloquant du lot précédent est fermé et vérifié. |
| P1 | 1 | La couche F2 est déclarée vivante par un critère aveugle à la mortalité : n'importe quel réglage futur de la démographie passera ou échouera pour de mauvaises raisons, et le harnais croira avoir une garde là où il n'en a pas. |
| P2 | 2 | Une famine qui ne tue jamais en dessous d'un seuil absolu : le mécanisme deviendra visible dès que les populations locales baisseront, et il produira des poches de survivants que rien ne justifie dans le monde simulé. |
| P3 | 3, 4, 5 | Un plafond de sécurité présenté comme actif alors qu'il ne l'est pas ; un compteur dont le nom promet plus que sa définition ; une « CI verte » énoncée alors qu'une vérification n'a jamais démarré, et une boucle d'audit dont les états terminaux restent manuels. |

## 8. Commandes rejouées (sorties collées)

Environnement : dépôt à `0e98199`, interpréteur `/workspace/.venv/bin/python`.
Les quatre sondes sont des programmes courts écrits sous `/tmp/`, qui n'écrivent
rien dans le dépôt. Les modifications de constantes des sondes 2 et 3 sont faites
**en mémoire** sur le module chargé, jamais sur les fichiers.

### 8.1 Sonde 1 — ration transférée, chaîne 1—2—3, plancher de mortalité

```python
# /tmp/probe_a_0e98199.py (extraits significatifs)
# A. temoin (possede sa ration, aucun voisin) vs receveuse (tout par le commerce)
_apply_commerce(w, acc); _apply_consumption(cell); _update_hunger(cell)
# B. chaine 1--2--3 : seule la cellule 1 a du stock, 3 n'est pas adjacente a 1
# D. mortalite : deficit enorme, populations croissantes
```

```
=== A. La ration transferee nourrit-elle une seule fois ? ===
population = 100  besoin/tick = 200.0 kg
temoin   : stock=0.0  deficit=0.0  hunger_ticks=1
receveuse: stock=0.0  deficit=0.0  hunger_ticks=1  recu=200.0 kg
ecart de stock final temoin/receveuse = 0.0

=== B. Le transport franchit-il plus d'une arete par tick ? ===
aretes [(1, 2), (2, 3)] : stocks={1: 800.0, 2: 200.0, 3: 0.0}  kg comptes=200.0
aretes [(2, 3), (1, 2)] : stocks={1: 800.0, 2: 200.0, 3: 0.0}  kg comptes=200.0
capacite par arete/tick = 200.0 ; la cellule 3 n'est pas adjacente a la cellule 1

=== D. Mortalite : la troncature int() ===
plafond documente MAX_DEATH_RATE_PER_TICK = 0.1
deficit ENORME (100x le besoin), taux au plafond :
  population=    1  morts=0  taux effectif=0.000
  population=    5  morts=0  taux effectif=0.000
  population=    9  morts=0  taux effectif=0.000
  population=   10  morts=1  taux effectif=0.100
  population=   50  morts=5  taux effectif=0.100
  population= 1000  morts=100  taux effectif=0.100
deficit croissant, population 1000 :
  deficit=     1e-09 kg  morts=0
  deficit=       1.0 kg  morts=0
  deficit=     100.0 kg  morts=0
  deficit=     399.0 kg  morts=1
  deficit=     400.0 kg  morts=2
  deficit=    1000.0 kg  morts=5

=== E. Une cellule affamee a-t-elle vraiment manque de nourriture ? ===
receveuse du cas A : deficit=0.0 (jamais en manque) et hunger_ticks=1
temoin  du cas A   : deficit=0.0 (jamais en manque) et hunger_ticks=1
```

### 8.2 Sonde 2 — le seuil dérivé suit-il la vitesse de pardon du déficit ?

```python
# /tmp/probe_b_0e98199.py § H — DEFICIT_RECOVERY_RATE_PER_TICK modifie en memoire
E.DEFICIT_RECOVERY_RATE_PER_TICK = taux
seuil = 0.9 - (0.09 + (11 / 18) * taux)     # formule livree, recalculee a la main
world = World.from_g3(rng_seed=42); rng = random.Random(42)
for _ in range(200): E.tick(world, rng)
```

```
formule livree : seuil = 0.9 - (0.09 + p_deficit x taux),  p_deficit = 11/18 = 0.6111

taux recup | seuil derive | survie mesuree |    marge | test
----------------------------------------------------------------
      0.00 |       0.8100 |       0.150687 |  -0.6593 | ECHOUE
      0.05 |       0.7794 |       0.620905 |  -0.1585 | ECHOUE
      0.10 |       0.7489 |       0.765706 |   0.0168 | PASSE <-- valeur livree
      0.25 |       0.6572 |       0.846542 |   0.1893 | PASSE
      0.50 |       0.5044 |       0.869985 |   0.3655 | PASSE
      1.00 |       0.1989 |       0.886762 |   0.6879 | PASSE

seuil livre = 0.748889   marge derivee = 0.151111
```

### 8.3 Sonde 3 — le seuil dérivé connaît-il les constantes de mortalité ?

```
HUNGER_DEATH_SCALE pilote la mortalite mais n'apparait PAS dans la formule du seuil.
seuil derive (constant) = 0.748889 ; fenetre du test = [0.7489, 1.0511]

 HUNGER_DEATH_SCALE | survie mesuree | seuil derive | test
--------------------------------------------------------------
              0.001 |       0.869657 |     0.748889 | PASSE
              0.005 |       0.765706 |     0.748889 | PASSE <-- valeur livree
              0.010 |       0.680871 |     0.748889 | ECHOUE
              0.020 |       0.551459 |     0.748889 | ECHOUE
              0.050 |       0.338088 |     0.748889 | ECHOUE

Idem pour MAX_DEATH_RATE_PER_TICK (plafond de mortalite), absent de la formule :
     MAX_DEATH_RATE | survie mesuree | seuil derive | test
--------------------------------------------------------------
               0.02 |       0.769788 |     0.748889 | PASSE
               0.05 |       0.765706 |     0.748889 | PASSE
               0.10 |       0.765706 |     0.748889 | PASSE <-- valeur livree
               0.30 |       0.765706 |     0.748889 | PASSE
```

### 8.4 Sonde 4 — seuil d'immunité absolu et inertie du plafond

```
Seuil d'immunite absolu : deaths = 0 tant que deficit x SCALE < 1,
soit deficit < 200.0 kg, quelle que soit la population.
  deficit= 150.0 kg  population=     20  morts=0
  deficit= 150.0 kg  population= 200000  morts=0
  deficit= 199.0 kg  population=     20  morts=0
  deficit= 199.0 kg  population= 200000  morts=0
  deficit= 200.0 kg  population=     20  morts=1
  deficit= 200.0 kg  population= 200000  morts=1

=== Part des cellules-ticks sous le plafond sur le monde reel (N=200) ===
cellules-ticks en deficit sous le plafond = 76932 / 76932 (100.0 %)
cellules-ticks au plafond                 = 0 / 76932
```

### 8.5 Sonde 5 — monde réel instrumenté (200 ticks, graines 42/42)

```
=== F. Monde reel : rejeu des compteurs + instrumentation ===
pop initiale = 66865505   pop finale = 51199297
morts_cumules_monde_reel_re  = 15666208
kg_transportes_monde_reel_re = 2676487
fraction_survie_monde_reel_re= 0.765706
cellules_affamees_monde_reel_re = 536  (sur 596)

kg comptes 2676487 vs kg reellement arrives 2676487 -> ecart = 0.0

cellules comptees affamees mais dont le deficit est TOUJOURS reste nul = 0  (0.0 % des affamees)
cellules ayant reellement connu un deficit = 536

cellules-ticks en deficit                       = 76932
  ... dont la troncature int() donne 0 mort     = 37384  (48.6 %)
morts fractionnaires perdues par la troncature  = 24345.7
cellules-ticks population < 10 avec deficit > 0 = 0
cellules a population < 10 au tick 200           = 0

=== G. Invariance a l'ordre des aretes quand le commerce est actif (tick 200) ===
melange #0: kg transportes=1200.0  etat identique au 1er = True
melange #1: kg transportes=1200.0  etat identique au 1er = True
melange #2: kg transportes=1200.0  etat identique au 1er = True
```

### 8.6 Portes mécaniques, CI et identité des commits

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
VERDICT: ACCEPT
$ .venv/bin/python -m pytest sim/tests/ -q
35 passed in 2.72s
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 20.85s
$ .venv/bin/python harness/harness_audit.py
SCORE: 20/24
$ git log --format='%an' 538be56..29913c0 | sort | uniq -c
      7 Cursor Agent
$ git diff --stat 538be56..0e98199 | tail -1
 22 files changed, 4011 insertions(+), 114 deletions(-)
$ gh run list --commit 0e98199... --json name,status,conclusion,workflowName,event
     success |          push | security
     success |          push | audit-guard
     success |          push | hermes-dashboard
     success |          push | pipeline-audit
     success |          push | harness-ci
$ gh pr checks 69
14 pass, 3 skipping, 1 pending (Reconcile local Hermes state)
```

## 9. Veille externe — section `cursor-qa-scout` (append-only)

Section produite par le rôle compagnon `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`), en appui de l'audit ci-dessus. Elle
compare l'état du dépôt à l'état de l'art ; **elle n'instruit rien** et ne
formule aucune recommandation exécutable.

**Thème du cycle : plafonds de coût (« cost caps »), l'un des trois axes du
brief 006.** État du dépôt : `harness/budget.py` mesure le budget **après coup**,
en relisant les transcriptions de session locales, et le lot 013 a dû porter une
dérogation dont le texte est sans ambiguïté (`deliverables/manifest.json`,
§ `waivers`) : « `status: UNMEASURABLE` […] Nothing is being enforced. This is
not OK -- it is unmeasured. » État de l'art 2026 : le consensus des sources
consultées est que le budget de jetons doit être évalué **avant** l'appel, dans
une couche de gouvernance extérieure au code de l'agent — un budget par session
qui termine la session avant que l'appel suivant n'aboutisse [S5], appliqué à la
passerelle pour qu'aucun chemin de code ne puisse le contourner [S6]. L'écart
est net et documenté des deux côtés : le dépôt observe, l'état de l'art impose.

**Comparaison sur l'axe « boucles agentiques ».** Le dépôt applique déjà deux
pratiques que les retours d'expérience 2026 désignent comme structurantes :
l'agent d'audit est en **lecture seule** et ne peut pas écrire de code [S3], et
les rôles sont spécialisés avec des portes de validation par phase [S4]. Le
point où le dépôt est en avance sur la plupart des retours publiés est la
déclaration de conflit d'intérêt de l'Évaluateur ; le point où il est en retard
est la validation d'un critère auto-dérivé, que la littérature traite
explicitement : un score de tâche aval **ne peut pas** valider un évaluateur qui
s'est co-construit avec ce qu'il mesure, et une métrique privée de ses ancres
externes « s'effondre en détecteur toujours-passant » [S1]. Le constat 1 de cet
audit est une instance mesurée de ce mécanisme. Le protocole `CapBencher` [S2]
propose le remède de forme le plus proche : rendre le plafond atteignable
**impossible** sans accès au résultat, de sorte qu'un score qui le dépasse
signale de lui-même que la mesure a fui dans le critère.

**Déclaration de non-duplication.** Briefs ouverts vérifiés avant écriture :
`006-full-auto-agent-pipeline`, `008-contexte-opus5-right-sizing`,
`008-full-auto-automation-gaps`, `009-full-auto-agent-invocation`,
`010-repartition-roles-full-auto`, `011-sim-monde-vivant-amorcage`,
`012-monde-vivant-commerce-inter-cellules`, `013-sim-tick-nourrit-une-fois`,
`014-pipeline-contre-audit-porte`. Aucun ne porte sur la dérivation d'un seuil de
survie ni sur l'arrondi de la mortalité : les deux briefs du § 6 ne dupliquent
rien. Sur l'axe budget, aucun brief ouvert ne traite l'imposition avant appel, et
l'audit qui s'en approchait, `CURSOR-6231186-execution-budgets`, est en état
terminal `AUDIT_ARCHIVED` après un passage par `AUDIT_STALE` : c'est pourquoi
cette veille se limite à une comparaison et ne propose **aucun** brief sur ce
thème.

## 10. Sources externes

| # | source | date de la source | consulté le |
|---|---|---|---|
| S1 | *Who Grades the Grader? Co-Evolving Evaluation Metrics and Skills for Self-Improving LLM Agents* — arXiv 2607.12790 — <https://www.alphaxiv.org/abs/2607.12790> (un score de tâche aval ne peut pas valider un évaluateur co-évolué ; sans ancre externe la métrique s'effondre en détecteur toujours-passant) | 2026-07 | 2026-08-13 |
| S2 | Ishida Lab — *CapBencher: Give Your LLM Benchmark a Built-in Alarm for Test-Set Overfitting* — <https://ishida-lab.github.io/blog_capbencher.html> (plafond de justesse volontairement inatteignable : un score au-dessus signale que la mesure a fui dans le critère) | 2026 | 2026-08-13 |
| S3 | Medium (B. Luelling) — *How We Built a 16-Agent SDLC That Ships Features End-to-End* — <https://medium.com/@brettluelling/how-we-built-a-16-agent-sdlc-that-ships-features-end-to-end-2a3621fc9e64> (agents de revue et d'audit strictement en lecture seule ; portes de validation par phase) | 2026 | 2026-08-13 |
| S4 | Growin — *AI Agents in Software Development: A 2026 CTO Guide* — <https://www.growin.com/blog/ai-agents-in-software-development-26/> (commencer par les domaines à vérifiabilité élevée : revue, tests, orchestration CI) | 2026 | 2026-08-13 |
| S5 | Waxell — *AI Agent Token Budget Enforcement [2026]* — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> (budget par session imposé avant l'appel, pas constaté après) | 2026 | 2026-08-13 |
| S6 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers With Code Examples (2026)* — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> (imposition à la passerelle, impossible à contourner par l'agent) | 2026 | 2026-08-13 |

S3 à S6 couvrent les trois thèmes de veille exigés par le contrat
(`architecture/agents/cursor-auditor.md`, § Preuve de fin) : pipeline de
développement autonome, orchestration d'agents en CI, budget de jetons des
agents. S1 et S2 fondent le constat 1 et le § 9.

---

Fin de l'audit. Statut `PROPOSED` : aucun point ci-dessus n'est une instruction,
aucun n'autorise une implémentation. Le contre-audit
(`architecture/reviews/`), puis la décision (`architecture/decisions/` ou la
politique automatique d'ADR-0006), restent seuls compétents.
