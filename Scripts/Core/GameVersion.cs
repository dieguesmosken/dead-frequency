// Scripts/Core/GameVersion.cs
// Versão do jogo, novidades (changelog) e créditos.
// Ao lançar uma atualização: suba Current e adicione a entrada no topo de Changelog.
// O menu principal mostra as novidades automaticamente uma vez a cada versão nova
// (compara com GameSettings.LastSeenVersion, salvo em user://settings.cfg).
namespace DeadFrequency;

public static class GameVersion
{
    public const string Current = "0.9.8";
    public const string Display = Current;

    public readonly record struct Release(string Version, string Date, string Title, string[] Changes);

    public static readonly Release[] Changelog =
    {
        new("0.9.8", "10/11/2026", "Rua de verdade", new[]
        {
            "Meio-fio branco nos trechos permitidos e amarelo nos cruzamentos e pontos proibidos; carros gerados só estacionam junto às marcações brancas.",
            "Placas de proibido estacionar nos trechos amarelos, com identificação de POLÍCIA e BOMBEIROS nas respectivas áreas.",
            "Céu procedural com nuvens em movimento, estrelas e transição de cores sincronizada ao ciclo de dia e noite.",
            "Bocas de lobo com grades, tampas de bueiro nas ruas e som de água corrente audível nas proximidades.",
            "Ciclovias pintadas e ciclista que percorre a cidade sincronizado pelo relógio; ele toca o sino quando há alguém à frente.",
            "Delegacia identificada na fachada e no minimapa, com policiais figurantes fardados.",
            "Roda do mouse alterna entre a câmera em primeira e em terceira pessoa; Ctrl + roda ajusta o campo de visão.",
            "Online: salas separadas por versão do protocolo (7 e 8), com aviso claro quando a sua versão não bate com a da sala. Atualize o servidor com npm run deploy.",
            "Correções: a caixa na mão de outro jogador não aparece mais duplicada e o boneco sentado reconhece mais animações.",
        }),
        new("0.9.7", "10/11/2026", "Mãos ocupadas", new[]
        {
            "Barra rápida 1–4: selecione um item para segurá-lo; clique esquerdo usa ou equipa.",
            "G larga itens no chão para qualquer jogador pegar; E entrega o item selecionado ao jogador mirado.",
            "No multiplayer, os itens na mão e no chão são sincronizados e validados pelo servidor.",
            "O Zé recompra itens por metade do preço, arredondado para baixo; a transação é validada pelo servidor.",
            "Chat da sala na tecla T, com limite de três mensagens por jogador a cada cinco segundos.",
            "Salas permanentes: ADMNA e ADMNB são privadas; SERVA e SERVB são públicas e não expiram nem podem ser encerradas.",
            "Lojas organizadas por vendedor e Auto Center Dois Mil com carros compráveis, compartilhados pela sala no online.",
            "Cidade com novas texturas, capim animado e otimizações de visibilidade.",
            "Online: protocolo 7 (hotfix de segurança multiplayer). Atualize o servidor com npm run deploy.",
        }),
        new("0.9.6", "08/11/2026", "Oficina", new[]
        {
            "Personagem: novo menu para escolher homem ou mulher e as cores da pele, do cabelo, da camisa e da calça. No online todo mundo vê você do seu jeito.",
            "Online: quem está dirigindo agora aparece sentado no banco para os outros (antes sumia).",
            "Carona: dá para entrar como passageiro na van (porta da esquerda), nos caminhões e na garupa da moto. Se outro jogador está dirigindo, a porta do motorista vira carona.",
            "Dirigir sem enjoo: câmera estabilizada (o horizonte não balança com a carroceria), suspensão mais firme e menos rolagem nas curvas. Dá para desligar em Opções → Jogo.",
            "O volante da van ganhou raios: agora dá para ver ele girando nas curvas. A cabine ganhou chão.",
            "Fuska refeito com carroceria arredondada de verdade e os vidros no lugar.",
            "Viatura com uma sirene só (estava duplicada) e botijão novo do Gás do Zé (costura, pé, colarinho e regulador).",
            "O fim do sítio deixou de ser parede invisível: muro de pedra com pilares e a serra atrás.",
            "Ícone novo do jogo e do launcher, e a etiqueta amarela ALPHA ao lado da versão.",
            "Online: atualize o servidor (npm run deploy) para a aparência e a carona aparecerem para os outros.",
        }),
        new("0.9.5", "08/11/2026", "Calçada viva", new[]
        {
            "Fôlego: correr cansa (barra no HUD). Esgotou, só volta a correr com um quarto do fôlego.",
            "Leseira da madrugada: entre meia-noite e 5h o sono vem, a tela escurece nas bordas e você anda mais devagar.",
            "Comidas e bebidas: Guaraná Pinguim (fôlego de volta), Café Marreta (corta o sono por 4 min), Marmitex Tia da Quentinha (fôlego cheio, mas 10 s de lerdeza pós-almoço) e Espetinho do Duvidoso.",
            "Chinelos Legítimas no novo slot de pés: 12% mais rápido com o ploc-ploc nos passos. Correndo na lama do sítio a tira pode arrebentar; o Prego conserta.",
            "Caramelos de rua pela cidade. Dê um espetinho e ele vira seu companheiro por 3 minutos, latindo e espantando os pernilongos do sítio à noite.",
            "Diálogos de calçada: pedestres, comerciantes e caixas falam com você quando passa perto (e discutem se o que você carrega é biscoito ou bolacha).",
            "Taxista: passageiros exigentes. O executivo quer a 96.6 ligada, sem passar de 60 km/h e sem batida; o apressado paga bônus se chegar rápido.",
            "O botijão de gás vaza se for arremessado ou cair forte: chiado, gás saindo e a entrega valendo menos.",
            "Churrasquinho do Duvidoso perto do depósito: depois das 18h acende a chapa, solta fumaça e junta gente. Vende espetinho, guaraná, café e marmitex.",
            "Opção para desligar a busca por atualizações (Opções → Jogo) e licença GPL-3.0 nos créditos.",
            "Online: atualize o servidor (npm run deploy) para comprar os itens novos no modo online.",
        }),
        new("0.9.4", "08/11/2026", "Gente de verdade", new[]
        {
            "Jogadores, pedestres, comerciantes e passageiros agora são personagens animados (homens e mulheres da Quaternius) que andam, correm e ficam parados de verdade, cada um com roupa, pele e cabelo próprios.",
            "Os equipamentos (visão noturna, luvas, mochila e radar) acompanham a cabeça, as mãos e as costas do personagem.",
            "Sítio com bichos animados: vacas, bois, porcos e ovelhas passeando no cercado, cachorro caramelo na varanda e cavalo ou jegue do lado da casa.",
            "Celeiros, silos e cataventos novos (com as pás girando), árvores, pinheiros, árvores secas, capim, samambaias, flores e pedras no sítio; árvores novas nas praças da cidade.",
            "Viaturas novas na delegacia.",
            "Orelhões por operadora: tchau ciano (e uns roxos, mais raros), morto roxo, laranja ou verde, e os raríssimos da Telefóbica (verde-limão com poste azul), todos desativados.",
            "No sítio agora só há dois orelhões: um que funciona e um velho da Telefóbica, desativado, na frente da capela.",
            "Debug (F3) completo: posição, quarteirão e ruas mais próximas, direção, FPS e tempo de quadro, RAM, VRAM, uso de CPU, placa de vídeo, draw calls e objetos na tela.",
        }),
        new("0.9.3", "08/11/2026", "Frota do pátio", new[]
        {
            "A moto de entrega do pátio agora anda: guarde uma caixa de até 20 kg no baú (E) e entregue de moto.",
            "Os caminhões do pátio podem ser emprestados para ir para casa ou passear.",
            "Endereço de casa no inventário (o apartamento que você ganha ao entrar) e o botão \"Marcar casa\": a seta do topo leva até lá.",
            "Frentista: o carro vem lá do fim da avenida, entra no posto pela rampa, para na bomba e, depois de abastecer, faz a volta por trás das bombas e vai embora pela outra saída.",
            "Postos com calçada rebaixada (rampas de ENTRADA e SAÍDA, meio-fio pintado) e logos paródia da Petrobraz, Ipiranguinha e Shello na cobertura.",
            "Carros refeitos, inspirados nos brasileiros dos anos 80/90 e com logo paródia: Opalinha (Chevrolé), Gou e Fuska (Folksvagem), F-Mil (Fórdi) e Kombe saia-e-blusa. Os vidros agora são fechados, com colunas.",
            "Semáforo de pedestre nas esquinas no lugar da placa: fica vermelho quando o ônibus, a van, um caminhão ou um carro vem passando.",
            "Estacionamentos com faixas amarelas entre as vagas, vaga PCD azul e corredor tracejado; os carros param dentro das vagas.",
            "Orelhões mais baixos, com o telefone no meio da concha (não atravessa mais) e o teclado com números e visor.",
            "Online: protocolo 5 (atualize o servidor com npm run deploy).",
        }),
        new("0.9.2", "08/11/2026", "Launcher e opções", new[]
        {
            "Launcher novo com janela: barra de progresso (MB e velocidade), botão JOGAR e engrenagem de configurações.",
            "Na primeira vez o launcher pergunta a pasta de instalação (e cria a pasta) e se você quer atualizações automáticas.",
            "Se o jogo não estiver instalado, ele pergunta se quer baixar; se sair versão nova, baixa sozinho ou pergunta antes (você escolhe).",
            "Opções com abas: Vídeo, Áudio, Controles, Interface e Jogo.",
            "Vídeo: modo de janela (janela, sem borda, exclusiva, maximizada), limite de FPS, resolução 3D, qualidade, antisserrilhado, campo de visão, brilho e contraste.",
            "Áudio: volume da rádio 96.6 e da interface separados, e silenciar quando trocar de janela.",
            "Controles: troque qualquer tecla (clique e aperte a nova), sensibilidade na van e balanço da câmera ao andar.",
            "Interface: escala da interface, mira, seta de objetivo, dicas de teclas, FPS, tempo dos avisos, linhas de CRT, km/h ou mph e relógio de 12/24 horas.",
            "Botão \"Restaurar padrão\" nas Opções.",
        }),
        new("0.9.1", "07/11/2026", "Orelhões de verdade", new[]
        {
            "Orelhões refeitos: concha oca de verdade (casca aberta na frente e embaixo, cinza por dentro, com borda), telefone com teclado lá dentro e o poste atrás.",
            "O logo aparece uma vez só, pintado na lateral da concha. O da morto agora é vertical; o da tchau tem a bolha amarela em volta do nome.",
            "Cada profissão começa no seu lugar de trabalho: locutor no estúdio da rádio, repositor na porta do Atacadinho, frentista no posto, gari na caçamba do depósito.",
            "A seta do topo da tela segue a profissão: rádio, mercado/estoque/prateleira, posto/carro, lixo/caçamba, passageiro/destino/van (entregador continua no depósito).",
            "Menu de pausa online mostra quem está na sala, com a cor, a profissão e quem é o anfitrião.",
            "Multiplayer: botão \"Salas online\" lista as salas abertas no servidor (com fase e jogadores) e entra com um clique.",
            "Tela de criar sala mais limpa: apelido e profissão na mesma linha e a engrenagem \"Sala\" com fase, profissões sorteadas e sala pública/privada. O botão Voltar não fica mais cortado.",
            "Mercado: os banners de oferta ficam entre as colunas da vitrine (não são mais cortados), letreiro acima da marquise e carrinhos de supermercado de verdade (cesto aramado, alça, cadeirinha e rodízios).",
            "Online: atualize o servidor com npm run deploy para a lista de salas (o protocolo continua 4).",
        }),
        new("0.9.0", "07/11/2026", "Cidade simulada", new[]
        {
            "Profissões! Escolha ao começar (ou deixe a sala online sortear): Entregador, Taxista, Gari, Repositor, Frentista e Locutor da 96.6, cada uma com as suas tarefas e pagamento.",
            "Taxista: pegue passageiros com a van e deixe no destino (corrida pela distância). Gari: recolha os sacos de lixo das calçadas e jogue nas caçambas.",
            "Repositor: reponha as prateleiras do Supermercado Atacadinho. Frentista: abasteça os carros que chegam no posto. Locutor: leia os comerciais no ar.",
            "Rádio Frequência 96.6 FM: estação com estúdio por dentro e torre de transmissão de 62 m. Cinco músicas originais, sem direitos autorais, tocando ao mesmo tempo para todo mundo.",
            "Rádio na caminhonete (tecla R), no escritório da empresa, no mercado, no saguão e no seu apartamento.",
            "Caminhonete nova: estilo Bongo dos anos 90, cabine avançada, carroceria de madeira e o rádio aceso no painel.",
            "Supermercado Atacadinho com interior: corredores, placas de seção, hortifrúti, padaria, frios, caixas com atendentes (E = comprar) e banners de oferta na porta.",
            "Edifício Sintonia: cada jogador ganha um apartamento. Dá para dormir e salvar o jogo, ver TV (jornal, clipe da 96.6, tempo) e passar o tempo de boas.",
            "Save do jogo na cama (user://save.tzn): profissão, turno, entregas e ganhos. O menu mostra o último save.",
            "Nomes das ruas no minimapa (cidade e rancho).",
            "Launcher: um programinha que baixa o jogo e instala as atualizações do GitHub sozinho. O menu também avisa quando sai versão nova.",
            "Outdoor \"siga twitch.tv/teuzin0dev · github.com/dieguesmosken\" e cartazes do recadin.com.br pela cidade.",
            "Correções: erro em série (ObjectDisposedException) ao jogar online depois de voltar ao menu; textos do quadro de avisos e do PC do escritório deslocados para a direita.",
            "Online: protocolo 4 (atualize o servidor com npm run deploy).",
        }),
        new("0.8.0", "07/11/2026", "Cidade comercial", new[]
        {
            "Bancos (Caixote Econômica, Etaú, Bravesco, Santo Ander), lotéricas, padarias e supermercados paródia, cada um com letreiro próprio.",
            "Postos Petrobraz, Ipiranguinha e Shello com cobertura, bombas, loja de conveniência e totem de preços; e o Lava-Jato Molhadinho com escovões girando.",
            "Caixa d'água municipal numa praça, com refletores à noite.",
            "Outdoors na beira da cidade, iluminados de baixo para cima à noite. Os anúncios mudam a cada partida.",
            "Ônibus da Viação Frequência, linha 966, com letreiro de LED e anúncios na lateral e na traseira.",
            "O ônibus buzina duas vezes quando você está na frente. Se não sair... é atropelado e voa longe (e larga a caixa).",
            "Pedestres desviam de postes, placas, orelhões, bancos, caçambas, pontos de ônibus e de você.",
            "Toldos das lojas inclinados para a calçada: agora dá para ler as faixas.",
            "Cada encomenda tem a sua cara: monitor de tubo, bateria, maleta, gerador, caixote de válvulas...",
            "Seis encomendas novas: botijão de gás, bolo de casamento, aquário com peixe, antena parabólica, caixote de tomates e panela de feijoada.",
            "Online: protocolo 3 (atualize o servidor com npm run deploy).",
        }),
        new("0.7.0", "07/11/2026", "Hospede no seu PC", new[]
        {
            "Multiplayer com três opções: Servidor oficial (padrão, sem precisar de endereço), Meu servidor e Hospedar no meu PC.",
            "Hospedar no meu PC: o jogo vira o servidor da sala e gera um link público grátis (Cloudflare Tunnel, tipo ngrok). Na primeira vez ele baixa o cloudflared oficial.",
            "Amigos na mesma rede também entram pelo IP do PC, sem túnel.",
            "\"Colar convite\" já preenche o código e o servidor do amigo.",
        }),
        new("0.6.2", "07/11/2026", "Letreiros e orelhões", new[]
        {
            "Lojas com letreiros de verdade: arte própria para cada uma das 12 paródias (ícone, nome e slogan), com moldura e luz à noite.",
            "Orelhões com o logo da tchau e da morto pintado na própria concha (atrás e dos lados), sem plaquinha em cima.",
        }),
        new("0.6.1", "07/11/2026", "Assets novos", new[]
        {
            "Modelos 3D gratuitos (CC0) da KayKit, de Kay Lousberg: mobília, cidade e Halloween.",
            "Escritório mobiliado: cadeira, sofá, mesinha com livros, tapete, abajur, prateleira, quadro e cacto na mesa.",
            "Cidade com bancos nas calçadas e caçambas de lixo (algumas com caixas de papelão do lado).",
            "Rancho: caixa d'água na cooperativa, Cemitério da Frequência ao lado da capela (com lanternas verdes), canteiros de abóbora, abóboras iluminadas nas varandas e árvores secas.",
        }),
        new("0.6.0", "07/11/2026", "Fase 2: Rancho", new[]
        {
            "Nova fase: o Rancho. Estradas de terra, sítios e fazendas para entregar, lago com pesqueiro, capela e mato fechado.",
            "Cada sítio tem casa com varanda, celeiro, silo, plantação de milho ou soja, curral com vacas ou porcos, trator e cata-vento girando.",
            "A Cooperativa Rancho Fundo é o depósito da roça (com o mesmo escritório e PC).",
            "Iniciar turno agora pergunta a fase. No multiplayer, quem cria a sala escolhe a fase e todos vão juntos.",
            "Orelhões das operadoras \"tchau\" (laranja) e \"morto\" (roxo), cada uma com suas ligações.",
            "O PC do escritório abre em tela cheia (E): relatório, histórico e pedidos abertos, fácil de ler.",
            "Minimapa desenha as estradas do rancho e marca as porteiras.",
        }),
        new("0.5.0", "07/11/2026", "Turno da madrugada", new[]
        {
            "Ciclo de dia e noite: o sol nasce no leste e se põe no oeste, a lua aparece à noite e a cidade acende aos poucos.",
            "Relógio no topo da tela. Em Opções: ciclo, sempre noite ou sempre dia, e a duração do dia.",
            "Música nova para o dia; à noite volta a trilha da frequência morta.",
            "A luz da lua vinha de baixo para cima; agora ilumina do céu para o chão.",
            "Depósito por dentro: prateleiras por setor, paletes com produtos, empilhadeira e carrinho.",
            "Escritório com PC de tubo mostrando caixa, ganhos, turno e histórico de entregas (E troca a página).",
            "Frota no pátio: caminhões e moto de entrega (modelos CC0 da Kenney).",
            "Van com para-choques, grade, piscas, retrovisores, placa DFQ-1996 e o nome da empresa nas portas.",
            "Carros com grade nova, piscas, retrovisores e maçanetas; a grade parou de piscar (z-fighting).",
            "Pneu cantando nas curvas e no freio de mão, e novos sons de batida (Kenney, CC0).",
            "Avisos do orelhão não ficam mais por trás do painel de pedido.",
            "Telas de Novidades e Créditos no menu.",
        }),
        new("0.4.0", "07/11/2026", "Cidade viva", new[]
        {
            "Orelhões com dicas da central (e ligações estranhas).",
            "Ar-condicionados nas fachadas, lojas paródia, corpo de bombeiros e delegacia.",
            "Placas de PARE corrigidas, pedestres andando para a frente e vidros dos carros acertados.",
        }),
        new("0.3.0", "07/11/2026", "Equipamento", new[]
        {
            "Inventário e mochila; radar com minimapa, visão noturna, luvas e consumíveis.",
            "Barracas de rua que vendem itens; seta apontando para o depósito.",
            "Tela de game over, sons de batida, ônibus com pontos, placas e faixas de pedestre.",
        }),
        new("0.2.0", "07/11/2026", "Online", new[]
        {
            "Multiplayer com salas por código no servidor da Cloudflare e painel /admin.",
            "Modo dia, passos novos e correção da van flutuando no online.",
        }),
        new("0.1.0", "07/11/2026", "Primeiro turno", new[]
        {
            "Andar, pegar e arremessar caixas, dirigir a van e entregar pela cidade gerada.",
            "Menu, pausa, opções, música e efeitos sintetizados.",
        }),
    };

