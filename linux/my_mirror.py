import fcntl
import os
import secrets
import shutil
import signal
import subprocess
import tempfile
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox

NAME = "My-Mirror"
BACKGROUND = "#ffffff"
FOREGROUND = "#1a1c1f"


def packages():
    return ["python3", "python3-tk", "uxplay", "avahi-daemon",
            "gstreamer1.0-plugins-base", "gstreamer1.0-plugins-good",
            "gstreamer1.0-plugins-bad", "gstreamer1.0-libav", "gstreamer1.0-gl"]


def install_command():
    if Path("/etc/NIXOS").exists():
        return None
    if shutil.which("apt-get"):
        return ["pkexec", "apt-get", "install", "-y", *packages()]
    return None


def engine_arguments(code, audio):
    args = ["uxplay", "-n", NAME, "-nh", "-pin", code, "-p", "7010", "-fps", "60", "-nofreeze"]
    if not audio:
        args += ["-as", "0"]
    return args


class Mirror:
    def __init__(self, root):
        self.root = root
        self.process = None
        self.log = None
        self.closed = False
        self.preparing = False
        self.result = None
        root.title("My Mirror")
        root.configure(bg=BACKGROUND, padx=24, pady=20)
        root.resizable(False, False)
        root.option_add("*Font", ("sans-serif", 10))
        root.option_add("*Button.Background", "#f5f5f5")
        root.option_add("*Button.Foreground", FOREGROUND)
        root.protocol("WM_DELETE_WINDOW", self.close)
        header = tk.Frame(root, bg=BACKGROUND)
        header.pack(fill="x", pady=(0, 14))
        logo = tk.Canvas(header, width=54, height=54, bg=BACKGROUND, highlightthickness=0)
        logo.pack(side="left", padx=(0, 14))
        scale = 54 / 64
        for points in (
            (3, 7, 61, 7, 61, 42, 46.5, 42, 44.2, 39.35, 58.4, 39.35,
             58.4, 9.6, 5.6, 9.6, 5.6, 39.35, 19.8, 39.35, 17.5, 42, 3, 42),
            (32, 31.5, 53, 56, 11, 56),
        ):
            logo.create_polygon(*(value * scale for value in points), fill=FOREGROUND, outline="")
        titles = tk.Frame(header, bg=BACKGROUND)
        titles.pack(side="left", fill="x", expand=True)
        tk.Label(titles, text="My Mirror", font=("sans-serif", 23, "bold"),
                 bg=BACKGROUND, fg=FOREGROUND, anchor="w").pack(fill="x")
        self.status = tk.Label(titles, text="Démarrage…", wraplength=270,
                               bg=BACKGROUND, fg="#525860", anchor="w", justify="left")
        self.status.pack(fill="x")
        self.label(NAME, ("sans-serif", 14, "bold"))
        self.label("Code AirPlay", underline=0)
        self.code_value = tk.StringVar(value="—")
        self.code = tk.Entry(root, textvariable=self.code_value, state="readonly",
                             readonlybackground="#e7f0fe", fg=FOREGROUND, justify="center",
                             font=("sans-serif", 32, "bold"), width=12, relief="flat",
                             highlightthickness=1, highlightbackground="#c8cdd4",
                             highlightcolor="#0056c5", exportselection=False)
        self.code.pack(fill="x", ipady=4, pady=(4, 8))
        root.bind("<Alt-c>", lambda event: self.code.focus_set())
        root.bind("<Alt-s>", lambda event: self.audio_button.invoke())
        self.audio = tk.BooleanVar(value=True)
        self.audio_button = tk.Checkbutton(
            root, text="Son AirPlay", variable=self.audio, command=self.restart,
            bg=BACKGROUND, fg=FOREGROUND, activebackground=BACKGROUND)
        self.audio_button.pack(anchor="w", pady=8)
        self.label("Sur l’iPhone, ouvre Recopie de l’écran puis choisis My-Mirror.")
        self.action = tk.Button(root, text="Réessayer", command=self.start)
        self.action.pack(anchor="w", pady=8)
        self.label("Created by 7myo", foreground="#525860")
        root.after_idle(self.start)
        root.after(400, self.poll)

    def label(self, text, font="TkDefaultFont", foreground=FOREGROUND, underline=-1):
        label = tk.Label(self.root, text=text, font=font, bg=BACKGROUND, fg=FOREGROUND,
                         anchor="w", justify="left", wraplength=350, underline=underline)
        label.config(fg=foreground)
        label.pack(fill="x", pady=4)
        return label

    def start(self):
        if self.preparing or self.process is not None:
            return
        if shutil.which("uxplay") is None:
            self.status.config(text="Une préparation AirPlay est nécessaire.")
            self.action.config(text="Préparer AirPlay", command=self.prepare)
            return
        self.action.config(text="Réessayer", command=self.start)
        code = str(1000 + secrets.randbelow(9000))
        self.log = tempfile.TemporaryFile(mode="w+b")
        try:
            self.process = subprocess.Popen(
                engine_arguments(code, self.audio.get()), start_new_session=True,
                stdout=self.log, stderr=subprocess.STDOUT)
        except OSError as error:
            self.log.close()
            self.log = None
            self.status.config(text=str(error))
            return
        self.code_value.set(code)
        self.status.config(text="Récepteur démarré.")
        self.action.pack_forget()

    def restart(self):
        try:
            self.stop()
            self.start()
        except (OSError, subprocess.TimeoutExpired) as error:
            self.status.config(text=str(error))

    def prepare(self):
        if self.preparing:
            return
        command = install_command()
        if command is None or not shutil.which("pkexec"):
            if Path("/.flatpak-info").exists():
                self.status.config(text="Le paquet Flatpak est incomplet. Réinstalle My Mirror.")
            elif Path("/etc/NIXOS").exists():
                self.status.config(text="Utilise le paquet Nix My Mirror et active le service Avahi dans NixOS.")
            elif shutil.which("pacman"):
                self.status.config(text="Arch : installe uxplay depuis l’AUR, puis active avahi-daemon. Consulte le guide Linux.")
            else:
                self.status.config(text="Installe UxPlay et Avahi avec le gestionnaire de paquets de ta distribution.")
            return
        if not messagebox.askokcancel("Préparer AirPlay",
                "Installer UxPlay, Avahi et les composants vidéo ? Linux demandera une autorisation administrateur.",
                parent=self.root):
            return
        self.preparing = True
        self.action.config(state="disabled")
        self.audio_button.config(state="disabled")
        self.status.config(text="Installation des composants AirPlay…")
        threading.Thread(target=self.install, args=(command,), daemon=True).start()

    def install(self, command):
        try:
            result = subprocess.run(
                command,
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=900)
            if result.returncode:
                raise RuntimeError("Installation annulée ou impossible. Vérifie la connexion et les dépôts de paquets.")
            result = subprocess.run(
                ["pkexec", "systemctl", "start", "avahi-daemon"],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
            if result.returncode:
                raise RuntimeError("Démarre le service avahi-daemon puis réessaie.")
            self.result = ""
        except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
            self.result = str(error)

    def poll(self):
        if self.closed:
            return
        if self.result is not None:
            error, self.result = self.result, None
            self.preparing = False
            self.action.config(state="normal")
            self.audio_button.config(state="normal")
            if error:
                self.status.config(text=error)
            else:
                self.start()
        if self.process is not None and self.process.poll() is not None:
            self.log.seek(0)
            detail = self.log.read(8192).decode("utf-8", errors="replace")
            self.stop()
            self.status.config(text="Le moteur AirPlay s’est arrêté. Vérifie Avahi et les autres récepteurs.")
            self.action.pack(anchor="w", pady=8)
            if detail:
                messagebox.showerror("My Mirror", detail[-3000:], parent=self.root)
        self.root.after(400, self.poll)

    def stop(self):
        if self.process is not None:
            if self.process.poll() is None:
                try:
                    os.killpg(self.process.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
                try:
                    self.process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    try:
                        os.killpg(self.process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    self.process.wait(timeout=2)
            self.process = None
        if self.log is not None:
            self.log.close()
            self.log = None
        self.code_value.set("—")

    def close(self):
        if self.preparing:
            return
        self.stop()
        self.closed = True
        self.root.destroy()


def main():
    root = tk.Tk()
    lock_dir = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "my-mirror"
    lock_dir.mkdir(parents=True, exist_ok=True, mode=0o700)
    with (lock_dir / "instance.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            messagebox.showinfo("My Mirror", "My Mirror est déjà ouvert.", parent=root)
            root.destroy()
            return
        mirror = Mirror(root)
        try:
            root.mainloop()
        finally:
            if not mirror.closed:
                mirror.stop()


if __name__ == "__main__":
    main()
