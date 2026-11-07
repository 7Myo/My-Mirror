import runpy
import socket
import time

module = runpy.run_path("/app/share/my-mirror/my_mirror.py")
root = module["tk"].Tk()
app = module["Mirror"](root)
try:
    root.update()
    assert app.audio.get()
    assert app.code_value.get().isdigit()
    connected = False
    for _ in range(50):
        assert app.process is not None and app.process.poll() is None
        try:
            with socket.create_connection(("127.0.0.1", 7011), timeout=0.2):
                connected = True
            break
        except OSError:
            time.sleep(0.1)
    assert connected, "AirPlay did not open TCP 7011"
    app.audio.set(False)
    app.restart()
    assert app.process is not None and app.process.poll() is None
    assert app.process.args[-2:] == ["-as", "0"]
    print("Linux interface, receiver startup and audio selection OK")
finally:
    app.close()
