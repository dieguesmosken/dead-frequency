#!/usr/bin/env python3
"""Histórico completo das builds (releases da main e pré-releases da Internal).

O histórico mora no arquivo builds.json da branch `historico` (nenhum workflow roda nela) e nunca perde
nada: quando uma pré-release antiga é apagada da página de Releases, ela continua aqui com estado
"removida". O site lê esse arquivo e mostra a página de versões (site/versoes.html).

Uso (dentro dos workflows, com GH_TOKEN e GITHUB_REPOSITORY):
  history.py sync     ARQUIVO              # registra toda release publicada que ainda não está no histórico
  history.py prune    ARQUIVO --keep 5     # apaga da página as pré-releases antigas (mantém a tag) e marca "removida"
  history.py included ARQUIVO TAG          # marca as internas que entraram na release estável TAG (git: commit ancestral)
"""
import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys

REPO = os.environ.get("GITHUB_REPOSITORY", "dieguesmosken/dead-frequency")


def gh(*args, check=True):
    r = subprocess.run(["gh", *args], capture_output=True, text=True)
    if check and r.returncode != 0:
        sys.exit(f"gh {' '.join(args)}: {r.stderr.strip()}")
    return r.stdout


def now():
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return {"descricao": "Histórico completo das builds do Dead Frequency (gerado pelos workflows).", "builds": []}


def save(path, data):
    data["builds"].sort(key=lambda b: b.get("data", ""), reverse=True)
    data["atualizado"] = now()
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def parse_tag(tag):
    m = re.match(r"^v(\d+\.\d+\.\d+)(?:\.(\d+)-pre)?$", tag)
    if not m:
        return tag.lstrip("v"), None
    return m.group(1), int(m.group(2)) if m.group(2) else None


def pr_for(sha):
    try:
        prs = json.loads(gh("api", f"repos/{REPO}/commits/{sha}/pulls", check=False) or "[]")
    except json.JSONDecodeError:
        prs = []
    if isinstance(prs, list) and prs:
        p = prs[0]
        return {"numero": p.get("number"), "titulo": p.get("title"), "branch": p.get("head", {}).get("ref"), "url": p.get("html_url")}
    msg = gh("api", f"repos/{REPO}/commits/{sha}", "--jq", ".commit.message", check=False)
    m = re.search(r"Merge pull request #(\d+)", msg or "")
    return {"numero": int(m.group(1))} if m else None


def entry_from_release(r):
    tag = r["tag_name"]
    versao, build = parse_tag(tag)
    sha = gh("api", f"repos/{REPO}/commits/{tag}", "--jq", ".sha", check=False).strip() or r.get("target_commitish")
    return {
        "tag": tag,
        "canal": "interna" if r.get("prerelease") else "estavel",
        "versao": versao,
        "build": build,
        "titulo": r.get("name") or tag,
        "data": r.get("published_at") or r.get("created_at"),
        "commit": sha,
        "pr": pr_for(sha) if sha else None,
        "notas": r.get("body") or "",
        "url": r.get("html_url"),
        "downloads": [{"nome": a["name"], "url": a["browser_download_url"], "tamanho": a["size"]} for a in r.get("assets", [])],
        "estado": "publicada",
        "historico": [{"quando": r.get("published_at") or now(), "evento": "publicada"}],
    }


def all_releases():
    out = gh("api", "--paginate", f"repos/{REPO}/releases?per_page=100", "--jq", ".[] | tojson")
    return [json.loads(line) for line in out.splitlines() if line.strip()]


def cmd_sync(path):
    data = load(path)
    known = {b["tag"]: b for b in data["builds"]}
    added = 0
    live = set()
    for r in all_releases():
        if r.get("draft"):
            continue
        live.add(r["tag_name"])
        if r["tag_name"] not in known:
            data["builds"].append(entry_from_release(r))
            added += 1
    # Alguém apagou na mão uma release que ainda constava como publicada: registra.
    for b in data["builds"]:
        if b["estado"] == "publicada" and b["tag"] not in live:
            b["estado"] = "removida"
            b["removida_em"] = now()
            b["historico"].append({"quando": now(), "evento": "removida da página de Releases (fora do workflow)"})
    save(path, data)
    print(f"sync: {added} nova(s), {len(data['builds'])} no histórico")


def cmd_prune(path, keep):
    data = load(path)
    internas = sorted((b for b in data["builds"] if b["canal"] == "interna" and b["estado"] == "publicada"),
                      key=lambda b: b.get("data", ""), reverse=True)
    for b in internas[keep:]:
        # Só a release sai da página; a tag fica (o número das próximas builds depende dela).
        r = subprocess.run(["gh", "release", "delete", b["tag"], "--yes", "--repo", REPO], capture_output=True, text=True)
        if r.returncode != 0 and "not found" not in r.stderr.lower():
            print(f"prune: falhou {b['tag']}: {r.stderr.strip()}")
            continue
        b["estado"] = "removida"
        b["removida_em"] = now()
        b["historico"].append({"quando": now(), "evento": f"removida da página de Releases (limpeza: ficam as {keep} internas mais novas; tag mantida)"})
        print(f"prune: {b['tag']} removida")
    save(path, data)


def cmd_included(path, tag):
    data = load(path)
    n = 0
    for b in data["builds"]:
        if b["canal"] != "interna" or b.get("incluida_em") or not b.get("commit"):
            continue
        ok = subprocess.run(["git", "merge-base", "--is-ancestor", b["commit"], tag], capture_output=True).returncode == 0
        if ok:
            b["incluida_em"] = tag
            b["historico"].append({"quando": now(), "evento": f"mesclada na main: entrou na release {tag}"})
            n += 1
    save(path, data)
    print(f"included: {n} interna(s) marcadas como parte de {tag}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["sync", "prune", "included"])
    ap.add_argument("file")
    ap.add_argument("tag", nargs="?")
    ap.add_argument("--keep", type=int, default=5)
    a = ap.parse_args()
    if a.cmd == "sync":
        cmd_sync(a.file)
    elif a.cmd == "prune":
        cmd_prune(a.file, a.keep)
    else:
        if not a.tag:
            sys.exit("included precisa da TAG")
        cmd_included(a.file, a.tag)


if __name__ == "__main__":
    main()
