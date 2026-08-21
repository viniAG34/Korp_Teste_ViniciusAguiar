# SDD-10 - Estratégia e Infraestrutura de Testes

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-20
> Gate A: aprovado em 2026-08-20
> Dependências: SDD-01 a SDD-09, ADR-009, ADR-014 e CONVENCOES-CODIGO.md

---

## 1. Objetivo

Especificar a estratégia verificável de testes do projeto, incluindo responsabilidades de cada nível, infraestrutura real, isolamento, cobertura, execução Docker-first e produção de evidências.

Este documento consolida como os critérios definidos nos SDDs serão provados. Ele não redefine regras de negócio nem autoriza a implementação das suítes durante a macroetapa documental.

---

## 2. Requisitos rastreados

- `QLT-001` a `QLT-008`;
- todos os requisitos funcionais e diferenciais que possuem critérios de aceite nos SDDs 01 a 09;
- critérios de apresentação `APR` que exigem demonstração ou evidência objetiva.

---

## 3. Escopo previsto

- níveis e responsabilidades dos testes;
- organização das suítes backend e frontend;
- ferramentas e dependências justificadas;
- testes unitários, de integração, contrato, arquitetura e ponta a ponta;
- PostgreSQL e RabbitMQ reais nos testes aplicáveis;
- isolamento, dados determinísticos e paralelismo;
- cobertura de linhas, branches e gates;
- execução integral por Docker e Docker Compose;
- evidências, relatórios, classificação de falhas e critérios de conclusão.

---

## 4. Fora do escopo

- redefinir comportamento já aprovado nos SDDs funcionais;
- testes de carga como gate obrigatório sem requisito de desempenho aprovado;
- teste de impressão física;
- dependência de serviços externos ou ambientes compartilhados;
- substituição indiscriminada de infraestrutura real por mocks;
- criação de pipeline de CI/CD nesta etapa documental;
- implementação de testes ou instalação de pacotes antes do plano do Gate B.

---

## 5. Blocos de decisão

1. estratégia, níveis e responsabilidade das provas;
2. ferramentas, projetos e organização das suítes;
3. testes unitários e de componentes;
4. integração com HTTP, PostgreSQL e RabbitMQ reais;
5. contratos, arquitetura e segurança;
6. testes ponta a ponta e fluxos distribuídos;
7. dados, isolamento, determinismo e paralelismo;
8. cobertura, Docker, evidências e gates;
9. critérios de aceite, riscos e rastreabilidade.

---

## 6. Decisões herdadas

- testes são derivados dos critérios de aceite e não de detalhes acidentais da implementação;
- testes unitários e de integração são obrigatórios;
- PostgreSQL e RabbitMQ reais são usados nas integrações que dependem de seu comportamento;
- o fluxo oficial é Docker-first e reproduzível no CI;
- backend usa Coverlet e Cobertura XML;
- cada assembly backend relevante possui gate mínimo de 80% de linhas;
- frontend manual possui gate mínimo de 80% de linhas;
- branch coverage é publicada sem gate percentual inicial;
- relatórios gerados não são versionados;
- cobertura não substitui cenários comportamentais críticos;
- falha omitida, teste ignorado ou infraestrutura substituída silenciosamente não constitui evidência válida.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Estratégia, níveis e responsabilidade das provas

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Níveis adotados

| Nível | Responsabilidade principal |
|---|---|
| Estático e arquitetura | Verificar dependências, fronteiras, configuração proibida, segredos e organização estrutural |
| Unitário | Validar invariantes, casos de uso, transições, cálculos e tratamento isolado de falhas |
| Componente frontend | Validar apresentação, formulários, permissões visuais, acessibilidade e interação Angular |
| Integração | Validar APIs, EF Core, PostgreSQL, RabbitMQ, autenticação, headers e transações com infraestrutura real |
| Contrato | Garantir compatibilidade de contratos HTTP e eventos entre produtores e consumidores |
| Ponta a ponta | Comprovar os poucos fluxos críticos completos do Angular ao Gateway e aos microsserviços |
| Validação manual | Complementar responsividade, experiência visual, impressão e roteiro demonstrativo sem substituir prova automatizável |

#### Alocação das provas

Cada critério de aceite possui ao menos uma prova principal identificada. Comportamento crítico pode ser comprovado em mais de um nível quando as provas forem complementares, como uma invariante unitária, sua persistência em integração e o resultado percebido no E2E.

