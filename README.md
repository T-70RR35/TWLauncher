# TW Launcher

Aplicativo de entretenimento para Windows, pensado para uso em PCs conectados à TV. Combina uma Home cinematográfica, atalhos de aplicativos e sites, catálogo de filmes e séries, notícias e clima.

**Versão de referência:** `0.6.14` · **Interface:** Focus · **Estado da distribuição:** código-fonte preparado para instalador e atualizações automáticas; a geração do `Setup.exe` e o teste completo dependem de compilação no Windows.

## Tecnologias

- C# / .NET 10, WPF e Blazor Hybrid.
- Microsoft Edge WebView2 para navegação e acesso aos streamings.
- SQLite com Entity Framework Core para persistência local.
- Watchmode para descobrir títulos e consultar fontes de disponibilidade; TMDB para informações complementares e sinopses em português.
- RSS para notícias e Open-Meteo para clima.
- Velopack 1.2.161 para instalador e atualizações; GitHub Releases como origem das versões distribuídas.

## Funcionalidades

- **Início:** destaque cinematográfico, carrossel, trailers por interação, títulos populares e faixas por gênero.
- **Apps:** cadastrar sites e executáveis, obter ícones, fixar atalhos na Home e escolher quais serviços de streaming participam dos destaques.
- **Navegação:** janela WebView2 com sessão persistente; barra superior que aparece ao levar o mouse ao topo e desaparece completamente quando recolhida. Atalho `Ctrl + Alt + Home` para retornar ao Launcher.
- **Notícias:** leitura de feeds, imagens quando disponíveis e cache local.
- **Clima:** temperatura e condição resumidas no cabeçalho; área de detalhes.
- **Configurações:** credenciais do catálogo, sincronização e situação das atualizações.
- **Energia (v0.6.14):** botão no rodapé do menu lateral Focus para suspender, reiniciar ou desligar o computador, com confirmação antes de executar. Não força o encerramento dos aplicativos.

A Home abre mesmo sem chaves de API. O catálogo automático mostra somente títulos com disponibilidade e links efetivamente conhecidos; as integrações externas dependem de rede, cobertura e limites das APIs. A reprodução de conteúdos protegidos por DRM e o áudio automático dos trailers dependem também das restrições dos serviços e do WebView2.

## Estrutura do repositório

```text
TWLauncher/
├── TWLauncher.sln
├── TVLauncher.App/                    # Aplicação WPF + Blazor
├── Publicar-TWLauncher.ps1            # Publicação Windows/Velopack
├── .github/workflows/
│   └── publicar-twlauncher.yml        # Geração/publicação no GitHub
├── README.md                          # Este guia
└── CHANGELOG.md                       # Histórico consolidado
```

Os nomes internos `TVLauncher.App` e `%LOCALAPPDATA%\TVLauncher` foram mantidos para compatibilidade com versões anteriores. O nome apresentado ao usuário e o executável são **TW Launcher** e `TWLauncher.exe`.

## Compilar e executar no Windows

**Pré-requisitos:** Windows 10/11 x64, SDK .NET 10, Visual Studio com suporte a desenvolvimento desktop .NET/WPF e WebView2 Runtime.

1. Abra `TWLauncher.sln` no Visual Studio.
2. Restaure os pacotes NuGet.
3. Execute o projeto `TVLauncher.App` (`F5`).

Ou use o terminal PowerShell na raiz do projeto:

```powershell
dotnet restore .\TWLauncher.sln
dotnet build .\TWLauncher.sln -c Debug
dotnet run --project .\TVLauncher.App\TVLauncher.App.csproj
```

Executar com `F5` serve para desenvolvimento. **O mecanismo Velopack de atualização automática requer uma instalação feita pelo `Setup.exe`.**

## Dados locais e credenciais

Dados pessoais e sessões ficam fora da pasta de instalação:

