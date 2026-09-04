# Brief 016 — La prise d'une carte et de ses fichiers est un seul geste

## But

Prendre une carte et poser le verrou de ses fichiers devient une seule
opération, indivisible. Deux tours concurrents ne peuvent pas prendre le
même lot, ni deux lots qui se disputent un fichier.

## Règle du monde

Aujourd'hui, `crons/tour.sh` fait deux appels : `atelier prochain` lit la
première carte admissible, puis `atelier verrouiller` pose le verrou. Il
y a un intervalle entre les deux, et il est visible dans le script.

Tant qu'un seul coder tourne à la fois, l'intervalle ne coûte rien : le
`flock` par rôle garantit qu'aucun autre tour du même rôle ne s'y
glisse. Le cycle automatique retire cette garantie — plusieurs lots
avancent de front, donc plusieurs tours du même rôle tournent en même
temps. Alors :

- deux tours lisent la même première carte et la prennent tous les deux ;
- ou deux tours lisent deux cartes différentes qui partagent un fichier,
  et le second verrou est refusé après que l'agent a été invoqué.

Ce n'est pas une course rare : la boîte est triée, et deux tours qui se
réveillent à la même minute lisent la même tête de file. C'est le cas
nominal.

La correction est un composant mince, et il tient en une phrase :
**on ne lit pas puis on écrit, on prend**. `atelier prendre --projet P
--role R` fait, sous un même verrou d'exclusion :

1. lister les cartes de la boîte du rôle ;
2. écarter celles dont un fichier est tenu par un autre lot ;
3. déplacer la première admissible vers `en-cours/` ;
4. poser le verrou de ses fichiers ;
5. rendre la carte, ou `RIEN`.

Rien de tout cela n'est nouveau : les cinq gestes existent, éparpillés.
Ce lot les met sous une même serrure et rend le tout ou rien.

L'exclusion est un **répertoire**, pas un fichier : `os.mkdir` est
atomique partout, `open(…, "x")` sur un partage réseau ne l'est pas
toujours, et l'atelier ne dépend pas de `fcntl` — les contrôles de ce
dépôt tournent aussi sur une machine qui ne l'a pas. Une serrure
abandonnée par un tour tué se déclare périmée après un délai, et le
délai se lit, il ne se devine pas.

`en-cours/` n'est pas une boîte de plus dans le chemin d'une carte :
c'est là qu'une carte séjourne pendant qu'un agent travaille dessus.
Une carte qui y dort après la fin d'un tour est un tour qui n'a pas
rangé sa carte — et c'est visible, ce qui est tout l'intérêt.

## Périmètre

En écriture : `atelier/prise.py`, le composant — la serrure, la prise,
la remise. `atelier/commandes/prise.py` pour `atelier prendre`.
`crons/tour.sh`, qui remplace ses deux appels par un seul et rend la
carte sur tous ses chemins de sortie. `docs/LE-WORKFLOW.md`, pour le
tour d'un rôle. `tests/test_prise.py` pour ses contrôles, et
`tests/test_run.py` pour y **ajouter** des cas. Enfin
`briefs/016-la-prise-d-une-carte-et-de-ses-fichiers-est-un-seul-geste.md`,
ce brief.

Tout autre chemin est interdit, nommément `atelier/boite.py`,
`atelier/verrou.py`, `atelier/feuille.py`, `atelier/verdict.py`,
`VISION.md`, `AGENTS.md` et les autres briefs.

Ni la boîte ni le verrou ne changent : le composant les appelle. La
commande `atelier prochain` reste — elle **regarde** sans prendre, et
un aperçu qui prendrait ne serait plus un aperçu.

## Conditions de succès

### SC1 — deux prises simultanées ne rendent pas la même carte

Deux processus lancent `atelier prendre` sur la même boîte au même
instant. L'un rend une carte, l'autre rend `RIEN` ou une autre carte.
Jamais deux fois la même.

```bash
python3 -m pytest tests/test_prise.py -q -k concurrence
```

### SC2 — le rouge est prouvé : sans la serrure, le contrôle échoue

Le contrôle sait désactiver la serrure par une variable, et il exige
qu'alors la collision **se produise**. Un contrôle de concurrence qui
passe aussi sans la garde ne prouve rien.

```bash
python3 -m pytest tests/test_prise.py -q -k sans_serrure
```

### SC3 — la prise est tout ou rien

Un verrou refusé au milieu de la prise laisse la carte dans sa boîte
d'origine et aucun verrou posé. L'état après un échec est l'état
d'avant.

```bash
python3 -m pytest tests/test_prise.py -q -k tout_ou_rien
```

### SC4 — deux lots qui partagent un fichier ne sont jamais pris ensemble

Deux cartes, un fichier commun, deux prises : la seconde rend `RIEN` et
dit sur `stderr` quel lot tient quel fichier.

```bash
python3 -m pytest tests/test_prise.py -q -k collision
```

### SC5 — deux lots disjoints sont pris tous les deux

Le même contrôle avec deux périmètres disjoints rend deux cartes
différentes, et deux verrous coexistent.

```bash
python3 -m pytest tests/test_prise.py -q -k disjoints
```

### SC6 — une serrure abandonnée se déclare périmée, elle ne se force pas

Une serrure plus vieille que le délai est reprise, et l'événement est
dit sur `stderr`. Une serrure fraîche fait attendre. Le délai se lit
dans l'environnement.

```bash
python3 -m pytest tests/test_prise.py -q -k perimee
```

### SC7 — `atelier prochain` ne prend toujours rien

Après un `atelier prochain`, aucune carte n'a bougé et aucun verrou
n'est posé.

```bash
python3 -m pytest tests/test_prise.py -q -k apercu
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le worktree du lot, qui est le lot suivant. Le nombre de cartes que le
pilote dépose : ce lot rend la prise sûre quand elles sont plusieurs, il
n'en fabrique aucune.
