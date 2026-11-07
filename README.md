# My Mirror

<img src="logo.svg" width="64" height="64" alt="Logo My Mirror">

My Mirror affiche l’écran d’un iPhone dans une fenêtre Windows. L’iPhone utilise la recopie AirPlay du Centre de contrôle.

## Démarrage

1. Télécharge et ouvre `MyMirror.exe` depuis les [versions GitHub](https://github.com/7Myo/My-Mirror/releases/latest) sur Windows x64.
2. Si **Préparer AirPlay** apparaît, clique dessus. My Mirror télécharge [UxPlay Windows](https://github.com/leapbtw/uxplay-windows/releases), vérifie le fichier, puis prépare Bonjour si nécessaire. Windows peut demander une autorisation administrateur pour ces étapes. Si UxPlay tourne déjà séparément, quitte-le depuis la zone de notification puis clique sur **Réessayer**. Le récepteur **My-Mirror** démarre dès que les prérequis sont prêts.
3. Sur le PC, laisse **Son AirPlay** coché pour recevoir le son ou décoche-le pour recevoir uniquement la vidéo. Changer l’option redémarre le récepteur et affiche un nouveau code.
4. Connecte l’iPhone au même réseau que le PC. Ouvre **Centre de contrôle → Recopie de l’écran**, sélectionne **My-Mirror**, puis saisis le code affiché si l’iPhone le demande.
5. La vidéo remplace l’accueil dans une fenêtre qui reprend les proportions initiales du rendu. Ferme cette fenêtre pour arrêter la recopie et quitter My Mirror.

Windows peut demander d’autoriser le moteur UxPlay à recevoir les connexions locales lors de sa première utilisation. My Mirror fonctionne sans droits administrateur une fois UxPlay et Bonjour installés.

Au lancement, My Mirror vérifie les versions sur GitHub. Si une version stable plus récente est disponible, **Mettre à jour** apparaît dans l’accueil. Un clic télécharge le nouvel exécutable, vérifie son empreinte GitHub, remplace le fichier et relance My Mirror. Garde l’exécutable dans un dossier où ton compte peut écrire.

Au clavier, utilise **Alt+C** pour accéder au code et le copier. My Mirror reprend les couleurs Windows quand un thème de contraste est actif au démarrage.

## Linux

La version Linux en préparation cible Ubuntu, Debian, Arch et NixOS avec un paquet Flatpak commun. Un exécutable direct et une définition Nix sont également préparés. Consulte le [guide Linux](linux/README.md) pour les dépendances et les limites de validation.

## Compilation

Exécute `powershell.exe -NoProfile -File .\build.ps1` depuis le dossier du projet. Le script utilise le compilateur C# de .NET Framework fourni avec Windows x64 et produit `MyMirror.exe`.

## Fonctionnement

Le lanceur WinForms démarre le processus UxPlay avec le nom du récepteur et un code temporaire. Bonjour annonce le service sur le réseau local. UxPlay décode le flux AirPlay et My Mirror intègre la surface Direct3D dans sa propre fenêtre.

My Mirror sauvegarde les paramètres UxPlay avant de les modifier. Il les restaure à l’arrêt normal ou au prochain démarrage après une fermeture forcée. Si UxPlay tourne encore après cette fermeture, quitte-le depuis la zone de notification, puis clique sur **Réessayer** dans My Mirror. Si le fichier a été modifié depuis, My Mirror garde les nouveaux réglages.

La [licence MIT](LICENSE) couvre le lanceur et son logo. [UxPlay](https://github.com/FDH2/UxPlay) et [UxPlay Windows](https://github.com/leapbtw/uxplay-windows) sont installés séparément et conservent leurs licences.
