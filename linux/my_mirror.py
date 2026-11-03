import re
import os
import secrets
import shutil
import signal
import subprocess
import tkinter as tk

BACKGROUND = "#101010"
FOREGROUND = "#f5f5f5"


class Mirror:
    def __init__(self, root):
        self.root = root
        self.process = None
        self.active_name = None
        root.title("My Mirror · AirPlay")
        root.configure(bg=BACKGROUND, padx=24, pady=20)
        root.protocol("WM_DELETE_WINDOW", self.close)

        self.label("My Mirror", ("TkDefaultFont", 24, "bold"))
        self.label("Nom affiché sur l’iPhone")
        self.receiver = tk.Entry(root, bg="#222222", fg=FOREGROUND, insertbackground=FOREGROUND)
        self.receiver.insert(0, "My-Mirror")
        self.receiver.pack(fill="x", pady=(2, 8))
        self.receiver.bind("<Return>", self.apply_name)
        tk.Button(root, text="Appliquer le nom", command=self.apply_name).pack(anchor="w")

        self.label("Code AirPlay")
        self.code = self.label("—", ("TkDefaultFont", 21, "bold"))
        self.label("Sur l’iPhone, ouvre Centre de contrôle → Recopie de l’écran.")
        self.status = self.label("")
        self.label("Created by 7myo")
        root.after_idle(self.apply_name)
        root.after(500, self.poll)

    def label(self, text, font="TkDefaultFont"):
        label = tk.Label(self.root, text=text, font=font, bg=BACKGROUND, fg=FOREGROUND,
                         anchor="w", justify="left", wraplength=400)
        label.pack(fill="x", pady=3)
        return label

    def apply_name(self, event=None):
        name = self.receiver.get().strip()
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,27}", name):
            self.status.config(text="Utilise 1 à 28 lettres sans accent, chiffres, tirets ou soulignés.")
            return
        if name == self.active_name and self.process is not None and self.process.poll() is None:
            return
        engine = shutil.which("uxplay")
        if engine is None:
            self.status.config(text="Installe UxPlay avec le gestionnaire de paquets de ta distribution.")
            return

        self.stop()
        code = str(1000 + secrets.randbelow(9000))
        try:
            self.process = subprocess.Popen(
                [engine, "-n", name, "-nh", "-pin", code, "-fps", "60", "-nofreeze"],
                start_new_session=True,
            )
        except OSError as error:
            self.status.config(text=str(error))
            return
        self.active_name = name
        self.code.config(text=code)
        self.status.config(text=f"Récepteur démarré : {name}")

    def poll(self):
        if self.process is not None and self.process.poll() is not None:
            self.stop()
            self.status.config(text="UxPlay s’est arrêté. Consulte la sortie dans le terminal.")
        self.root.after(500, self.poll)

    def stop(self):
        self.stop_engine()
        self.code.config(text="—")

    def stop_engine(self):
        if self.process is not None and self.process.poll() is None:
            self.signal_engine(signal.SIGTERM)
            try:
                self.process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                self.signal_engine(signal.SIGKILL)
                self.process.wait(timeout=2)
        self.process = None
        self.active_name = None

    def signal_engine(self, signum):
        try:
            os.killpg(self.process.pid, signum)
        except ProcessLookupError:
            pass

    def close(self):
        self.stop()
        self.root.destroy()


if __name__ == "__main__":
    window = tk.Tk()
    mirror = Mirror(window)
    try:
        window.mainloop()
    finally:
        mirror.stop_engine()
