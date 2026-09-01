# Brief 002 — les rôles viennent du branchement

## But

Qu'il n'y ait **qu'un seul endroit** qui dise qui tient quel poste : le
`atelier.toml` du dépôt produit. Après ce lot, `atelier invocation
--role relire` nomme le binaire que le branchement désigne, et non
celui qu'une table de l'atelier a décidé.

Et que la validation des rôles cesse d'être plus stricte que la règle
qu'elle protège.

## Règle du monde

Aucun fondement dans `sim/MODELE.md`. Ce lot ne change aucun nombre du
jeu.

**Fidélité : hors jeu.**

La règle qui fait foi est celle de [VISION.md](../VISION.md) :
*celui qui a écrit le **code** ne dit pas s'il est recevable.* Écrire
un brief n'est pas écrire du code. Claude peut donc briefer le matin
et relire le diff le soir — [docs/MISE-EN-PLACE.md](../docs/MISE-EN-PLACE.md)
le dit déjà, et c'est ce qui tient avec trois abonnements.

`atelier/projet.py` interdit aujourd'hui `ecriture == controle`. Cette
interdiction n'est écrite dans aucune règle : elle force le fichier de
branchement à nommer un quatrième abonnement que le propriétaire n'a
pas. La seule interdiction fondée est `execution == controle`.

Le lot 001 a livré une seconde table (`POSTES_DU_ROLE`) et une
troisième (le `case` de `tour.sh`). Trois endroits répondent à « qui
relit ». Ce lot en laisse un.

## Périmètre

Écriture autorisée, et rien d'autre :

- `atelier/projet.py`
- `atelier/backends.py`
- `atelier/__main__.py`
- `crons/tour.sh`
- `crons/veille.sh`
- `profiles/forgehistory.toml`
- `docs/MISE-EN-PLACE.md`
- `ROADMAP.md`
- `tests/test_roles.py` (nouveau)
- `tests/test_invocation.py`

Interdit : `sim/`, `viewer/`, `data/`, `VISION.md`, le `atelier.toml`
du jeu (il vit dans le dépôt produit, et sa PR est au propriétaire),
fusionner, invoquer un agent pendant les tests ou la CI.

## Conditions de succès

### SC1 — un même agent peut briefer et relire

Un `atelier.toml` avec `ecriture = "claude"` et `controle = "claude"` :

```bash
python3 -m atelier doctor --projet <produit>
# code = 0
```

### SC2 — celui qui écrit le code ne se relit pas

Le même fichier avec `execution = "cursor"` et `controle = "cursor"` :

```bash
python3 -m atelier doctor --projet <produit>
# code = 1, le message nomme l'exécution et le contrôle
```

### SC3 — le binaire du rôle vient du branchement

```bash
# controle = "claude"
python3 -m atelier invocation --role relire --projet <p> --lot L --brief B
# commence par « claude »

# controle = "codex"
python3 -m atelier invocation --role relire --projet <p> --lot L --brief B
# commence par « codex »
```

Aucune table de `atelier/` ne nomme le binaire d'un rôle de la boîte.

### SC4 — l'abonnement aussi vient du branchement

```bash
python3 -m atelier poste --projet <p> --role relire --champ abo
# claude-pro si controle = "claude" ; chatgpt-plus si controle = "codex"
```

`crons/tour.sh` ne contient plus de table qui associe un rôle à un
abonnement.

### SC5 — un relecteur qui garde la main qui écrit se déclare

```bash
python3 -m atelier poste --projet <p> --role relire --champ lecture_seule
# « tenue » si le binaire sait retirer les outils qui écrivent,
# « non-tenue » sinon
```

Quand elle est `non-tenue`, `tour.sh` l'imprime sur stderr avant
d'invoquer. Une absence se déclare ; elle ne se devine pas.

### SC6 — la veille déclare un branchement absent

```bash
ATELIER_PROJET=/un/chemin/sans/atelier.toml ./crons/veille.sh
# code ≠ 0, stderr nomme atelier.toml
```

Aujourd'hui elle sort 0 sans rien mesurer.

## Hors périmètre

- Modifier le `atelier.toml` du jeu (PR de branchement du propriétaire).
  L'atelier met à jour **son gabarit**, `profiles/forgehistory.toml`,
  et dit ce que le produit devrait écrire.
- Faire revenir le numéro de PR dans la carte (lot suivant).
- Un conseil, Mem0, E2B, Browser Use, Qwen, Goose.
- Toute fusion. Toute invocation dans la CI.
