# Retour — Brief `009`, lot 009a, itération `2` (REJECT)

**Authored**: 2026-08-10T20:59:27Z
**Author**: forge-evaluateur-codex

Lire avec la section de réévaluation ajoutée à `verdict.md`. B1 est fermé et
les cas précis vide/espaces/non-UTF-8 demandés par B2 sont corrigés. La phrase
ciblée par B3 n'annonce plus le maillon challenge comme déjà câblé. Une
recherche adverse a toutefois trouvé deux contre-exemples supplémentaires
dans les mêmes fichiers ; ils sont décrits en C3 et C4.

Quatre défauts restent bloquants. Les deux premiers portent sur la preuve ;
les deux suivants portent sur des affirmations plus larges que le code réel.

## C1 — la sortie complète de la suite après l'itération 2 manque dans le journal

La suite est réellement verte : ma propre commande
`py -m pytest harness/tests/ -q` donne `284 passed in 22.11s`, et le fichier
`deliverables/pytest-full-output.txt` contient bien une exécution verte à
284 tests. En revanche, `deliverables/generator-log.md` ne contient aucune
occurrence de `284 passed`. Sa seule sortie complète est celle de l'itération
1, à 280 tests. Le journal nomme les quatre nouveaux tests et le fichier
annexe, mais ne recopie pas la sortie complète après correction comme
l'exigent le contrat d'exécution et la liste de re-soumission du premier
retour.

Correction attendue : ajouter au journal, sans réécrire l'historique, une
section datée qui cite la commande réellement relancée après l'itération 2
et recopie sa sortie complète. Si la commande est relancée, remplacer aussi
le fichier annexe par cette nouvelle sortie réelle ; ne pas réutiliser le
temps d'une ancienne exécution.

## C2 — la commande du compteur de transition ne couvre pas tout le lot

La valeur reste correcte. Ma reconstruction indépendante sur
`244a4f2~1..a16b18c` ne trouve que les deux lignes suivantes :

```
-mode: full_auto
+mode: full_auto_decision_only
```

Mais l'entrée `config_mode_single_commit_transition_count` de
`deliverables/manifest.json` cite encore la commande limitée à
`244a4f2~1..244a4f2`. Le journal reconnaît explicitement que cette commande
devait être relancée sur la plage élargie une fois le commit d'itération 2
créé. Ce commit existe maintenant (`a16b18c`) ; la commande déclarée ne mesure
donc plus le périmètre que le compteur affirme couvrir.

Correction attendue : relancer la mesure sur
`244a4f2~1..a16b18c`, recopier la sortie réelle dans le journal et mettre à
jour uniquement le champ `command` du compteur si sa valeur reste `2`. Ne
modifier ni la valeur ni l'échantillon sans nouvelle mesure qui le justifie.

## C3 — le garde accepte encore des faux workflows que son propre texte dit refuser

Les cas vide, espaces seuls, tronqué avant `jobs:` et non-UTF-8 sont bien
fermés. En revanche, le module recherche seulement les sous-chaînes `jobs:` et
`runs-on:` n'importe où dans le texte. Ma commande directe, contre le vrai
`validate_mode`, obtient trois acceptations silencieuses :

```text
commentaires_seuls: ACCEPTED returned None
tronque_apres_runs_on: ACCEPTED returned None
workflow_echo_sans_agent: ACCEPTED returned None
```

Le premier fichier ne contient que `# jobs:` et `# runs-on:`. Le deuxième
s'arrête juste après un vrai `runs-on:`. Le troisième est un YAML minimal qui
fait seulement `echo no-agent`. Aucun ne prouve que forge-run est câblé.

Ce résultat contredit les affirmations ajoutées à l'itération 2 : « positive
structural evidence », « real, complete GitHub Actions workflow » et « there
is no fourth, silently-permissive outcome ». La paire SC1/SC2 du brief reste
verte ; le défaut est le nouvel overclaim et la prétention de complétude.

Correction attendue : soit rendre le contrôle réellement cohérent avec la
preuve positive annoncée, tout en conservant la branche SC2, soit limiter
explicitement le contrat et le journal à l'heuristique exacte réellement
appliquée. Ne présenter pas la présence textuelle de deux marqueurs comme une
preuve sémantique que l'agent est câblé.

## C4 — le mode est encore présenté comme actif et comme coupe-circuit réel

La correction B3 a retiré l'annonce prématurée du maillon challenge. Mais les
fichiers modifiés affirment encore :

- `docs/rules/full-auto-pipeline.md` : « this activates the audit ->
  owner-decision half » et « the diagram above runs unattended » ;
- le même document : `mode: manual` est un des deux interrupteurs qui
  « stops the loop » ;
- `harness/pipeline/config.yaml` : `mode: manual` est qualifié de
  « Emergency kill-switch ».

Or `rg -n "config\.yaml|full_auto_decision_only|full_auto_mode_guard|mode:"
.github/workflows` ne retourne aucune occurrence, tandis que les trois étapes
d'invocation audit, challenge et forge-run contiennent encore chacune
`TODO(operator`. Le brief lui-même réserve le premier branchement d'un mode
réel à 009c SC15. Le texte décrit donc une capacité opérationnelle absente.

Correction attendue : dire qu'en 009a le fichier déclare une posture et que la
CI valide sa valeur, mais qu'aucun workflow ne la consulte encore à
l'exécution. Réserver les verbes « active », « runs unattended » et
« kill-switch » au moment où un workflow réel lit effectivement le mode.

## Re-soumission

1. Fermer C1 et C2 par des sorties réellement exécutées.
2. Fermer C3 sans affaiblir les branches SC1/SC2 déjà vertes.
3. Fermer C4 sans anticiper le câblage réservé à 009c.
4. Rejouer le gate mécanique et la suite complète.
5. Conserver les frontières de périmètre déjà respectées : aucun workflow,
   aucun fichier de 009b/009c et aucun texte d'ADR-0006 modifié.
