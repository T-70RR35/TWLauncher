# Histórico de alterações — TW Launcher

> **Documentação consolidada:** nomes como `ALTERACOES_v*.md` e `QUALITY_CHECKS*.md` abaixo são referências históricas ao conteúdo incorporado neste mesmo documento, não arquivos necessários ao projeto.


Histórico consolidado a partir dos documentos `ALTERACOES_v*.md`, relatórios de validação e documentação das versões anteriores. As entradas descrevem **mudanças no código-fonte**, não homologação ou publicação de instaladores.

O projeto foi inicialmente chamado de **TV Launcher**; nas versões seguintes passou a usar a marca **TW Launcher**. A pasta local `%LOCALAPPDATA%\TVLauncher` e o namespace `TVLauncher.App` permaneceram por compatibilidade.

## 0.6.13 — Instalador e atualizações automáticas

- Integração do **Velopack 1.2.161**, incluindo inicialização antes de abrir as janelas WPF.
- Preparação de instalador Windows x64 self-contained, com WebView2 Runtime como dependência.
- Serviço de atualização: verificação ao iniciar e a cada **6 horas**, download em segundo plano, estado/progresso nas Configurações e ação manual.
- Aplicação de pacotes ao fechar normalmente ou por **Reiniciar e instalar**, sem interromper a sessão durante o uso.
- Script `Publicar-TWLauncher.ps1` e workflow do GitHub Actions para gerar o `Setup.exe` e publicar pacotes no GitHub Releases.
- Persistência de SQLite, chaves e perfil WebView2 fora da instalação; chaves pessoais não são empacotadas.
- Ícone com fundo transparente.
- **Pendente:** compilar no Windows, publicar um release real e validar instalação e atualização de ponta a ponta.

## 0.6.12 — Barra de navegação integrada ao Focus

- Cabeçalho da `StreamingWindow` redesenhado, com **58 px**, fundo grafite, tipografia neutra e botões minimalistas.
- Símbolo TW transparente na barra, com clima e relógio discretos.
- Barra continua totalmente oculta fora da região superior; navegação e sessão WebView2 preservadas.

## 0.6.11 — Focus

- Novo design-base aprovado: **menu lateral** para Início, Apps, Notícias, Clima e Ajustes.
- Cabeçalho compacto com pesquisa, relógio e clima; destaque cinematográfico sem moldura.
- Dock de aplicativos rolável; cartazes maiores e menos itens visíveis em cada fileira.
- Nenhuma alteração intencional na persistência, APIs ou regras do catálogo.

## 0.6.10 — Night Stream

- Home com barra superior, banner escuro arredondado e indicadores incorporados.
- Faixa de aplicativos, títulos populares e categorias reorganizados.
- Pesquisa local e expansão de fileiras; mantidos os dados reais do SQLite e as integrações existentes.
- Visual posteriormente substituído pelo Focus.

## 0.6.8 — Cinema Elegante

- Experimento de interface com banner cinematográfico contínuo e trailer integrado à arte.
- Botão **Detalhes**, aplicativos em atalhos horizontais e fileiras compactas.
- Identidade visual e ícone TW incorporados ao executável.
- Direção visual posteriormente substituída.

## 0.6.7.3 — Barra completamente invisível

- Removida a faixa visível de 6 px no topo do navegador.
- Barra revelada por uma região virtual invisível junto à borda superior, usando posição do cursor com correção de DPI.
- WebView2 ocupa 100% da altura quando o cabeçalho está recolhido; barra oculta em vídeo fullscreen.

## 0.6.7.2 — Barra retrátil do navegador

- Barra WebView2 recolhível com Início, Voltar, Avançar, Atualizar, nome do serviço, clima e relógio.
- Primeira implementação com uma faixa de ativação visível de 6 px; corrigida na 0.6.7.3.
- Mantido o atalho `Ctrl + Alt + Home`.

## 0.6.7.1 — Correção de compilação

- Em `TrendingCatalogService.EnrichExistingCatalogAsync`, corrigido o uso de `Count` para `Length` no array `pending`.
- Nenhuma mudança de comportamento de catálogo ou persistência.

## 0.6.7 — Seleção de streamings dos destaques

- Cada aplicativo Web pode ser vinculado a um provedor do **Watchmode Brasil**.
- **Fixar no Início** e **Usar nos destaques** passaram a ser opções independentes.
- Descoberta e filtragem de títulos somente pelos provedores selecionados pelo usuário.
- Colunas incrementais no SQLite para indicar inclusão e identificação do provedor, preservando registros existentes.
- Home continua abrindo normalmente quando nenhum serviço está selecionado.

## 0.6.6 — Gerenciamento de aplicativos

- Nova aba **Apps**, com cadastro, edição, remoção e fixação de sites e executáveis.
- Obtenção de favicon dos sites e extração de ícones de programas do Windows, quando disponíveis.
- Ícones normalizados visualmente e armazenados localmente; migração sem apagar atalhos antigos.

## 0.6.5 — Sinopses pt-BR e enriquecimento de gêneros

- Recuperação gradual de gêneros de títulos já armazenados, sem apagar o SQLite e sem esperar a sincronização completa.
- Sinopses em português disponibilizadas pelo TMDB; quando não há tradução, o aplicativo evita apresentar o texto em inglês como se fosse pt-BR.
- Persistência de `TmdbId` e idioma da sinopse, com limites de consultas e cooldowns para preservar a cota da API.

## 0.6.4 — Carrossel e categorias

- Fila de destaques embaralhada **uma vez a cada entrada na Home**; anterior/próximo seguem a sequência da fila.
- Avanço automático após **10 segundos**, interrompido quando o conjunto foi percorrido; atualização do catálogo não reordena os itens durante a visita.
- Fileiras por gênero (Ação, Aventura, Comédia, Romance, Ficção científica e outras), usando metadados reais.
- Coluna de gêneros adicionada de modo incremental ao SQLite.

## 0.6.3 — Trailer com áudio e limpeza de interface

- Trailer ativado por hover ou foco do teclado, com solicitação de áudio; saída da área remove o player.
- Alternativa clicável para som quando a política de autoplay do navegador bloquear áudio por hover.
- Remoção de textos auxiliares e explicações desnecessárias da Home.

## 0.6.2 — Identidade TW e ajustes dos destaques

- Nome e executável padronizados para **TW Launcher** / `TWLauncher.exe`, preservando caminhos antigos de dados.
- Ajustes nos indicadores, nas setas do destaque e nas barras de rolagem.
- Trailer apenas durante interação com banner/indicadores e retorno à imagem estática ao sair.

## 0.6.1 — Correção do destaque Blazor

- Corrigida colisão de chaves `@key` entre componentes irmãos no banner cinematográfico.

## 0.6.0 — Home premium

- Banner cinematográfico, backdrop e vinheta, tipografia adaptativa e trailer incorporado com layout responsivo.
- Mantida a abertura na Home mesmo sem credenciais de catálogo.

## 0.5.x — Configurações de API e evolução do banner

- Configuração de chaves via `api-keys.json`, com continuidade do SQLite e das preferências por dispositivo.
- Opções experimentais de compartilhamento doméstico documentadas em versões antigas; **a configuração recomendada permanece `standalone`**.
- Ajustes na apresentação de destaques e na economia de chamadas às APIs.

