# SDD-09 - Frontend Angular

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-20
> Gate A: aprovado em 2026-08-20
> Dependências: SDD-01, SDD-03, SDD-04, SDD-05, SDD-06, SDD-07, SDD-08, ADR-003, ADR-009, ADR-014, ADR-015 e ADR-016

---

## 1. Objetivo

Especificar a aplicação Angular que permite autenticar o usuário, administrar produtos, criar e editar invoices, acompanhar a emissão distribuída e acionar a impressão pelo navegador.

A interface deve ser minimalista, responsiva e acessível, com identidade visual verde e branca, sem ocultar estados relevantes do domínio ou transformar o frontend em fonte de regras de negócio.

---

## 2. Requisitos rastreados

- `OBR-001` a `OBR-015` e `OBR-021`, no limite da experiência do usuário;
- `OPA-001` e `OPA-002`, no feedback e na preservação dos contratos;
- `DIF-002`, `DIF-003`, `DIF-005`, `DIF-008`, `DIF-009` e `DIF-010`;
- `QLT-001`, `QLT-003`, `QLT-004` e `QLT-006` a `QLT-008`;
- `APR-001` a `APR-007` e `APR-009`.

---

## 3. Escopo previsto

- identidade visual, tema e tokens semânticos;
- shell, navegação e responsividade;
- arquitetura Angular standalone por feature;
- sessão, login, logout, interceptor e guards;
- cadastro, listagem e consulta de produtos;
- criação, listagem, detalhe e itens de invoice;
- tratamento de ETag e Idempotency-Key;
- emissão, polling, atraso, rejeição e intervenção;
- visualização imprimível e `window.print()`;
- estados vazios, carregamento, erro e confirmação;
- acessibilidade, feedback e prevenção de ações duplicadas;
- integração exclusiva com o Gateway;
- testes de componentes, serviços, navegação e fluxos críticos.

---

## 4. Fora do escopo

- acesso direto a Identity, Inventory, Billing ou RabbitMQ;
- regras de domínio como fonte definitiva;
- renderização server-side;
- PWA, funcionamento offline ou sincronização posterior;
- refresh token ou revogação de sessão;
- WebSocket, SignalR ou server-sent events;
- geração ou armazenamento de PDF;
- confirmação de impressão física;
- dashboard analítico, relatórios ou gráficos sem requisito;
- fornecedores, compras, entradas e ajustes de estoque;
- edição ou exclusão de Product;
- cancelamento, reabertura ou reimpressão funcional de invoice;
- biblioteca visual adicional a Angular Material;
- fontes, ícones ou recursos dependentes de CDN.

---

## 5. Blocos de decisão

1. identidade visual, shell, navegação e responsividade;
2. arquitetura Angular, estado, RxJS e ciclo de vida;
3. sessão, login, guards e integração autenticada;
4. telas e fluxos de Product;
5. telas, itens e concorrência de Invoice;
6. emissão, polling, feedback e impressão;
7. erros, acessibilidade, configuração e observabilidade;
8. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- Angular standalone conforme setup aprovado;
- Angular Material é a única biblioteca completa de componentes;
- tema customizado e SCSS próprio são obrigatórios;
- componentes importam somente módulos Material realmente utilizados;
- frontend acessa exclusivamente o API Gateway;
- operações funcionais usam JWT bearer;
- guard e ocultação de botão melhoram a experiência, mas não substituem o backend;
- `401` encerra sessão local e conduz ao login;
- `403` informa falta de permissão sem apagar sessão válida;
- ETag é opaco e enviado por `If-Match` nas mutações aprovadas;
- `PrintInvoice` usa Idempotency-Key e retorna processo assíncrono;
- acompanhamento ocorre por polling HTTP, sem canal em tempo real;
- atraso é informativo e não decide falha;
- impressão ocorre em HTML por `window.print()` somente depois de `Completed`;
- não existe nova operação de negócio para reimpressão;
- testes automatizados comprovam o acionamento do diálogo, não impressão física;
- recursos visuais funcionam sem internet.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Identidade visual, shell, navegação e responsividade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Direção visual

A interface é minimalista, predominantemente branca, com fundo neutro claro e verde concentrado em identidade, seleção, foco e ações principais. Estados de erro, atraso, processamento e intervenção usam tokens próprios e nunca dependem somente de cor.

| Token | Cor | Uso |
|---|---|---|
| `primary-700` | `#166534` | Botões principais e identidade |
| `primary-600` | `#15803D` | Hover e elementos ativos |
| `primary-500` | `#22C55E` | Destaques controlados |
| `primary-100` | `#DCFCE7` | Seleção e chips leves |
| `primary-50` | `#F0FDF4` | Fundo informativo sutil |
| `surface` | `#FFFFFF` | Cards, tabelas e formulários |
| `background` | `#F6F8F6` | Fundo geral |
| `text-primary` | `#17211B` | Texto principal |
| `text-secondary` | `#607066` | Texto auxiliar |
| `border` | `#DCE5DF` | Divisores e contornos |
| `danger` | `#B42318` | Erros e rejeições |
| `warning` | `#B54708` | Atraso e atenção |
| `info` | `#175CD3` | Processamento e informação |

#### Linguagem e espaçamento