| Caminho | Conteúdo |
| --- | --- |
| `%LOCALAPPDATA%\TVLauncher\tvlauncher.db` | Banco SQLite, aplicativos, destaques e cache |
| `%LOCALAPPDATA%\TVLauncher\api-keys.json` | Configurações e chaves das APIs |
| `%LOCALAPPDATA%\TVLauncher\WebView2Profile` | Cookies, logins e perfil do navegador |
| `%LOCALAPPDATA%\TVLauncher\update-source.json` | Origem de atualização opcional para testes |

Use `TVLauncher.App/api-keys.example.json` como referência para o arquivo de configuração local:

```json
{
  "watchmodeApiKey": "",
  "tmdbReadAccessToken": "",
  "catalogMode": "standalone",
  "sharedCatalogPath": "",
  "maxTitlesPerSync": 12,
  "monthlyWatchmodeRequestBudget": 500
}
```

O modo utilizado é **standalone**: cada instalação mantém seus próprios dados e consulta as APIs conforme sua configuração. Não publique chaves pessoais no GitHub, no instalador ou em chamados. Para atualizar o aplicativo, preserve toda a pasta `%LOCALAPPDATA%\TVLauncher`; nunca distribua uma cópia do banco ou do perfil WebView2 junto com o executável.

## Instalador e atualizações automáticas (v0.6.14)

O projeto contém **código e automação para gerar** o instalador Velopack e publicar versões no GitHub Releases. Isso **não significa que o instalador já tenha sido compilado ou publicado**.

### Publicar usando GitHub Actions

1. Coloque o projeto em um **repositório público** do GitHub, mantendo `.github/workflows/publicar-twlauncher.yml`.
2. Em **Actions**, execute **Gerar e publicar TW Launcher para Windows**, informando a versão, por exemplo `0.6.14`. Também é possível disparar com uma tag `v0.6.14`.
3. O workflow usa Windows, .NET 10 e Velopack para criar o `Setup.exe` e os pacotes de atualização em **Releases**.
4. Instale o `Setup.exe` para habilitar as verificações automáticas. Para a próxima entrega, publique uma versão maior, como `0.6.15`.

### Publicar pelo PowerShell

No Windows, com o SDK .NET 10:

```powershell
dotnet tool install --global vpk --version 1.2.161
.\Publicar-TWLauncher.ps1 -Version 0.6.13 -RepositoryUrl https://github.com/SEU_USUARIO/SEU_REPOSITORIO
```

O script gera os artefatos em `Releases/`. Para enviá-los, ele aceita os parâmetros `-Upload -GitHubToken $env:GH_TOKEN`; nunca armazene tokens no repositório.

**Funcionamento previsto após a instalação:** procurar atualizações ao iniciar e a cada seis horas, baixar em segundo plano e aplicar ao fechar ou quando o usuário escolher **Reiniciar e instalar**. A instalação deve ser testada com duas versões consecutivas em um Windows real antes de distribuição.

### Documentação consolidada

A documentação foi reunida em dois arquivos: `README.md` (guia de uso, requisitos e publicação) e `CHANGELOG.md` (histórico, alterações e testes). O script `Publicar-TWLauncher.ps1` já utiliza o `CHANGELOG.md` como notas da versão (`--releaseNotes`); não há dependência dos antigos arquivos `ALTERACOES_*.md`, `REQUIREMENTS.md` ou `INSTALACAO_E_ATUALIZACOES.md`.

## Limitações e verificações pendentes

- Não há, nesta entrega de documentação, prova de compilação WPF nem de geração real do `Setup.exe`.
- O fluxo completo de atualização, instalação, troca de versão e preservação de dados ainda deve ser validado no Windows.
- Logins e reprodução de cada serviço de streaming dependem das políticas do próprio serviço e do WebView2.
- Consumo e cobertura de Watchmode/TMDB variam conforme as chaves e os limites contratados.
- Recomenda-se assinatura digital antes de distribuição pública para reduzir alertas do Windows SmartScreen; ela não está configurada.
- Mudanças futuras no esquema SQLite devem preservar os dados existentes e prever backup/migração.

