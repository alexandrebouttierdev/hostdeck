# HostDeck — Package Codex / Grok

Ce ZIP contient la spécification complète de création de HostDeck en **Go + Fyne** ainsi que les **9 maquettes finales validées**.

## Contenu

- `PROMPT_HOSTDECK_GO_FYNE.md` — prompt principal, déjà mis à jour pour référencer explicitement les maquettes.
- `mockups/` — 9 écrans HostDeck servant de source de vérité visuelle.
- `MOCKUPS_CONTACT_SHEET.png` — aperçu de l'ensemble des maquettes.
- `references/netdata_charts_reference.webp` — référence graphique Netdata uniquement pour le style des graphiques.

## Utilisation recommandée

Joindre **le fichier Markdown et le dossier de maquettes / le ZIP complet** à l'agent.

Lui demander de :
1. lire entièrement le prompt ;
2. ouvrir toutes les maquettes ;
3. ne commencer l'UI qu'après leur analyse ;
4. utiliser Context7 pour Fyne et toutes les APIs importantes ;
5. comparer chaque écran implémenté à sa maquette via captures d'écran.

Les maquettes finales utilisent un **rail vertical compact**, et non une sidebar webapp large.