Não são usados gradientes, glassmorphism, sombras fortes, bordas excessivas, animações ornamentais, cards indiscriminados ou ícones sem rótulo.

O layout usa hierarquia curta, uma ação principal evidente, bordas discretas, raio de 8 px, sombras leves apenas para separar camadas e escala de espaçamento `4, 8, 12, 16, 24, 32, 48 px`.

#### Tipografia

```text
Inter, ui-sans-serif, system-ui, -apple-system,
BlinkMacSystemFont, "Segoe UI", sans-serif
```

Inter somente é usada se empacotada localmente. A aplicação funciona com fontes do sistema e não depende de CDN.

| Elemento | Tamanho |
|---|---:|
| Título de página | 24 px |
| Título de seção | 18 px |
| Corpo | 14 a 16 px |
| Texto auxiliar | 13 a 14 px |
| Rótulo e tabela | 14 px |

#### Login

A tela anônima usa fundo claro e painel branco central com largura máxima aproximada de 400 px. Contém somente marca textual Korp ERP, título, e-mail, senha, erro aplicável e botão Entrar, sem shell, ilustração ou marketing fictício.

#### Shell autenticado

Em desktop, barra superior branca apresenta marca, usuário e logout. Navegação lateral branca e estreita apresenta Produtos e Notas fiscais; seleção usa fundo verde-claro e indicador verde. O conteúdo fica sobre fundo neutro com largura controlada.

Não existe dashboard vazio. A rota inicial autenticada é Produtos, primeiro cadastro necessário ao fluxo.

Breadcrumb aparece somente quando adiciona contexto real, como `Notas fiscais / Nota 42`.

#### Responsividade

```text
compact:  abaixo de 600 px
medium:   600 a 959 px
expanded: 960 px ou mais
```

- expanded: navegação lateral, tabelas e ações no cabeçalho;
- medium: navegação recolhível e redução de colunas secundárias;
- compact: drawer, uma coluna, tabelas convertidas em listas ou cards e ações adaptadas;
- o fluxo principal não exige rolagem horizontal.

#### Componentes recorrentes

Componentes próprios representam conceitos reais, como `InvoiceStatusChip`, `IssuanceProgress`, `StockBalanceIndicator`, `PageHeader`, `EmptyState` e `ConfirmationDialog`. Não existe wrapper próprio para cada componente Material.

#### Acessibilidade visual

- contraste mínimo WCAG AA;
- foco visível com contorno além da cor;
- alvos interativos de pelo menos 44 por 44 px quando aplicável;
- botões importantes possuem texto;
- ícones decorativos não são anunciados;
- erro e loading possuem texto ou semântica adicional;
- `prefers-reduced-motion` reduz transições;
- zoom de 200% preserva o fluxo principal.

Ícones necessários são SVGs locais ou recursos empacotados, nunca fontes ou serviços de CDN.

### 7.2 Bloco 2 - Arquitetura Angular, estado, RxJS e ciclos de vida

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Organização

```text
src/app/
|- core/
|  |- auth/
|  |- http/
|  |- layout/
|  |- routing/
|  `- configuration/
|- shared/
|  |- ui/
|  |- models/
|  `- utilities/
|- features/
|  |- login/
|  |- products/
|  `- invoices/
|- app.config.ts
`- app.routes.ts
```

`core` contém serviços singleton e infraestrutura transversal; `shared` recebe somente elementos realmente reutilizados; `features` possui telas, rotas, integração e comportamento de cada área.

O frontend não replica as quatro camadas do backend nem cria pasta genérica de helpers sem responsabilidade real.

#### Standalone e rotas

Componentes, rotas e providers seguem o modelo standalone. Features autenticadas usam lazy loading e cada componente importa somente os módulos Material utilizados.

```text
/login
/products
/products/new
/products/:productId
/invoices
/invoices/new
/invoices/:invoiceId
/invoices/:invoiceId/print-view
```

A visualização de impressão não inicia nova emissão e somente apresenta invoice concluída.

#### Estrutura de feature

Uma feature pode separar `data-access`, `pages` e `ui`. Páginas coordenam rota e estado visual; componentes de UI recebem dados e emitem intenções; serviços HTTP conhecem endpoints e DTOs, mas não manipulam DOM.

Contratos TypeScript são tipados manualmente conforme SDD-03, pois a superfície é pequena. Não existem gerador OpenAPI, entidades C# espelhadas, pacote compartilhado frontend/backend, DTO universal ou uso de `any` para contornar contrato.

Datas chegam como ISO strings e são convertidas somente para apresentação. UUID e ETag permanecem opacos.

#### Estado e Signals

Signals representam estado síncrono de interface, incluindo carregamento, erro, dados apresentados, paginação, menu, sessão, ações habilitadas e valores derivados por `computed`.

Não serão adotados NgRx, NGXS, Akita ou outro store. Somente sessão e configuração pública possuem escopo global; estado de produtos, invoices, formulários e processos permanece na feature ou página.

#### RxJS

RxJS atende `HttpClient`, parâmetros de rota, polling, cancelamento, chamadas dependentes, headers e prevenção de submissão concorrente. Um signal simples não é substituído por `Subject` genérico.

Parâmetros usam composição tipada e `switchMap`, cancelando consulta anterior. Submissões desabilitam a ação, impedem concorrência, usam `finalize` para restaurar estado e não criam subscriptions aninhadas.

