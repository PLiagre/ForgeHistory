# Analyse — dix dépôts, sept couches, ce qu'on retient

Ce fichier est la proposition. Le code de ce dépôt n'implémente que ce
qu'elle retient. Une idée séduisante qui n'est pas ici n'est pas un oubli :
c'est un refus.

Les dix dépôts ne sont pas des produits à collectionner. Ce sont des
*couches* que des gens ont dû construire parce qu'un agent seul ne tient
pas. ForgeHistory a déjà payé cette leçon : avant le dégraissage V1,
l'outillage pesait six fois le jeu, et le harnais protégeait un flux
qui n'existait plus.

---

## Ce que ForgeHistory a déjà appris, et qu'on ne réécrit pas

Mesuré au tag `v0-avant-degraissage` : 28 774 lignes d'outillage,
5 041 lignes de jeu. Quatre organisations superposées, aucune n'ayant
retiré le code de la précédente. Le lot 035 a consommé un quota Claude
sans rien livrer, parce qu'on lançait des agents hors de la machine à
états. Le lot 033 a rendu un bundle de revue illisible : le fichier
existait, git l'ignorait, l'agent ne le voyait pas.

De cet historique, cinq contrats survivent. Ils sont la colonne
vertébrale de l'atelier, pas un souvenir.

1. **Auteur ≠ relecteur.** Tenue par les rôles, pas par une porte qui
   juge le fond.
2. **Le brief est la seule source d'instruction.** Le cycle prépare ;
   il n'ajoute pas d'ordre oral.
3. **Aperçu, puis `--run`.** La forme d'appel qui a brûlé le 035 est
   interdite : pas d'invocation hors run durable.
4. **Canal d'échange.** Git-invisible *et* lisible par l'agent. Les
   deux conditions, ou aucune.
5. **La fusion est au propriétaire.** `atelier fusionner` sort en
   erreur. C'est un test, pas un commentaire.

Le reste du vieux ForgePilot — niveaux de risque R0/R1/R2, bot de
fusion, comptage de jetons comme critère, douze workflows, vingt et un
ADR — reste dans l'historique. On ne le rapatrie pas.

---

## Les dix dépôts, couche par couche

### Intelligence — qui raisonne

| dépôt | ce qu'il est | retenu | laissé |
|---|---|---|---|
| **Hermes Agent** | agent avec mémoire, skills, cron, messagerie | la *console* : un pilote qui dialogue et lance, qui n'écrit pas le code du lot, qui n'a pas de cron de fusion | Hermes n'entre pas dans le dépôt produit. Sa config vit chez lui (`~/.hermes`), pas dans le jeu. |
| **Qwen Code** | agent terminal, MCP, multi-modèles | l'idée d'un *adaptateur* de lancement, pas d'un modèle unique | on n'ajoute pas Qwen à la rotation. Trois postes suffisent. |
| **Goose** | agent local, inspecte, édite, teste | local-first, extensions MCP | Goose n'est pas un quatrième exécutant. |

Les trois postes de ForgeHistory tiennent : Claude écrit le brief,
Cursor exécute, Codex relit. L'atelier les *invoque* ; il ne les
remplace pas. Un backend est un binaire + un prompt, jamais un
raisonneur embarqué.

### Outils — compétences réutilisables

| dépôt | retenu | laissé |
|---|---|---|
| **Superpowers** | une méthodologie *conditionnée en skills* (format agentskills.io), déclenchée avant d'agir ; worktrees ; rouge-vert ; relecture par un autre | le brainstorming qui précède le spec. Ici le spec *est* le brief. Une seconde langue de planification redevient une source parallèle. |
| **Browser Use** | rien pour l'instant | naviguer le web n'est pas une couche du lot. La règle 11 de ForgeHistory dit : le propriétaire *regarde* la carte. Ce n'est pas un agent. |

Les skills de ce dépôt sont les quatre actes du cycle, plus
l'isolation par worktree. Elles ne paraphrasent pas le brief.

### Mémoire — ce qui survit

| dépôt | retenu | laissé |
|---|---|---|
| **Mem0** | le *besoin* : un lot dans trois semaines ne réinvente pas l'incident du 035. ADD-only : on n'écrase pas une leçon | pas de vecteur, pas de SaaS, pas d'extraction silencieuse. Une mémoire qui invente est le mode de défaillance n°10. |

La mémoire de l'atelier, ce sont des fichiers git : incidents, décisions,
leçons payées. Le propriétaire les lit. Un embedding que personne ne
peut citer n'existe pas.

La mémoire du *produit* (ce que le monde sait faire) reste dans le
dépôt produit : `ROADMAP.md`, `MODELE.md`, les briefs. L'atelier ne
la duplique pas.

### Exécution — où ça s'écrit

