# MODE-EMPLOI — je veux faire avancer le jeu, je fais quoi maintenant ?

> Une page. Ce qu'il faut faire, dans l'ordre, avec les commandes exactes.
> Les **règles** vivent dans [AGENTS.md](../AGENTS.md), l'**état du projet**
> dans [ROADMAP.md](../ROADMAP.md), le **modèle du monde** dans
> [sim/MODELE.md](../sim/MODELE.md). Cette page ne les paraphrase pas : elle
> dit qui agit, sur quelle machine, avec quelle commande.

---

## Les trois acteurs, et surtout ce qu'ils ne font pas

| acteur | où il tourne | il fait | **il ne fait pas** |
|---|---|---|---|
| **Hermes** | VPS | demande le `brief.md` à Claude, le fait relire, lance ForgePilot, mesure, rend compte | **il n'écrit pas le brief, ne code pas, ne fusionne pas, et ne dit jamais si un lot est recevable** |
| **Cursor** | VPS, lancé par ForgePilot | exécute le brief, ouvre la PR, se relit dans une invocation neuve, itère jusqu'au vert | **il ne décide pas de ce qui est recevable — même sur son propre travail** |
| **Claude** | à la demande, hors du harnais | **écrit le `brief.md`** (ADR-0019) ; tient `sim/MODELE.md` ; regard de dernier recours quand un lot ne converge pas | **il ne relit pas son propre brief, ne juge aucun lot, et n'a ni agent ni cron dans le pilotage quotidien** |

Et vous : **vous seul fusionnez.** C'est le seul geste que personne d'autre
ne peut faire.

Le PC Windows, allumé, est un **worker** GitHub, pas un second Hermes :
`forgepilot workers --repo /srv/ForgeHistory` le constate ; s'il est éteint,
la tâche machine est refusée et le VPS continue. Hermes Desktop se branche
sur le Hermes du VPS ; le profil local n'écrit pas ForgeHistory. Détail :
[operations/pc-windows-worker.md](operations/pc-windows-worker.md).

La règle qui tient tout : **celui qui produit ne prononce pas la recevabilité
de son propre travail.** Elle est tenue par deux choses mécaniques — la porte
`harness/verdict_audit.py`, et la relecture Cursor dans une invocation neuve
qui n'a pas vu le code s'écrire.

---

## Le chemin d'un lot, de l'idée à la fusion

| # | qui | machine | commande exacte | ce qui sort |
|---|---|---|---|---|
| 1 | **vous** | n'importe où | dire à Hermes ce que vous voulez | rien encore |
| 2 | **Claude** | à la demande, hors du harnais | *(il rédige)* | `harness/queue/briefs/NNN-slug/brief.md` |
| 3 | Hermes | VPS | `forgepilot brief-review harness/queue/briefs/NNN-slug/brief.md --repo /srv/ForgeHistory --run` | un verdict sur le **brief**, avant tout code |
| 4 | **vous** | — | **vous lisez le brief et son verdict** | votre accord, ou une correction |
| 5 | Hermes | VPS | `forgepilot doctor --repo /srv/ForgeHistory --check-auth` | « poste de pilotage sain » |
| 6 | Hermes | VPS | `forgepilot start /srv/ForgeHistory/harness/queue/briefs/NNN-slug/brief.md --repo /srv/ForgeHistory --run` | un `RUN_ID`, une branche `agent/NNN-slug`, une draft PR |
| 7 | Cursor | VPS | *(lancé par ForgePilot)* | des commits, les tests verts, une relecture |
| 8 | Hermes | VPS | `forgepilot status latest --repo /srv/ForgeHistory` | où en est le lot |
| 9 | Hermes | VPS | `forgepilot verdict latest --repo /srv/ForgeHistory` | le compte-rendu, passé à la porte |
| 10 | CI | GitHub | *(automatique sur la PR)* | 5 travaux : `sim-tests`, `viewer-tests`, `harness-tests`, `control-plane-tests`, `f0-demo` |
| 11 | **vous** | GitHub | **vous lisez le diff, puis vous fusionnez** | `master` |

**L'étape 3 est nouvelle, et c'est la moins chère du tableau.** Un relecteur
lit le brief avant qu'une seule ligne soit écrite, et cherche six défauts :
plusieurs lots dans un seul, un critère invérifiable, un compteur sans
dénominateur dérivé, une demande de modifier un test existant, un niveau de
fidélité absent, un périmètre d'écriture trop large. Le plus grave est le
quatrième : ajuster un contrôle après avoir vu une mesure est une calibration
déguisée. Sans cette étape, ces défauts se découvraient par l'échec du code,
après jusqu'à deux heures de travail.

Elle n'est pas facultative parce que le brief vient de Claude : **l'auteur
d'un brief n'est jamais son relecteur.** C'est la même règle que pour le code,
appliquée un cran plus tôt.