#### Ciclos de vida

- construção e `inject()` obtêm dependências, sem HTTP ou DOM;
- `ngOnInit` coordena apenas o que não puder ser expresso declarativamente;
- subscriptions imperativas usam `takeUntilDestroyed()`;
- timers, polling e observers encerram ao sair da página;
- operação dependente do DOM usa mecanismo de pós-renderização, não `setTimeout` arbitrário;
- componentes usam `OnPush` quando aplicável e atualizações imutáveis.

#### Formulários e estados

Reactive Forms tipados refletem o contrato, validam ao sair do campo e ao submeter, apresentam erros próximos ao controle e direcionam foco ao primeiro campo inválido. Erros de validação do backend são mapeados quando possível, sem presumir que a validação visual substitui a API.

Páginas representam explicitamente `initial`, `loading`, `loaded`, `empty`, `submitting` e `error`. Estados distribuídos não são reduzidos a loading genérico.

#### Concorrência visual

Botões de mutação permanecem desabilitados durante o request correspondente. Essa proteção melhora a experiência, mas não substitui Idempotency-Key, ETag, constraints ou idempotência do backend.

### 7.3 Bloco 3 - Sessão, login, interceptors e guards

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Persistência da sessão

A sessão usa `sessionStorage` com `accessToken`, `expiresAtUtc`, ID, e-mail e papéis do usuário. Não usa `localStorage`, cookie ou IndexedDB.

Reload na mesma aba preserva a sessão; fechamento da aba encerra sua persistência; abas não sincronizam automaticamente. Logout e `401` removem o registro. Não existe refresh token.

Como `sessionStorage` é acessível a JavaScript, `innerHTML` funcional, `bypassSecurityTrust*`, scripts externos e CDN são proibidos. Token não aparece em log ou mensagem.

#### Restauração

No startup, a aplicação valida estrutura e `expiresAtUtc`, descarta registro inválido ou expirado e restaura estado em memória. Não decodifica JWT para autorização; identidade e papéis vêm do response do login. A verificação local melhora a experiência, mas o backend decide validade.

#### Login

O Reactive Form possui e-mail com `autocomplete="username"` e senha com `autocomplete="current-password"`. Ambos são obrigatórios; e-mail recebe validação estrutural; Enter submete quando válido; botão fica indisponível durante envio.

Credenciais inválidas produzem mensagem genérica. Senha não permanece depois da tentativa. Sucesso inicia a sessão e navega ao retorno seguro ou `/products`.

#### Return URL

Somente caminho interno iniciado por `/` é aceito. URL absoluta, protocolo, host, prefixo `//`, retorno ao login ou formato inválido usa `/products`, impedindo open redirect.

#### AuthSessionService

Serviço singleton expõe signals somente leitura `currentUser`, `isAuthenticated`, `isAdmin` e `expiresAtUtc`. Somente seus métodos privados de coordenação escrevem no storage e alteram a sessão.

#### Bearer interceptor

Adiciona `Authorization: Bearer` apenas a rotas funcionais relativas em `/api/v1`, exceto login. Não envia token para OpenAPI, health, assets ou URL externa e não transforma ou renova o bearer.

#### Correlation interceptor

Cada request funcional recebe `X-Correlation-ID` criado com `crypto.randomUUID()`. A resposta efetiva pode apoiar diagnóstico, sem exibir UUID rotineiramente ao usuário. Navegador incompatível falha explicitamente; não existe UUID inseguro com `Math.random()`.

#### Respostas 401 e 403

`401` em rota protegida limpa sessão, cancela estado autenticado, preserva return URL segura, navega uma vez ao login e informa sessão expirada ou inválida. Chamadas concorrentes não duplicam navegação ou aviso.

`401 invalid_credentials` do login permanece no formulário. `403` não limpa sessão ou redireciona ao login; apresenta falta de permissão sem revelar a política interna.

#### Guards e papel

`authGuard` exige sessão local não expirada. `adminGuard` exige adicionalmente papel canônico `Admin`. Guards retornam redirecionamento declarativo, sem navegação imperativa durante avaliação.

Usuário autenticado acessa consultas; rotas e ações de mutação exigem Admin. O login redireciona sessão já autenticada a Produtos. Ocultação visual não substitui a autorização das APIs.

#### Logout

Logout limpa storage e memória, cancela fluxos autenticados e navega ao login. Não chama backend, pois não existem revogação ou refresh. Token emitido permanece válido até expirar, limitação documentada na entrega.

### 7.4 Bloco 4 - Telas e fluxos de Product

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Rotas

```text
/products
/products/new
/products/:productId
```

A rota individual utiliza o `GET` já aprovado e foi acrescentada ao bloco arquitetural. Não cria nova operação.

#### Listagem

`/products` exige usuário autenticado. O cabeçalho apresenta título, descrição curta e `Novo produto` apenas para Admin.

Em expanded, a tabela possui código, descrição, saldo, criação e ação de detalhe. Em compact, cada registro vira item de lista com os mesmos dados essenciais, sem rolagem horizontal.

Ordenação permanece a do backend, por código e ID. A interface não oferece ordenação, filtro ou pesquisa inexistentes na API.

#### Saldo

