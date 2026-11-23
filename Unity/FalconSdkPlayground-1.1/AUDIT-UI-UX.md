# Audit et corrections — Falcon SDK Playground 1.1

19 novembre 2026. Périmètre : Player Windows, scène Unity, correspondance des axes, calibration, parcours des essais et gestion des forces. L’ancien projet 1.0 est conservé séparément. Le diagnostic Windows séparé et les anciennes scènes FalconLab ne sont pas modifiés.

## Constat principal

Retour utilisateur : « mouvement gauche droite réel fait haut bas dans l’appli ». Le mapping précédent appliquait origine, échelle, inversion de Z et Transform Unity ; il ne permettait pas d’identifier quelle direction brute correspond à droite, haut ou vers soi. Une capture des positions extrêmes ne détermine pas cette correspondance.

L’interface 1.0 a été examinée dans le Player Windows, ainsi que les sources du SDK et de la scène. Les corrections suivantes sont implémentées en 1.1. Les forces réelles et les gestes sur le Falcon restent à confirmer sur place.

## Problèmes et traitement

| Priorité | Problème | Conséquence | Correction livrée |
| --- | --- | --- | --- |
| Critique | Directions brutes supposées identiques à celles de l’écran | Gauche/droite peut déplacer le curseur verticalement | Trois gestes droite, haut, vers soi ; base orthonormale, permutations et inversions |
| Critique | Inversion de Z insuffisante pour un échange d’axes | Réglage par tâtonnement incomplet | Conversion commune positions, vitesses, géométrie et forces ; transposée conservant le travail |
| Élevée | Efforts accessibles avant vérification des directions | Essai avec un repère incompris | Activation matérielle refusée avant confirmation explicite des directions |
| Élevée | Caméra oblique et surface opaque | Mouvement ambigu et curseur masqué | Vue de face orthographique, surface transparente, zone de rendu séparée des commandes |
| Élevée | Cinq onglets techniques, nombreuses commandes équivalentes | Premier essai difficile à trouver | Parcours Appareil → Directions → Essayer ; trois essais principaux et autres effets à la demande |
| Élevée | Texte IMGUI petit dans le Player examiné | Instructions difficiles à lire | Police principale 17 px, titres 25 px, boutons hauts, panneau contrasté et défilant |
| Élevée | JSON v1 limité au centre et aux bornes | Orientation et échelle perdues au rechargement | JSON v2 complet, validation avant application et fichiers distincts par backend |
| Moyenne | Sliders bruts X/Y/Z | Confusion entre pilote et directions humaines | Commandes gauche/droite, bas/haut, loin/vers soi ; données brutes dans Avancé |
| Moyenne | Capture des bornes assimilée à une calibration complète | Boîte correcte mais axes incorrects | Directions et capture optionnelle distinctes ; mention « limite logicielle » |
| Moyenne | Nombreux paramètres au premier essai | Rigidité/amortissement difficiles à régler | Deux matériaux prédéfinis et friction activable ; détails et six faces dans Avancé |
| Moyenne | Arrêt, préparation et activité peu distincts | Doute sur l’effort | Arrêt toujours visible, Échap et états explicites ; arrêt demandé sans présumer son acquittement |
| Moyenne | Configuration changée pendant l’essai | Commande incohérente | Désarmement au changement d’axes, matériau, recentrage, page ou appareil |
| Moyenne | Erreur de sauvegarde masquée par la confirmation | Réglages supposés conservés | Confirmation de session et échec de sauvegarde affichés séparément |

## Réglage des directions

Chaque geste compare deux lectures récentes en mètres, avec un déplacement d’au moins 5 mm. Les gestes droite/haut trop proches sont refusés ; la profondeur doit être distincte de leur plan. La base est orthogonalisée et accepte les deux orientations du repère brut. L’utilisateur vérifie visuellement, puis centre la poignée. La vue guidée remet la rotation locale à zéro et définit une profondeur positive vers l’utilisateur avant inversion vers Unity.

Le réglage ne déduit pas les axes du seul modèle de périphérique. La documentation [DHD — dhdGetPosition](https://downloads.forcedimension.com/sdk/doc/fdsdk-3.14.0/dhd/dhdc_8h.html) décrit les positions cartésiennes en mètres ; la correspondance de l’installation est déterminée ici par les gestes observés.

La confirmation retire les anciennes limites logicielles pour permettre le recentrage. Une nouvelle capture reste disponible dans Avancé. Les fichiers v1 sont lisibles en secours mais ne valident pas les directions matérielles. Les fichiers v2 conservent origine, bornes, axes, échelle, inversion et rotation locale ; aucun profil matériel n’est appliqué automatiquement au démarrage.

## Couverture fonctionnelle et compatibilité

Les six effets restent accessibles : eau, paroi du pilote, résistance, ressort, gravier et moteur. La paroi historique utilise X brut et est nommée explicitement ; pour une surface alignée sur la scène, utiliser Surface + friction. La surface offre six faces via Avancé, placement à 6 mm, friction, matériaux et intensité. Quatre boutons, déconnexion simulée, reprise de lecture, capture, profils Unity et export de diagnostic restent disponibles.

Sans alignement fourni, l’API du SDK garde le mapping existant. Les profils Unity conservent centre, bornes et axes ; échelle et Transform restent des réglages de scène. Le JSON de l’application conserve également échelle, inversion et rotation locale.

## Vérification et limites

- 27 scénarios numériques réussis : 48 permutations signées, inverses, conservation du travail, gestes invalides, contacts et cycle de session.
- API Unity compilée sans erreur ni avertissement.
- Test Unity du défaut : droite brute Y, haut brut Z, profondeur brute X. Directions monde et projection caméra vérifiées, blocage avant confirmation, profils et JSON v2 conservant les axes ; capture, six faces, friction, déconnexion et reprise.
- Contrôles du Player et observations visuelles détaillés dans `VALIDATION.md`.

À valider sur le Falcon : précision des gestes, sensations, butées, relâchement, perte de focus et coupure physique. Aucun test actuel ne certifie 1 kHz ou une force mesurée. Une seule face statique ; pas d’adhérence statique, de proxy contraint, de Rigidbody haptique ou de contacts multiples.

L’interface reste en IMGUI : navigation clavier complète, lecteur d’écran et fenêtres très étroites restent à améliorer. La vue de face privilégie droite/haut ; la profondeur est numérique et pourrait bénéficier d’une vue latérale optionnelle. Ces points ne sont pas présentés comme résolus.
