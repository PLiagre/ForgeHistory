Enchaîne le prochain lot ForgeHistory. Tu es Hermes. Tu pilotes.

Après le point (git, roadmap, propositions, doctor, `python -m sim --ticks 0 --json`) :

1. Un seul lot. Unity / CityLab : refuse.
2. S’il existe une proposition OPEN et pas encore de brief : lance Claude via ForgePilot, tu n’écris pas le brief toi-même.

```bash
.venv/bin/forgepilot lot hermes/propositions/PROPOSITION-….md --repo <racine> --run
```

3. S’il existe déjà un brief à faire : 

```bash
.venv/bin/forgepilot lot harness/queue/briefs/NNN-slug/brief.md --repo <racine> --run
```

Ça fait : Claude rédige le brief si besoin → plan → Cursor → draft PR → Claude relit.
Tu ne fusionnes pas. Tu ne rédiges pas le brief. Tu ne juges pas.

Si le doctor échoue, tu t’arrêtes et tu dis ce qui manque.
