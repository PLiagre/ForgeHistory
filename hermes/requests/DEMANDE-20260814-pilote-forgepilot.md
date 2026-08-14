---
author: hermes
kind: demande
created_at: 2026-08-14T12:00:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Tester un workflow Hermes, Claude Code et Cursor simplifié

Le propriétaire valide la simplification suivante : Hermes pilote sans modèle
local lourd, Claude Code prépare le plan et relit le résultat via l'abonnement
Pro actif jusqu'en avril 2027, Cursor est le seul agent qui modifie le code,
les tests mécaniques restent autoritaires et la fusion reste humaine pendant
le pilote.

Le pilote doit être neuf, lisible et réversible. L'ancien full-auto passe en
mode manuel. L'observateur Windows local est suspendu. Aucun cron et aucun
auto-merge ne sont ajoutés avant un bilan de trois lots réels.

Hermes sait exposer un serveur ACP mais ne possède pas encore de client ACP
générique. Claude Code est donc lancé avec son CLI headless. Claude Pro ne peut
pas servir de provider Anthropic natif à Hermes ; le CLI `claude -p` est la
frontière retenue pour profiter de l'abonnement sans facturation API.

Le pilote commence sur la partition Linux du propriétaire (environ 32 Go de
RAM et 140 Go libres). Après trois lots seulement, un bilan décide d'une
éventuelle migration vers un VPS 4 Go/2 vCPU/40 Go autour de 6 € par mois.
Render n'est pas retenu pour héberger Hermes, son compute persistant adapté
étant beaucoup plus cher. Le PC pourra rester un worker SSH facultatif pour
Unity ; lorsqu'il est éteint, les tâches qui l'exigent attendent sans bloquer
Hermes sur le VPS.
