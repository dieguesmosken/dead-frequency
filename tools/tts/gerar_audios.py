#!/usr/bin/env python3
"""
Gera os áudios falados do Dead Frequency com o Amazon Polly (AWS).

Lê as planilhas de roteiro e cria um .ogg por linha, no caminho da coluna "arquivo":
  - docs/radio/anuncios_966.csv  -> Audio/Radio/Ads/<id>.ogg  (Rádio 96.6 e carro de som)
  - docs/radio/carros_de_rua.csv -> Audio/Street/<id>.ogg     (carro do ovo)

Uso (no PC, na pasta do projeto):
  python tools/tts/gerar_audios.py --simular          # só mostra o que vai gerar e quantos caracteres (não chama a AWS)
  python tools/tts/gerar_audios.py                    # gera o que ainda não existe
  python tools/tts/gerar_audios.py --so ad_rabibs     # gera só um (ou vários: --so ad_rabibs ovo_01)
  python tools/tts/gerar_audios.py --refazer          # gera de novo mesmo o que já existe
  python tools/tts/gerar_audios.py --perfil dead-frequency-tts   # usa um perfil do "aws configure"

Precisa de: Python 3.10+, `pip install boto3` e as credenciais da AWS configuradas no PC
(passo a passo em tools/tts/LEIA-ME.md). O ffmpeg é opcional: se estiver instalado, o áudio sai em mono,
44,1 kHz e no mesmo volume das músicas (-16 LUFS); sem ele, fica o .ogg direto do Polly.

Custo: o script soma os caracteres antes de começar e para se passar de --limite (padrão 60 mil).
Toda a leva atual dá uns 5,6 mil caracteres: centavos de dólar dos créditos, mesmo na voz generativa.
"""
from __future__ import annotations

import argparse
import csv
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from xml.sax.saxutils import escape

RAIZ = Path(__file__).resolve().parents[2]
PLANILHAS = [RAIZ / "docs/radio/anuncios_966.csv", RAIZ / "docs/radio/carros_de_rua.csv"]

# Locutor da planilha -> (voz, motor preferido, velocidade em %, tom em semitons). Se o motor falhar, tenta o "neural".
# Vozes pt-BR do Polly: generativas Camila, Carolina, Leo (as mais naturais); neurais Camila, Vitória, Thiago;
# padrão Ricardo. Todo mundo usa o motor generativo. Ele aceita só velocidade e volume no <prosody>, sem "pitch";
# então o tom de cada personagem é ajustado depois, no ffmpeg (Leo mais grave para o Seu Waldir, mais agudo
# para o Toninho), e o mesmo Leo vira três locutores diferentes.
VOZES = {
    "waldir": ("Leo", "generative", 92, -1.5),       # locutor veterano, grave e pausado
    "katia": ("Camila", "generative", 108, 0.0),     # varejo animado
    "prefeitura": ("Carolina", "generative", 98, 0.0),  # aviso oficial, voz diferente da Kátia
    "toninho": ("Leo", "generative", 106, 1.5),      # humor, mais leve
    "ovo": ("Leo", "generative", 110, 0.5),          # carro do ovo, empolgado
    "locutor": ("Leo", "generative", 100, -0.5),     # locutor genérico da rádio
}
RESERVA = {"Leo": "Thiago", "Carolina": "Vitoria"}  # vozes generativas sem versão neural

# Planilhas tocadas no alto-falante de um carro: ganham o filtro de "corneta" (sem graves e sem agudos).
MEGAFONE = {"carros_de_rua.csv"}
FILTRO_MEGAFONE = "highpass=f=350,lowpass=f=3400,acompressor=threshold=-18dB:ratio=4:attack=5:release=80"
TAXA_POLLY = 24000  # Hz do áudio que o Polly devolve (o máximo das vozes generativas)

# Como falar alguns nomes (docs/radio/ANUNCIOS_966.md, "Dicas de pronúncia").
PRONUNCIA = {
    "96.6": "noventa e seis ponto seis",
    "Rabib's": "Rabibs",
    "Shello": "Xélo",
    "R$ 0,89": "oitenta e nove centavos",
    "R$ 5": "cinco reais",
}

PRECO_POR_MILHAO = {"standard": 4.0, "neural": 16.0, "generative": 30.0}


def ler_roteiros() -> list[dict]:
    linhas = []
    for planilha in PLANILHAS:
        if not planilha.exists():
            continue
        with planilha.open(encoding="utf-8", newline="") as f:
            for row in csv.DictReader(f):
                if row.get("id") and row.get("texto") and row.get("arquivo"):
                    row["_megafone"] = planilha.name in MEGAFONE
                    linhas.append(row)
    return linhas


def preparar_texto(texto: str) -> str:
    for escrito, falado in PRONUNCIA.items():
        texto = texto.replace(escrito, falado)
    return texto


def ssml(texto: str, velocidade: int) -> str:
    # "..." do roteiro vira uma pausa curta; a velocidade vai no <prosody>.
    corpo = escape(texto).replace("...", '<break time="350ms"/>')
    if velocidade != 100:
        corpo = f'<prosody rate="{velocidade}%">{corpo}</prosody>'
    return f"<speak>{corpo}</speak>"