| dépôt | retenu | laissé |
|---|---|---|
| **E2B** | l'isolation comme contrat | pas de bac cloud tant que le worktree git suffit. Un sandbox est un *backend* d'exécution, pas le produit. |

Un agent, un worktree, une branche. Trois agents sur le même clone se
marchent dessus : c'est déjà écrit dans le VPS Hermes, ça devient une
commande de l'atelier.

### Orchestration — l'ordre et la reprise

| dépôt | retenu | laissé |
|---|---|---|
| **Mission Control** | le plan de contrôle *au-dessus* des runtimes : dispatcher, suivre, examiner, ne pas raisonner à leur place. État local (fichiers, pas un cluster). | les 32 panneaux Next.js. Un tableau de bord qui décide est une base parallèle. Pour un propriétaire, `atelier status` et git suffisent. |

ForgePilot avait déjà cette couche, trop grosse. L'atelier la refait
mince : `start` / `resume` / `status`. Jamais `merge`.

### Coordination — quotas, files, transferts

| dépôt | retenu | laissé |
|---|---|---|
| **llmquota** | le quota est un *fait* : inconnu se déclare (`-1`), il ne se compte pas comme zéro. `hop` choisit qui a de la marge. Un bus pour se passer un lot (objectif, fichiers, tests, suite). Un *claim* de fichiers avant d'écrire. | le TUI arène, les referrals, lancer les CLI. llmquota *lit* ; l'atelier *refuse* si le quota est inconnu et que l'étape dépense. |

L'incident du lot 035 est la preuve que cette couche n'est pas une
démo. Sans elle, on relance le même juge jusqu'à la facture.

### Vérification — mesurer, pas affirmer

| dépôt | retenu | laissé |
|---|---|---|
| **Council of High Intelligence** | positions d'abord indépendantes ; FACT / INFERENCE / ASSUMPTION / UNKNOWN ; le dissensus se conserve ; des kill criteria ; le propriétaire tranche encore | un conseil à chaque lot. AGENTS.md du jeu a tué la relecture obligatoire. Le conseil est pour une décision *irréversible* (un mécanisme du modèle), pas pour la PR 044. |

La porte mécanique (LLM-free) survit, dégraissée : elle refuse un
brief sans les cinq sections, un SC sans commande, un périmètre
« tout le dépôt », un échantillon vide qui passe, un auteur qui
signe sa propre revue. Elle ne juge pas le fond. Le fond, c'est
un autre agent, puis l'œil du propriétaire.

---

## La solution retenue

Deux dépôts. Une frontière nette.

```
ForgeHistory          le produit
  sim/ viewer/ data/
  VISION ROADMAP MODELE
  AGENTS.md           règles du JEU (invariants, fidélité, brief)
  briefs/             les lots de CE jeu
  atelier.toml        comment ce produit se branche

ForgeAtelier          l'infrastructure
  les sept couches
  le cycle d'un lot
  les skills des postes
  le canal d'échange, le worktree, le verrou, le quota
  la porte mécanique
```

Le jeu ne porte plus la marche à suivre. Il porte un fichier de
branchement, et ses règles à lui. L'atelier ne porte aucune formule
du monde. Il ne sait pas ce qu'est une cellule.

### Ce que v0 fait, concrètement

- lire un `atelier.toml` et un brief
- vérifier le brief (cinq sections, SC qui peuvent échouer, périmètre)
- montrer le cycle : qui fait quoi, avec quel prompt
- sans `--run` : n'écrire nulle part
- avec `--run` : créer le worktree, poser le verrou de fichiers,
  ouvrir le canal d'échange, enregistrer l'état durable
- refuser `fusionner`
- traiter un quota manquant comme `-1`, jamais comme `0`

### Ce que v0 ne fait pas

- invoquer Cursor, Claude ou Codex. Un lancement hors contrat a
  déjà coûté un lot. Les adaptateurs *nomment* la commande ; le
  propriétaire (ou, plus tard, un backend testé) l'exécute.
- installer Hermes, Mem0, E2B, Mission Control, llmquota.
  Ce sont des *références de couche*, pas des dépendances.
- un tableau de bord web
- un conseil automatique

### Comment ça grandit

Chaque couche s'ouvre par un lot de *l'atelier*, avec un brief de
l'atelier. Le premier client reste ForgeHistory. Un second produit
n'entre que s'il fournit un `atelier.toml` et que les tests de
branchement passent.

---

## Pourquoi ce n'est pas un retour en arrière

Le dégraissage V1 a sorti le harnais du jeu parce qu'il étouffait
le jeu. Ça reste vrai. L'atelier est un autre dépôt, un autre
périmètre, un autre budget d'attention. S'il redevient trop gros,
on le dégraisse *ici*, sans toucher à `sim/`.
