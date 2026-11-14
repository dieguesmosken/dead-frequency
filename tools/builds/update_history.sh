#!/usr/bin/env bash
# Atualiza o histórico das builds na branch `historico` (builds.json) — chamado pelos workflows.
#   update_history.sh internal TAG   → registra a build e apaga da página as pré-releases antigas (ficam 5)
#   update_history.sh release  TAG   → registra a release e marca as internas que entraram nela
# Tenta 3 vezes se outro workflow empurrar ao mesmo tempo.
set -euo pipefail
MODE="$1"; TAG="$2"
ROOT="$(git rev-parse --show-toplevel)"
PY="$ROOT/tools/builds/history.py"
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
for attempt in 1 2 3; do
  rm -rf "$ROOT/.hist"; git worktree prune
  if git fetch -q origin historico 2>/dev/null; then
    git worktree add -q -f "$ROOT/.hist" FETCH_HEAD
    (cd "$ROOT/.hist" && git checkout -q -B historico)
  else
    git worktree add -q --orphan -b historico "$ROOT/.hist"
  fi
  python3 "$PY" sync "$ROOT/.hist/builds.json"
  if [ "$MODE" = "internal" ]; then
    python3 "$PY" prune "$ROOT/.hist/builds.json" --keep 5
  else
    git fetch -q --tags --unshallow origin 2>/dev/null || git fetch -q --tags origin
    python3 "$PY" included "$ROOT/.hist/builds.json" "$TAG"
  fi
  cd "$ROOT/.hist"
  git add builds.json
  git commit -q -m "historico: $TAG" || { echo "sem mudanças"; exit 0; }
  if git push -q origin historico; then echo "histórico atualizado ($TAG)"; exit 0; fi
  cd "$ROOT"; echo "push recusado, tentando de novo ($attempt)"; sleep 5
done
exit 1
