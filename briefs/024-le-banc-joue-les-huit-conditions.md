# Brief 024 — Le banc joue les huit conditions

## But

Une commande joue les huit conditions du cycle automatique sur le banc,
avec de faux agents et un faux GitHub, et rend `PASS` ou nomme celle qui
manque. Rien n'est armé sur le produit avant qu'elle passe.

## Règle du monde

Le 3 septembre 2026 au soir, deux pannes de plomberie ont été trouvées
en exécutant chaque scénario sur un produit jetable — pas en lisant le
code. Le tour nominal salissait le worktree du rôle et faisait échouer
le lot suivant ; `echec/` n'avait pas de porte de sortie. Aucune des
deux ne se voyait à la lecture, et les deux coûtaient une journée de
boucle.

Le cycle automatique ajoute la fusion. Une panne de plomberie qui
fusionne est d'une autre nature : elle écrit dans `master`. Le banc n'est
donc plus un confort, c'est la porte d'entrée.

`crons/epreuve.sh` monte un banc neuf et joue huit scénarios, chacun
nommé d'après la condition qu'il éprouve :

1. **un avis en prose ne verdit rien** — le faux relecteur dépose un
   texte français, aucun état de commit n'est posé, le contrôle requis
   reste en attente, aucune fusion n'est demandée ;
2. **une PR fusionne sans personne** — tous les contrôles verts sur la
   révision courante, aucune main humaine dans le scénario, la PR passe
   `fusionnee` ;
3. **deux lots disjoints s'exécutent ensemble** — deux worktrees, deux
   branches, deux cartes prises, deux agents invoqués dans le même
   cycle ;
4. **deux lots en collision ne s'exécutent jamais ensemble** — même
   scénario avec un fichier commun : une seule carte prise, et le motif
   du retenu nomme le fichier et le lot qui le tient ;
5. **la seconde PR est retestée avec la première intégrée** — la
   première fusionne, la seconde devient en retard, elle est mise à
   jour, ses contrôles rejouent sur le nouveau SHA, elle fusionne
   ensuite ;
6. **un refus revient à son auteur** — un `FAIL` sur un diff renvoie la
   carte dans `a-coder` avec ses motifs ; un `FAIL` sur un brief la
   renvoie dans `a-briefer` ;
7. **une direction produit plusieurs lots** — une phrase, une carte de
   cadrage, N fiches `a-briefer`, puis N briefs en file ;
8. **aucun quota n'est consommé** — le `PATH` du banc ne donne accès à
   aucun binaire réel, et le journal du faux GitHub porte tous les
   appels.

Chaque scénario **affirme**, il ne raconte pas. Un scénario qui
imprimerait « on voit que ça marche » ne vaut rien : il compare un état
attendu, dérivé de ce qu'il a posé, à l'état trouvé, et il sort en
erreur en nommant l'écart.

Et chacun **prouve son rouge**. Un scénario porte un interrupteur qui
casse la garde qu'il éprouve, et exige qu'alors le scénario échoue. Une
épreuve qui passe aussi quand la garde est retirée n'éprouve rien — c'est
la quatrième des douze règles du produit, et elle a été payée.

## Périmètre

En écriture : `crons/epreuve.sh`, les scénarios et leur verdict.
`tests/test_epreuve.py`, qui les lance sous `pytest` quand `bash` est
là. `docs/BOUCLES.md`, pour dire comment on joue l'épreuve et ce qu'elle
garantit. `ROADMAP.md`, pour la section de cette série une fois livrée,
et rien d'autre de ce fichier. Enfin
`briefs/024-le-banc-joue-les-huit-conditions.md`, ce brief.

Tout autre chemin est interdit, nommément `crons/banc.sh`,
`crons/faux-gh.sh`, `crons/tour.sh`, `crons/profils/atelier.sh`, tout
`atelier/`, tout `tests/` sauf le fichier nommé, `VISION.md`, `AGENTS.md`
et les autres briefs.

Le périmètre est étroit exprès. Si un scénario échoue, la réparation est
un lot sur le composant fautif — jamais une retouche du scénario pour le
faire passer. Un banc qu'on ajuste après avoir vu la mesure est une
calibration déguisée.

## Conditions de succès

### SC1 — les huit scénarios passent sur un banc neuf

```bash
bash crons/epreuve.sh --neuf
```

### SC2 — le compte des scénarios se dérive

Le verdict final compte les scénarios qu'il a joués et les compare à
ceux que le script déclare. Un échantillon vide **échoue** : une épreuve
qui n'a rien joué ne rend pas `PASS`.

```bash
python3 -m pytest tests/test_epreuve.py -q -k compte
```

### SC3 — le rouge est prouvé, scénario par scénario

Chaque scénario joué avec sa garde retirée **échoue**, et le verdict
nomme lequel. Le contrôle parcourt les huit ; il n'en code aucun en dur.

```bash
python3 -m pytest tests/test_epreuve.py -q -k rouge
```

### SC4 — l'épreuve ne touche jamais au produit

Le chemin de ForgeHistory n'apparaît nulle part dans le script, et le
scénario s'exécute avec `ATELIER_PROJET` pointant sur le banc. Un
contrôle le vérifie.

```bash
! grep -rn 'ForgeHistory' crons/epreuve.sh
```

### SC5 — aucun binaire réel n'est atteignable pendant l'épreuve

Le journal du faux GitHub porte tous les appels, et aucun processus
n'est sorti du `PATH` du banc.

```bash
python3 -m pytest tests/test_epreuve.py -q -k hors_de_portee
```

### SC6 — l'épreuve est rejouable

Deux exécutions d'affilée rendent le même verdict, et la seconde ne
dépend pas de ce que la première a laissé.

```bash
bash crons/epreuve.sh --neuf && bash crons/epreuve.sh --neuf
```

### SC7 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

L'armement sur le produit. Ce lot rend l'épreuve jouable et la fait
passer ; poser la protection de branche et basculer le profil sont des
gestes d'exploitation, à faire après, et une fois.

Toute correction d'un composant : un scénario rouge ouvre un lot sur le
composant, il ne se répare pas ici.