Un lot purement documentaire (R0) n'a pas de relecteur : la commande le
refuse en le disant, au lieu de lancer un agent pour rien.

**Ce qui produit quoi, sur le disque :**

```text
harness/queue/briefs/NNN-slug/
├── brief.md          ← écrit par Claude. LA seule source d'instruction du lot.
├── eval-rubric.md    ← les critères, écrits AVANT les livrables
├── verdict.md        ← le compte-rendu, écrit à la fin
└── deliverables/
    └── manifest.json ← les compteurs mesurés

.forgepilot/runs/<RUN_ID>/state.json   ← l'état atomique, reprenable
```

Le brief n'est pas une formalité : c'est le seul document qui dit quoi faire.
Aucun autre ne peut le paraphraser — `harness/tests/test_single_source_of_instruction.py`
le vérifie.

---

## Ce que vous faites, vous, et à quels moments

Quatre moments, pas plus.

1. **Avant le lot** — vous lisez le `brief.md`. C'est le seul moment où
   corriger la cible coûte zéro. Après, ça coûte un lot.
2. **Si un arbitrage remonte** — Hermes expose un blocage précis. Vous
   tranchez. Il écrit votre décision dans `hermes/requests/DEMANDE-*.md`.
3. **Avant de fusionner** — vous lisez le diff. Pas le compte-rendu : **le
   diff**. La règle 11 du dépôt existe pour ça : quatre défauts majeurs ont
   été vus à l'œil que des suites 100 % vertes n'ont jamais attrapés.
4. **Pour une décision structurelle** — vous demandez un ADR daté sous
   `docs/adr/`. Un changement d'organisation sans ADR se rejoue six mois plus
   tard.

Le reste tourne sans vous.

---

## Quand ça coince

**Le lot ne converge pas en trois itérations.**
Arrêtez de relancer : trois échecs disent que le brief est faux, pas que
l'exécutant est mauvais. Appelez Claude — c'est lui qui a écrit le brief et
qui tient le modèle (`forgepilot witness latest --repo /srv/ForgeHistory`, ou
directement).

Il n'a que **deux réponses possibles**, et c'est ADR-0019 qui les lui impose :
*le brief est faux, en voici la réécriture*, ou *je ne peux pas trancher,
c'est à vous*. Il ne peut pas répondre « le brief est bon, relancez » — ce
serait déclarer recevable son propre travail. Si vous recevez cette
réponse-là, refusez-la. Un brief réécrit repasse par `brief-review`, comme le
premier.

**La CI est rouge.**
Regardez *quel* travail. `sim-tests` rouge = le jeu est cassé, c'est le lot.
`control-plane-tests` rouge = l'outil de pilotage, pas le jeu. Reproduisez en
local avant de corriger :

```bash
python -m pytest sim/tests/ -q            # le jeu
python -m pytest viewer/tests/ -q         # le regard mince
python -m pytest harness/tests/ -q        # la porte mécanique
cd control-plane && python -m unittest discover -s tests
```

Un test rouge n'est jamais désactivé pour passer au vert. S'il gêne, c'est
qu'il protège quelque chose ou qu'il ne devrait pas exister : les deux
réponses sont dans la règle d'admission d'AGENTS.md, aucune n'est « le
sauter ».

**Le lot est bloqué en cours de route.**

```bash
forgepilot status latest --repo /srv/ForgeHistory   # voir où
forgepilot resume latest --repo /srv/ForgeHistory   # repartir de l'étape incomplète
```

`resume` repart de la première étape incomplète : rien n'est refait deux fois,
une branche déjà créée est récupérée et pas recréée.

**Deux acteurs ne sont pas d'accord.**
Ce n'est pas à eux de trancher, et surtout pas à celui qui a produit le code.
L'ordre est : la porte mécanique d'abord (elle est déterministe, elle ne
discute pas) ; puis la règle d'AGENTS.md, s'il y en a une qui couvre le cas ;
puis vous. Si le désaccord porte sur **comment le monde fonctionne**, c'est
`sim/MODELE.md`, donc Claude. Si personne ne peut trancher sans vous, la
bonne réponse d'Hermes est d'exposer le blocage — pas de fabriquer une
décision.

**Le doute porte sur une anomalie historique.**
Regardez le niveau de fidélité (AGENTS.md, « Vraisemblable, pas véridique »).
Un rendement bizarre, un village mal peuplé, un climat local surprenant :
c'est du **niveau 2**, généré et jamais sourcé. **Ce n'est pas un défaut** et
ça n'ouvre ni correctif, ni brief. Ne lancez pas un lot là-dessus.

---

## Un exemple complet : un lot réel, du début à la fin