`StockBalanceIndicator` exibe valor inteiro e texto acessível: zero como `Sem estoque`, um como `1 unidade em estoque` e demais valores no plural. Não inventa limite de estoque baixo. Cor nunca substitui valor e texto.

#### Paginação

`pageNumber=1` e `pageSize=20` são padrões. Opções são 10, 20, 50 e 100. Os valores ficam na query string; mudar tamanho retorna à primeira página. Página além do final permite voltar sem apresentar falha inexistente.

#### Estados da lista

Loading preserva estrutura e usa indicador discreto. Atualização de página pode manter dados anteriores identificados como atualizando. Empty informa ausência; Admin recebe CTA de cadastro e leitor apenas a explicação. Error preserva dados anteriores quando existentes e oferece retry iniciado pelo usuário.

#### Cadastro

`/products/new` exige Admin e usa Reactive Form tipado:

| Campo | Regra visual |
|---|---|
| Código | Obrigatório, máximo 50, letras, números, ponto, hífen e underscore |
| Descrição | Obrigatória, máximo 200 |
| Saldo inicial | Inteiro obrigatório, mínimo zero, `step=1` |

O frontend impede formato evidentemente incompatível, mas o backend normaliza e decide. Não existe conversão escondida; o response normalizado é apresentado. Saldo inicial não é chamado de compra ou entrada.

Cancelar retorna diretamente se intacto e pede confirmação se alterado. Salvar é a única ação primária, impede submissão concorrente e preserva valores não sensíveis em erro.

Depois do `201`, a aplicação informa sucesso, usa o ID do response, navega ao detalhe e apresenta os valores normalizados. `Location` continua testado no contrato HTTP.

#### Detalhe

`/products/:productId` apresenta código, descrição, saldo atual, criação e atualização. Possui somente retorno à lista, loading, erro e not found. Não oferece editar, excluir, ajustar saldo ou consultar movimentos.

#### Erros

| Código | Mensagem principal |
|---|---|
| `product_code_already_exists` | Já existe um produto com esse código. |
| `product_not_found` | Produto não encontrado. |
| `inventory_unavailable` | O serviço de estoque está temporariamente indisponível. |
| Validação por campo | Associada ao controle correspondente |
| `401` | Fluxo central de sessão |
| `403` | Falta de permissão |
| Desconhecido | Mensagem genérica e retry seguro quando aplicável |

Diagnóstico técnico não aparece como mensagem principal. Correlation ID pode ser exibido apenas em área secundária de suporte.

#### Atualização do saldo

Ao retornar depois de uma emissão, listagem e detalhe consultam novamente a API. Não existe cache global de saldo capaz de apresentar valor anterior como atual.

### 7.5 Bloco 5 - Telas, itens e concorrência de Invoice

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Estados separados

`InvoiceStatusChip` representa somente `Open` como Aberta e `Closed` como Fechada. `IsIssuanceInProgress` aparece em indicador operacional separado e `InvoiceIssuanceProcessStatus` pertence ao acompanhamento da emissão. A interface não converte estados técnicos em status fiscal.

#### Listagem

`/invoices` exige autenticação e apresenta `Nova nota fiscal` apenas para Admin. Em expanded, tabela possui número, status, quantidade de itens, situação da emissão, criação, atualização e detalhe. Em compact, preserva essas informações em item de lista.

Não exibe valor, imposto, cliente, fornecedor ou total fiscal. Paginação usa query string, padrão 1/20 e opções 10, 20, 50 e 100. Não oferece filtro ou ordenação inexistentes.

#### Criação

`/invoices/new` exige Admin e informa que uma nota vazia receberá numeração automática. Somente o botão explícito `Criar nota fiscal` envia o `POST` sem body; entrar na rota não cria recurso.

Depois do `201`, a aplicação captura invoice e ETag, informa sucesso e navega ao detalhe.

#### Detalhe

`/invoices/:invoiceId` apresenta número, status, bloqueio operacional, criação, atualização, fechamento quando existente, itens e processo quando conhecido. ETag e versão de banco não aparecem ao usuário.

#### ETag

O serviço retorna `InvoiceWithEtag`, contendo representação e header opaco. Toda mutação envia o ETag atual em `If-Match`. Response com invoice substitui ambos; `DELETE 204` remove o item localmente e armazena o novo header.

O frontend não calcula ou interpreta ETag. Ausência em resposta que o exige é incompatibilidade técnica.

#### Adição de item

`Adicionar produto` exige Admin, invoice aberta e ausência de emissão ativa. Um seletor paginado apresenta código, descrição e saldo atual; produtos já presentes podem ser desabilitados visualmente. O catálogo completo continua navegável e não é reduzido aos primeiros 100 registros.

Saldo mostrado é informativo: adicionar item não reserva ou comprova estoque. A emissão decide a suficiência.

Quantidade é inteira, obrigatória e mínima 1. Sucesso substitui invoice e ETag pelos valores retornados.

#### Alteração e remoção

Alterar quantidade envia somente `quantity`; produto e snapshots são imutáveis. Remover exige confirmação com produto e número da nota. Depois do `204`, estado local e ETag são atualizados; invoice aberta pode ficar vazia.

As duas ações exigem Admin, estado aberto e ausência de emissão ativa.

#### Somente leitura e bloqueio