O teste é colocado no nível mais baixo capaz de comprovar corretamente o comportamento. Não serão impostos percentuais artificiais de testes por nível: a distribuição resulta do risco, da natureza da regra e da fronteira exercitada.

E2E não substitui testes unitários e de integração mais precisos. Da mesma forma, cobertura de linha não substitui asserção funcional nem autoriza omitir caminho crítico.

#### Fidelidade e isolamento

- testes não acessam membros privados nem reproduzem internamente o algoritmo testado;
- doubles representam dependências controláveis em testes isolados;
- mocks não substituem comportamento próprio de PostgreSQL ou RabbitMQ nos testes de integração correspondentes;
- nenhuma suíte depende de ambiente compartilhado, internet ou estado residual de execução anterior;
- resultado esperado não depende da ordem global de execução;
- relógio, UUID e falhas técnicas recebem controle explícito quando o cenário exigir determinismo;
- teste instável é defeito da suíte, não ocorrência normal aceitável.

#### Forma das especificações

Cenários comportamentais usam Dado/Quando/Então no SDD e podem refletir essa estrutura no nome ou corpo do teste. Verificações técnicas simples podem usar Arrange/Act/Assert. Nomes descrevem comportamento e resultado, sem depender do nome interno de um método quando esse não for parte do contrato.

#### Falhas e conclusão

Testes ignorados, desabilitados, instáveis ou dependentes de intervenção manual não satisfazem silenciosamente um gate. Falha de infraestrutura deve ser distinguida de regressão funcional para diagnóstico, mas ambas impedem declaração de sucesso até resolução ou registro explícito de validação não executada e risco residual.

Validação manual complementa a automação somente onde percepção humana ou plataforma externa faz parte da evidência. A impressão física permanece fora da automação: a suíte comprova preparação do conteúdo e acionamento de `window.print()`, sem afirmar que papel foi produzido.

### 7.2 Bloco 2 - Ferramentas, projetos e organização das suítes

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Backend

A suíte preserva xUnit v3, `Microsoft.NET.Test.Sdk`, Coverlet e a separação existente entre projetos unitários, de integração e arquitetura. Na futura implementação, `Microsoft.AspNetCore.Mvc.Testing` poderá ser adicionado aos projetos que precisarem subir o host ASP.NET Core e exercitar seu pipeline HTTP real.

ReportGenerator será disponibilizado como ferramenta no ambiente Docker para consolidação e leitura da cobertura. Ele não determina aprovação dos testes nem substitui o gate do Coverlet.

Não serão adotados inicialmente FluentAssertions, Moq, NSubstitute, Testcontainers, Respawn, Pact ou equivalentes. Recursos do xUnit, fakes específicos e infraestrutura coordenada pelo Docker Compose atendem ao escopo atual. Nova dependência exigirá necessidade concreta, justificativa e aprovação no plano da implementação afetada.

#### Frontend e navegador

Vitest, Angular TestBed, jsdom e Angular Material Component Harnesses atendem testes unitários e de componentes. Arquivos `*.spec.ts` permanecem próximos ao código que verificam.

Playwright será adotado para os poucos fluxos E2E e verificações que exigem navegador real. A integração do Axe com Playwright realizará auditorias automatizadas de acessibilidade; ela complementa, mas não substitui, teclado, foco, contraste e inspeção manual.

Dependências e navegadores do Playwright serão instalados dentro da imagem de testes fixada. A execução oficial não dependerá de navegador ou Node.js instalados diretamente no host.

#### Organização backend

```text
tests/
|- Architecture/
|- Identity/
|  |- Korp.Identity.UnitTests/
|  `- Korp.Identity.IntegrationTests/
|- Inventory/
|  |- Korp.Inventory.UnitTests/
|  `- Korp.Inventory.IntegrationTests/
|- Billing/
|  |- Korp.Billing.UnitTests/
|  `- Korp.Billing.IntegrationTests/
|- Gateway/
|  `- Korp.Gateway.IntegrationTests/
|- EndToEnd/
|  `- Korp.System.EndToEndTests/
`- Shared/
   `- Korp.Testing/
