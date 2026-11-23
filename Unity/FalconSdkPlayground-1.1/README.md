# Falcon SDK Playground 1.1

Projet autonome avec SDK `com.falcon.haptics 1.2.0-preview.5` embarqué. Cette version corrige les échanges d’axes et simplifie le premier essai. L’ancien projet 1.0 est conservé séparément.

## Démarrer

Extraire tout le ZIP. Dans Unity Hub, ajouter le dossier contenant Assets, Packages et ProjectSettings avec Unity **6000.3.24f1**, ouvrir `Assets/Scenes/FalconPlayground.unity` puis lancer Play. La scène s’ouvre aussi depuis Tools → Falcon → Ouvrir le banc d’essai. Démarrage en simulation, sans effet armé.

Pour l’application Windows séparée, extraire tout le ZIP puis lancer `FalconPlayground.exe`. Garder les DLL et dossiers à côté. Aucun Unity Editor nécessaire.

## Trois étapes

1. **Appareil** : simulation sans périphérique, ou Falcon en lecture seule. Pour le matériel : SDK Force Dimension DHD x64 et pilote installés séparément, alimentation/USB branchés et autres lecteurs du Falcon arrêtés.
2. **Directions** : cliquer Départ, déplacer doucement vers votre droite, valider. Répéter vers le haut puis vers vous. Au moins 5 mm par geste, sans forcer les butées. Vérifier droite → droite et haut → haut ; la profondeur se lit en mm. Placer la poignée confortablement puis confirmer/centrer. Au prochain lancement, recharger et confirmer les réglages. Aucun effort matériel avant confirmation.
3. **Essayer** : Surface + friction, Ressort ou Eau. Pour la surface : placer devant la poignée, activer l’essai, relâcher puis maintenir le bouton 1 et pousser doucement vers la surface. Pour le ressort : s’écarter du point de prise. Relâcher coupe l’effort ; ARRÊTER ou Échap désarme.

En simulation, les curseurs de droite correspondent à gauche/droite, bas/haut et loin/vers soi. Le bouton 1 simulé maintient la prise.

## Fonctions avancées

Afficher les autres effets donne accès à résistance, gravier, moteur et paroi du pilote. Cette dernière travaille sur X brut ; utiliser la surface pour un mur aligné sur la scène.

Réglages avancés : boutons 2 à 4, disponibilité simulée, réinitialisation, capture optionnelle de zone, taille d’affichage, six faces, friction continue, rigidité/amortissement, profil exemple, sauvegarde, relance et diagnostic JSON. Les autres propriétés restent dans les Inspecteurs Unity. Les changements concernés désarment l’essai.

La capture de zone définit une boîte logicielle et un centre ; elle ne remplace pas les trois gestes. Confirmer les directions retire les anciennes bornes pour permettre le recentrage. Le profil exemple sans axes nécessite ensuite le réglage des directions.

Les fichiers JSON v2 sont dans le dossier persistant de l’application, séparés pour simulation et Falcon. Ils conservent centre, limites, axes, échelle, inversion et rotation locale. Le diagnostic exporté reste local. Les fichiers v1 sont lus en secours sans être écrasés ; ils ne valident pas les directions.

## Portée

Une surface statique, une face et friction dynamique ; six modes exclusifs de la surface. Pas de contacts multiples, d’objets mobiles haptiques ou d’adhérence statique. SDK DHD natif non redistribué. Variantes FalconLab 4/8 N non intégrées.

[Audit et corrections](AUDIT-UI-UX.md) · [Parcours manuel](TEST_CHECKLIST.md) · [Validation](VALIDATION.md).