O histórico das versões anteriores está em [`CHANGELOG.md`](CHANGELOG.md).


---

## Documentação técnica integral reunida

As seções a seguir incorporam **integralmente** os documentos de requisitos, design e instalação do pacote de 23 arquivos. Elas servem como referência técnica e histórica, sem exigir arquivos `.md` separados.

> **Atenção às versões:** `DESIGN.md` descreve o design da família **v0.6**, anterior ao **Focus v0.6.11**, que é o design de referência atual. `REQUIREMENTS.md` contém decisões e planos de versões anteriores (por exemplo, a rotação de 20 segundos da v0.3, posteriormente alterada para 10 segundos na v0.6.4). Para o comportamento vigente, prevalecem as versões mais recentes descritas no [CHANGELOG.md](CHANGELOG.md). Nenhum material do ZIP comprova a execução real do instalador em Windows.



---

## Instalação, distribuição e atualizações — especificação v0.6.13



> Conteúdo consolidado do antigo documento `INSTALACAO_E_ATUALIZACOES.md`.



### TW Launcher 0.6.13 — Instalador e atualização automática

#### O que está implementado

A partir da v0.6.13, o TW Launcher contém o SDK **Velopack 1.2.161**. Quando aberto a partir de uma instalação Velopack, ele:

1. Verifica atualizações ao abrir a Home e depois a cada **6 horas**.
2. Baixa uma versão mais recente em segundo plano, priorizando pacote diferencial quando disponível.
3. Mostra versão, situação, progresso e botão manual na aba **Configurações**.
4. Aplica a versão baixada **depois que o programa for fechado normalmente**, ou se o usuário escolher **Reiniciar e instalar**. Não interrompe um streaming durante o uso.
5. O instalador verifica o **WebView2 Runtime**. O app .NET é publicado em modo **self-contained**.

Os dados pessoais continuam em `%LOCALAPPDATA%\TVLauncher\` (incluindo `tvlauncher.db`, `api-keys.json`, credenciais protegidas DPAPI e `WebView2Profile`). **Nunca colocar esses dados na pasta de publicação do app.** O instalador Velopack usa outra pasta para os executáveis, como `%LOCALAPPDATA%\TWLauncher.Desktop\current\`.

#### A primeira publicação (GitHub público)

Você precisa de um repositório no GitHub, acessível publicamente para os usuários, e de subir estes arquivos-fonte nele. Não há URL fixa inventada no projeto.

- Crie um repositório **público** para o TW Launcher e envie o conteúdo desta pasta (inclusive `.github/workflows/publicar-twlauncher.yml`).
- No GitHub, abra **Actions** e execute o workflow **Gerar e publicar TW Launcher para Windows** pelo botão **Run workflow**, com a versão `0.6.13`.
- O workflow prepara o `.exe` via .NET 10 no Windows, executa o `vpk pack` e publica os arquivos em **Releases** usando o `GITHUB_TOKEN` automático.
- Na página **Releases** do próprio repositório, baixe o arquivo que termina em `Setup.exe` e instale no Windows.
- Para cada versão seguinte, altere o código e publique uma nova versão com o workflow (ex. `0.6.14`). Os usuários instalados receberão o update. Use versão sempre crescente.

**Importante:** o `workflow_dispatch` também pode criar o release sem precisar criar tag manualmente; o upload do Velopack usa `v0.6.13` como tag. No upload automático o token precisa da permissão `contents: write` (declarada no workflow). Se houver regras do repositório que impeçam tags, revise as permissões.

##### Publicação alternativa diretamente do Windows

Na pasta do projeto, com Windows + .NET 10 SDK:

```powershell
# Instale a ferramenta uma única vez
dotnet tool install --global vpk --version 1.2.161

