"""Texto do release no GitHub: novidades da versão (GameVersion.cs), fotos dos testes e downloads.
Uso: python3 site/release_notes.py 0.9.4 [ref] > notes.md   (ref = tag/commit das imagens; padrão main)"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ver = sys.argv[1].lstrip("v")
ref = sys.argv[2] if len(sys.argv) > 2 else "main"
RAW = f"https://raw.githubusercontent.com/dieguesmosken/dead-frequency/{ref}/site/assets/shots/"
SITE = "https://www.teuzin.com.br/dead-frequency/"
release_tag = sys.argv[3] if len(sys.argv) > 3 else "v" + ver
DL = "https://github.com/dieguesmosken/dead-frequency/releases/download/" + release_tag + "/"

src = open(os.path.join(ROOT, "Scripts", "Core", "GameVersion.cs"), encoding="utf-8").read()
m = re.search(r'new\("' + re.escape(ver) + r'", "([^"]+)", "([^"]+)", new\[\]\s*\{(.*?)\}\),', src, re.S)
if not m:
    sys.exit(f"versão {ver} não está no GameVersion.cs")
date, title, body = m.groups()
changes = [c.replace('\\"', '"') for c in re.findall(r'"((?:[^"\\]|\\.)*)"', body)]

out = [f"![Dead Frequency]({RAW}hero.jpg)", "", f"## v{ver} — {title}", f"*{date}*", ""]
out += [f"- {c}" for c in changes]
caps = os.path.join(ROOT, "site", "assets", "shots", "v" + ver, "captions.json")
if os.path.exists(caps):
    shots = json.load(open(caps, encoding="utf-8"))[:9]
    out += ["", "### Fotos desta versão", ""]
    row = []
    for i, s in enumerate(shots):
        row.append(f'<img src="{RAW}v{ver}/{s["file"]}" width="32%" alt="{s["caption"]}" title="{s["caption"]}">')
    out.append("<p>" + " ".join(row) + "</p>")
out += [
    "", "### Baixar", "",
    "| | Arquivo |", "|---|---|",
    f"| **Recomendado (Windows)** — instala e atualiza sozinho | [DeadFrequencyLauncher-windows.exe]({DL}DeadFrequencyLauncher-windows.exe) |",
    f"| Launcher Linux (`chmod +x` e abrir) | [DeadFrequencyLauncher-linux]({DL}DeadFrequencyLauncher-linux) |",
    f"| Jogo completo Windows (extraia e abra `DeadFrequency.exe`) | [DeadFrequency-v{ver}-windows.zip]({DL}DeadFrequency-v{ver}-windows.zip) |",
    f"| Jogo completo Linux (extraia e rode `./DeadFrequency.x86_64`) | [DeadFrequency-v{ver}-linux.zip]({DL}DeadFrequency-v{ver}-linux.zip) |",
    "", "### Verificar o download", "",
    f"- Confira o SHA-256 com o [SHA256SUMS.txt]({DL}SHA256SUMS.txt) (no Windows: `certutil -hashfile ARQUIVO SHA256`).",
    "- A partir da v0.9.5, os arquivos têm atestado de build do GitHub (prova que saíram do GitHub Actions deste repositório): "
    "`gh attestation verify ARQUIVO --repo dieguesmosken/dead-frequency`.",
    "", f"🌐 Site: {SITE} · 📜 [Histórico completo]({SITE}#novidades) · 💚 [Apoie o projeto (Pix)]({SITE}#apoie)",
]
print("\n".join(out))