Invoice fechada permanece consultável e não oferece mutação, emissão, cancelamento, reabertura ou exclusão. Durante emissão, itens continuam visíveis, mutações ficam indisponíveis e um banner explica o bloqueio. Sair da página não cancela o processamento.

#### Conflito de versão

Em `412 invoice_version_mismatch`, a interface não repete ou faz merge. Informa alteração concorrente, recarrega invoice e ETag e permite revisão antes de nova intenção. `428 invoice_version_required` representa incompatibilidade do cliente e não provoca envio com versão inventada.

#### Erros

| Código | Tratamento visual |
|---|---|
| `invoice_not_found` | Nota fiscal não encontrada |
| `product_not_found` | Produto selecionado não está mais disponível |
| `product_already_added` | Produto já pertence à nota |
| `invoice_not_open` | Nota não está aberta para alteração |
| `invoice_issuance_in_progress` | Nota bloqueada durante a emissão |
| `product_catalog_unavailable` | Catálogo temporariamente indisponível |
| `billing_unavailable` | Faturamento temporariamente indisponível |
| `invoice_item_not_found` | Item não encontrado nesta nota |
| `invoice_version_mismatch` | Recarregar e revisar conflito |
| Validação por campo | Associar ao controle |

Estado inesperado provoca recarga segura, nunca correção local artificial.

#### Navegação com edição

Formulário ou dialog alterado pede confirmação antes de fechar ou navegar. Dados persistidos não geram aviso e alteração incompleta não é salva automaticamente.

### 7.6 Bloco 6 - Emissão, polling, feedback e impressão

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Disponibilidade e confirmação

`Imprimir nota` aparece somente para Admin, invoice aberta, desbloqueada, com ao menos um item e ETag disponível. A confirmação informa que a emissão baixará estoque, fechará a nota depois da confirmação e não pode ser desfeita.

#### Idempotency-Key

Uma nova intenção cria UUID por `crypto.randomUUID()` uma única vez. `PendingIssuanceIntent` em `sessionStorage` preserva `invoiceId`, chave, ETag usado, criação e, depois do aceite, process ID e Location.

A chave não muda quando a resposta é desconhecida, não aparece em logs ou UI, é removida quando a intenção é rejeitada antes do aceite e permanece durante recuperação.

#### Chamada inicial e resposta desconhecida

O `POST /print` envia `If-Match`, Idempotency-Key e nenhum body. Durante envio, ação e edição ficam bloqueadas. `202` armazena processo e inicia polling, sem chamar Inventory ou imprimir.

Timeout, desconexão ou falha sem confirmação oferecem `Verificar novamente`, que repete manualmente o mesmo comando, com chave e ETag originais. Não existe retry automático. Billing recupera a intenção idempotente se ela tiver sido aceita.

#### Polling

O processo é consultado imediatamente e depois conforme `Retry-After`: um segundo nos primeiros dez segundos e três posteriormente. A próxima chamada somente é agendada depois da atual, sem sobreposição.

Polling termina em estado terminal, logout ou destruição da página e pode ser retomado na mesma aba pelo registro persistido. Retorno da aba visível provoca atualização imediata.

#### Estados ativos e atraso

`Pending` informa solicitação registrada e publicação pendente. `AwaitingStock` informa atualização de estoque. `isDelayed` explica demora, mas não altera estado, interrompe polling, desbloqueia invoice, inicia emissão ou declara falha.

#### Completed

Ao concluir, a aplicação encerra polling, recarrega a invoice, confirma `Closed`, prepara HTML, navega à visualização transitória e solicita `window.print()` depois da renderização.

#### Rejected

A interface informa que nenhuma baixa foi aplicada e apresenta razão funcional, produtos, quantidade solicitada e saldo conhecido quando fornecidos. Polling termina, invoice é recarregada e desbloqueada, itens podem ser corrigidos e nova intenção usa nova chave. Impressão não abre.

#### ManualIntervention

Informa necessidade de verificação técnica sem afirmar se houve baixa. Invoice permanece aberta e bloqueada; edição, nova emissão e impressão não aparecem. Não existe resolução fictícia. Correlation ID pode aparecer em suporte.

#### Falha da consulta

Falha de polling informa que o processamento pode continuar no servidor e oferece apenas nova consulta. Não repete `PrintInvoice`, altera estado, fecha ou desbloqueia invoice. Processo `404` é inconsistência técnica.

#### Conteúdo imprimível

Inclui marca Korp ERP, título, número, status Fechada, datas de criação e fechamento e itens com código, descrição e quantidade. Não inclui shell, botões administrativos, saldo, usuário, processo, erros, impostos, valores, QR Code ou dados fiscais inventados.

#### Acesso transitório

`/invoices/:invoiceId/print-view` exige estado em memória criado pela conclusão observada naquela aba. Acesso direto, bookmark, reload ou simples invoice fechada retornam ao detalhe. Ao sair, a permissão é descartada.

#### Diálogo e fallback

Depois do render, `window.print()` é chamado via `BrowserPrintService`. O frontend não sabe se o usuário confirmou, cancelou, salvou ou imprimiu fisicamente.

Enquanto a visualização transitória permanece aberta, `Abrir diálogo de impressão` pode chamar apenas `window.print()` caso o diálogo não tenha aberto. Não chama backend, cria chave, baixa estoque ou inicia nova emissão.

