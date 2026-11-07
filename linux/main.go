package main

import (
    _ "embed"
    "fmt"
    "os"
    "os/exec"
    "syscall"
)

//go:embed my_mirror.py
var source string

func notify(message string) {
    fmt.Fprintln(os.Stderr, message)
    if path, err := exec.LookPath("zenity"); err == nil {
        exec.Command(path, "--error", "--title=My Mirror", "--text="+message).Run()
    } else if path, err := exec.LookPath("kdialog"); err == nil {
        exec.Command(path, "--error", message, "--title", "My Mirror").Run()
    }
}

func main() {
    if len(os.Args) > 1 && os.Args[1] == "--version" {
        fmt.Println("My Mirror Linux 1.4.0")
        return
    }
    python, err := exec.LookPath("python3")
    if err != nil {
        notify("Installe Python 3 et Tkinter. Ubuntu/Debian : python3 python3-tk. Arch : python tk. NixOS : utilise le paquet Nix My Mirror.")
        os.Exit(1)
    }
    if err := exec.Command(python, "-c", "import tkinter").Run(); err != nil {
        notify("Tkinter manque. Ubuntu/Debian : installe python3-tk. Arch : installe tk. NixOS : utilise le paquet Nix My Mirror.")
        os.Exit(1)
    }
    if err := syscall.Exec(python, []string{python, "-c", source}, os.Environ()); err != nil {
        notify("My Mirror ne peut pas démarrer. Vérifie la session graphique Linux et les composants Python/Tkinter.")
        os.Exit(1)
    }
}