# Gera o Setup e os pacotes em .\Releases
.\Publicar-TWLauncher.ps1 -Version 0.6.13 -RepositoryUrl https://github.com/SEU_USUARIO/SEU_REPOSITORIO
```

Para publicar diretamente, acrescente `-Upload -GitHubToken $env:GH_TOKEN` usando token **apenas localmente ou pelo GitHub Actions**, nunca versionado dentro do projeto. A ferramenta pode falhar ao baixar a versão anterior na primeira publicação; nesse caso cria o pacote completo, sem diferencial.

##### Testar sem publicar para todos

Você pode executar o projeto pelo Visual Studio para programar normalmente, mas o auto-update **só funciona quando o executável foi instalado pelo Setup Velopack**, não no F5. Depois da primeira publicação, para confirmar o mecanismo: instale a v0.6.13, publique v0.6.14 e verifique na aba Configurações da instalação antiga.

##### Cuidados

- `api-keys.json` do desenvolvedor **não é empacotado**; cada usuário configura o arquivo dentro de `%LOCALAPPDATA%\TVLauncher`. O arquivo `api-keys.example.json` acompanha o projeto.
- Apenas release público pode ser consumido pelo aplicativo do jeito que está: o cliente não guarda token do GitHub, para não expor credenciais privadas.
- A assinatura digital de código é recomendada antes da distribuição pública (reduz alertas do SmartScreen). **Não está configurada**: requer certificado.
- Não houve publicação em GitHub, compilação do WPF ou geração de `Setup.exe` neste ambiente Linux; o workflow no Windows produz esses artefatos. Teste de ponta a ponta é necessário.
- Mudanças futuras de esquema SQLite devem usar migrações/backups, sem apagar o banco já existente.



---

## Requisitos funcionais — decisões originais e evolução



> Conteúdo consolidado do antigo documento `REQUIREMENTS.md`.



### Requisitos funcionais — TW Launcher

#### Decisões acordadas

1. Windows continua instalado e acessível. A Home abre em tela cheia, sem substituir o Explorer.
2. Mouse e teclado são os dispositivos principais; suporte a controle remoto é futuro.
3. **Três abas**: Início (streamings), Notícias e Clima.
4. Na Home, somente **ícone do clima, temperatura e horário** resumidos no cabeçalho.
5. Banner superior **maior e cinematográfico**; trailers acionados por hover ou foco por Tab, **com áudio quando autorizado pelo navegador**.
6. Abaixo do banner, **Seus aplicativos**, depois fileiras de filmes/séries que seguem verticalmente.
7. Usuário pode cadastrar sites e programas Windows adicionais.
8. Ao selecionar um filme elegível, abrir o **link direto** no serviço, sem obrigar nova pesquisa; clicar Assistir não garante autoplay do streaming.
9. **Somente filmes e séries com endereço direto conhecido** entram no catálogo. Não inventar catálogo nem link.
10. **Home retorna imediatamente** e fecha a sessão de navegador/aplicativo que o Launcher criou, sem encerrar indiscriminadamente processos de terceiros.
11. Autenticação dos serviços Web persistida pelo WebView2, separada do SQLite.
12. SQLite local para armazenar aplicativos e destaques; evolução posterior de catálogo e preferências.

#### Incluído no protótipo 0.1

Home, cadastro SQLite de apps e destaques, vídeo incorporado silenciado, WebView2 em janela de reprodução separada, notícias RSS básicas, clima Open-Meteo básico, atalhos e retorno ao Windows.

#### Validações abertas

- Teste de DRM e login de Netflix, Prime Video e serviços relacionados.
- Player YouTube incorporado no contexto do Blazor Hybrid, política de origem e possíveis bloqueios de autoplay.
- Captura do atalho global no Windows em execução e fullscreen.
- Troca segura entre janela original e janela WebView2, sem áudio/sessões órfãs.
- Instabilidade ou mudanças de RSS/APIs e política de atribuição.

#### Evolução posterior (parcialmente implementada)

Integração TMDB, disponibilidade por país, links diretos verificados via provedor apropriado, descoberta de "em alta", filtros de notícias, cidade configurável, trailer oficial de cada título, imagens licenciadas, gerenciador de aplicativos completo, instalador e atualização.

#### Destaques dinâmicos v0.3

- Quando integrações estiverem configuradas, usar tendências recentes de filmes e séries, com trailer oficial incorporável e URL de serviço reconhecido disponível no Brasil.
- Mostrar apenas serviços cadastrados. Não inferir a assinatura do usuário pelo simples cadastro.
- Trailer parado por padrão; reprodução com tentativa de áudio somente ao passar o mouse sobre o banner/seletor ou focar via Tab. Ao retirar o foco/hover, interromper vídeo e áudio. Quando o navegador bloquear autoplay sonoro, disponibilizar um ícone clicável de som.
- Rotação padrão após 20 segundos, priorizando títulos pouco exibidos e sem reapresentar um título já mostrado na mesma visita à Home.
- Salvar a chave do título e horário de exibição no SQLite, para diversificar também entre reinicializações.
- Quando o catálogo for esgotado, interromper a rotação automática em vez de repetir o mesmo conjunto várias vezes; seleção manual continua possível.
- Atualizar as tendências em até 24 h em operação normal, com opção de atualização manual.
- Não listar como "em alta" os dois curtas de demonstração da v0.2.
- Nunca criar URLs diretas, disponibilidade ou tendências fictícias. Falhas das APIs não impedem o Launcher de funcionar.

#### Catálogo automático v0.4

- **Nenhum título, trailer ou link precisa de cadastro manual.**
- Configurar credencial Watchmode uma única vez na interface; a chave é protegida por DPAPI para o usuário do Windows.
- TMDB opcional para ampliar imagens e trailers, não mais obrigatório para descobrir conteúdos.
- Consultar automaticamente conteúdos populares no Brasil, com URLs Web HTTPS reconhecidas dos provedores e acesso por assinatura/gratuito.
- Manter configurações manuais somente em Opções avançadas.
- Não exibir os curtas de amostra no catálogo automático, mesmo se existirem no SQLite antigo.
- A cada sincronização programada (72 horas), variar os candidatos dentro da lista de populares, dando preferência no carrossel aos menos exibidos na última semana.
- Mostrar estado de erro/ausência de dados quando a integração não estiver configurada, respeitando o fato de que APIs comerciais exigem credenciais e cota.
- O Watchmode indica popularidade da base, não um ranking oficial de visualizações de cada plataforma.

#### v0.5 — Catálogo distribuído e inicialização segura

1. **A rota inicial deve ser sempre `home`.** A inexistência ou corrupção das credenciais NÃO pode abrir a tela Configurações automaticamente.
2. O arquivo `TVLauncher.App/api-keys.json` pode ser preenchido antes de compilar. Na primeira execução é importado para `%LOCALAPPDATA%/TVLauncher/api-keys.json`, que passa a ser a versão prioritária e editável.
3. API Watchmode: usada somente no publicador ou standalone para links diretos; quota local mensal limitada por configuração, no SQLite, consumida antes de cada requisição.
4. API TMDB: segunda fonte de descoberta de tendências e metadados; não inventar deep links quando o Watchmode estiver indisponível.
5. `catalogMode=publisher`: consulta e escreve um catálogo JSON sem chaves numa pasta de rede criada pelo usuário.
6. `catalogMode=reader`: lê exclusivamente o JSON na rede, sem chamar APIs externas de catálogo. Usa cache local quando a pasta compartilhada não estiver disponível.
7. `catalogMode=standalone`: comportamento individual sem compartilhamento; padrão para instalações existentes.
8. A sessão da Netflix/YouTube (cookies, autenticação) nunca pode ser exportada para outros dispositivos.
9. Capas/trailers só aparecem se houver links diretos reconhecidos e validados pelo resolver; links vindos do TMDB isoladamente não contam como deep links.
10. A UI continua funcional sem internet, sem API, sem catálogo e com JSON malformado; exibe aplicativos sempre.
11. Respeitar prazo de cache de dados do Watchmode no plano gratuito (máximo de 30 dias); a implementação usa validade máxima de 14 dias para o catálogo automático.


##### Sequência e classificação (v0.6.4)

- O sorteio ocorre uma vez a cada entrada na Home, sem alterar a sequência no avanço/retorno ou em atualização assíncrona.
- O número do carrossel deve corresponder exatamente ao índice selecionado da fila.
- Avanço automático não reinicia depois do último; avanço manual permite retornar ao primeiro.
- Gêneros são alimentados somente por metadados reais, sem novas consultas por cada fileira.
- O app mantém a Home como primeira tela, independentemente das chaves de API.



---

## Design e identidade visual — registro histórico anterior ao Focus



> Conteúdo consolidado do antigo documento `DESIGN.md`.



### DESIGN — TW Launcher v0.6

#### Direção visual

Interface cinematográfica e sóbria, com hierarquia de conteúdo inspirada no padrão de streaming para TV, sem copiar marca/identidade de nenhum sistema específico. Um único destaque grande no topo, seguido por apps e fileiras horizontais.

#### Cores

| Token | Cor | Uso |
|--|--|--|
| Background | `#090D15` | Superfície principal |
| Surface | `#151D2A` | Contêiner e cards |
| Text | `#F5F7FB` | Texto principal |
| Muted | `#9AA9BB` | Apoio |
| Accent | `#85B5F9` | Destaque, indicador ativo |
| Border | `rgba(238,245,255,.11)` | Separações discretas |

