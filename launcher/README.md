# Launcher do Dead Frequency

Janela pequena (feita em Godot, só GDScript) que instala e mantém o jogo atualizado:

1. **Primeira vez:** pergunta a pasta de instalação (cria a pasta) e se você quer atualizações automáticas.
2. Confere o último release em [github.com/dieguesmosken/dead-frequency/releases](https://github.com/dieguesmosken/dead-frequency/releases).
3. **Jogo não instalado:** pergunta se quer baixar agora. **Versão nova:** baixa sozinho (ou pergunta, se as automáticas estiverem desligadas).
4. Barra de progresso no download (MB e velocidade) e na instalação, depois o botão **JOGAR**.
5. A **engrenagem** troca a pasta, liga/desliga as automáticas, abre a pasta, verifica agora e reinstala.
6. **Pré-releases (instável):** na engrenagem, "Receber pré-releases" passa a baixar as builds `vX.Y.Z.N-pre` da `Internal`. Antes de ligar aparece um aviso de que são instáveis e não testadas; ao desligar, o launcher volta para a versão estável mais nova.

Configuração em `user://launcher.cfg` (no Windows: `%APPDATA%\Godot\app_userdata\Dead Frequency Launcher`).
A versão instalada fica em `<pasta>/version.txt`. Os saves do jogo ficam na pasta de usuário do jogo, então
atualizar ou reinstalar não apaga nada. Quem usava o launcher antigo (pasta `game/` ao lado dele) continua com a mesma pasta.

## Código

O projeto do launcher fica em `launcher/app/` (`launcher.gd` monta a interface toda em código).
A pasta `launcher/` tem `.gdignore` para o projeto do jogo não enxergar o launcher.

## Compilar

Precisa do Godot 4.7.2 **padrão** (sem .NET) e dos templates de exportação:

```bash
godot --headless --path launcher/app --export-release "Windows Desktop" ../../out/DeadFrequencyLauncher.exe
godot --headless --path launcher/app --export-release "Linux" ../../out/DeadFrequencyLauncher.x86_64
```

O GitHub Actions (`.github/workflows/release.yml`) já faz isso e anexa os dois no release.