    public readonly record struct Credit(string Section, string[] Lines);

    public static readonly Credit[] Credits =
    {
        new("Criação", new[]
        {
            "Matheus (teuzin0dev) — direção, design e testes",
            "Código, cidade procedural, servidor e áudio sintetizado feitos com o Claude (Anthropic)",
            "Código aberto sob a licença GNU GPL v3.0 · github.com/dieguesmosken/dead-frequency",
        }),
        new("Motor", new[]
        {
            "Godot Engine 4.7 (.NET) — licença MIT · godotengine.org",
            "Servidor multiplayer: Cloudflare Workers + Durable Objects",
            "Link público ao hospedar: cloudflared (Cloudflare Quick Tunnels)",
        }),
        new("Modelos 3D e sons de terceiros", new[]
        {
            "Kenney · www.kenney.nl — Starter Kit Racing (github.com/KenneyNL/Starter-Kit-Racing)",
            "   caminhões, moto, pneu cantando e batida — licença CC0 (domínio público)",
            "KayKit · Kay Lousberg (www.kaylousberg.com) — City Builder Bits, Furniture Bits, Halloween Bits e Restaurant Bits",
            "Quaternius · quaternius.com — Man/Woman Animated, Ultimate Animated Animals, Farm Animals, Farm Buildings, Stylized Nature MegaKit, Realistic Car Pack e Public Transport Pack (CC0)",
            "   mobília do escritório e dos apartamentos, bancos, caçambas, caixa d'água, cemitério, abóboras, caixotes do hortifrúti e panela — licença CC0",
            "Operadoras \"tchau\" e \"morto\": paródias, sem relação com empresas reais.",
        }),
        new("Áudio original", new[]
        {
            "Músicas do menu, da noite e do dia, ambiente da cidade e efeitos de interface:",
            "   sintetizados por código (Python + NumPy), ver Audio/synth*.py.txt",
            "Rádio Frequência 96.6: \"Frequência Morta\" (Os Sintonizados), \"Bossa da Madrugada\" (Trio Estática),",
            "   \"Asfalto Molhado\" (Lo-Fi do Viaduto), \"Forró do Entregador\" (Zé da Van e Banda) e \"Neon na Rodovia\" (Cartucho 16)",
            "   — bandas fictícias; músicas originais geradas por código (Audio/synth_radio.py.txt), livres de direitos autorais",
        }),
        new("Paródias", new[]
        {
            "Os nomes de lojas são paródias e não têm relação com marcas reais.",
        }),
        new("Agradecimentos — Testadores Alpha", new[]
        {
            "Agradecemos a todos que ajudam a testar e melhorar Dead Frequency!",
            "",
            "CassolendaBR — Testador Alpha",
            "Twitch: twitch.tv/cassolendabr",
            "",
        }),
        new("Apoie o projeto", new[]
        {
            "twitch.tv/teuzin0dev · github.com/dieguesmosken · recadin.com.br",
        }),
        new("Obrigado por jogar", new[]
        {
            "Sintonize 96.6 e boa entrega.",
        }),
    };