#### Tipografia

`Manrope` com fallback `Segoe UI`. Em ambientes sem internet, o fallback mantém a interface legível. Títulos de destaque: 29–67 px de acordo com o comprimento e a largura; seções: 21–29 px; texto descritivo: 13–16 px.

#### Cabeçalho

Três abas: **Início, Notícias, Clima**. À direita: **ícone do tempo + temperatura + horário**, com botão de configurações. Clima não ocupa card grande na Home.

#### Hero

- Região ampla: altura mínima adaptativa, permitindo expansão quando o conteúdo exigir; sem cortes de títulos.
- Esquerda: nome do título, categoria, sinopse, CTA "Assistir agora", anterior/próximo.
- Direita: player oficial YouTube visível e **não encoberto** por overlays; silenciado por padrão.
- O iframe só existe quando a seção está ao menos 50% visível. Se não houver trailer, exibe imagem estática; bloqueios externos do YouTube ainda precisam ser tratados/testados no WebView2.
- 20 segundos por destaque, sem repetição automática do mesmo título na visita. Pausar troca na interação com o banner.
- Um player por vez; troca de aba remove o iframe.

#### Apps e filmes

- Aplicativos em cards horizontais grandes; CTA Adicionar app sempre visível.
- Conteúdos em fileira horizontal com rolagem, título e provedor.
- Mostrar apenas títulos com link direto conhecido e considerado válido; cadastro manual fica como opção avançada.

