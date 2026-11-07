# My Mirror pour Linux

Le format de distribution principal prévu est **Flatpak**, commun à Ubuntu, Debian, Arch et NixOS. L’interface est blanche, le nom est fixé à **My-Mirror** et le son est activé par défaut.

## Flatpak

Après installation de Flatpak sur le système, le paquet prévu s’installe avec la même commande sur chaque distribution.

```sh
flatpak install --user ./MyMirror-Linux-x86_64.flatpak
flatpak run io.github.SevenMyo.MyMirror
```

Le manifeste inclut Python, Tcl/Tk, UxPlay et les bibliothèques de découverte. L’application utilise le réseau local, le serveur graphique, la sortie audio et l’accélération graphique. Le service Avahi du système hôte doit fonctionner et le pare-feu doit laisser passer mDNS (UDP 5353) et AirPlay (TCP/UDP 7010 à 7012).

Pour construire le paquet, utilise un poste Linux x86_64 avec Flatpak Builder et le dépôt Flathub configuré, puis exécute `sh linux/flatpak/build.sh`. La compilation du paquet Flatpak est encore à valider sur Linux.

## Exécutable direct (option secondaire)

`MyMirror-Linux-x86_64` est un fichier ELF construit depuis Windows. Il utilise Python, Tkinter et UxPlay du système. Les étapes ci-dessous concernent ce mode et le lancement du code source.

## Ubuntu et Debian

Installe Python et Tkinter une fois si nécessaire.

```sh
sudo apt install python3 python3-tk
chmod +x MyMirror-Linux-x86_64
./MyMirror-Linux-x86_64
```

Le bouton **Préparer AirPlay** peut ensuite installer UxPlay, Avahi et les plugins GStreamer via apt, avec une autorisation administrateur. Le gestionnaire de paquets utilise les dépôts déjà configurés sur le PC.

## Arch Linux

UxPlay est disponible dans l’AUR. Installe-le avec la méthode AUR de ton choix après avoir consulté son paquet.

```sh
sudo pacman -S --needed python tk avahi gst-plugins-base gst-plugins-good gst-plugins-bad gst-libav
paru -S uxplay
sudo systemctl enable --now avahi-daemon
chmod +x MyMirror-Linux-x86_64
./MyMirror-Linux-x86_64
```

La commande `paru` suppose que cet assistant AUR est déjà installé. Le lanceur ne l’installe pas. Un `pacman -S uxplay` ne suffit pas puisque UxPlay n’est pas dans les dépôts officiels consultés.

## Nix et NixOS

Depuis le dépôt, lance `nix run .` ou construis le paquet avec `nix build .`. Le paquet déclare Python, Tkinter et UxPlay et ajoute une entrée dans le menu des applications s’il est installé dans le profil.

Sur NixOS, active le service réseau et les ports AirPlay dans ta configuration système.

```nix
services.avahi = {
  enable = true;
  publish.enable = true;
  publish.userServices = true;
  openFirewall = true;
};
networking.firewall.allowedTCPPorts = [ 7010 7011 7012 ];
networking.firewall.allowedUDPPorts = [ 7010 7011 7012 ];
```

Applique ensuite ta configuration avec `sudo nixos-rebuild switch`. Le paquet est défini pour x86_64 et aarch64. Le fichier Linux de la release est destiné aux PC x86_64.

## Compilation du fichier Linux

Depuis le dossier `linux`, avec Go 1.24 ou plus récent.

```sh
CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -trimpath -ldflags="-s -w" -o MyMirror-Linux-x86_64 .
```

Sous Windows, utilise `linux/build.ps1` avec Go disponible dans le PATH. Le fichier produit est un exécutable ELF x86_64 qui lance l’interface Python embarquée. Python, Tkinter et le moteur UxPlay restent des dépendances système.

## État de validation

Le fichier ELF a été construit depuis Windows. Le démarrage graphique, la recopie depuis un iPhone, les installations apt/AUR et la construction Nix restent à vérifier sur les distributions ciblées.

UxPlay ouvre sa propre fenêtre vidéo. L’accueil reste ouvert pour le code et le choix du son. Fermer l’accueil coupe la réception et ferme le moteur. La mise à jour automatique de la version Windows est propre à Windows à ce stade.