#### CSS e testes

`@media print` oculta elementos interativos, usa fundo branco, texto escuro, margens adequadas e tabela legível sem cor, sem paginação documental complexa.

Testes comprovam ausência de print antes de Completed ou em outros resultados, solicitação após render, fallback sem novo comando, CSS e ausência de alteração de estado pelo diálogo. Não tentam provar impressão física.

### 7.7 Bloco 7 - Erros, acessibilidade, configuração e observabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Modelo central de erro

Problem Details é convertido pela infraestrutura HTTP em `ApiError` tipado com status, code, title, detail, errors, traceId e correlationId. Componentes não interpretam formatos distintos individualmente.

A apresentação prioriza erros por campo, código funcional conhecido, status conhecido e mensagem genérica segura. `detail` desconhecido não é tratado como HTML ou texto confiável.

#### Categorias e retry

Validação fica junto do campo; erro funcional fica no contexto; sucesso breve usa snackbar; not found recebe estado próprio; conflitos recarregam contexto; indisponibilidade recebe explicação segura.

Mutações não possuem retry automático. Consultas oferecem retry explícito. Polling, resposta desconhecida de PrintInvoice, ETag e `429` seguem suas políticas específicas.

Eventos online/offline do navegador podem informar conectividade, mas não ativam modo offline, cache, fila local ou conclusão sobre estado dos serviços.

#### Feedback

Snackbar atende sucessos transitórios. Aviso contextual preserva conflito, rejeição, atraso, falha de polling, intervenção e indisponibilidade. Dialog é reservado a descarte de edição, remoção de item e emissão.

#### Estrutura acessível

A aplicação usa landmarks, link `Ir para o conteúdo principal`, um `h1` por página e hierarquia correta. Mudança de rota atualiza título, foco e anúncio quando necessário.

Campos possuem label, descrição e erro associados; foco alcança o primeiro inválido; tabulação segue ordem visual; senha possui controle textual de visibilidade. Tabelas possuem cabeçalhos, paginação rotulada e ações contextualizadas.

Região `aria-live="polite"` anuncia sucesso e mudanças relevantes. `role="alert"` atende erro imediato. Polling não rouba foco ou anuncia cada consulta, apenas transições significativas.

Dialogs preservam focus trap, retorno ao acionador, Escape somente quando seguro e foco inicial não destrutivo. Dialog de emissão não fecha externamente depois do envio.

#### Idioma e formatação

Documento usa `lang="pt-BR"`; datas usam `Intl.DateTimeFormat("pt-BR")` e identificam horário local quando exibem hora. Quantidades permanecem inteiras. UUID, ETag e chave não recebem localização. Não existe biblioteca de internacionalização para o único idioma.

#### Configuração de runtime

`/assets/config.json` não secreto fornece `apiBaseUrl`, carregado antes das rotas funcionais. Aceita URL HTTP/HTTPS absoluta ou valor vazio para mesma origem, aponta exclusivamente ao Gateway e não possui token, usuário ou segredo.

Valor inválido impede bootstrap funcional sem fallback escondido. Todas as features usam `apiBaseUrl + /api/v1`; nenhuma conhece endereço de serviço. O SDD-11 define o valor local final.

#### Segurança de conteúdo

Não existe HTML de API, execução de string, URL dinâmica não validada, segredo no bundle, CDN ou source map público de entrega sem decisão. Token é enviado somente ao Gateway validado.

#### Diagnóstico

Não será adicionada plataforma externa. Eventos sanitizados podem cobrir navegação, falha HTTP, expiração, polling interrompido e pedido do diálogo de impressão.

Campos permitidos são route template, status, error code, correlationId, traceId e duração. Token, senha, e-mail, usuário, path com UUID, body, invoice, produto, Idempotency-Key, ETag e query string são proibidos.

Erro persistente pode apresentar correlation ID em área secundária copiável como código de suporte, nunca como mensagem principal.

Falha de configuração mostra somente impossibilidade de iniciar e orienta verificar o ambiente, sem tentar destino alternativo.

### 7.8 Bloco 8 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Critérios de aceite