Le lot 033 — **faire jouer le relief par le tick** — est passé par les onze
étapes et a été fusionné le 2026-08-25 (PR #137). Une cellule de montagne ne
produit plus comme une plaine. Déroulé tel qu'il s'est passé.

**1. Vous** — « le relief est dans la carte mais le tick l'ignore,
occupe-t'en. »

**2. Claude** écrit `harness/queue/briefs/033-relief-dans-le-rendement/brief.md`.
Le brief dit, au minimum :
- ce qui change : `production_kg()` dans `sim/engine.py`, et **rien d'autre**
  dans le moteur ;
- la donnée lue : `carte[cell_id]["relief"]`, une des cinq classes
  (`plaine`, `colline`, `montagne`, `haute_montagne`, `marais`) ;
- le niveau de fidélité : les facteurs de rendement par classe sont du
  **niveau 2** — plausibles, jamais sourcés, et une valeur qui surprend n'est
  pas un défaut ;
- les compteurs à mesurer, avec leur dénominateur dérivé ;
- ce qui doit rester vrai : `python -m sim --ticks 20 --json` reste
  déterministe, et les trois propriétés de `sim/tests/test_survie.py` restent
  vertes **sans être modifiées**.

Ce dernier point est le cœur du lot, et il est vrai depuis peu : le plafond
physique de survie appelle `production_kg()`, la même fonction que le tick.
Il suit donc tout seul. Un modèle analytique prédisait autrefois la valeur
absolue de la survie et aurait dû être re-dérivé ici ; il ne l'est plus.

**3. Vous** lisez le brief. Point à vérifier : est-ce qu'il touche à une
seule fonction ? Si le brief commence à parler de climat ou de gisements en
même temps, c'est trois lots, pas un.

**4-5. Hermes**, sur le VPS :

```bash
forgepilot doctor --repo /srv/ForgeHistory --check-auth
forgepilot start /srv/ForgeHistory/harness/queue/briefs/033-relief-dans-le-rendement/brief.md \
    --repo /srv/ForgeHistory --run
```

**6. Cursor** modifie `production_kg()`, ajoute les facteurs comme constantes
nommées dans `sim/constants.py` (`sim/tests/test_no_hardcoded.py` refuse tout
littéral numérique dans une fonction), lance les tests, se relit.

Ce qui rougira si le lot est bâclé, et c'est voulu :
- une constante ajoutée que personne ne lit → `test_aucune_constante_terminale` ;
- une constante lue par valeur au lieu du module → `test_le_moteur_ne_lie_aucune_constante_par_valeur` ;
- un facteur qui fait mourir tout le monde → `test_le_monde_ne_meurt_pas...` ;
- un facteur qui fait apparaître de la nourriture → le même test, par le haut.

**7-8. Hermes** suit et fait passer la porte :

```bash
forgepilot status  latest --repo /srv/ForgeHistory
forgepilot verdict latest --repo /srv/ForgeHistory
```

**9. La CI** rejoue les cinq travaux sur la PR.

**10. Vous** lisez le diff. Trois questions, dans cet ordre :
- est-ce que `production_kg()` est le **seul** endroit modifié dans le moteur ?
- est-ce que les tests de survie sont **inchangés** ? (s'ils ont été retouchés
  pour passer, c'est de la calibration après mesure — refusez) ;
- est-ce que la sortie de `python -m sim --ticks 20 --json` a changé ?
  **Elle doit changer, et beaucoup.** Mesuré en rejouant le tick avec des
  facteurs plausibles (plaine 1,0 · colline 0,8 · montagne 0,45 · haute
  montagne 0,15 · marais 0,5), à 20 ticks et graine 0 :

  | | sans relief (`448aa2a`) | avec relief (`8bc3ce0`) |
  |---|---|---|
  | population à l'arrivée | 66 649 442 | 58 660 996 |
  | cellules affamées | 3 | 219 |
  | kg transportés | 800 | 527 850 |

  Si la ligne est identique, le relief ne joue toujours pas et le lot n'a
  rien fait. Si les cellules affamées ne bougent quasiment pas, les facteurs
  sont trop timides pour que la carte compte.

  Ces six nombres sont datés du 2026-08-26 et vieilliront : chaque lot qui
  suit les déplace. Ils sont ici parce qu'ils portent les **deux SHA** qui
  permettent de les rejouer, jamais comme une valeur à conserver (règle 12).

Puis vous fusionnez. C'est ce qui s'est passé le 2026-08-25.

---

## Les commandes que vous taperez vous-même

```bash
python -m sim                      # le jeu tourne, un an de ticks
python -m sim --ticks 20 --json    # une ligne de chiffres, déterministe
python -m sim --ticks 0 --json     # fumée : le monde s'amorce
python -m pytest sim/tests/ -q     # est-ce que le jeu tient ?
```

Sur votre machine Windows : `py`, jamais `python` — c'est un faux alias du
Microsoft Store, et c'est la règle 1 d'AGENTS.md, payée par un vrai défaut.
Sur Linux : `python3`.