```

`Korp.Testing` conterá apenas builders, fakes, relógios e utilitários comprovadamente reutilizados. Não possuirá regras de negócio, banco próprio, service locator nem fixtures que acessem internals de vários microsserviços.

Não será criado projeto unitário do Gateway sem lógica isolável que o justifique. Suas políticas são verificadas principalmente por integração e arquitetura.

#### Organização frontend e E2E

```text
frontend/korp-erp-web/
|- src/app/
|  `- ... arquivos *.spec.ts próximos ao código
`- e2e/
   |- fixtures/
   |- pages/
   |- accessibility/
   `- specs/
```

Page Objects encapsulam apenas interação estável do navegador. Não devem reproduzir regras de negócio, ocultar asserções importantes ou formar uma camada genérica para cada elemento da tela.

#### Contratos e infraestrutura

Contratos HTTP e eventos serão verificados por serialização, schemas, headers e exemplos canônicos aprovados, sem plataforma Pact. O Docker Compose permanece responsável por PostgreSQL, RabbitMQ e aplicações; Testcontainers não duplicará essa orquestração.

Nenhuma dependência citada como futura está autorizada para instalação durante a macroetapa documental.

### 7.3 Bloco 3 - Testes unitários e de componentes

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Backend unitário

| Área | Responsabilidade da prova unitária |
|---|---|
| Domain | Invariantes, factory methods, transições, estados inválidos e cálculos |
| Application | Casos de uso, autorização contextual, coordenação de portas, respostas funcionais e cancelamento |
| Infrastructure | Apenas algoritmo manual legitimamente isolável, como backoff, envelope e classificação de falha |
| API | Mapeador ou política com decisão própria; pipeline, autenticação e HTTP pertencem à integração |

Domain não usa banco, HTTP, broker ou container de DI. Application usa fakes específicos das portas necessárias, sem mockar detalhes internos do caso de uso.

`TimeProvider` controla cenários temporais. Fontes controláveis de UUID e outros identificadores são introduzidas apenas quando seu valor ou repetição influencia a asserção. Não se abstrai `Guid.NewGuid()` de forma global sem necessidade demonstrada.

Builders de teste produzem objetos válidos por padrão e tornam explícito somente o aspecto relevante ao cenário. Não podem aceitar combinações que o domínio público jamais permitiria, salvo fixture técnica delimitada para materializar dado legado ou corrompido em teste de infraestrutura.

`[Theory]` atende variações equivalentes. Casos com preparação, consequência ou diagnóstico materialmente diferentes permanecem separados.

São proibidos:

- provider EF Core InMemory como substituto de PostgreSQL;
- mock de `DbSet` ou LINQ do provider;
- simulação de internals do RabbitMQ para afirmar comportamento do broker;
- acesso a método privado apenas para elevar cobertura;
- asserção de texto integral de log, salvo campo estruturado obrigatório de segurança ou diagnóstico;
- teste cuja asserção apenas replique o algoritmo de produção.

Cada teste deve ter causa principal de falha compreensível. Múltiplas asserções são permitidas quando descrevem o mesmo resultado observável.

#### Frontend unitário e de componente

Vitest e Angular TestBed verificam sessão, configuração, guards, interceptors, normalização de erros, validações, Signals derivados, fluxos RxJS, polling e adapters de navegador.

O testing provider do `HttpClient` controla contratos HTTP; `RouterTestingHarness` controla navegação; Angular Material Component Harnesses são preferidos quando evitarem acoplamento ao HTML interno do Material.

Os testes devem:

- verificar conteúdo, semântica, disponibilidade de ações e reação observável;
- controlar relógio em polling, atraso e expiração, sem espera real;
- acessar impressão somente pelo `BrowserPrintService`;
- controlar e limpar `sessionStorage` entre casos;
- testar Signals e RxJS por seus resultados, não por detalhes internos;
- manter componentes filhos simples quando sua presença fizer parte do comportamento;
- substituir somente fronteiras que tornariam o teste irrelevante ou instável.

Snapshots extensos de HTML e seletores internos do Angular Material são proibidos. Um teste de página não deve se transformar em E2E nem repetir todos os cenários já cobertos no serviço ou componente responsável.

#### Acessibilidade no nível de componente

Quando aplicável, são verificados nome acessível, associação de label, descrição e erro, foco de dialogs, anúncios de mudanças importantes, indisponibilidade não dependente somente de cor e interação básica por teclado.

Contraste final, zoom, responsividade em navegador real e integração assistiva completa pertencem às provas E2E ou manuais definidas nos blocos posteriores.