| ID | Critério verificável |
|---|---|
| `CA-FRT-01` | O frontend acessa exclusivamente o API Gateway e não conhece endereços internos dos microsserviços ou do RabbitMQ. |
| `CA-FRT-02` | A interface utiliza identidade visual minimalista, predominantemente branca e verde, conforme os tokens aprovados. |
| `CA-FRT-03` | Navegação, formulários, tabelas e dialogs funcionam por teclado e permanecem utilizáveis nos breakpoints definidos. |
| `CA-FRT-04` | O usuário consegue autenticar-se, restaurar sessão válida na mesma aba e encerrar a sessão localmente. |
| `CA-FRT-05` | Resposta `401` em rota protegida encerra a sessão, enquanto `403` a preserva e informa falta de permissão. |
| `CA-FRT-06` | Produtos são listados com paginação e estados explícitos de carregamento, vazio, atualização e falha. |
| `CA-FRT-07` | Um Admin cadastra produto válido e consulta os dados normalizados no detalhe. |
| `CA-FRT-08` | Invoices são listadas, criadas vazias mediante ação e confirmação explícitas e consultadas individualmente. |
| `CA-FRT-09` | Um Admin adiciona, altera e remove itens somente de invoice aberta e sem emissão ativa. |
| `CA-FRT-10` | Toda mutação de invoice usa o ETag mais recente e conflito `412` exige recarga e revisão manual. |
| `CA-FRT-11` | A emissão exige confirmação e reutiliza a mesma Idempotency-Key quando o resultado do comando for desconhecido. |
| `CA-FRT-12` | O polling respeita `Retry-After`, não sobrepõe consultas e termina em estado terminal, logout ou destruição da tela. |
| `CA-FRT-13` | `Pending`, `AwaitingStock` e atraso operacional são apresentados sem antecipar sucesso ou falha. |
| `CA-FRT-14` | `Completed` encerra o polling, atualiza a invoice, confirma `Closed` e disponibiliza a impressão transitória. |
| `CA-FRT-15` | `Rejected` apresenta a justificativa disponível, não abre impressão e permite corrigir a invoice antes de nova tentativa. |
| `CA-FRT-16` | `ManualIntervention` mantém a invoice bloqueada e não permite editar, emitir novamente ou imprimir. |
| `CA-FRT-17` | Falha ao consultar o processo repete somente a consulta mediante ação segura, nunca o comando de emissão automaticamente. |
| `CA-FRT-18` | A visualização de impressão não é autorizada por acesso direto, bookmark, reload ou simples consulta de invoice fechada. |
| `CA-FRT-19` | A impressão contém somente os dados aprovados e possui estilo próprio para mídia impressa. |
| `CA-FRT-20` | Erros HTTP são normalizados e tratados centralmente, sem apresentar conteúdo desconhecido da API como HTML confiável. |
| `CA-FRT-21` | A URL do Gateway vem da configuração de runtime e configuração inválida impede o bootstrap funcional. |
| `CA-FRT-22` | Mudanças importantes são anunciadas de forma acessível, sem roubar foco ou anunciar cada consulta do polling. |
| `CA-FRT-23` | Tokens, credenciais, dados sensíveis e conteúdo integral de requests não aparecem em logs, mensagens ou diagnóstico. |
| `CA-FRT-24` | Falha persistente apresenta código de suporte copiável quando houver correlation ID ou trace ID. |

#### Testes planejados

| ID | Prova planejada | Nível | Critérios |
|---|---|---|---|
| `TST-FRT-001` | Verificar arquitetura por feature, lazy loading e ausência de destinos internos | Arquitetura | CA-FRT-01, CA-FRT-21 |
| `TST-FRT-002` | Verificar tokens, tema, tipografia local e ausência de recursos por CDN | Componente/inspeção | CA-FRT-02, CA-FRT-23 |
| `TST-FRT-003` | Exercitar shell e telas nos três breakpoints, zoom e navegação por teclado | Componente/E2E | CA-FRT-03, CA-FRT-22 |
| `TST-FRT-004` | Autenticar com sucesso e falha sem enumerar usuário | Unitário/integração | CA-FRT-04 |
| `TST-FRT-005` | Restaurar somente sessão íntegra e não expirada na mesma aba | Unitário | CA-FRT-04 |
| `TST-FRT-006` | Encerrar sessão, cancelar fluxos e limpar armazenamento | Unitário/componente | CA-FRT-04 |
| `TST-FRT-007` | Validar bearer, correlação, guards, return URL, `401` e `403` | Unitário/integração | CA-FRT-05, CA-FRT-23 |
| `TST-FRT-008` | Listar e paginar produtos nos estados loading, loaded, empty e error | Componente/integração | CA-FRT-06 |
| `TST-FRT-009` | Validar e cadastrar produto, preservando resposta normalizada e navegação ao detalhe | Componente/integração | CA-FRT-07 |
| `TST-FRT-010` | Consultar detalhe e tratar not found e indisponibilidade | Componente/integração | CA-FRT-06, CA-FRT-07, CA-FRT-20 |
| `TST-FRT-011` | Listar, paginar, criar mediante confirmação e consultar invoice | Componente/integração | CA-FRT-08 |
| `TST-FRT-012` | Adicionar, alterar e remover item conforme papel, estado e bloqueio | Componente/integração | CA-FRT-09 |
| `TST-FRT-013` | Propagar ETag opaco, atualizar sua versão e tratar `412` e `428` sem retry | Unitário/integração | CA-FRT-10 |
| `TST-FRT-014` | Criar uma chave por intenção e reutilizá-la após resposta desconhecida | Unitário/integração | CA-FRT-11 |
| `TST-FRT-015` | Restaurar intenção pendente na mesma aba sem iniciar novo comando | Unitário/componente | CA-FRT-11, CA-FRT-12 |
| `TST-FRT-016` | Controlar polling imediato, `Retry-After`, visibilidade, sequência e cancelamento | Unitário com relógio controlado | CA-FRT-12 |
| `TST-FRT-017` | Apresentar `Pending`, `AwaitingStock` e `isDelayed` sem decisão indevida | Componente | CA-FRT-13 |
| `TST-FRT-018` | Tratar `Completed`, atualizar invoice e chamar impressão somente após render | Componente/integração | CA-FRT-14, CA-FRT-19 |
| `TST-FRT-019` | Tratar `Rejected` sem impressão e liberar correção com nova intenção | Componente/integração | CA-FRT-15 |
| `TST-FRT-020` | Tratar `ManualIntervention` sem afirmar efeito e mantendo bloqueio | Componente/integração | CA-FRT-16 |
| `TST-FRT-021` | Tratar falha do polling sem repetir o comando de emissão | Unitário/integração | CA-FRT-17 |
| `TST-FRT-022` | Negar print-view direto, por reload e por invoice apenas fechada | Roteamento/E2E | CA-FRT-18 |
| `TST-FRT-023` | Verificar conteúdo, CSS de impressão, adapter e fallback sem novo comando | Componente/inspeção | CA-FRT-18, CA-FRT-19 |
| `TST-FRT-024` | Normalizar validação, conflito, not found, indisponibilidade, `429` e erro desconhecido | Unitário/componente | CA-FRT-20, CA-FRT-24 |
| `TST-FRT-025` | Carregar configuração válida e falhar sem fallback diante de destino inválido | Unitário/integração | CA-FRT-01, CA-FRT-21 |
| `TST-FRT-026` | Auditar landmarks, foco, labels, live regions, contraste e teclado | Acessibilidade/E2E | CA-FRT-03, CA-FRT-22 |
| `TST-FRT-027` | Verificar ausência de segredo, HTML inseguro e dados proibidos no diagnóstico | Segurança/inspeção | CA-FRT-23, CA-FRT-24 |
| `TST-FRT-028` | Executar o fluxo crítico Angular → Gateway com infraestrutura real | E2E | CA-FRT-04 a CA-FRT-21 |

