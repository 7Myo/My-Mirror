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

My Mirror pour Linux utilise le même paquet **Flatpak** sur Ubuntu, Debian, Arch et NixOS. La fenêtre blanche reprend le logo, le nom **My-Mirror**, le grand code AirPlay et la case **Son AirPlay** cochée par défaut.

### 1. Préparer Flatpak et la découverte réseau

Sur Ubuntu ou Debian :

```sh
sudo apt update
sudo apt install flatpak avahi-daemon
sudo systemctl enable --now avahi-daemon
```

Sur Arch :

```sh
sudo pacman -Syu flatpak avahi
sudo systemctl enable --now avahi-daemon
```

Sur NixOS, ajoute ces réglages à ta configuration système, puis applique-la :

```nix
services.flatpak.enable = true;
services.avahi = {
  enable = true;
  publish.enable = true;
  publish.userServices = true;
  openFirewall = true;
};
networking.firewall.allowedTCPPorts = [ 7010 7011 7012 ];
networking.firewall.allowedUDPPorts = [ 7010 7011 7012 ];
```

```sh
sudo nixos-rebuild switch
```

Si ton pare-feu bloque les connexions locales, autorise mDNS sur UDP 5353 et AirPlay sur TCP/UDP 7010 à 7012 pour ton réseau local. Le téléphone et le PC doivent être sur le même réseau, avec la communication entre appareils autorisée.

### 2. Installer My Mirror

Ajoute le dépôt des composants Flatpak une seule fois :

```sh
flatpak remote-add --user --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
```

Télécharge `MyMirror-Linux-x86_64.flatpak` depuis les releases quand le paquet validé sera disponible. Dans le dossier du téléchargement :

```sh
flatpak install --user ./MyMirror-Linux-x86_64.flatpak
flatpak run io.github.SevenMyo.MyMirror
```

Le paquet inclut l’interface Python/Tkinter, UxPlay et ses bibliothèques. Flatpak installe le runtime nécessaire. Le service Avahi et les règles de pare-feu restent des réglages du système hôte.

### 3. Partager l’écran de l’iPhone

Ouvre My Mirror depuis le menu des applications ou avec la commande ci-dessus. Laisse **Son AirPlay** coché pour recevoir le son, puis choisis **My-Mirror** dans **Centre de contrôle → Recopie de l’écran** sur l’iPhone. Saisis le code affiché si l’iPhone le demande.

Sur Linux, UxPlay ouvre sa propre fenêtre vidéo et l’accueil reste ouvert. Fermer l’accueil coupe la réception et ferme le moteur. Cette première version Linux doit encore être validée avec un iPhone sur les distributions ciblées.

### Mettre à jour ou désinstaller

Pour une nouvelle release téléchargée, exécute `flatpak install --user ./MyMirror-Linux-x86_64.flatpak` à nouveau. La mise à jour automatique intégrée au lanceur Windows est spécifique à Windows.

```sh
flatpak uninstall --user io.github.SevenMyo.MyMirror
```

Les instructions de compilation, l’exécutable direct et la définition Nix sont dans le [guide technique Linux](linux/README.md).

## Compilation

Exécute `powershell.exe -NoProfile -File .\build.ps1` depuis le dossier du projet. Le script utilise le compilateur C# de .NET Framework fourni avec Windows x64 et produit `MyMirror.exe`.

## Fonctionnement

Le lanceur WinForms démarre le processus UxPlay avec le nom du récepteur et un code temporaire. Bonjour annonce le service sur le réseau local. UxPlay décode le flux AirPlay et My Mirror intègre la surface Direct3D dans sa propre fenêtre.

My Mirror sauvegarde les paramètres UxPlay avant de les modifier. Il les restaure à l’arrêt normal ou au prochain démarrage après une fermeture forcée. Si UxPlay tourne encore après cette fermeture, quitte-le depuis la zone de notification, puis clique sur **Réessayer** dans My Mirror. Si le fichier a été modifié depuis, My Mirror garde les nouveaux réglages.

La [licence MIT](LICENSE) couvre le lanceur et son logo. [UxPlay](https://github.com/FDH2/UxPlay) et [UxPlay Windows](https://github.com/leapbtw/uxplay-windows) sont installés séparément et conservent leurs licences.
