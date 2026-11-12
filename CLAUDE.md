# Dead Frequency — contexto do projeto

**Engine:** Godot 4.7.2 .NET (Windows x86_64, C# / .NET 10)
**Linguagem:** C# para toda a lógica de gameplay + GDShader para efeitos PS1/CRT
**Estética:** PS1/PSX low-poly (320x240 / 640x480, nearest-neighbor, vertex jiggle, dithering, fog denso)
**Gênero:** simulador de entrega e sobrevivência em 1ª pessoa, com UI diegética e física tátil

## Visão
Entregador solitário dirigindo uma van utilitária anos 90 por uma zona perigosa.
- Sem HUD tradicional: a informação vem do painel físico da van (ponteiros, luzes, monitor CRT verde).
- Caixas são `RigidBody3D` carregadas na mão, empilhadas na caçamba, que sofrem dano por impacto.
- A van consome bateria, queima fusíveis físicos e sofre interferência eletromagnética da carga.

## Regras de código
- Sempre `public partial class X : NodeType`; `[Export]` para o Inspector; `[GlobalClass]` em Resources.
- `GetNode<T>()` no `_Ready()`, com referências guardadas em campos privados. Nunca `GetParent().GetParent()`.
- `Vector3`, `Transform3D`, `Basis` e `Color` são structs: copie, altere e reatribua.
- Física em `_PhysicsProcess(double delta)` com `float dt = (float)delta;`.
- Interação via interface `IInteractable` (`collider is IInteractable`), sem checar grupos por string.
- "Call down, signal up": `[Signal]` ou `event Action<T>` para falar com quem está acima.
- Dados de design ficam em `Resource` `.tres`. Save, manifesto e telemetria ficam em `.tzn` (JSON via `System.Text.Json` + `FileAccess`). `ResourceSaver` não aceita extensões customizadas.

## Collision layers
1 World · 2 Player · 3 Interactables · 4 Cargo · 5 Vehicle

## Estrutura
- `Scripts/Core` interfaces e utilitários · `Scripts/Player` · `Scripts/Cargo` · `Scripts/Vehicle` · `Scripts/UI`
- UI montada em código com o tema de `Scripts/Core/UiStyle.cs` (verde fósforo CRT)
- `Scenes/...` cenas · `Data/Cargo/*.tres` catálogo de cargas

## Roadmap
1. ✅ Movimento, interação e pegar/arremessar caixas (IInteractable, CargoData, CargoItem, PlayerController)
2. ✅ VanController (VehicleBody3D) + DriverSeatInteractable, entrar/sair com tween
   ✅ UI: menu principal, pausa (Esc/P), opções salvas em user://settings.cfg (autoload GameSettings), HUD de protótipo (F3 = debug)
   ✅ Cidade procedural (Scripts/World/CityGenerator.cs + Shaders/), noite com neblina, depósito no quarteirão (0,0)
   ✅ Loop de entregas (Scripts/Gameplay/DeliveryManager.cs): feixe verde no destino, pagamento pela integridade, turnos
   ✅ Áudio: autoload AudioManager, buses Music/SFX/UI, trilha e efeitos sintetizados (Audio/synth.py.txt)
   ✅ Multiplayer: servidor em server/ (Cloudflare Worker + Durable Object por sala, código de 5 letras);
      cliente em Scripts/Net (NetClient autoload, NetSync na fase, RemotePlayer). Servidor manda nas regras,
      física roda no dono de cada objeto. CARGO_CATALOG do servidor = DeliveryManager.CargoCatalog (mesma ordem).
   ✅ Itens: ItemCatalog + PlayerInventory (autoload, salvo em user://inventory.json), InventoryScreen (I/Tab),
      ShopScreen + StreetVendor (barracas), radar/minimapa, visão noturna (N), luvas, mochila, consumíveis.
      Preços = SHOP_PRICES no servidor. Game over quando todas as caixas do turno quebram.
   ✅ Cidade: CarModels (4 tipos), placas PARE/faixa, BusLine (ônibus + pontos, posição pelo relógio), Pedestrians.
   ✅ v0.9 Cidade simulada: profissões (Scripts/Jobs: ProfessionManager + JobBase; regras = PROFESSIONS no servidor
      e LocalServer.ProfessionRules), Rádio 96.6 (Scripts/Audio/RadioStation/RadioPlayer, Audio/Radio/*.ogg),
      mercado com interior, Edifício Sintonia (apartamentos 60 m abaixo, teleporte), SaveManager (user://save.tzn),
      caminhonete estilo Bongo (VanDetails), launcher em Godot (launcher/app, publicado pelo CI).
   ✅ v0.9.3: moto (baú) e caminhões drivable (VehicleFactory; NetIds = VEHICLES no servidor), HomeGuide (casa),
      semáforo de pedestre, CarModels com estufa fechada. Protocolo 5.
   ✅ v0.9.4: assets Quaternius (CC0) em Assets/Quaternius: CharacterModel usa Man/Woman Animated (paleta 32x32
      gerada em código), FarmAnimal (bichos animados), NatureKit (árvores/mato via MultiMesh), prédios da fazenda,
      viaturas. Orelhões por operadora/cor (PhoneBooth.Pick) e Telefóbica desativada. Debug F3 completo.
      Pasta de assets do usuário: C:\Users\teuzin\Documents\Projetos Godot\Assets Download (zips; perguntar antes de pedir novos).
   ✅ v0.9.5 Calçada viva: PlayerController.Needs.cs (fôlego, leseira, comidas, chinelo), slot Feet, itens novos
      (SHOP_PRICES no servidor), FarmAnimal caramelo companheiro (NearestDog/FollowPlayer), Mosquitoes no sítio,
      SidewalkTalk (balões de fala, CharacterModel.Chatty), TaxiJob com humores, vazamento do botijão (CargoItem),
      StreetVendor.Churrasco. Planejamento v0.9.6/v0.9.7 no ROADMAP.md.
   ✅ v0.9.6 Oficina: CharacterLook + CharacterPanel (aparência no menu, campo "lk" no tick), PlayerState.Riding
      (carona: VanController.PassengerSeat), st=2 no servidor, câmera estável no carro, Fusca via CarModels.Loft,
      muro+serra no sítio, ícone (Assets/Textures/icon.py.txt) e UiStyle.AlphaBadge.
   🚧 v0.9.7 (em andamento): texturas da cidade (Assets/Textures/City/city_textures.py.txt), GrassTufts (capim),
      CityGenerator.OptimizeVisibility, ShopStock (abas por vendedor), concessionária (CityGenerator.Dealer + CarGarage +
      VehicleFactory.BuildCar; carros = CARS/VEHICLES/SHOP_PRICES no servidor, room.cars), ForbidParking.
3. Painel diegético: DashboardButton, ElectricalSystem, EngineSystem, fusíveis
4. CargoBayManager + SaveManager (.tzn)
5. Shaders PS1 + monitor CRT

## Versões e GitHub
- Repositório: https://github.com/dieguesmosken/dead-frequency (público).
- A cada atualização: subir `GameVersion.Current`, escrever a entrada em `GameVersion.Changelog`,
  regenerar `CHANGELOG.md`, fazer um commit "vX.Y.Z — título" e criar a tag `vX.Y.Z`.
- Site: `site/` (GitHub Pages via .github/workflows/pages.yml). `site/build.py` gera data/site.json do GameVersion.cs;
  fotos de cada versão em `site/assets/shots/vX.Y.Z/` + captions.json; notas de release = `site/release_notes.py`.
  Servidor tem `/api/stats` público (só totais). Pix de apoio: contato@badwolf3d.com.br.
- Nunca versionar `server/.dev.vars` (ADMIN_TOKEN), `server/node_modules`, `.godot/` nem `_to_delete/`.


---

## Planejamento, Lore e Modo História
- Antes de implementar missões, novos cenários (`Bananal`, `Gruta`, `Serra`), Inverse Kinematics ou transições de capítulo, leia obrigatoriamente `docs/dead-frequency-docs/README_PARA_IAS.md`.
- **Tom do jogo:** Simulador de vida cotidiana e aventura leve estilo PS1 / anos 90–2000 (vale cercado por serra, mata preservada e minério que bloqueia internet/celular, mantendo a Rádio 96.6 FM como elo da comunidade). **NUNCA** adicionar elementos de terror, monstros ou sobrenatural.

## Foco Atual (Controle de Escopo)
- **Prioridade imediata:** Detalhamento da **Fase 1 (Cidade)** e reconstrução da **Fase 2 (Rancho)**.
- **Cenas isoladas:** As novas fases (`Bananal.tscn`, `Gruta.tscn`, `Serra.tscn`) devem ser criadas como cenas fechadas e independentes, sem alterar o fluxo principal do jogo até que estejam prontas para serem conectadas via corredores de transição na estrada (`ChapterTransitionZone`).

## Convenções Técnicas (Godot 4.7 · .NET 10 / C#)
- **Inverse Kinematics (IK):** Utilizar apenas os nós modernos de `SkeletonModifier3D` (`TwoBoneIK3D`, `LookAtModifier3D`, `FABRIK3D`). **Nunca** utilizar `SkeletonIK3D` (deprecated na Godot 4).
- **Otimização:** Agrupar vegetação (bananeiras, capim, árvores), cercas e postes repetitivos em `MultiMeshInstance3D` e utilizar `OccluderInstance3D` nas áreas densas.
- **Desacoplamento:** O Modo História (`StoryManager.cs`) deve escutar eventos das profissões (`JobEvents.cs`) sem acoplar lógica narrativa dentro dos scripts de trabalho.
- **Multiplayer:** Manter a arquitetura atual em `server/` (Cloudflare Workers + Durable Objects + Cloudflare Tunnel). O **Colyseus** está planejado apenas para testes futuros pós-v1.0 na Oracle Cloud Free Tier (via pacote NuGet `Colyseus` no `DeadFrequency.csproj`, nunca plugin GDExtension).

## Checklist ao Fechar uma Release
1. Atualizar a versão em `Scripts/GameVersion.cs` (sem sufixo interno para releases públicos).
2. Documentar as mudanças em `CHANGELOG.md` e atualizar o status em `ROADMAP.md`.
3. Adicionar screenshots em `site/assets/shots/vX.Y.Z/` com legendas em `captions.json` e rodar `python3 site/build.py` para atualizar `site/data/site.json`.
4. Conferir build do projeto (`DeadFrequency.csproj`) e compatibilidade com o `launcher/` antes do push/tag.