def sintetizar(polly, texto: str, voz: str, motor: str, velocidade: int) -> bytes:
    from botocore.exceptions import ClientError

    tentativas = [(voz, motor)]
    if motor != "neural":
        tentativas.append((RESERVA.get(voz, voz), "neural"))
    erro = None
    for v, m in tentativas:
        for usar_ssml in (True, False):
            try:
                args = dict(OutputFormat="ogg_vorbis", VoiceId=v, Engine=m, LanguageCode="pt-BR", SampleRate="24000")
                if usar_ssml:
                    args.update(Text=ssml(texto, velocidade), TextType="ssml")
                else:
                    args.update(Text=texto.replace("...", ","), TextType="text")
                r = polly.synthesize_speech(**args)
                if (v, m) != (voz, motor):
                    print(f"    (usou {v}/{m} no lugar de {voz}/{motor})")
                return r["AudioStream"].read()
            except ClientError as e:
                erro = e
                # Alguns motores não aceitam todas as marcas de SSML: tenta em texto puro antes de trocar de voz.
                continue
    raise RuntimeError(f"Polly recusou {voz}/{motor}: {erro}")


def filtros(semitons: float, megafone: bool) -> str:
    """Cadeia do ffmpeg: tom (sem mudar a duração), corneta opcional e volume igual ao das músicas."""
    partes = []
    if semitons:
        fator = 2 ** (semitons / 12)
        partes += [f"asetrate={TAXA_POLLY * fator:.0f}", f"aresample={TAXA_POLLY}", f"atempo={1 / fator:.4f}"]
    if megafone:
        partes.append(FILTRO_MEGAFONE)
    partes.append("loudnorm=I=-16:TP=-1.5:LRA=11")
    return ",".join(partes)


def finalizar(bruto: bytes, destino: Path, semitons: float = 0.0, megafone: bool = False) -> None:
    destino.parent.mkdir(parents=True, exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        destino.write_bytes(bruto)
        return
    with tempfile.TemporaryDirectory() as tmp:
        entrada = Path(tmp) / "polly.ogg"
        entrada.write_bytes(bruto)
        cmd = [ffmpeg, "-y", "-loglevel", "error", "-i", str(entrada),
               "-af", filtros(semitons, megafone), "-ac", "1", "-ar", "44100",
               "-c:a", "libvorbis", "-q:a", "5", str(destino)]
        if subprocess.run(cmd).returncode != 0:
            destino.write_bytes(bruto)  # se o ffmpeg falhar, fica o arquivo do Polly mesmo


def main() -> int:
    ap = argparse.ArgumentParser(description="Gera os áudios do Dead Frequency com o Amazon Polly.")
    ap.add_argument("--simular", action="store_true", help="não chama a AWS: só lista e soma os caracteres")
    ap.add_argument("--refazer", action="store_true", help="gera de novo mesmo os arquivos que já existem")
    ap.add_argument("--so", nargs="*", default=None, help="ids para gerar (ex.: ad_rabibs ovo_01)")
    ap.add_argument("--perfil", default=None, help="perfil do aws configure (padrão: o default)")
    ap.add_argument("--regiao", default="us-east-1", help="região da AWS (as vozes generativas estão na us-east-1)")
    ap.add_argument("--limite", type=int, default=60000, help="para antes de começar se passar deste total de caracteres")
    a = ap.parse_args()

    pendentes = []
    for row in ler_roteiros():
        if a.so and row["id"] not in a.so:
            continue
        destino = RAIZ / row["arquivo"]
        if destino.exists() and not a.refazer:
            continue
        locutor = row.get("locutor", "waldir").strip().lower()
        voz, motor, vel, tom = VOZES.get(locutor, VOZES["waldir"])
        pendentes.append((row, destino, voz, motor, vel, preparar_texto(row["texto"]), tom))

    total = sum(len(p[5]) for p in pendentes)
    custo = sum(len(p[5]) / 1e6 * PRECO_POR_MILHAO[p[3]] for p in pendentes)
    print(f"{len(pendentes)} áudio(s) para gerar · {total} caracteres · no máximo US$ {custo:.2f} dos créditos")
    for row, destino, voz, motor, vel, texto, tom in pendentes:
        extra = (f" tom {tom:+.1f}" if tom else "") + (" megafone" if row["_megafone"] else "")
        print(f"  {row['id']:<28} {voz}/{motor} {vel}%{extra}  -> {destino.relative_to(RAIZ)}")
    if not pendentes or a.simular:
        return 0
    if total > a.limite:
        print(f"Passou do limite de {a.limite} caracteres. Use --limite se for isso mesmo.")
        return 1

    try:
        import boto3
    except ImportError:
        print("Falta o boto3: rode  pip install boto3")
        return 1
    sessao = boto3.Session(profile_name=a.perfil, region_name=a.regiao)
    polly = sessao.client("polly")
    if not shutil.which("ffmpeg"):
        print("Aviso: ffmpeg não encontrado; os arquivos ficam sem normalizar o volume.")

    falhas = 0
    for i, (row, destino, voz, motor, vel, texto, tom) in enumerate(pendentes, 1):
        print(f"[{i}/{len(pendentes)}] {row['id']}")
        try:
            finalizar(sintetizar(polly, texto, voz, motor, vel), destino, tom, row["_megafone"])
        except Exception as e:  # segue para o próximo e mostra no fim
            falhas += 1
            print(f"    ERRO: {e}")
    print("Pronto." if not falhas else f"Terminou com {falhas} erro(s).")
    print("Abra o Godot uma vez para ele importar os .ogg novos.")
    return 1 if falhas else 0


if __name__ == "__main__":
    sys.exit(main())