#### Navegação e a11y

- Mouse e teclado inicialmente; `button` semântico, `aria-label`, foco visível.
- `Ctrl + Alt + Home`: sair do navegador e retornar à Home.
- `prefers-reduced-motion` desativa animações não essenciais.
- Contrastes fortes e alvos grandes para uso em TV; testar em Full HD, 4K e 125–150% de escala do Windows.

#### Referências

Google TV e Fire TV como **hierarquia e experiência**, não para copiar telas. Player do YouTube fica desobstruído, conforme suas condições para players incorporados.

#### Aba Notícias — 0.2

- Cabeçalho editorial compacto: título e descrição, última atualização + ação manual.
- Filtros discretos em pills, com foco visível e filtro selecionado contrastante.
- Card de destaque em duas colunas: imagem 16:9 à esquerda e headline/resumo/metadados à direita.
- Cards em grade responsiva (3 colunas em Full HD, 2 em telas médias, 1 em pequenas).
- Imagens reais quando disponibilizadas pelos veículos, preenchimento `object-fit: cover`; fallback neutro para links sem capa.
- Textos e fontes identificados, indicação de atualização e navegação para a notícia original.
- A estrutura original da Home/Clima foi preservada.

#### v0.4 — Configuração invisível no uso normal

A Home prioriza o conteúdo do catálogo automático; quando ele não estiver configurado exibe um convite único para conectar a fonte. A tela de Configurações mostra um painel para conectar Watchmode (obrigatório) e TMDB (opcional). Formulários manuais ficam recolhidos por padrão em Opções avançadas. Não destacar curtas de demonstração como recomendações.

