# Hotfix de segurança multiplayer — protocolo 7

Este branch reúne correções para integridade de inventário, economia e operações com itens no mundo.

## Mudanças

- Operações que alteram o estado da partida exigem que a conexão tenha concluído o `hello` e esteja marcada como `joined`.
- O servidor não aceita mais inventário ou equipamento inicial enviado pelo cliente no `hello`.
- O inventário online começa vazio por sessão e passa a ser atualizado pelo servidor após compras, vendas, coletas, descartes e transferências.
- A mensagem `inventory` só pode remover itens ou mover itens já existentes entre mochila e equipamento. Ela não pode criar itens.
- Equipamentos são validados por uma lista explícita de slots; a mochila e o limite de capacidade são calculados no servidor.
- O servidor valida compras e vendas, mantém números de requisição monotônicos por conexão e rejeita repetições.
- Descarte valida posição, distância em relação ao último estado de jogador recebido, rotação e limites absolutos das coordenadas.
- Coleta valida a distância entre o jogador e a posição registrada do objeto antes de removê-lo do mundo.
- O protocolo foi atualizado de 6 para 7; cliente e Worker precisam ser atualizados juntos.
- O inventário local/offline é preservado em memória durante a partida online e restaurado ao sair. Itens adquiridos na sessão online não são gravados no save offline.

## Limitação conhecida

A posição do jogador ainda é enviada pelo próprio cliente no pacote `tick`. O hotfix valida formato, limites e distância de interação usando o último estado recebido, mas isso não equivale a movimento totalmente autoritativo: um cliente modificado ainda pode tentar falsificar sua própria posição por meio de `tick`. Uma proteção completa exigiria validação de deslocamento no servidor e tratamento explícito de teletransportes legítimos (por exemplo, elevadores e portas).

Os objetos descartados usam a posição inicial registrada no servidor; a física local pode deslocá-los um pouco. O limite de coleta foi definido com tolerância para pequenas movimentações.

## Testes

O smoke test foi ampliado para cobrir:
- compra antes do `hello`;
- tentativa de criar inventário no cliente e venda de item inexistente;
- repetição de requisição de compra/venda;
- descarte distante e coleta distante;
- concessão de itens pelo servidor e sincronização de inventário/equipamento.

Os testes e a compilação Godot ainda precisam ser executados em ambiente local/CI antes do deploy. Este branch não altera `main` e não faz deploy do Cloudflare Worker.