### 7.4 Bloco 4 - Integração com HTTP, PostgreSQL e RabbitMQ

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Definição

Um teste é classificado como integração quando exercita código de produção por pelo menos uma fronteira técnica real: host ASP.NET Core, EF Core com PostgreSQL, RabbitMQ, autenticação JWT, serialização HTTP ou de evento, repository, Inbox ou Outbox.

Instanciar várias classes sem atravessar uma dessas fronteiras não transforma um teste unitário em integração.

#### APIs e HTTP

Cada serviço é iniciado com seu pipeline ASP.NET Core real por mecanismo suportado de test host. São verificados método, rota, query, autenticação, autorização, JSON, status, Problem Details e headers aplicáveis, incluindo `Location`, `ETag`, `If-Match`, `Idempotency-Key`, `Retry-After` e correlação.

JWTs de teste são assinados por chave exclusiva do ambiente de testes e validados pelo mesmo pipeline de produção quanto a assinatura, algoritmo, issuer, audience, validade e claims. Autenticação não é desabilitada para facilitar testes protegidos.

Os testes também verificam validação de fronteira, cancelamento quando observável e ausência de detalhes sensíveis nas respostas.

#### PostgreSQL

Integrações de persistência usam PostgreSQL e migrations reais para provar mappings, constraints, sequences, normalização, unicidade, transações, queries do provider, concorrência por `xmin`, Inbox, Outbox, atomicidade, rollback e retomada.

`EnsureCreated`, SQLite, EF Core InMemory e mock de repository são proibidos quando a prova exige comportamento persistente. Cada microsserviço acessa somente seu próprio banco e credencial.

#### RabbitMQ

Integrações aplicáveis usam broker real e topologia equivalente à aplicação para provar exchanges, queues, bindings, routing keys, publisher confirms, persistência, acknowledgement manual, prefetch, redelivery, Inbox idempotente, retry por TTL, dead-letter routing, DLQ e retomada após indisponibilidade.

A prova observa efeito público, persistência de Inbox/Outbox ou mensagem recebida por consumidor de teste. Não usa espera fixa como evidência de conclusão nem depende de internals não contratuais do broker.

#### Fronteiras entre serviços

Na suíte de integração de um serviço, dependência HTTP externa pode ser representada por servidor mínimo controlado. O request produzido, o response consumido, a serialização e a reação do serviço permanecem reais; o outro microsserviço completo não é iniciado dentro dessa suíte.

Billing usa essa abordagem para a consulta interna ao Inventory. O Gateway pode rotear para destinos HTTP controlados. Fluxos com todos os serviços reais simultâneos e a cooperação completa Inventory ↔ Billing pertencem aos testes distribuídos e E2E.

Essa divisão mantém a causa de falha localizada sem dispensar a validação completa posterior.

### 7.5 Bloco 5 - Contratos, arquitetura e segurança

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Contratos HTTP

Os testes comparam semanticamente rotas públicas e internas, métodos, parâmetros, campos, tipos, nulabilidade, formatos, enums, paginação, status, códigos de erro, segurança e headers obrigatórios. Também verificam que responses não expõem entidades de domínio.

Exemplos JSON canônicos são fixtures pequenas e legíveis. OpenAPI é validado por operações, schemas, segurança, responses e headers relevantes; não será usado snapshot textual integral sensível a ordenação ou metadado sem efeito contratual.

#### Contratos de eventos

Cada evento prova `eventId`, nome, versão, `occurredAtUtc`, correlation ID, causation ID, identificadores funcionais, payload, serialização e desserialização. Versão incompatível e mensagem inválida seguem o tratamento e a DLQ definidos nos SDDs. Campo adicional é tolerado somente quando não altera semântica ou obrigatoriedade aprovada.

O produtor serializa uma fixture canônica e o consumidor desserializa um exemplo independente. Não se reutiliza a mesma chamada ou objeto nos dois lados como única prova, pois isso poderia preservar um erro comum.

Microsserviços não compartilham entidade de domínio ou DTO interno para obter compatibilidade artificial.

#### Arquitetura

O projeto de arquitetura verifica:

- Domain sem dependência de Application, Infrastructure ou API;
- Application sem referência a Infrastructure;
- ausência de referências internas entre microsserviços;
- Gateway sem EF Core, banco, RabbitMQ ou domínio;
- bancos e migrations pertencentes ao serviço proprietário;
- contratos de integração separados do domínio;
- endpoints sem regra empresarial;
- ausência de Service Locator e dependências proibidas;
- frontend sem URLs de Identity, Inventory, Billing ou RabbitMQ;
- frontend acessando exclusivamente o Gateway.

