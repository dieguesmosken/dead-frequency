# Gerar as vozes com o Amazon Polly

O script `gerar_audios.py` lê as planilhas de roteiro (`docs/radio/*.csv`) e cria os `.ogg` no lugar certo do
jogo. A rádio e o carro de som usam `Audio/Radio/Ads/`, e o carro do ovo usa `Audio/Street/`. O jogo toca
sozinho quando o arquivo existe.

## Custo

- A conta nova da AWS fica no **plano gratuito**. Nele não há cobrança enquanto você **não mudar para o plano pago**.
- Os créditos valem para o Polly e duram até 6 meses a partir da criação da conta.
- A leva atual tem uns 5,6 mil caracteres, o que dá centavos de dólar mesmo na voz generativa.
- O script soma tudo antes de começar e mostra o máximo que pode gastar. Rode primeiro com `--simular`.
- Os áudios ficam no projeto (vão para o GitHub). Nada fica guardado na AWS.

## Uma vez só: preparar o PC

1. Instale o **Python 3** (python.org) e rode `pip install boto3`.
2. Opcional, mas recomendado: instale o **ffmpeg** (`winget install ffmpeg`). Com ele, o áudio sai no mesmo
   volume das músicas da rádio.
3. Instale o **AWS CLI** (`winget install Amazon.AWSCLI`).

## Uma vez só: criar um acesso só para gerar voz

Não use o login principal (root) da conta. Crie um usuário que só consegue gerar voz:

1. No console da AWS, abra **IAM → Usuários → Criar usuário**, com o nome `dead-frequency-tts`. Não precisa
   de acesso ao console.
2. Em permissões, escolha **Anexar políticas diretamente → Criar política → JSON** e cole o conteúdo de
   `tools/tts/politica-iam-polly.json`. Salve com o nome `PollySoGerarVoz` e anexe ao usuário.
3. Abra o usuário, vá em **Credenciais de segurança → Criar chave de acesso → Interface de linha de comando
   (CLI)** e copie a chave e o segredo.
4. No PC, rode `aws configure --profile dead-frequency-tts`, cole a chave e o segredo e use a região
   `us-east-1`.

A chave fica só no seu PC (pasta `.aws` do seu usuário). Nunca coloque a chave no repositório nem cole no chat.

## Gerar

Na pasta do projeto:

```
python tools/tts/gerar_audios.py --perfil dead-frequency-tts --simular
python tools/tts/gerar_audios.py --perfil dead-frequency-tts
```

- O script pula o que já existe. `--refazer` gera de novo, e `--so ad_rabibs ovo_01` gera só esses.
- Depois, abra o projeto na Godot uma vez para importar os arquivos.

## Vozes

| Locutor (planilha) | Voz do Polly | Observação |
|---|---|---|
| `waldir` (Seu Waldir) | Leo, generativa, 92% | Se a generativa falhar, usa Thiago neural |
| `katia` (Kátia Varejão) | Camila, generativa, 108% | |
| `prefeitura` | Vitória, neural, 100% | |
| `toninho` (Toninho Gozação) | Thiago, neural, 104% | |
| `ovo` (carro do ovo) | Leo, generativa, 110% | |

Para trocar voz ou velocidade, edite `VOZES` no começo do script. Para trocar a pronúncia de um nome, edite
`PRONUNCIA`. Depois de ouvir, anote em `docs/ASSETS_E_SONS.md` que as vozes vieram do Amazon Polly.