#### v0.5 — Estado sem catálogo

A primeira tela SEMPRE é Home. Quando não houver APIs configuradas, mostrar o banner estático "Seu entretenimento começa aqui." e os aplicativos normalmente; não deslocar o usuário para uma tela de login/configuração, não usar modal obrigatório e não simular recomendações. Configurações continuam acessíveis pela engrenagem no cabeçalho.

#### v0.6 — Destaque premium (decisão atual)

- Imagem do título ocupa todo o fundo do hero, com vinheta funcional para garantir legibilidade.
- Tipografia equilibrada e proporcional ao comprimento do título; sem `line-clamp` no nome do filme ou série.
- Sinopse limitada a três linhas para preservar a hierarquia; o título nunca é truncado.
- Ação Assistir ao lado das setas de navegação; estados de foco visíveis.
- Trailer em enquadramento 16:9 e com legendas/controles oficiais do player preservados. Nunca encobrir controles do YouTube com overlays artificiais.
- Marcadores discretos e índice numérico fora do painel cinematográfico.
- 1920×1080 e 1366×768: composição lado a lado; em larguras menores a composição se empilha e a página rola naturalmente.
- A prévia local utiliza arte ilustrativa gerada apenas para demonstração; no aplicativo real, o fundo usa o URL da fonte de catálogo.
- `catalogMode` permanece `standalone` por padrão, com configuração de credenciais via `api-keys.json`. A Home não exige API para abrir.


##### Home v0.6.4

Carrossel numerado com pontos fixos relativos à fila sorteada na entrada da Home; indicadores compactos scrolláveis para catálogos grandes. Fileiras horizontais organizadas por gêneros de metadados reais abaixo de **Populares para assistir**.



---

## Cobertura da consolidação

O arquivo original reunia **23 documentos Markdown**. Este `README.md` incorpora as instruções ativas, as decisões de design, os requisitos e o manual de instalação. O [`CHANGELOG.md`](CHANGELOG.md) concentra as alterações por versão, as verificações realizadas, as pendências e o `README.md` histórico do pacote. Assim os dois documentos cobrem o conteúdo do ZIP sem exigir os 23 arquivos separados.

## Opções de energia (v0.6.14)

O botão **Energia** fica abaixo de **Ajustes** no menu lateral Focus. Selecione **Suspender**, **Reiniciar** ou **Desligar**, revise a mensagem e confirme. Cancelar não executa nenhuma operação.

- **Suspender:** usa a função nativa `SetSuspendState(false, false, false)` do Windows (`powrprof.dll`). Disponibilidade sujeita às configurações e permissões de energia do computador.
- **Reiniciar / Desligar:** usa `shutdown.exe /r /t 0` ou `shutdown.exe /s /t 0`. Não utiliza `/f` para forçar o encerramento de programas. O Windows ainda poderá bloquear a operação por trabalho não salvo.
- **Cuidados:** salve arquivos abertos antes de confirmar. Teste as três opções em um computador Windows sem trabalho pendente; elas afetam o computador inteiro, e não somente o TW Launcher.