Reflexão, referências de projetos e inspeções determinísticas de arquivos serão usadas antes de considerar biblioteca arquitetural adicional.

#### Segurança

A suíte verifica:

- matriz de rotas anônimas, autenticadas e `Admin`;
- JWT inválido, expirado, com algoritmo, issuer ou audience incorretos;
- distinção entre `401` e `403`;
- ausência de open redirect;
- bearer nunca enviado a destino externo;
- seed sem credencial padrão embutida;
- startup recusando configuração sensível ausente;
- respostas inesperadas sem detalhes internos;
- logs sem senha, token, Idempotency-Key, ETag ou payload proibido;
- correlation ID válido e sanitizado;
- configuração e arquivos rastreados sem segredo conhecido.

São sentinelas de regressão e não representam auditoria de segurança ou teste de invasão completo.

#### Fixtures contratuais

Fixtures são mínimas, versionadas, sem credenciais reais e independentes da ordem de propriedades quando ela não pertence ao contrato. Mudança incompatível exige decisão prévia no SDD correspondente; atualizar snapshot automaticamente não constitui aprovação.

### 7.6 Bloco 6 - Testes ponta a ponta e fluxos distribuídos

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### E2E pelo navegador

Playwright comprova os fluxos percebidos pelo usuário:

1. autenticar como Admin e encerrar a sessão;
2. cadastrar e consultar Product;
3. criar Invoice e administrar itens;
4. emitir com estoque suficiente;
5. acompanhar `Pending` e `AwaitingStock`;
6. observar fechamento e solicitação de impressão;
7. receber rejeição por saldo insuficiente;
8. corrigir a Invoice rejeitada e iniciar nova emissão;
9. verificar bloqueios para usuário sem permissão;
10. validar páginas críticas quanto a responsividade e acessibilidade.

O teste intercepta `window.print()` para provar seu acionamento, sem criar nova operação de backend ou alegar impressão física.

Dados do cenário principal são preparados pela interface ou por APIs públicas. O E2E de navegador não insere Product ou Invoice diretamente no banco para abreviar o fluxo que pretende comprovar.

#### Testes sistêmicos do backend

`Korp.System.EndToEndTests` comprova cenários distribuídos difíceis de expressar pela interface:

- fluxo Billing → RabbitMQ → Inventory → RabbitMQ → Billing;
- mesma Idempotency-Key sem nova baixa;
- comandos concorrentes sobre a mesma Invoice;
- duas Invoices disputando a última unidade;
- baixa atômica de múltiplos Products;
- redelivery sem movimento duplicado;
- Outbox durante indisponibilidade e publicação após recuperação;
- retry técnico e DLQ;
- resultado anterior à transição para `AwaitingStock`;
- caminho para `ManualIntervention`;
- reinício de consumer sem perda da intenção persistida.

APIs e efeitos públicos — estado da Invoice, processo e saldo — são preferidos. Inbox, Outbox, filas ou DLQ são inspecionados diretamente somente quando o requisito técnico não possui representação pública suficiente.

#### Ambiente completo

Um perfil exclusivo do Docker Compose contém frontend, Gateway, três microsserviços, três bancos PostgreSQL, RabbitMQ, runner sistêmico e runner Playwright.

Falhas são induzidas de maneira controlada pelo ambiente de teste, sem espera aleatória ou ramo artificial no código de produção. O mecanismo operacional exato para interromper e restaurar dependências será fechado no bloco Docker sem adicionar plataforma externa desnecessária.

#### Espera determinística

Fluxo assíncrono aguarda condição observável com prazo máximo explícito, intervalo limitado, diagnóstico do último estado e encerramento imediato quando atendido. Espera fixa não constitui prova de sucesso.

Timeout falha o teste com diagnóstico; nunca converte processo ativo em rejeição ou sucesso.

#### Limite da suíte

E2E cobre fluxos críticos e cooperação entre fronteiras. Não repete todas as combinações de validação já comprovadas nos níveis inferiores. Cenário E2E deve justificar seu custo por risco ou integração que não possa ser demonstrada com fidelidade em suíte menor.

