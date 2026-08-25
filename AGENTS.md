# AGENTS.md — les règles, pour tous les agents

Le seul fichier de règles du dépôt (ADR-0018). Hermes, Cursor et Claude
lisent celui-ci. Il ne paraphrase aucun autre document : ce qui est ici
n'est écrit qu'ici.

Les quatre documents vivants : **VISION.md** (ce qu'on construit, gelé) ·
**ROADMAP.md** (où on en est, tenu par Hermes) · **AGENTS.md** (ce fichier) ·
**hermes/DASHBOARD.md** (généré, jamais édité à la main).

---

## Le projet en trois phrases

ForgeHistory est un moteur de simulation historique vivant (1400-1900) dont
le gameplay émerge. Le produit vivant est `sim/` — `python -m sim`, sans
Unity. Le monde lit une carte figée, `data/world-1400.json`.

## Langue

Toute communication avec le propriétaire et tout écrit du dépôt — messages
de commit, comptes-rendus, documents — est en **français clair**. Phrases
courtes, concrètes : ce qui a été fait, pourquoi, ce qui reste. Un terme
technique nécessaire s'explique en une phrase la première fois.

---

## Qui fait quoi

| acteur | fait | ne fait pas |
|---|---|---|
| **Hermes** (Sol 5.6, VPS) | roadmap, suivi, **écrit les briefs**, lance Cursor, mesure, rend compte | ne code pas, ne fusionne pas, ne juge pas un lot |
| **Cursor** (Grok 4.6 plan, Composer code) | exécute le brief, ouvre la PR, se relit dans une invocation neuve, itère jusqu'au vert | ne décide pas de ce qui est recevable |
| **Claude** (à la demande) | architecte du modèle (`sim/MODELE.md`), regard de dernier recours quand un lot ne converge pas | n'a plus d'agent, plus de cron, plus de rôle dans le harnais |

Le processus complet :

> Hermes écrit un brief → Cursor l'exécute et ouvre une PR → les tests
> passent et la porte mécanique vérifie le compte-rendu → le propriétaire
> fusionne.

**Celui qui produit ne prononce pas la recevabilité de son propre travail.**
C'est la seule règle de rôle qui subsiste, et elle ne se contourne pas.

## Une seule source d'instruction

Exactement un document dit ce qu'un agent doit faire pour un lot : le
`brief.md` de ce lot. Tout autre document peut y renvoyer ; aucun ne peut le
paraphraser. Vérifié par
`harness/tests/test_single_source_of_instruction.py`.

---

## Les trois principes non négociables

1. **Une seule source de vérité.** Monde → Pays → Province → Ville →
   Quartier → Bâtiment → Famille → Personne. Les vues lisent cette
   hiérarchie ; elles ne deviennent jamais une base de données parallèle.

2. **Le moteur raisonne en termes de monde, jamais de gameplay.**
   Interdit : « si famine alors +20 % de criminalité ».
   Exigé : ils ont faim → ils cherchent → certains volent → la criminalité
   monte.

3. **L'économie est physique.** Rien ne se téléporte. Tout a une origine, un
   transport, un stockage, une destination.

## Vraisemblable, pas véridique

Trois niveaux de fidélité. Tout brief qui touche au monde s'y réfère.

- **Niveau 1 — juste dans les grandes lignes. Obligatoire.** La Méditerranée
  est là où elle est ; les Alpes sont des montagnes ; Venise est grande en
  1400.
- **Niveau 2 — plausible, généré, jamais sourcé.** Rendements, gisements
  secondaires, population des villages, climat local. **Une anomalie de
  niveau 2 n'est pas un défaut** : elle n'ouvre ni correctif, ni brief.
- **Niveau 3 — pas simulé.** Ce qui a besoin d'une source pour exister
  n'entre pas dans le jeu.

Les constantes : pas de nombre magique dans le code du moteur — la règle
reste. Justifier chaque constante par une source — la règle est abandonnée ;
« ordre de grandeur plausible » en commentaire suffit.

## La règle d'admission des tests

Un test existe s'il protège **l'une de ces trois choses**, et seulement :

1. un **invariant physique** (la masse se conserve, l'adjacence est
   symétrique, une dette ne se rembourse pas plus vite que le surplus) ;
2. une **règle de jeu visible** (on ne mange pas deux fois, on a faim, on
   meurt) ;
3. le **déterminisme** (même graine, même monde).

Corollaire : **ne pas ajouter un fichier de test par lot.** Un lot ajoute ses
cas au fichier qui porte déjà l'invariant concerné.

---

## Les douze règles payées par un vrai défaut

Chacune a coûté un défaut mesuré dans VictoriaProject. Écrites verbatim.

1. `py`, jamais `python` (sur la machine Windows du propriétaire, `python`
   est un faux alias du Microsoft Store). Sur Linux : `python3`.
2. Un contrôle **dérive** sa référence ; il n'est jamais nommé d'après sa
   cible. (Six récurrences historiques.)
3. Un compteur dérive aussi.
4. **Prouver le rouge d'abord.** Un contrôle qui ne peut pas rougir ne
   prouve rien.
5. Une garde placée après l'effet qu'elle doit empêcher ne protège rien.
6. Un contrôle trop grossier coûte aussi cher qu'un contrôle laxiste.
7. La présence n'est pas la fonction.
8. Un zéro peut être une vraie mesure — sentinelle `-1`, jamais `0`, pour
   « non calculé ».
9. Une impossibilité se teste avant d'être invoquée : une commande et un
   message d'erreur, sinon ce n'est pas un constat mais une abdication.
10. Quand une donnée manque, l'agent l'invente en silence par défaut —
    l'absence doit donc être **déclarable**, et le code doit refuser de
    deviner.
11. **Regarder les captures soi-même.** Quatre défauts majeurs ont été vus à
    l'œil que des suites 100 % vertes n'ont jamais attrapés.
12. Une empreinte de parité se cite par **nom**, jamais par valeur : elle
    sera rebasée un jour, et le document qui porte la constante morte piège
    tous les briefs suivants.

## Les sept modes de défaillance diagnostiqués

| # | mode de défaillance | contre-mesure structurelle |
|---|---|---|
| 1 | double clé primaire (ProvinceId de la sim contre cell_id de la géométrie) | UNE clé spatiale, décidée avant tout code (ADR-0003) |
| 2 | champ déclaré que personne n'écrit | `sim/tests/test_write_coverage.py` : chaque champ a un site d'écriture et un site de lecture, rouge sinon |
| 3 | variable terminale (calculée, lue par personne) | avant d'ouvrir un levier, vérifier que sa conséquence atteint quelque chose de mesurable hors de son module |
| 4 | la présentation réimplémente la simulation | la présentation LIT, elle ne décide jamais |
| 5 | compteur codé en dur | un compteur dérive des données, ou il n'existe pas |
| 6 | contrôle qui nomme sa propre référence (échantillon vide, passage silencieux) | référence DÉRIVÉE de la mesure ; un échantillon vide doit ÉCHOUER, jamais passer |
| 7 | le producteur prononce sa propre recevabilité | celui qui produit ne juge pas — porte mécanique + relecture en invocation neuve |

---

## Où vit quoi

| chemin | quoi |
|---|---|
| `sim/` | **le produit vivant** — `python -m sim`. Voir `sim/README.md`. |
| `data/` | la carte figée `data/world-1400.json` et les centres de province. La seule entrée géographique du jeu. |
| `viewer/` | un regard mince sur un snapshot. Jamais une seconde simulation. |
| `tools/map/` | l'outil qui fabrique la carte. Hors du chemin quotidien : ne se ressort que si on refait la carte. Voir `tools/map/BUILD.md`. |
| `harness/` | la porte mécanique, et rien d'autre. Voir `harness/README.md`. |
| `hermes/` | le pilotage : propositions, demandes, rapports, crons, tableau de bord. |
| `control-plane/` | ForgePilot — l'outil qui lance un lot chez Cursor. |
| `docs/adr/` | une décision structurelle = un ADR daté. **ADR-0018 est le point d'entrée.** |

## Les archives

`unity/`, `architecture/` et les 33 briefs terminés sont sortis de l'arbre
de travail au dégraissage (ADR-0018). Ils restent dans l'historique git, au
commit du lot D : **`da1596d`**. C'est la référence qui marche aujourd'hui,
depuis n'importe quel clone :

```bash
git show da1596d:unity/<chemin>          # relire un fichier
git checkout da1596d -- unity/           # récupérer tout un dossier
git ls-tree --name-only da1596d:unity    # voir ce qu'il y a
```

> **Le tag `archive/2026-08` n'existe pas sur `origin`.** Deux sessions ont
> essayé de le pousser, les deux ont reçu un `HTTP 403` : le jeton pousse des
> branches, pas des tags. Vérifiable : `git ls-remote --tags origin` ne le
> montre pas. Tant que quelqu'un ne l'aura pas posé depuis un clone local
> (`git tag -a archive/2026-08 da1596d && git push origin archive/2026-08`),
> **utiliser le SHA ci-dessus** — les commandes en `archive/2026-08` échouent
> avec `fatal: invalid object name`.

## Les commandes

```bash
python -m sim                            # le produit vivant
python -m sim --ticks 0 --json           # fumée : le monde s'amorce
python -m pytest sim/tests/ -q           # les tests du jeu
python -m pytest viewer/tests/ -q        # le regard mince
python -m pytest harness/tests/ -q       # la porte mécanique
python harness/verdict_audit.py <brief>  # la porte, sur un lot
python tools/map/build_world.py          # refaire la carte figée
python hermes/dashboard.py               # régénérer le tableau de bord
cd control-plane && python -m unittest discover -s tests   # ForgePilot
```

## Notes d'environnement (VM Linux Cursor Cloud)

Le `python3` du système est un interpréteur Debian géré (PEP 668). Un
`.venv` à la racine est créé au démarrage avec `pytest` et la pile
scientifique : **utiliser `.venv/bin/python` pour tout ce qui a besoin d'un
paquet tiers**. Ne jamais `pip install --user` contre le python système.

La porte mécanique et le moteur sont en bibliothèque standard seule :
`python3` suffit pour eux.

Il n'y a pas de linter configuré. Les garde-fous du dépôt sont les tests et
`harness/verdict_audit.py`.
