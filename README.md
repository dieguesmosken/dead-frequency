# Histórico das builds

`builds.json` guarda **todas** as builds do Dead Frequency: releases da `main` (canal estável) e pré-releases da
`Internal`. Nada é apagado daqui: quando uma pré-release antiga sai da página de Releases (limpeza automática,
ficam as 5 internas mais novas; a tag continua), ela fica com `"estado": "removida"`. Quando a `Internal` chega na
`main`, as internas que entraram na release ganham `"incluida_em"`. Cada build tem `historico` com os eventos.

Atualizado pelos workflows (`tools/builds/update_history.sh` na branch principal). Mostrado no site em
`updates.html` (abas Lançamentos e Desenvolvimento).