### 7.7 Bloco 7 - Dados, isolamento, determinismo e paralelismo

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Identidade da execução

Cada execução recebe `testRunId`, utilizado em dados funcionais, e-mails administrativos de teste, correlação, recursos isolados, arquivos temporários e diagnóstico. Identificadores do domínio continuam seguindo seus contratos; o run ID apenas evita colisões e permite localizar evidências.

#### PostgreSQL

Cada microsserviço usa banco e credencial próprios. Migrations são aplicadas antes da suíte; seu histórico é preservado. A limpeza remove somente dados das tabelas pertencentes ao serviço e reinicia sequence apenas quando uma prova exige estado conhecido.

A suíte não depende de IDs de execução anterior e confirma estado inicial antes do primeiro cenário. A limpeza é explícita, centralizada por serviço e não fica espalhada pelos testes.

Rollback de transação externa não será estratégia universal porque background workers, múltiplos contextos e mensageria não participariam corretamente dela.

#### RabbitMQ

A execução usa broker ou virtual host exclusivo do ambiente de teste. A topologia é declarada idempotentemente; filas principais, retries e DLQs iniciam sem resíduo e são diagnosticadas e limpas após cenário interrompido.

Cenários que interrompem broker, consumer ou publisher são serializados e restauram o ambiente mesmo quando falham.

#### Paralelismo

- testes unitários independentes podem executar em paralelo;
- testes frontend isolados podem usar o paralelismo seguro do Vitest;
- testes que compartilham banco, fila ou host pertencem a collections serializadas;
- serviços diferentes só executam simultaneamente quando seus recursos estiverem isolados;
- testes sistêmicos e Playwright são seriais inicialmente;
- nenhum cenário depende da ordem ou de dado criado por outro teste.

O paralelismo poderá ser ampliado somente após evidência de estabilidade e sem relaxar o isolamento.

#### Tempo e identificadores

Regras temporais unitárias usam `TimeProvider`; polling frontend usa relógio falso. Integrações entre processos usam relógio UTC real e tolerâncias explícitas, sem asserção de instante exato sujeita a agendamento.

UUID aleatório é aceito quando apenas unicidade importa. Fonte fixa ou controlada é usada quando repetição, causalidade ou correlação pertence à asserção. Cultura e timezone dos containers são definidos, enquanto persistência permanece UTC.

#### Dados

Builders e fixtures usam valores válidos, explícitos e semanticamente claros. Dados sensíveis são fictícios. São proibidos dados pessoais reais, tokens reais, cópia de produção, seed global excessivo e registros compartilhados mutáveis.

#### Diagnóstico

Falha distribuída preserva cenário, testRunId, último estado observado, correlation ID, serviços envolvidos, timeout e logs sanitizados relevantes. Senha, JWT, Idempotency-Key, ETag e payload proibido permanecem ausentes da evidência.

### 7.8 Bloco 8 - Cobertura, Docker, evidências e gates

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Cobertura backend

O gate mínimo de 80% de linhas é aplicado separadamente a Domain, Application, Infrastructure, API, Gateway e Building Blocks que contenham lógica manual. Assembly exclusivamente declarativo de contratos pode aparecer no relatório sem gate individual, mediante classificação explícita.

Não podem ser excluídos domínio, casos de uso, handlers, erros, repositories, adapters manuais, consumers, publishers, Inbox, Outbox, Process Manager, autenticação, políticas próprias do Gateway ou lógica vinculada a critério de aceite.

Migrations geradas, código comprovadamente gerado, designer, bootstrap estritamente declarativo e contratos sem lógica podem ser excluídos de modo explícito e revisável.

Coverlet produz Cobertura XML e determina o gate por assembly. ReportGenerator consolida HTML e resumo, mas não decide aprovação.

#### Cobertura frontend

`@vitest/coverage-v8` será a dependência de desenvolvimento aprovada para instrumentação do Vitest durante a futura implementação. O código frontend manual possui gate mínimo de 80% de linhas; branch coverage é coletada e publicada sem limiar inicial.

Código gerado, configuração declarativa trivial e contratos sem lógica podem ser excluídos explicitamente. Sessão, interceptors, guards, polling, erros, idempotência e comportamento visual manual não podem ser retirados do gate.

A saída inclui formato compatível com Cobertura e resumo textual.

#### Etapas oficiais em Docker