O SDD-10 definirá ferramentas, fixtures, execução e distribuição da pirâmide. A suíte Angular usará o runner já aprovado no setup e dependências adicionais de teste somente poderão ser adotadas com justificativa e aprovação.

O código de produção frontend escrito manualmente terá cobertura mínima de 80% de linhas. Arquivos gerados, configuração declarativa trivial e contratos sem lógica podem ser excluídos por regra explícita no SDD-10. Branch coverage será coletada e publicada sem gate inicial. Cobertura não substitui as provas comportamentais.

#### Riscos e mitigação

| Risco | Consequência | Mitigação aprovada |
|---|---|---|
| Duplicar domínio no navegador | Divergência entre tela e API | Frontend valida formato e experiência; backend permanece autoridade |
| Saldo ou ETag desatualizado | Decisão visual incorreta ou conflito | Saldo informativo, recarga após emissão e `If-Match` obrigatório |
| Recriar chave após resultado desconhecido | Possível nova intenção lógica | Persistir e reutilizar `PendingIssuanceIntent` na mesma aba |
| Polling permanecer ativo | Requests e atualização de estado indevidos | Agendamento sequencial e cancelamento por terminal, logout e destruição |
| Autorizar reimpressão pelo estado fechado | Fluxo fora do escopo | Permissão transitória em memória, derivada de conclusão observada |
| XSS alcançar `sessionStorage` | Exposição do bearer | APIs seguras do Angular, ausência de HTML dinâmico, CDN e scripts externos |
| Material ou cor ocultar semântica | Interface inacessível | Texto, semântica, contraste, foco e auditoria automatizada/manual |
| Layout perder informação em tela compacta | Fluxo principal incompleto | Listas responsivas com os mesmos dados essenciais e sem rolagem horizontal |
| Configurar destino interno | Violação da fronteira | Configuração validada e teste de arquitetura apontando somente ao Gateway |
| Diagnóstico registrar dados de negócio | Vazamento de dados | Allowlist explícita de campos e testes sentinela |

#### Marcadores de acompanhamento

| Marcador | Aplicação neste SDD |
|---|---|
| `UI` | Identidade, composição, feedback e responsividade |
| `A11Y` | Semântica, teclado, foco, contraste e anúncios |
| `AUT` | Sessão, guards e autorização visual |
| `API` | Contratos HTTP somente por meio do Gateway |
| `CON` | ETag e tratamento explícito de conflitos |
| `IDM` | Idempotency-Key e intenção persistida |
| `RES` | Polling, retry seguro, atraso e retomada |
| `PRN` | Impressão transitória por navegador |
| `SEC` | Token, XSS, configuração e dados protegidos |
| `TST` | Provas derivadas de cada critério |
| `OBS` | Correlação e diagnóstico sanitizado |
| `DOC` | Contratos e decisões sincronizados |
| `QA` | Validação integrada e regressão visual/funcional |

---

## 8. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os oito blocos estiverem aprovados;
- cada tela possuir estados de carregamento, vazio, erro e sucesso aplicáveis;
- sessão, ETag, idempotência, polling e impressão forem implementáveis sem decisão implícita;
- acessibilidade e responsividade possuírem provas planejadas;
- cada critério possuir ao menos um teste;
- matriz de rastreabilidade e índice estiverem atualizados;
- nenhuma regra definitiva de domínio ou integração direta tiver sido atribuída ao frontend.

A condição foi atendida em 2026-08-20: os oito blocos foram aprovados, os critérios possuem provas planejadas e a auditoria do documento não encontrou decisão funcional pendente.

A aprovação estabiliza a experiência Angular, mas não autoriza implementação antes da baseline documental conjunta.
