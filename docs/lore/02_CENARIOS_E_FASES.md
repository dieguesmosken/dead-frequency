# Cenários e fases

Ordem de trabalho: de cima para baixo. Fases 1 e 2 são a prioridade; 3 a 5 só depois, como cenas isoladas.

## Fase 1 · Cidade (em andamento)

Cena atual: `Scenes/Levels/test_level.tscn` com `Scripts/World/CityGenerator*.cs` (cidade procedural).

O que falta para a ambientação "Brazilcore":

- Lombadas com física de suspensão (a van tem que pular de verdade).
- Asfalto remendado e bueiros (bueiros já existem: `CityGenerator.Drainage.cs`).
- Postes com fios e cartazes colados.
- Orelhões (já existem: `PhoneBooth`), lixeiras de grade, calçadas irregulares.
- Árvores com a base pintada de branco.

## Fase 2 · Rancho (reconstrução)

Cena atual: `Scenes/Levels/farm_level.tscn` (`WorldBiome.Farm`).

- Estradas de terra vermelha em circuito, ligando os sítios.
- Sítios com casa, varanda e bichos (`FarmAnimal`).
- *Packing house* de banana e palmito (galpão de embalar), ponto de carga para o entregador.
- Lago com pesqueiro.
- Sinalização rústica (placas de madeira pintadas à mão).

## Fase 3 · O Bananal — `Scenes/Levels/Bananal.tscn`

- Plantação fechada de bananeiras onde é fácil se perder.
- Navegação pelo **sinal da 96.6**: quanto mais perto da antena, mais limpa a música; longe, mais chiado.
  Modelo pronto: [`templates/RadioBeacon.cs.txt`](../templates/RadioBeacon.cs.txt).
- Bananeiras em `MultiMeshInstance3D` (são milhares).

## Fase 4 · Gruta da Estática — `Scenes/Levels/Gruta.tscn`

- Caverna de calcário montada com salas modulares encaixadas (geração procedural com semente fixa).
  Modelo pronto: [`templates/CaveGenerator.cs.txt`](../templates/CaveGenerator.cs.txt).
- Escura de verdade: lanterna e visão noturna (item que já existe, tecla N).
- A sala final tem a repetidora de rádio.

## Fase 5 · Estrada da Serra — `Scenes/Levels/Serra.tscn`

- Subida sinuosa com neblina densa.
- Teste de integridade da carga: curvas e buracos danificam as caixas na caçamba (`CargoItem`).

## Ideias de polimento (backlog opcional)

Entram como módulos pequenos, sem atrasar as fases:

- Praça da Matriz à noite com barracas de pastel e lanche; carro de som anunciando eventos.
- Festivais sazonais na praça (ex.: Festa da Colheita, Bon Odori).
- Travessia de balsa no rio.
- Caramelo na praça (o caramelo companheiro já existe desde a v0.9.5).