## 0.4.x — Catálogo automático e economia de créditos

- Coleta automática de títulos populares com **Watchmode**, disponibilidade no Brasil e links de provedores válidos.
- Cache e sincronização programada para reduzir chamadas; limites locais de requisições.
- Cadastro manual deixou de ser obrigatório para alimentar os destaques.

## 0.3 — Destaques dinâmicos

- Integração inicial de descoberta de títulos, metadados e trailers a partir de serviços externos.
- Persistência de exibições e lógica de seleção de conteúdos; sem inventar links diretos de streaming.

## 0.2 — Notícias

- Notícias com múltiplos feeds, imagens, filtros e cache persistente no SQLite.
- Atualizações periódicas da seção de notícias.

## 0.1 — Protótipo inicial

- Aplicação WPF + Blazor Hybrid para Windows, com Home, atalhos de serviços e executáveis locais.
- WebView2 com perfil persistente, SQLite para aplicativos e destaques, notícias RSS e clima Open-Meteo.
- Barra de navegação do streaming e retorno por `Ctrl + Alt + Home`.

---

**Qualidade e validação:** as versões tiveram revisões estáticas e, em algumas entregas, prévias HTML/CSS testadas no Chromium. Isso **não substitui** testes de compilação e execução WPF/WebView2 no Windows, compatibilidade com serviços de streaming nem testes reais do instalador/atualizador. Essas verificações permanecem necessárias antes da distribuição pública.


---

## Detalhamento completo das versões — documentos históricos

Os registros abaixo preservam as descrições das alterações que estavam espalhadas por arquivos `ALTERACOES_v*.md`. Os textos incluem limitações de validação informadas em cada época; **não são evidência de compilação ou de execução no Windows**. Em caso de diferença entre versões, vale a especificação da versão posterior.



---

## Registro detalhado — `ALTERACOES_v0.6.13.md`

### TW Launcher v0.6.13 — Instalador e atualização automática

- Pacote **Velopack 1.2.161** com bootstrap no Main antes de criar qualquer janela WPF.
- Atualização em segundo plano ao iniciar e a cada seis horas, com opção manual em Configurações.
- Download validado pelo Velopack (SHA); instalação ao sair do aplicativo ou ao clicar em Reiniciar e instalar.
- Compilação self-contained Windows x64, com WebView2 Runtime como dependência do Setup.
- Publicação para GitHub Releases via `vpk` e workflow GitHub Actions.
- Banco, chaves e perfil WebView2 seguem em `%LOCALAPPDATA%\TVLauncher`, fora de `current` do Velopack.
- Nunca são empacotadas chaves pessoais de `api-keys.json`; apenas arquivo de exemplo.
- Ícone com fundo transparente, sem bordas pretas.

Veja `INSTALACAO_E_ATUALIZACOES.md` para a primeira publicação e o processo das próximas versões.



---

## Registro detalhado — `ALTERACOES_v0.6.12.md`

### TW Launcher v0.6.12 — Barra de navegação integrada ao Focus

