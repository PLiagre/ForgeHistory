# outils/

Ce que la CI **décide**. Lecture seule vis-à-vis de GitHub : aucune de ces
commandes ne fusionne, n'approuve, ni ne pose un état. Le geste vit dans
`.github/scripts/`. Les règles sont dans [AGENTS.md](../AGENTS.md) ; la
conduite dans [docs/WORKFLOW.md](../docs/WORKFLOW.md).

Ce paquet est en bibliothèque standard. Les tests demandent l'atelier sur
le `PYTHONPATH` — c'est lui qui lit le registre, et il n'y a qu'un lecteur.

```bash
export PYTHONPATH=/opt/ForgeAtelier   # ou le clone de la branche atelier
python3 -m pytest outils/tests/ -q
```

## Les cinq commandes

Chacune imprime **une** ligne sur stdout — celle que le workflow lit — et
son compte rendu sur stderr.

```bash
python3 -m outils relecture   --depot PLiagre/ForgeHistory --pr N
python3 -m outils integration --depot PLiagre/ForgeHistory --projet .
python3 -m outils palier      --projet .
python3 -m outils tableau     --depot PLiagre/ForgeHistory --projet . --sortie /tmp/etat.html
python3 -m outils saisie      --projet . --corps demande.md
```

| commande | elle dit | elle écrit |
|---|---|---|
| `relecture` | la PR a-t-elle une approbation de tiers sur sa révision courante ? | jamais |
| `integration` | quelle PR entre, ou `RIEN` | jamais |
| `palier` | une couche finie attend-elle son lot de stabilisation ? | le registre, **seulement** avec `--ecrire` |
| `tableau` | la page « où en est le travail » | le fichier `--sortie` (HTML) |
| `saisie` | une demande de lot devient une fiche | le registre, **seulement** avec `--ecrire` |

Sans `--ecrire`, `palier` et `saisie` montrent ce qu'elles écriraient et
laissent le registre intact. C'est le mode par défaut, et c'est celui de
la main.

`--jeton` est facultatif : sans lui, la variable `GITHUB_TOKEN` est lue.
Sans ni l'un ni l'autre, `relecture`, `integration` et `tableau` refusent
(`GithubErreur`). `palier` et `saisie` n'en ont pas besoin.

## Pièges

- **Atelier absent.** `FAIL  …` et code 1. Les tests d'`outils/` et
  `palier` / `saisie` / `tableau` lisent le registre via l'atelier. Sans
  `PYTHONPATH`, ce n'est pas un bug du jeu.
- **`--ecrire` à la main.** Ça pose une fiche en tête de `ROADMAP.md`. Le
  registre ne s'édite plus autrement. Vérifier d'abord sans le drapeau.
- **Confondre l'état `relecture` et la porte.** L'état posé par le
  workflow tourne sur le code de la PR. `python3 -m outils relecture`
  calcule le même verdict ; l'intégration, elle, le rejoue depuis
  `master`. Voir [docs/WORKFLOW.md](../docs/WORKFLOW.md).
- **Banc des scripts.** Un geste nouveau vit dans `.github/scripts/` et se
  rejoue avec de faux `gh` / `git` / `python` en tête du `PATH`
  (`outils/tests/banc.py`). Un `run:` YAML n'est pas un test (règle 13).

La liste des contrôles qui ouvrent `master` n'est pas ici : elle est dans
[`atelier.toml`](../atelier.toml) § `[integration]`.