    /// <summary>Texto BBCode das novidades. onlyNewerThan = mostra só o que veio depois dessa versão.</summary>
    public static string ChangelogBbcode(string onlyNewerThan = null)
    {
        var sb = new System.Text.StringBuilder();
        foreach (Release r in Changelog)
        {
            if (!string.IsNullOrEmpty(onlyNewerThan) && Compare(r.Version, onlyNewerThan) <= 0)
                continue;
            sb.Append($"[color=#8cff9e][b]v{r.Version} — {r.Title}[/b][/color]  [color=#478c54]{r.Date}[/color]\n");
            foreach (string c in r.Changes)
                sb.Append($" • {c}\n");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static string CreditsBbcode()
    {
        var sb = new System.Text.StringBuilder();
        foreach (Credit c in Credits)
        {
            sb.Append($"[color=#ffb840][b]{c.Section.ToUpperInvariant()}[/b][/color]\n");
            foreach (string l in c.Lines)
                sb.Append(l).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Compara "0.5.0" com "0.4.2". Versões vazias contam como mais antigas.</summary>
    public static int Compare(string a, string b)
    {
        string[] pa = (a ?? "").Split('.'), pb = (b ?? "").Split('.');
        for (int i = 0; i < 3; i++)
        {
            int x = i < pa.Length && int.TryParse(pa[i], out int vx) ? vx : 0;
            int y = i < pb.Length && int.TryParse(pb[i], out int vy) ? vy : 0;
            if (x != y)
                return x.CompareTo(y);
        }
        return 0;
    }
}