1. build em configuração de entrega;
2. unitários backend;
3. arquitetura;
4. unitários e componentes Angular;
5. integração Identity;
6. integração Inventory;
7. integração Billing;
8. integração Gateway;
9. testes sistêmicos distribuídos;
10. E2E Playwright e acessibilidade;
11. consolidação da cobertura;
12. avaliação dos gates e resumo.

O SDD-11 define nomes finais de serviços, profiles e comandos do Compose. O host precisa somente de Docker e Docker Compose.

#### Artefatos

```text
artifacts/
|- test-results/
|  |- backend/
|  |- frontend/
|  `- e2e/
|- coverage/
|  |- backend/
|  |- frontend/
|  |- summary.md
|  `- report/
`- diagnostics/
```

Artefatos são gerados sob demanda, ignorados pelo Git e não constituem fonte da verdade permanente.

#### Gate da execução

Uma execução é aprovada somente quando:

- builds não possuem erro ou novo aviso injustificado;
- nenhum teste obrigatório falha, está ignorado ou desabilitado;
- backend e frontend atingem seus gates de linha;
- branch coverage é publicada;
- arquitetura, integrações e E2E críticos passam;
- infraestrutura exigida foi realmente utilizada;
- nenhum retry automático oculta instabilidade;
- relatórios e diagnósticos permanecem sanitizados;
- provas executadas estão vinculadas à rastreabilidade.

Falha de infraestrutura ainda representa execução não aprovada, embora seja diagnosticada separadamente de regressão funcional.

#### Evidência final

O resumo registra comando, data, ambiente, versões de imagens, quantidade de testes, aprovados, falhos, ignorados, duração, cobertura por assembly, cobertura frontend, branches, E2E executados, limitações e validações não realizadas.

### 7.9 Bloco 9 - Critérios de aceite, riscos e rastreabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Critérios de aceite

| ID | Critério verificável |
|---|---|
| `CA-TST-01` | Cada critério dos SDDs aprovados possui ao menos uma prova principal identificada. |
| `CA-TST-02` | Testes unitários backend exercitam Domain e Application sem banco, HTTP ou RabbitMQ. |
| `CA-TST-03` | Testes Angular exercitam serviços e componentes com TestBed, Vitest e Harnesses aplicáveis. |
| `CA-TST-04` | APIs são testadas pelo pipeline ASP.NET Core real, incluindo autenticação, autorização, erros e headers. |
| `CA-TST-05` | Persistência é validada com PostgreSQL e migrations reais, sem InMemory, SQLite ou `EnsureCreated`. |
| `CA-TST-06` | Mensageria é validada com RabbitMQ real, incluindo confirms, acknowledgement, retry, redelivery e DLQ. |
| `CA-TST-07` | Contratos HTTP e eventos são verificados independentemente nos lados produtor e consumidor. |
| `CA-TST-08` | Testes arquiteturais impedem dependências e acessos contrários aos limites aprovados. |
| `CA-TST-09` | Sentinelas de segurança verificam JWT, permissões, segredos, logs e respostas sanitizadas. |
| `CA-TST-10` | Playwright comprova os fluxos críticos percebidos pelo usuário em navegador real. |
| `CA-TST-11` | Testes sistêmicos comprovam idempotência, concorrência, atomicidade e recuperação distribuída. |
| `CA-TST-12` | Cada execução começa com bancos, filas e dados isolados de execuções anteriores. |
| `CA-TST-13` | Fluxos assíncronos usam condições observáveis e não dependem de espera fixa. |
| `CA-TST-14` | Paralelismo não permite interferência entre testes ou dependência de ordem. |
| `CA-TST-15` | Cada assembly backend aplicável alcança no mínimo 80% de linhas. |
| `CA-TST-16` | O código manual do frontend alcança no mínimo 80% de linhas. |
| `CA-TST-17` | Branch coverage é coletada e publicada para backend e frontend. |
| `CA-TST-18` | A validação oficial é executável usando somente Docker e Docker Compose no host. |
| `CA-TST-19` | Teste obrigatório ignorado, desabilitado, instável ou aprovado somente após retry não satisfaz o gate. |
| `CA-TST-20` | Artefatos registram resultados, cobertura, ambiente e limitações sem expor dados sensíveis. |

#### Provas planejadas

| ID | Prova planejada | Critério |
|---|---|---|
| `TST-TST-001` | Auditar vínculo entre critérios, suítes e matriz | CA-TST-01 |
| `TST-TST-002` | Executar unitários backend sem dependência técnica externa | CA-TST-02 |
| `TST-TST-003` | Executar unitários e componentes Angular | CA-TST-03 |
| `TST-TST-004` | Executar matriz HTTP pelo test host real | CA-TST-04 |
| `TST-TST-005` | Executar integrações em PostgreSQL migrado | CA-TST-05 |
| `TST-TST-006` | Executar integrações no RabbitMQ real | CA-TST-06 |
| `TST-TST-007` | Validar exemplos canônicos HTTP e eventos em produtor e consumidor | CA-TST-07 |
| `TST-TST-008` | Executar sentinelas de arquitetura | CA-TST-08 |
| `TST-TST-009` | Executar matriz de segurança e varredura de evidências | CA-TST-09 |
| `TST-TST-010` | Executar fluxos críticos Playwright | CA-TST-10 |
| `TST-TST-011` | Executar cenários sistêmicos distribuídos | CA-TST-11 |
| `TST-TST-012` | Repetir suíte a partir de ambiente limpo e sem resíduo | CA-TST-12 |
| `TST-TST-013` | Inspecionar e executar esperas assíncronas determinísticas | CA-TST-13 |
| `TST-TST-014` | Executar política aprovada de paralelismo e isolamento | CA-TST-14 |
| `TST-TST-015` | Aplicar gate Coverlet por assembly backend | CA-TST-15 |
| `TST-TST-016` | Aplicar gate Vitest ao código frontend manual | CA-TST-16 |
| `TST-TST-017` | Gerar branch coverage backend e frontend | CA-TST-17 |
| `TST-TST-018` | Executar fluxo oficial em host contendo apenas Docker e Compose | CA-TST-18 |
| `TST-TST-019` | Auditar testes ignorados, retries e instabilidade | CA-TST-19 |
| `TST-TST-020` | Gerar e inspecionar resumo e artefatos sanitizados | CA-TST-20 |

Essas provas combinam execução de suítes, inspeção de configuração, sentinelas, relatórios, ambiente Docker limpo e auditoria documental. Não serão criados testes artificiais que apenas confirmem a existência da infraestrutura.

#### Riscos e mitigação

| Risco | Consequência | Mitigação aprovada |
|---|---|---|
| Suíte lenta demais | Feedback tardio e abandono dos testes | Pirâmide equilibrada e E2E limitado a fluxos críticos |
| Infraestrutura instável | Falso negativo e flakiness | Recursos exclusivos, readiness e espera por condição |
| Cobertura inflada | Percentual sem proteção comportamental | Gate por assembly e exclusões revisáveis |
| Mocks produzirem falsa confiança | Diferença não detectada em banco ou broker | PostgreSQL e RabbitMQ reais nas integrações |
| E2E ocultar causa | Diagnóstico caro | Provas menores e correlação entre serviços |
| Dados residuais | Resultado dependente da ordem | Limpeza centralizada e testRunId |
| Paralelismo prematuro | Interferência entre cenários | Serialização inicial de recursos compartilhados |
| Snapshot aceitar quebra | Contrato alterado sem decisão | Comparação semântica e revisão obrigatória |
| Segredos nos artefatos | Vazamento de credencial ou dado | Allowlist, sanitização e sentinelas |
| Alterar teste para acompanhar defeito | Especificação invertida pelo código | Critério aprovado permanece fonte da verdade |

#### Marcadores de acompanhamento

`[ESP] [RAS] [ARC] [DOM] [API] [INT] [CON] [SEC] [TST] [E2E] [COV] [DCK] [OBS] [DOC] [QA]`

---

## 8. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os nove blocos estiverem aprovados;
- todo critério dos SDDs anteriores possuir nível de teste e prova planejados;
- dependências adicionais estiverem justificadas;
- isolamento e infraestrutura forem reproduzíveis sem estado externo;
- gates de cobertura e qualidade forem executáveis no Docker;
- limitações, testes manuais e evidências estiverem explícitos;
- índice e matriz de rastreabilidade estiverem atualizados.

A condição foi atendida em 2026-08-20: os nove blocos foram aprovados, cada critério possui prova planejada e a auditoria não encontrou decisão técnica pendente.

A aprovação deste documento estabiliza a estratégia de testes, mas não autoriza implementação antes da baseline documental conjunta.