- `StreamingWindow.xaml`: novo cabeçalho de 58px com cores grafite (#101217), tipografia neutra, botão Início claro, controles de navegação com ícones lineares, clima e relógio.
- Marca na barra: símbolo TW transparente incorporado como recurso WPF `Assets/TWLauncher_Symbol.png` (registrado no `.csproj`).
- `StreamingWindow.xaml.cs`: altera apenas fallback de altura do cabeçalho de 64px para 58px na detecção do mouse.
- **Sem mudanças** na lógica de navegação, autenticação WebView2, URLs, tempo de exibição da barra, fechamento, clima ou streaming.
- A barra continua `Visibility=Collapsed` fora do topo, **sem reservar espaço** na página.

Validação: XML XAML bem-formado, nomes de controles/callbacks existentes, recurso de imagem no projeto e integridade do ZIP. Não houve compilação/execução no Windows (SDK .NET indisponível neste ambiente).



---

## Registro detalhado — `ALTERACOES_v0.6.11.md`

### TW Launcher 0.6.11 — Focus

Mudança de direção visual a partir do código da 0.6.10.

- Navegação lateral persistente (Início, Apps, Notícias, Clima e Ajustes), com foco visível e rótulos.
- Área principal com cabeçalho compacto, pesquisa real, relógio local e clima.
- Destaque cinematográfico livre de borda, maior e com tipografia editorial; trailer continua acionado por hover/foco.
- Aplicativos transformados em dock discreto e rolável, conectados aos registros reais.
- Faixas com menos cartões por vez e imagens maiores; navegação de catálogo mantida.
- Sem alteração no SQLite, nas APIs, na lógica de rotação, no WebView2 ou no gerenciamento de apps.

#### Como executar
Abra `TWLauncher.sln` no Visual Studio para Windows com .NET 10 e WebView2 instalado. Execute `TVLauncher.App`.

> Esta entrega foi verificada em estrutura e CSS/DOM com Chromium. O projeto WPF precisa de compilação final no Visual Studio/Windows.



---

## Registro detalhado — `ALTERACOES_v0.6.10.md`

### TW Launcher v0.6.10 — Night Stream

Reestilização da Home com base na última referência visual aprovada: barra superior própria, hero escuro com bordas arredondadas, Apps em uma faixa horizontal, Populares com capas altas, categorias com capas paisagem e indicadores no canto do banner.

#### Arquivos alterados
- `TVLauncher.App/Components/AppShell.razor`: hierarquia do banner, navegação, busca local, expansão funcional de fileiras e logo no cabeçalho.
- `TVLauncher.App/wwwroot/css/night-stream.css`: novo design da Home e da barra de navegação.
- `TVLauncher.App/wwwroot/index.html`: referência para o novo CSS.
- `TVLauncher.App/wwwroot/img/tw-brand.png`: marca TW redimensionada.

#### Não alterado
Serviços de catálogo e Watchmode/TMDB, SQLite, clima, notícias, comportamento de rotação do banner, abertura WebView2, atalhos e seleção de provedores, registro de visualizações, autenticação e configurações. O app inicia na Home.

#### Observações
- O design usa imagens, logos e conteúdo disponíveis no banco, não filmes ou exemplos estáticos da referência.
- O trailer permanece por hover/foco quando houver ID de vídeo.
- A pesquisa consulta apenas títulos já carregados em memória, sem gastar créditos de API.
- Este pacote contém código-fonte WPF/Blazor; não é um instalador. Compilação exige .NET 10 SDK e workload Windows no Visual Studio.



---

## Registro detalhado — `ALTERACOES_v0.6.8_CINEMA_ELEGANTE.md`

### TW Launcher — v0.6.8 Cinema Elegante

Versão criada a partir do código-fonte v0.6.7.3 enviado pelo usuário.

#### O que mudou
- Nova Home de estilo cinematográfico, fundo escuro e banner em tela cheia, sem player emoldurado separado.
- Player/trailer integrado à arte à direita; o comportamento de reprodução por hover/foco foi mantido.
- Botão «Detalhes» apresenta sinopse, gêneros e streaming do título selecionado em uma janela interna.
- Setas de anterior/próximo permanecem ligadas ao carrossel, agora à direita do banner.
- Aplicativos fixados se tornam atalhos horizontais, sem alterar cadastro, logos nem seleção de streamings.
- Fileiras de filmes/séries com capas paisagem compactas e título/serviço ao focar ou passar o mouse.
- Cabeçalho, relógio, clima e navegação com visual dark TV; sem mudança de dados de APIs.
- TWLauncher.ico selecionado foi incorporado ao ícone do executável e da janela Windows.

#### Arquivos principais alterados
- TVLauncher.App/Components/AppShell.razor
- TVLauncher.App/wwwroot/index.html
- TVLauncher.App/wwwroot/css/cinema-elegante.css (novo; carregado após app.css)
- TVLauncher.App/MainWindow.xaml
- TVLauncher.App/TVLauncher.App.csproj
- TVLauncher.App/Assets/TWLauncher.ico (novo)

#### Como testar
1. Abra TVLauncher.App/TVLauncher.App.csproj no Visual Studio com SDK .NET 10 e componentes WPF.
2. Restaure os pacotes NuGet e compile para Windows.
3. Execute TWLauncher.exe. Configurações e banco local continuam no caminho da versão anterior.
4. As imagens/títulos exibidos dependem do catálogo local e dos aplicativos fixados — os exemplos dos previews são ILUSTRATIVOS.

#### Prévia visual
- Preview/TWLauncher_Cinema_Elegante_1920x1080.png
- Preview/TWLauncher_Cinema_Elegante_1366x768.png
- Preview/TWLauncher_Cinema_Elegante_Autonomo.html

#### Validações realizadas
- XML do .csproj e XAML analisados sem erros de estrutura.
- Renderização HTML/CSS em navegador Chromium em 1920x1080 e 1366x768, sem overflow horizontal.
- Verificação estrutural básica da alteração Razor e presença dos arquivos.
- Não foi possível compilar/executar o WPF (.NET 10 para Windows) neste ambiente Linux.



---

## Registro detalhado — `ALTERACOES_v0.6.7.3.md`

### TW Launcher v0.6.7.3 — Barra 100% invisível quando recolhida

#### Correção solicitada

- Removido `HeaderHotZone` (a faixa azul de 6 px da versão anterior).
- O WebView2 ocupa **100% da altura** da janela enquanto o cabeçalho está oculto.
- O ponto de ativação é uma **área virtual invisível** de 8 unidades no topo: ao aproximar o cursor, a barra TW Launcher aparece.
- Para detectar o mouse mesmo quando ele está dentro da janela filha do WebView2, a janela consulta `GetCursorPos` a cada 90 ms e converte as coordenadas considerando o DPI.
- Ao sair da região do cabeçalho, ele é totalmente recolhido; em vídeo fullscreen fica sempre oculto.
- Nenhum elemento visual, texto, borda nem espaço vazio deve aparecer no topo antes de a barra ser chamada.
- A barra continua com Início, navegação, temperatura e relógio; o atalho `Ctrl + Alt + Home` não foi alterado.

#### Arquivos alterados

- `TVLauncher.App/StreamingWindow.xaml`
- `TVLauncher.App/StreamingWindow.xaml.cs`
- `Preview/barra-webview2.html`

#### Validação

- XML XAML analisado estruturalmente.
- Verificada a ausência de `HeaderHotZone` e a configuração `Visibility="Collapsed"` do cabeçalho.
- Lógica de coordenadas e visibilidade inspecionada estaticamente.
- Compilação WPF e interação real com o WebView2 devem ser confirmadas no Windows.



---

## Registro detalhado — `ALTERACOES_v0.6.7.2.md`

### TW Launcher v0.6.7.2 — Barra retrátil do navegador

#### Alterações

- A barra superior de `StreamingWindow` não fica mais permanentemente visível.
- Uma faixa discreta de **6 px** no topo ativa o cabeçalho ao receber o mouse.
- Ao sair do cabeçalho, a barra se recolhe; a WebView2 volta a ocupar a área disponível.
- O visual foi padronizado com o TW Launcher: marca TW, gradiente escuro, botões discretos, clima e hora.
- Adicionados Voltar, Avançar e Atualizar, preservando o botão Início e o atalho global `Ctrl + Alt + Home`.
- Horário atualizado a cada 15 segundos; temperatura consultada por `WeatherService` a cada 30 minutos, usando seu cache existente, sem bloquear navegação.
- Durante a reprodução de um elemento em tela cheia do WebView2, o cabeçalho e a faixa superior ficam ocultos para não comprometer o vídeo.
- Nenhuma alteração no SQLite, no catálogo, nas chaves de API ou na Home.

#### Arquivos alterados

- `TVLauncher.App/StreamingWindow.xaml`
- `TVLauncher.App/StreamingWindow.xaml.cs`
- `TVLauncher.App/Services/WindowsHostService.cs`

#### Limites e teste no Windows

A barra usa uma linha própria do Grid em vez de sobrepor elementos WPF sobre o HWND do WebView2; isso é intencional para evitar conflitos de sobreposição (*airspace*). Enquanto aberta, a barra reduz temporariamente a altura útil do site em 64 px.

Os controles do cabeçalho ficam ocultos durante o fullscreen HTML. O atalho global continua disponível.

Validar no Windows: hover na faixa superior, saída do mouse, operação dos botões, atualização do clima, fullscreen e retorno à Home. A compilação WPF não pôde ser executada no ambiente Linux de criação.



---

## Registro detalhado — `ALTERACOES_v0.6.7.1.md`

### TW Launcher v0.6.7.1 — Correção de compilação

#### Correção
- Arquivo: `TVLauncher.App/Services/TrendingCatalogService.cs`.
- No método `EnrichExistingCatalogAsync`, a variável `pending` é um `AutoFeaturedTitle[]` criado por `.ToArray()`.
- Substituídos `pending.Count == 0` e `pending.Count > 0` por `pending.Length == 0` e `pending.Length > 0`.
- `pendingPortuguese.Count` foi mantido, pois está declarado como `IReadOnlyList<AutoFeaturedTitle>` e possui a propriedade `Count`.
- Não foram alteradas consultas às APIs, migrações, banco, chaves ou funcionalidades.

#### Verificação
- Conferência estática das duas referências, além de comparação dos arquivos com a v0.6.7.
- Não foi possível executar a compilação WPF/.NET 10 neste ambiente (SDK indisponível).
- No Windows, abra `TWLauncher.sln` e execute **Recompilar Solução**.



---

## Registro detalhado — `ALTERACOES_v0.6.7.md`

### TW Launcher v0.6.7 — streamings escolhidos pelo usuário

#### Decisão

**Fixar no Início** e **Usar nos destaques** são opções independentes em Apps.

- Um site ou programa Windows continua funcionando como atalho, mesmo sem participar dos destaques.
- Somente apps Web vinculados explicitamente a um serviço da lista Watchmode Brasil podem fornecer destaques.
- Se nenhum serviço for escolhido, **a Home continua abrindo** e não se fazem chamadas de descoberta Watchmode.
- O usuário pode cadastrar GloboPlay, Mercado Play ou qualquer outro serviço disponível na lista da API. A disponibilidade e os links dependem da cobertura real dos provedores; não inventamos IDs nem links.
- Ao desativar um serviço, seus títulos são retirados imediatamente do catálogo da Home e de todas as fileiras de gêneros.

#### Pipeline

1. Na aba Apps, consultar a lista de serviços disponíveis no Brasil do Watchmode (cache local até 30 dias, com proteção de 24h contra tentativas falhas).
2. O usuário marca `Usar este streaming nos destaques` e escolhe o provedor correspondente. O sistema tenta pré-selecionar pelo nome do app.
3. Salvar `IncludeInFeatured` e `WatchmodeSourceId` na tabela `aplicativos`, sem alterar `PinnedToHome`.
4. Filtrar `/v1/list-titles/` por `source_ids` + `regions=BR` + assinatura/gratuito.
5. No endpoint de disponibilidade por título, escolher **somente** o `source_id` habilitado.
6. TMDB: procurar rankings via `discover/movie` e `discover/tv` com os provedores equivalentes filtrados para BR quando for possível identificar seus IDs. Metadados pt-BR continuam sendo enriquecidos para os títulos selecionados.
7. Guardar `WatchmodeSourceId` no título e aplicar uma segunda filtragem na leitura da Home a partir do SQLite.
8. Em mudanças de seleção, a Home refiltra imediatamente o cache. Nova coleta só acontece após o cooldown e respeitando o orçamento mensal.

#### Migração

Incremental, sem apagar dados existentes:

- `aplicativos.IncludeInFeatured` (BOOLEAN/INTEGER, padrão 0)
- `aplicativos.WatchmodeSourceId` (nullable)
- `destaques_automaticos.WatchmodeSourceId` (INTEGER, padrão 0)

O padrão 0 é proposital: títulos de catálogos antigos não entram indevidamente nos destaques enquanto não for confirmada sua origem por uma nova consulta.

#### Limitações

- O Watchmode pode não listar um serviço ou um título no Brasil; nesse caso não há resultados para aquele provedor.
- A integração Watchmode continua exigindo a chave já configurada em `api-keys.json` e tem limites de créditos.
- O filtro TMDB de provedor não oferece link direto: o endereço de reprodução só vem das fontes de disponibilidade Watchmode.
- Não houve compilação nem execução WPF real neste ambiente; é necessário validar no Visual Studio/Windows com credenciais próprias.



---

## Registro detalhado — `ALTERACOES_v0.6.6.md`

### TW Launcher v0.6.6 — Apps

#### Implementado
- Aba Apps; lista completa de atalhos e aplicativos.
- Nova ação fixar/desafixar com persistência no banco e filtragem na Home.
- Cadastro, edição e remoção (com confirmação) dos aplicativos personalizados.
- Recuperação automática da logo usando os favicons publicados no domínio informado.
- Extração do ícone do executável Windows quando disponível.
- Imagens armazenadas localmente como data URI e renderizadas com object-fit: contain.
- Migração SQLite sem excluir apps existentes. Todos os antigos permanecem fixados na Home.
- Apps passa a ser o lugar para adicionar sites/programas; em Configurações ficam as opções avançadas do catálogo de filmes.

#### Limitações conhecidas
- Alguns sites não disponibilizam ícones compatíveis ou bloqueiam solicitações automáticas. Nesses casos aparece o símbolo do atalho.
- Ícones de sites são recuperados por solicitação de rede e podem falhar sem impedir o cadastro.
- A interface desktop WPF, a migração real no seu banco e a extração de ícone .exe ainda precisam ser testadas no Windows.



---

## Registro detalhado — `ALTERACOES_v0.6.5.md`

### TW Launcher v0.6.5 — descrições em português e categorias automáticas

#### Corrigido

- Gêneros do catálogo antigo são preenchidos em segundo plano, sem apagar o SQLite e sem precisar esperar pela atualização completa de 72 horas.
- No máximo 24 consultas de detalhes Watchmode por execução de enriquecimento, com trava de 24 horas persistida no SQLite e respeito ao orçamento mensal já definido. O serviço de gêneros pode exigir uma chamada extra. O consumo depende dos metadados faltantes.
- A Home reserva espaço para Ação, Aventura, Comédia, Romance, Ficção científica e Suspense logo abaixo de “Populares para assistir”, mesmo enquanto os gêneros do catálogo antigo são obtidos. Outros gêneros aparecem quando houver títulos classificados.
- Com token **TMDB** configurado, títulos recebem sinopses localizadas em `pt-BR` via API oficial, sem consumir créditos Watchmode adicionais na fase de tradução de títulos cujo TMDB ID já foi salvo.
- Sem token TMDB ou quando a tradução não existe, a Home **não mostra a sinopse inglesa**. Não gera traduções fictícias; a disponibilidade do filme e o link continuam funcionando.
- Novos títulos guardam `TmdbId` e `SynopsisLanguage` no SQLite para enriquecimento gradual sem redundância de consultas.
- As chaves continuam em `%LOCALAPPDATA%\TVLauncher\api-keys.json`; Home continua a tela inicial.

#### Observações

Com somente Watchmode configurado, os **gêneros** são recuperados automaticamente, mas o Watchmode não garante sinopses em português. Para obter descrições reais em português, preencha `tmdbReadAccessToken` no `api-keys.json` — não há cadastro manual de filmes. A tradução depende de estar disponível no TMDB.

O enriquecimento antigo consulta a API somente quando existem gêneros faltando. Em uma máquina com muitos títulos, o restante é processado gradualmente em execuções futuras; não há obrigação de gastar créditos a cada abertura.

**Validação pendente:** compilar e executar no Windows, testar as respostas com suas próprias chaves, conferir TMDB pt-BR e preenchimento das categorias.



---

## Registro detalhado — `ALTERACOES_v0.6.4.md`

### TW Launcher v0.6.4 — Fila do carrossel e categorias

#### Destaques

- A cada **entrada na Home**, embaralha todos os destaques elegíveis uma única vez usando Fisher–Yates.
- A ordem permanece fixa durante toda a visita, mesmo que ocorra atualização em segundo plano; itens novos são acrescentados no fim.
- O contador mostra a **posição exata 01/N** dentro da fila. Todos os indicadores são fixos e correspondem a essa ordem.
- Setas: `Anterior` percorre o item imediatamente anterior; `Próximo` vai para o seguinte. Nos extremos, os botões permitem circular entre primeiro e último.
- Após 10 s sem hover/foco, o carrossel avança automaticamente. Ao chegar no último, o avanço automático **para** para evitar repetições incessantes; o botão Próximo permite voltar para o primeiro.
- O trailer mantém a regra da v0.6.3: só inicia ao interagir por mouse ou teclado; áudio é solicitado durante a interação.
- Na entrada seguinte na Home (vindo de Notícias/Clima ou de um aplicativo externo), uma **nova fila** é sorteada.

#### Gêneros

- Novas fileiras: Ação, Aventura, Comédia, Romance, Ficção científica, Suspense, Drama, Terror, Fantasia, Animação, Documentário, Crime, Família, Mistério, História, Musical e Faroeste.
- **Só aparecem gêneros com títulos disponíveis e com link direto conhecido.** Um título pode constar em mais de um gênero.
- Gêneros são obtidos de campos reais dos detalhes do Watchmode e do TMDB, sem dedução por sinopse.
- No Watchmode, quando existem somente IDs numéricos, o sistema pode consultar a lista de gêneros **uma vez por sincronização**, respeitando o orçamento local de chamadas. Essa consulta eventual consome crédito e é evitada se o TMDB já disponibilizar os nomes.
- Uma coluna `Genres` é adicionada **sem apagar o banco SQLite**. Registros antigos ficam sem gênero até serem enriquecidos pela próxima sincronização automática.
- No modo standalone cada dispositivo consulta sua própria API, como já definido.

#### Arquivos alterados

`Components/AppShell.razor`, `Services/FeaturedRotationService.cs`, `Services/ContentGenres.cs` (novo), `Services/TrendingCatalogService.cs`, `Services/LauncherRepository.cs`, `Services/SharedCatalogService.cs`, `Models/AutoFeaturedTitle.cs`, `Models/FeaturedTitle.cs`, `wwwroot/css/app.css`, `wwwroot/js/launcher.js`, `Preview/premium.html`.

#### Escopo de validação

Prévia HTML testada no Chromium em 1920×1080, 1366×768 e 900×700, incluindo: ordem sequencial, voltar, selecionar ponto, contador, categorias, avanço após dez segundos e ausência de overflow horizontal.

**Não foi possível compilar/testar a aplicação WPF nem consultar APIs com uma chave real neste ambiente.** As alterações de EF Core, WebView2 e parsing de gêneros precisam ser verificadas no Windows.



---

## Registro detalhado — `ALTERACOES_v0.6.3.md`

### TW Launcher 0.6.3 — trailer com som e limpeza visual

- Banner permanece parado (trailer não carregado) sem interação.
- Ao colocar o mouse sobre o banner/seletor ou focar via Tab, o player inicia e solicita ativação do áudio via YouTube IFrame API.
- A saída do hover/foco silencia e interrompe o vídeo, removendo o iframe pelo Blazor.
- Controle discreto de som para usuários cuja política do Chromium bloquear a ativação sem clique.
- Eliminados textos auxiliares da Home: «Acesse com um clique», instruções do trailer, rodapé do catálogo e explicações extras.
- Créditos Watchmode/TMDB preservados em Configurações.
- Sem mudanças no banco, credenciais, seleção de catálogo ou tempo de rotação de 10 s.

**Limitação técnica:** um evento `mouseenter` isolado não concede permissão de autoplay sonoro em todos os ambientes Chromium/WebView2. O Launcher solicita som; se a política local negar, o usuário pode clicar no ícone 🔊. Não forçamos flags inseguras do navegador.



---

## Registro detalhado — `ALTERACOES_v0.6.2.md`

### TW Launcher v0.6.2 — correções do destaque

- Nome exibido e executável: **TW Launcher / TWLauncher.exe**, solução **TWLauncher.sln**. Mantidos namespace, pasta `%LOCALAPPDATA%\TVLauncher`, banco e JSON existentes para não perder configurações.
- O seletor apresenta uma **janela fixa de sete títulos** por vez. O ativo muda de posição dentro da janela (não fica mais fixo no primeiro traço). A cada sete destaques, mostra a próxima página.
- Passar o mouse **sobre o banner ou sobre os indicadores** inicia o trailer silenciado, quando há vídeo e o banner está visível. Foco por teclado também ativa a prévia. Fora da interação, o player é removido e fica apenas a imagem.
- Sem hover/foco e com o banner visível, avançar após **10 segundos**. Ao sair do hover/foco, contar 10 segundos completos. A rotação não repete um título durante a mesma volta; ao esgotar o catálogo, inicia nova volta evitando repetir imediatamente o último título.
- Scrollbars vertical e horizontal com cores e cantos do design TW.
- Home abre sempre, com ou sem API. Sem mudança nas chaves ou no SQLite.

#### Testes para realizar no Windows

1. Abrir a Home; conferir trailer parado e imagem estática.
2. Passar o mouse sobre o banner; conferir início da prévia sem som.
3. Tirar o mouse do banner e da barra de indicadores; conferir desaparecimento do player.
4. Deixar sem foco; observar avanço após 10 segundos e o indicador passando de 1/7 para 2/7, etc.
5. Passar o mouse sobre um indicador e navegar com teclado; confirmar seleção e prévia.
6. Verificar salto dos indicadores após o sétimo destaque, e a continuidade da rotação.
7. Testar Notícias, Clima, links de streaming e retorno à Home.
8. Verificar que `%LOCALAPPDATA%\TVLauncher\api-keys.json` continua sendo utilizado sem recadastro.

**Limitação:** a compilação WPF e a reprodução real do YouTube exigem execução no Windows; não foram homologadas neste ambiente.

#### Validações executadas neste ambiente

- `node --check` no `wwwroot/js/launcher.js`: passou.
- Prévia HTML testada em Chromium nas resoluções 1920×1080, 1366×768 e 900×700: sem rolagem horizontal; indicador acompanha mudança do destaque; sem erros JavaScript.
- Eventos reais no Chromium: entrar/sair com mouse do banner/seletor e navegar com teclado produz eventos de interação coerentes.
- Relógio simulado no Chromium: muda após 10 segundos sem hover; não muda durante 15 segundos com hover; muda 10 segundos depois de sair do hover.
- Não há SDK .NET/WPF Windows neste ambiente; compilação nativa, integração com SQLite, WebView2 e streaming deverão ser validados no Visual Studio.



---

## Verificações, testes e limitações registrados

Esta seção mantém os documentos de qualidade anteriores, distinguindo as verificações **estáticas, simuladas e prévias em navegador** da execução **WPF real**. Nenhum teste foi reexecutado na consolidação dos documentos.



---

### Registro de qualidade — `QUALITY_CHECKS.md`

#### Validação — TW Launcher v0.5

##### Verificações realizadas neste ambiente

- Leitura e inspeção dos arquivos reais da v0.4.1.
- XML da solução `TVLauncher.App.csproj` e dos XAMLs verificados pelo parser.
- `api-keys.json` e `api-keys.example.json` validados com JSON parser.
- Verificação estática: Home inicial (`Tab="home"`), sem regra de redirecionamento obrigatório para settings.
- Verificação estática: leitor importa snapshot antes de qualquer chamada à API e publicador exporta JSON.
- Verificação estática: chamadas Watchmode passam pelo controle local de orçamento.
- Verificação estática: integridade do ZIP com teste CRC.

##### Não realizados / pendentes

- Compilação e execução WPF/Blazor Hybrid: o SDK .NET e Windows não estão disponíveis neste ambiente.
- Comportamento real em pasta UNC no Windows, especialmente concorrência, permissões e PC publicador offline.
- Respostas reais do TMDB e Watchmode com credenciais válidas (chaves do usuário não foram solicitadas nem usadas).
- Autoplay/WebView2 com Netflix e YouTube.
- Não há validação de aprovação contratual para compartilhar dados Watchmode com terceiros. A intenção é apenas dispositivos próprios, sem redistribuição externa.

Não considerar estas integrações homologadas antes de testes reais em Windows.


##### v0.6 — Verificação complementar

- Alterações localizadas em `Components/AppShell.razor` e `wwwroot/css/app.css`.
- Testar título curto, título de 50+ caracteres, sem trailer, trailer bloqueado e rotação no Windows.
- Verificação visual da prévia HTML em 1920×1080, 1366×768 e 900×700.
- Sem compilação .NET/WPF neste ambiente: não declarar integração desktop homologada.

###### Execução dos testes da prévia premium

- Renderização Chromium com HTML e CSS reais da versão 0.6 em 1920×1080, 1366×768, 900×700 e 2560×1440.
- Comportamento do botão Próximo verificado e sem erros JS detectados.
- `document.documentElement.scrollWidth` igual à largura do viewport em todas as resoluções.
- Título dentro dos limites do hero e trailer com proporção 16:9 para os casos medidos.
- Integridade ZIP, configuração JSON e parsing XML/XAML verificados.
- **Compilação .NET/WPF não executada:** SDK e ambiente Windows indisponíveis nesta sessão.


##### v0.6.1 — Correção da colisão de `@key`
- `div.hero-content`: `HeroContentKey(selected)`.
- `div.hero-showcase`: `HeroShowcaseKey(selected)`.
- Teste estático: prefixos de chave diferentes entre os irmãos.
- A reprodução real e a compilação WPF ainda devem ser validadas no Windows.


##### v0.6.3 — Trailer com áudio e interface enxuta

- `node --check`: arquivo `launcher.js` válido.
- Teste de eventos com Node (ambiente simulado): hover emite `HeroInteractionChanged(true)` e solicita `unMute`, saída solicita `mute`, foco via Tab ativa o estado de interação e o descarte dos handlers executa sem erro.
- Verificação estática de `AppShell.razor`: instruções auxiliares e rodapé da Home removidos; atalho funcional «Adicionar app» preservado.
- Fontes Watchmode/TMDB permanecem creditadas nas Configurações.
- XAML/csproj e JSON validados com parser.
- **Limitação:** testes de reprodução real com áudio no YouTube/WebView2 e compilação WPF não são possíveis neste ambiente. O Chromium pode bloquear áudio iniciado só por hover; há alternativa clicável.
- A execução do teste visual Chromium no container foi bloqueada pelo ambiente (`ERR_BLOCKED_BY_ADMINISTRATOR`), portanto não foi declarada aprovação de layout nesta versão.



---

### Registro de qualidade — `QUALITY_CHECKS_v0.6.4.md`

#### Validações v0.6.4

- Prévia HTML Chromium: **PASS** 1920×1080, 1366×768, 900×700, sem overflow horizontal.
- Prévia HTML: **PASS** próximo 01→02, anterior 02→01, selecionar 05, seguinte 06, seguinte manual 01.
- Prévia HTML: **PASS** indicador ativo bate com número; gêneros estão na ordem configurada.
- Prévia HTML: **PASS** avanço automático em dez segundos sem interação; sem erros JavaScript observados.
- JS `launcher.js`: **PASS** `node --check`.
- Banco: migração incremental planejada para preservar registros já existentes, **sem teste em EF Core/WPF**, pois dotnet SDK não está instalado aqui.
- API: resposta de gêneros e consumo de créditos **não testados com credenciais reais**.

**Pendente:** recompilar em Windows, testar migração em banco real e validar gêneros do Watchmode/TMDB com dados reais.



---

### Registro de qualidade — `QUALITY_CHECKS_v0.6.5.md`

#### Qualidade — TW Launcher v0.6.5

##### Verificado nesta entrega

- Inspeção das mudanças em `TrendingCatalogService`, `LauncherRepository`, `AppShell` e modelos.
- Verificação estática de ligações entre serviço de atualização, campos de locale e renderização das categorias.
- Cenário SQLite antigo simulado com adição não destrutiva das colunas `Genres`, `SynopsisLanguage` e `TmdbId` (dados existentes preservados).
- Sintaxe JavaScript da prévia HTML validada com Node.js.
- Integridade do arquivo ZIP verificada (`unzip -t`).

##### Pendente no Windows

- Compilação real da solução WPF com .NET 10 e restauração NuGet.
- Atualização com chaves de API Watchmode/TMDB reais; não foram usadas credenciais privadas.
- Verificar preenchimento das fileiras com `genre_ids` retornados pelo Watchmode e traduções `pt-BR` retornadas pelo TMDB.
- Avaliar custo de chamadas do enriquecimento em conta real.

Não confundir a prévia (dados ilustrativos) com resultados vindos das APIs. A ausência de tradução no TMDB não é substituída por texto inventado.



---

### Registro de qualidade — `QUALITY_CHECKS_v0.6.6.md`

#### Verificações — v0.6.6

- Verificação estática das rotas da UI: Apps, Novo, Editar, Fixar, Remover, Home.
- Verificação estática da modelagem e chamadas do repositório.
- Migração SQLite simulada em um banco de versão anterior: dois apps preservados, novas colunas e fixação inicial confirmadas.
- Enquadramento visual dos ícones definido via CSS fixo com `object-fit: contain`.
- Limite de tamanho para download dos ícones: 256 KB. Apenas PNG, JPEG, WebP, GIF e ICO.
- Nenhuma alteração intencional em contas, tokens de API, reprodução ou catálogo de filmes.

**Não executado aqui:** compilação real de .NET/WPF/Blazor, execução no Windows, extração de ícones .exe e validação real do WebView2. A geração da captura de tela de prévia não ficou disponível neste ambiente.



---

### Registro de qualidade — `QUALITY_CHECKS_v0.6.7.3.md`

#### Verificações da v0.6.7.3

- XAML de `StreamingWindow.xaml`: XML válido, cabeçalho inicialmente `Collapsed`, sem `HeaderHotZone`.
- Revisão estática: janela com `RowDefinition Height="Auto"`, ocupação integral do WebView2 quando a primeira linha fica vazia.
- Revisão estática: leitura nativa da posição do cursor por `GetCursorPos`, conversão de DPI por `PointFromScreen`, atualização a cada 90 ms, bloqueio em tela cheia e timer encerrado ao fechar a janela.
- Prévia HTML no Chromium via Playwright: fechada -> área Web iniciando em y=0; mouse no topo -> cabeçalho abre (área Web inicia em y=64); mouse fora -> cabeçalho recolhe e a área Web volta a y=0.
- Não foi possível compilar ou executar WPF/WebView2 no Windows neste ambiente. O comportamento real deve ser validado no dispositivo de destino.



---

## Notas operacionais e históricas do README original do pacote

O ZIP contém um `README.md` antigo cujo título principal ainda se refere à v0.6.1 e que acumula informações das versões v0.5–v0.6.7.3. Seu conteúdo foi preservado a seguir por fidelidade histórica, mas **não representa, por si só, a configuração mais recente**. O README principal desta entrega é o guia atualizado para a v0.6.13. Referências a nomes `ALTERACOES_v*.md` neste trecho apontam para as seções de mesmo nome **já incluídas neste CHANGELOG**.

### TW Launcher v0.6.1 — Correção do carrossel de destaques

**Correção pontual:** elimina a exceção do Blazor `InvalidOperationException: More than one sibling of element 'div' has the same key value, 'auto:watchmode:...'`.

**Causa:** na v0.6, os dois irmãos `div.hero-content` e `div.hero-showcase` usavam o mesmo `@key="selected.RotationKey"`.

**Correção:** foram adicionados `HeroContentKey` e `HeroShowcaseKey`, com prefixos distintos. O `iframe` permanece com seu `@key` original, pois está em um nível diferente da árvore de renderização.

Não foram modificados o esquema SQLite, as APIs, a Home, a rotação, o CSS, a abertura dos streamings ou a configuração de chaves.

#### Como atualizar

1. Feche o TW Launcher antes de substituir os arquivos.
2. Extraia este ZIP em uma pasta de código-fonte (substituindo o código v0.6 caso deseje).
3. No Visual Studio, abra `TWLauncher.sln`, reconstrua a solução e execute.
4. Não apague `%LOCALAPPDATA%\TVLauncher`: suas chaves locais, preferências e o banco SQLite devem ser preservados.
5. Teste o carrossel alternando pelo menos três destaques e observando se a exceção desapareceu.

**Validação:** análise estática dos pontos com `@key` e verificação de integridade do pacote. Não foi possível executar/compilar WPF e Blazor Hybrid neste ambiente, pois o SDK .NET/Windows não está disponível.

---

### TW Launcher v0.5 — Home permanente, fontes alternativas e catálogo doméstico

Aplicativo Windows WPF / Blazor Hybrid / .NET 10 com SQLite e WebView2.

#### Alterações

- **Home sempre inicial**, com ou sem chaves de API, sem acesso obrigatório a Configurações.
- Arquivo editável de chaves e catálogo: `%LOCALAPPDATA%\TVLauncher\api-keys.json` (criado automaticamente ao iniciar).
- Duas fontes de descoberta: **TMDB** (tendências e trailers, opcional) e **Watchmode** (popularidade alternativa e resolução de links).
- Watchmode utilizado **somente para links diretos**; TMDB sozinho não entrega deep links, portanto não exibimos links inventados.
- Orçamento mensal local de requisições Watchmode (padrão de **500**) e 12 candidatos no máximo por sincronização de 72 horas. Sem consultas pagas ao rotacionar trailers ou simplesmente iniciar a Home durante o período de cache.
- Modos domésticos: `publisher` grava catálogo JSON numa pasta compartilhada; `reader` lê o mesmo arquivo, sem qualquer consulta à Watchmode ou TMDB; `standalone` funciona individualmente.
- Apps, Notícias e Clima continuam funcionando sem credenciais do catálogo.

#### Onde colocar as chaves, sem abrir o aplicativo

1. **Antes da primeira compilação**, edite `TVLauncher.App\api-keys.json` incluído no ZIP. Ele será copiado com o aplicativo e importado na primeira execução. **Ou**, se já executou o Launcher antes, edite diretamente `%LOCALAPPDATA%\TVLauncher\api-keys.json`.

2. Preencha no Bloco de Notas (UTF-8), por exemplo:

```json
{
  "watchmodeApiKey": "SUA_CHAVE_AQUI",
  "tmdbReadAccessToken": "SEU_TOKEN_TMDB_OPCIONAL",
  "catalogMode": "standalone",
  "sharedCatalogPath": "",
  "maxTitlesPerSync": 12,
  "monthlyWatchmodeRequestBudget": 500
}
```

3. Salve e execute (ou reinicie) o Launcher. **Mesmo que deixe as duas chaves vazias, ele abrirá na Home.**

**Prioridade:** a configuração existente em `%LOCALAPPDATA%` é a definitiva. Alterar o arquivo do projeto não sobrescreve uma instalação já configurada.

**Atenção:** este JSON contém segredos em texto simples. Não compartilhe, anexe a chamados, coloque em repositórios, nem copie com a chave preenchida para os outros computadores. É mantido no perfil local do Windows. O painel antigo de configuração continua aceitando DPAPI, mas valores não vazios no JSON têm prioridade.

#### Opcional: modo compartilhado entre computadores

Por padrão, mantenha `catalogMode` em `standalone` em **cada computador** e configure a chave em cada instalação, sem pasta compartilhada. Atenção: se todos usarem a mesma chave Watchmode, consumirão a **mesma franquia total da conta**; o orçamento mensal configurado é local por dispositivo, não um bloqueio global. Caso prefira centralizar as chamadas futuramente, use o modo opcional abaixo, com **um computador publicador**. Compartilhe no Windows **somente uma pasta para o catálogo JSON**, NUNCA o SQLite, o WebView2Profile nem as chaves.

1. No computador que fará a consulta, crie uma pasta compartilhada (exemplo: `C:\TVLauncherShared`) e dê permissão de escrita ao usuário publicador e leitura aos outros. O arquivo `catalog.json` será criado automaticamente.
2. No `api-keys.json` do **publicador** configure:

```json
{
  "watchmodeApiKey": "SUA_CHAVE_PRIVADA",
  "tmdbReadAccessToken": "SEU_TOKEN_TMDB_OPCIONAL",
  "catalogMode": "publisher",
  "sharedCatalogPath": "\\\\PC-COZINHA\\TVLauncherShared\\catalog.json",
  "maxTitlesPerSync": 12,
  "monthlyWatchmodeRequestBudget": 500
}
```

3. Nos `api-keys.json` dos **demais dispositivos** configure **sem chaves**:

```json
{
  "watchmodeApiKey": "",
  "tmdbReadAccessToken": "",
  "catalogMode": "reader",
  "sharedCatalogPath": "\\\\PC-COZINHA\\TVLauncherShared\\catalog.json",
  "maxTitlesPerSync": 12,
  "monthlyWatchmodeRequestBudget": 500
}
```

Todos os leitores carregam seu próprio cache SQLite, inclusive se a pasta compartilhada estiver temporariamente fora do ar. Eles verificam o JSON compartilhado a cada 10 minutos e na inicialização; **nunca consultam API do catálogo**.

Os três aparelhos precisam ter acesso à mesma pasta via rede local. O PC publicador deve ficar ligado no momento de publicar uma atualização; os leitores continuam usando seu cache ao ficar offline. Os perfis de Netflix/YouTube são separados por computador.

**Limites e direitos:** Watchmode permite cache não comercial no plano gratuito por até 30 dias e proíbe repasse para terceiros. O compartilhamento foi concebido somente entre dispositivos pessoais do mesmo usuário, sob sua responsabilidade e respeitando as condições da API. Não publique o arquivo na internet. Imagens de terceiros exigem seus próprios direitos; não redistribua arquivos de imagem.

#### Sem as chaves ou sem internet

- Home SEMPRE abre.
- Apps instalados e cadastrados continuam disponíveis.
- Notícias e Clima usam suas próprias fontes; podem mostrar cache.
- Se não houver links diretos validados, o banner mostra a apresentação estática da Home, sem filmes falsos.
- É possível cadastrar aplicativos e títulos manualmente pelas configurações avançadas, mas isso não é necessário para uso normal.

#### Compilar e executar

Abra `TWLauncher.sln` no Visual Studio com .NET 10 para Windows e execute `TVLauncher.App` (F5). A aplicação WebView2 necessita do Runtime instalado no Windows.

A compilação WPF, o funcionamento dos serviços com chaves reais e a rede UNC ainda precisam de testes em Windows; não foram homologados neste ambiente.


#### v0.6 — Destaque cinematográfico premium

- Hero da Home redesenhado: backdrop real do título, vinheta legível, trailer em 16:9 integral e caption separada do player.
- Título com fonte adaptativa por comprimento, sem cortar com elipse; sinopse limitada a três linhas.
- Setas integradas à ação principal e indicação de posição em uma trilha discreta abaixo do banner.
- Ajustes específicos para monitores/TVs grandes e telas de 720p, com composição vertical quando a largura é menor.
- **Home continua sendo a tela inicial**, com ou sem API, e as chaves permanecem editáveis em `api-keys.json`.
- O player oficial do YouTube é mantido intacto, inclusive sua identidade e seus controles.
- A prévia HTML, em `Preview/premium.html`, é demonstrativa e não consulta serviços.


#### TW Launcher v0.6.3 — som dos trailers

- O trailer só é criado quando há mouse sobre o banner/seletor ou foco por Tab.
- O Launcher envia os comandos `unMute` e `playVideo` ao player do YouTube durante a interação.
- O áudio não inicia sozinho com o Launcher nem ao passar os destaques automaticamente.
- Se o WebView2/Chromium bloquear áudio automático em `hover`, clique no ícone de alto-falante no destaque. Este é um limite do player/navegador, não do cadastro de APIs.
- Textos auxiliares e rodapé de explicação foram removidos da Home. Créditos dos dados continuam nas Configurações.
- Sem mudanças no SQLite, no local do `api-keys.json`, nos tempos de rotação ou na política de abertura da Home.


#### v0.6.4 — Carrossel previsível e fileiras por gênero

Leia `ALTERACOES_v0.6.4.md`. A Home embaralha ao entrar, mas avança/retorna sequencialmente.
Gêneros aparecem abaixo dos Populares após serem obtidos da próxima atualização do catálogo; o cache antigo não tem essa informação.
O projeto mantém `api-keys.json`, inicia na Home e preserva `%LOCALAPPDATA%\TVLauncher` para banco/chaves existentes.

Prévia: `Preview/premium.html` (exemplos fictícios da interface, não catálogo em tempo real).


#### Alterações v0.6.5 — sinopses e gêneros

- Os títulos já salvos ganham gêneros progressivamente em segundo plano, sem precisar excluir o banco ou esperar 72 horas. A recuperação de gêneros pode gastar até 24 créditos Watchmode por lote, além de uma consulta de referência quando necessária. Uma trava de 24 horas impede repetição frequente.
- A Home apresenta Ação, Aventura, Comédia, Romance, Ficção científica e Suspense logo abaixo dos populares. Se algum gênero ainda não tem títulos classificados, aparece um estado de atualização, não filmes inventados. Outras categorias surgem quando houver títulos.
- Para obter **sinopses de verdade em pt-BR**, preencha `tmdbReadAccessToken` no JSON de chaves, além do Watchmode. A aplicação busca as traduções disponíveis no TMDB. Sem tradução disponível, suprime o texto inglês em vez de exibi-lo como se fosse português.
- As chaves continuam editáveis no JSON; Home continua sendo a primeira tela; `standalone` segue como padrão.

Leia também `ALTERACOES_v0.6.5.md`.

#### Atualização v0.6.6 — Aba Apps

- Nova aba **Apps** na navegação principal, com a biblioteca completa de sites e aplicativos Windows.
- Cada atalho pode ser **fixado/desafixado do Início**. A Home exibe somente os atalhos fixados.
- É possível **adicionar e editar** URLs e executáveis `.exe` e **remover atalhos personalizados** com confirmação. Os apps pré-instalados podem ser editados e desafixados, mas não são excluídos do catálogo base.
- Ao preencher a URL e sair do campo, o sistema tenta identificar o nome e a logo do próprio site. Se não tiver esquema, o endereço recebe `https://` automaticamente. O fallback visual permanece padronizado caso não exista favicon compatível.
- Aplicativos Windows tentam obter a imagem do ícone do `.exe` usando as APIs do Windows, sem executá-lo.
- Logos são normalizadas visualmente no mesmo espaço (`object-fit: contain`, 44 × 44 pixels) e salvas no SQLite como imagens `data:` limitadas a 256 KB. Não é necessário acessar o site a cada abertura.
- **Migração não destrutiva:** o banco antigo recebe `PinnedToHome`, `IconDataUrl` e `IconCheckedAtUnix`. Todos os atalhos já cadastrados continuam fixados no Início ao atualizar; novos atalhos começam desafixados, salvo se você selecionar a opção.
- O banco e as chaves continuam em `%LOCALAPPDATA%\\TVLauncher`, preservando a compatibilidade com as versões anteriores.

##### Como testar no Windows

Abra `TWLauncher.sln` no Visual Studio com .NET 10 e execute o projeto. Na aba **Apps**, clique em **Novo aplicativo**. Cole uma URL HTTPS, saia do campo para carregar a imagem e salvar, depois use a estrela **Fixar**. Volte ao **Início** e confira o atalho. Retorne à aba para **Editar**, **Desafixar** ou **Remover**. Teste também com um executável `.exe` instalado.

A compilação WPF e a extração de ícones do Windows devem ser validadas no equipamento de destino; estes passos não foram executados no ambiente Linux de preparação.


#### v0.6.7 — Escolher os streamings dos destaques

Na aba **Apps**, editar um site de streaming, marcar **Usar este streaming nos destaques**, escolher o provedor do **Watchmode Brasil** e salvar. **Fixar no Início** é uma opção independente. Só os provedores selecionados participam da consulta e do catálogo da Home. Caso não haja serviços marcados, a Home abre normalmente, sem recomendações.

A lista de provedores é buscada uma vez, armazenada em `%LOCALAPPDATA%\TVLauncher\watchmode-sources-br.json` e renovada no máximo mensalmente (há também um bloqueio de novas tentativas após falhas). O banco migra automaticamente os campos necessários e preserva os atalhos e as chaves existentes.

Se quiser usar **Mercado Play** e **Globoplay**, cadastre os sites, habilite-os nos destaques e selecione os nomes correspondentes na lista Watchmode, se fornecidos para a região BR. Não há cadastramento manual de filmes. A cobertura do provedor determina os títulos efetivamente disponíveis.

Para os detalhes, consulte `ALTERACOES_v0.6.7.md`.

##### v0.6.7.2 — Barra do navegador retrátil

A barra das janelas WebView2 aparece ao encostar o mouse nos **6 px superiores** da tela e recolhe ao retirar o ponteiro. Ela exibe TW Launcher, Início, nome do serviço, botões voltar/avançar/atualizar, clima e relógio. Em vídeos HTML em tela cheia a barra fica ocultada. O atalho `Ctrl + Alt + Home` continua disponível. Veja `ALTERACOES_v0.6.7.2.md`.


#### Ajuste v0.6.7.3 — Cabeçalho totalmente oculto

A barra do WebView2 não desenha mais faixa ou borda no topo quando recolhida. Mova o cursor até a borda superior para revelá-la, sem tirar área do site enquanto estiver oculta. Consulte `ALTERACOES_v0.6.7.3.md`.



---

## Inventário de origem

Os **23 arquivos** do ZIP estão incorporados nos dois documentos finais:

| Documento final | Conteúdo de origem |
|---|---|
| `README.md` | `DESIGN.md`, `REQUIREMENTS.md`, `INSTALACAO_E_ATUALIZACOES.md` e guia atualizado |
| `CHANGELOG.md` | `README.md` legado, todos os `ALTERACOES_v*.md` e todos os `QUALITY_CHECKS*.md` |

O registro foi preservado como documentação; a consolidação **não modifica** o código nem os scripts do TW Launcher. Consulte no README o ajuste de referência do script de publicação ao substituir os `.md` antigos por estes dois arquivos.
