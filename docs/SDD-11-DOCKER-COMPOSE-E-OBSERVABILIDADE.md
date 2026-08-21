# SDD-11 - Docker Compose e Observabilidade

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-20
> Gate A: aprovado em 2026-08-20
> Dependências: SDD-01, SDD-04 a SDD-10, ADR-004, ADR-008, ADR-009, ADR-012 a ADR-014 e CONVENCOES-CODIGO.md

---

## 1. Objetivo

Especificar a composição operacional local do sistema, incluindo containers, redes, volumes, configuração, migrations, ordem de prontidão, health checks, logs, métricas, diagnóstico e profiles de validação.

O ambiente deve permitir desenvolver, executar e validar a solução em uma máquina que possua somente Docker e Docker Compose, preservando os limites entre serviços e sem armazenar segredos no repositório.

---

## 2. Requisitos rastreados

- `OBR-001`, `OBR-016`, `OBR-019` a `OBR-022`;
- `DIF-001`, `DIF-004`, `DIF-005`, `DIF-007` e `DIF-009`;
- `QLT-002` a `QLT-008`;
- critérios operacionais e de observabilidade aprovados nos SDDs 04 a 10.

---

## 3. Escopo previsto

- serviços, profiles, redes, portas e exposição local;
- imagens, builds, configuração e segredos;
- PostgreSQL, bancos, credenciais, volumes e migrators;
- RabbitMQ, persistência, topologia e interface de administração;
- frontend estático, Gateway e APIs;
- startup, readiness, dependências e desligamento gracioso;
- liveness, readiness e diagnóstico de dependências;
- logs estruturados, correlação, tracing e métricas;
- profiles e comandos oficiais de desenvolvimento e testes;
- artefatos, limpeza segura e diagnóstico operacional.

---

## 4. Fora do escopo

- Kubernetes, service mesh ou orquestrador de produção;
- cloud, infraestrutura como código ou deploy remoto;
- plataforma externa de logs, APM ou métricas;
- alta disponibilidade, replicação ou backup automatizado;
- TLS público e gerenciamento produtivo de certificados;
- autoscaling ou service discovery externo;
- alteração de regras funcionais ou contratos aprovados;
- implementação do Compose final durante a macroetapa documental.

---

## 5. Blocos de decisão

1. topologia, profiles, redes e exposição;
2. imagens, configuração e segredos;
3. PostgreSQL, migrations e volumes;
4. RabbitMQ, topologia, persistência e administração;
5. startup, dependências, readiness e desligamento;
6. health checks e diagnóstico de dependências;
7. logs, correlação, tracing e métricas;
8. comandos, profiles de teste, artefatos e falhas controladas;
9. critérios de aceite, riscos e rastreabilidade.

---

## 6. Decisões herdadas

- o host necessita somente de Docker e Docker Compose;
- builds e imagens são multi-stage e possuem versões fixadas;
- Angular acessa somente o Gateway;
- Gateway usa HTTP interno para Identity, Inventory e Billing e não acessa bancos ou RabbitMQ;
- Billing usa HTTP interno para consultar Product no Inventory;
- RabbitMQ comunica somente Inventory e Billing;
- cada microsserviço possui PostgreSQL e credencial próprios;
- migrations são executadas por migrators, nunca no startup das APIs;
- serviços validam configuração obrigatória e falham de modo explícito;
- liveness não depende de infraestrutura externa;
- readiness e diagnóstico não expõem endereço, porta, credencial ou exceção;
- logs são estruturados e sanitizados;
- não será adicionada plataforma externa de observabilidade;
- execução de testes e cobertura segue o SDD-10.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Topologia, profiles, redes e exposição

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Serviços previstos

```text
frontend
gateway-api
identity-api
inventory-api
billing-api
identity-db
inventory-db
billing-db
identity-migrator
inventory-migrator
billing-migrator
rabbitmq
backend-unit-tests
backend-integration-tests
system-tests
frontend-tests
playwright-tests
coverage-report
```

Runners podem ser consolidados na implementação quando a mesma imagem e entrypoint preservarem comandos e resultados independentes. A consolidação não pode esconder qual etapa falhou.

#### Entrada local

Nginx serve os artefatos Angular e encaminha `/api/*`, `/health/*` e, somente em Development, `/openapi/*` ao Gateway sem transformar contrato, autenticar ou aplicar regra de negócio.

```text
Navegador
  -> frontend/Nginx
       -> arquivos Angular
       `-> /api/*, /health/* e /openapi/* permitido -> Gateway
```

O frontend usa `apiBaseUrl` vazio para mesma origem. Nomes internos não chegam ao bundle ou à configuração pública. Gateway permanece a única entrada das APIs, embora não seja publicado diretamente no host no profile local padrão.

A aplicação fica disponível inicialmente em `http://localhost:8080`, vinculada a loopback. A porta pode ser sobrescrita por variável não secreta.

#### Redes

| Rede | Membros | Comunicação permitida |
|---|---|---|
| `edge` | frontend, Gateway | Assets e tráfego HTTP de entrada |
| `identity-http` | Gateway, Identity | Login |
| `inventory-http` | Gateway, Inventory, Billing | Product público e snapshot interno |
| `billing-http` | Gateway, Billing | Invoice e processo |
| `messaging` | Inventory, Billing, RabbitMQ | Eventos de baixa e resultado |
| `identity-data` | Identity, migrator, identity-db | Persistência exclusiva |
| `inventory-data` | Inventory, migrator, inventory-db | Persistência exclusiva |
| `billing-data` | Billing, migrator, billing-db | Persistência exclusiva |

Gateway não participa de `messaging` ou redes de dados. Identity não participa da mensageria. Billing alcança Inventory somente pela rede HTTP prevista. A segmentação complementa autenticação, credenciais próprias e limites no código.

#### Portas

Por padrão, somente frontend é publicado em `127.0.0.1`. Gateway, APIs, PostgreSQL e AMQP permanecem internos. RabbitMQ Management pode ser publicado em loopback exclusivamente pelo profile operacional. Health e OpenAPI seguem as superfícies aprovadas e não justificam expor diretamente os microsserviços.

#### Profiles

| Profile | Responsabilidade |
|---|---|
| `local` | Aplicação completa, infraestrutura e migrators |
| `tooling` | Restore, build e utilitários |
| `tests` | Infraestrutura isolada, aplicações e runners |
| `operations` | Ferramentas locais opcionais de diagnóstico |
| `coverage` | Consolidação e relatório de cobertura |

O fluxo local terá comando único equivalente a `docker compose --profile local up --build`. Profiles podem ser combinados explicitamente quando necessário, sem alterar imagens.

#### Restrições

- não usar `network_mode: host` ou container privilegiado;
- não montar Docker socket nas aplicações;
- bind mount de código somente em tooling ou desenvolvimento controlado;
- execução normal usa imagens construídas;
- persistência local usa volumes nomeados;
- runners e migrators são efêmeros;
- endereço interno não é publicado ao frontend;
- porta externa pode variar sem modificar a imagem.

### 7.2 Bloco 2 - Imagens, configuração e segredos

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Imagens

O SDK .NET existe somente em build, migration e testes; APIs usam runtime ASP.NET Core e usuário não root. Imagens finais não carregam SDK, ferramentas de desenvolvimento, caches ou código-fonte.

Node.js existe somente no build Angular. `npm ci` respeita o lockfile e uma imagem Nginx sem privilégios serve os artefatos. A imagem final não contém Node.js, npm ou fontes do projeto.

Tags `latest` são proibidas. Tags exatas e digests das imagens externas serão registrados na implementação e novamente na evidência de entrega. Atualização de segurança exige rebuild e validação deliberados.

#### Configuração pública

O Compose pode fornecer ambiente ASP.NET Core, hosts e portas internos, nomes de bancos, issuer, audience, origens CORS, timeouts, intervalos, lotes, topologia RabbitMQ, níveis de log, apiBaseUrl público e porta local.

Configuração obrigatória é tipada e validada no startup. Ausência, formato inválido ou destino contrário às fronteiras impede inicialização ou readiness conforme o SDD proprietário, sem fallback escondido.

#### Segredos

Segredos locais são arquivos ignorados e montados por Docker Compose secrets:

```text
.secrets/
|- jwt-signing-key
|- identity-db-password
|- inventory-db-password
|- billing-db-password
|- rabbitmq-admin-password
|- billing-rabbitmq-password
|- inventory-rabbitmq-password
`- identity-seed-password
```

As aplicações .NET utilizam provider nativo de configuração por arquivos. Conteúdo secreto não é copiado para `config.json`, bundle Angular, build args, layers, health, logs ou mensagens de erro.

Chave JWT, senhas de PostgreSQL, senhas do RabbitMQ e senha inicial do Admin são secretas. O mesmo material JWT aprovado é montado somente em Identity, Gateway, Inventory e Billing. Cada senha de banco chega apenas ao banco, API e migrator proprietários. Cada credencial RabbitMQ chega somente ao processo operacional ou serviço que a utiliza.

#### Exemplos versionados

O repositório contém `.env.example` com valores públicos e placeholders e `.secrets.example/README.md` com instruções. Não contém chave válida, senha funcional ou connection string completa. `.env`, variantes locais e `.secrets/` permanecem ignorados.

#### Configuração Angular

No startup, o container frontend materializa `/assets/config.json` a partir de template controlado com `apiBaseUrl` vazio para mesma origem. O arquivo nunca aceita token, segredo ou nome interno; Nginx encaminha `/api` ao Gateway.

#### Endurecimento

Quando compatível, containers usam filesystem raiz somente leitura, `tmpfs` mínimo, usuário não root, capabilities removidas e limites documentados. Não usam modo privilegiado ou Docker socket. O processo principal recebe sinais diretamente e logs seguem stdout e stderr.

#### Reprodutibilidade

Restore usa versões fixadas, frontend usa `npm ci`, `.dockerignore` limita contexto e manifests são copiados antes do código para aproveitar cache. Configuração externa varia sem reconstruir a imagem funcional.

### 7.3 Bloco 3 - PostgreSQL, migrations e volumes

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Versão e instâncias

PostgreSQL 16 será usado por ser uma versão madura e suportada. Patch e digest exatos serão fixados depois da verificação da imagem na implementação e registrados na entrega; atualização não ocorrerá por tag flutuante.

```text
identity-db  -> identity_db
inventory-db -> inventory_db
billing-db   -> billing_db
```

Cada instância possui usuário, senha, banco, volume, rede e health próprios. Nenhuma publica porta por padrão.

#### Migrators

Serão criados executáveis console independentes:

```text
Korp.Identity.Migrator
Korp.Inventory.Migrator
Korp.Billing.Migrator
```

Cada migrator valida configuração, conecta somente ao banco proprietário, aplica migrations pendentes, registra resultado sanitizado e termina. Não hospeda HTTP, consumer ou publisher, não executa caso de uso funcional e não permanece ativo.

O Identity Migrator executa também o inicializador idempotente do papel e Admin depois das migrations. Falha em migration ou seed produz exit code não zero e impede o início da Identity API.

#### Ordem

```text
banco healthy
  -> migrator concluído com sucesso
       -> API proprietária pode iniciar
```

O Compose usa condições explícitas de health e conclusão; não usa atraso fixo para presumir prontidão. Falha de preparação não é mascarada ou repetida infinitamente.

RabbitMQ indisponível não impede Inventory ou Billing de iniciar após seus bancos e migrations. O componente de mensageria fica degradado e segue a recuperação aprovada.

#### Política de migrations

- migrations ficam na Infrastructure proprietária e são versionadas;
- somente o migrator chama a aplicação de migrations;
- APIs não usam `EnsureCreated` ou `Migrate` no startup;
- SQL gerado é revisado antes da entrega;
- downgrade ou rollback automático de schema são proibidos;
- nova execução reconhece migrations já aplicadas;
- mudança destrutiva exige decisão específica e plano de recuperação.

#### Volumes

```text
identity-db-data
inventory-db-data
billing-db-data
rabbitmq-data
```

`docker compose down` preserva volumes. Remoção de dados será comando separado e explicitamente destrutivo, limitado ao projeto Compose local.

Testes usam volumes ou recursos efêmeros, não reutilizam estado local, vinculam limpeza ao testRunId e aplicam migrations reais em ambiente conhecido.

#### Health e conexão

PostgreSQL usa health nativo equivalente a `pg_isready`, sem senha na linha de comando. Host, porta, banco e usuário são configuração operacional; senha vem do secret proprietário. Connection string completa não é gravada em Compose, log ou artefato.

#### Limites

Não serão implementados réplica, alta disponibilidade, backup automático, restore automatizado, pooler externo, banco compartilhado ou migrator central conhecedor dos três serviços.

### 7.4 Bloco 4 - RabbitMQ, persistência e administração

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Versão e ambiente

RabbitMQ 4.1 será usado com imagem oficial que inclua Management Plugin e permaneça compatível com RabbitMQ.Client 7.x. Patch e digest exatos serão verificados e fixados na implementação.

O ambiente local usa vhost `/korp` e volume `rabbitmq-data`. Testes usam broker ou vhost exclusivo e não reutilizam mensagens, DLQs ou definições locais.

#### Identidades

| Usuário | Responsabilidade |
|---|---|
| `korp-rabbit-admin` | Preparação e inspeção operacional |
| `korp-billing` | Publicações e consumo pertencentes ao Billing |
| `korp-inventory` | Publicações e consumo pertencentes ao Inventory |

O usuário `guest` não é usado pelas aplicações. Gateway, Identity e frontend não recebem endereço, usuário ou secret do RabbitMQ.

O inicializador concede configuração dos recursos aprovados, escrita nos exchanges necessários e leitura somente da fila consumida pelo serviço. A identidade administrativa não é usada por Inventory ou Billing.

#### Inicialização operacional

`rabbitmq-init` é processo efêmero que cria vhost, usuários e permissões depois do health básico do broker. Configuração repetida é idempotente; falha produz exit code não zero e diagnóstico sanitizado.

Inventory e Billing não dependem da conclusão desse processo para iniciar suas APIs. Seus componentes de mensageria tentam conectar e permanecem degradados até broker, vhost e permissões estarem disponíveis. Depois da recuperação, cada serviço declara e valida sua topologia antes de iniciar consumers e dispatchers.

O inicializador operacional não declara eventos, acessa bancos ou conhece Invoice e Product.

#### Topologia

São preservados exatamente os exchanges `korp.billing.v1`, `korp.inventory.v1`, `korp.retry.v1` e `korp.dead-letter.v1`, duas filas principais, seis filas de retry, duas DLQs, bindings e routing keys aprovados no SDD-07.

Filas e exchanges são duráveis; mensagens são persistentes; publishers usam confirms e `mandatory`; consumers usam acknowledgement manual. Recurso existente incompatível impede o início da mensageria e não é apagado ou recriado automaticamente.

#### Health do broker

O container usa comandos nativos para verificar processo Erlang, aplicação RabbitMQ e resposta local. Seu health não depende de conexão de Inventory ou Billing. Cada serviço reporta separadamente sua conexão, topologia, consumers e dispatchers.

#### Management

A interface fica disponível somente no profile `operations`, publicada em loopback e protegida pelo usuário administrativo. Não passa por Gateway ou Nginx e não é necessária ao fluxo normal. Serve para diagnóstico e demonstração, sem redrive automático.

#### Limites

Não serão adotados cluster, quorum queue em nó único, federation, shovel, delayed-message plugin, serviço gerenciado, exactly-once, limpeza automática de DLQ ou usuário compartilhado entre administração e aplicações.

### 7.5 Bloco 5 - Startup, dependências, readiness e desligamento

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Inicialização

```text
banco healthy -> migrator concluído -> API proprietária
RabbitMQ healthy -> rabbitmq-init -> mensageria conecta ou recupera
APIs iniciadas -> Gateway encaminha quando cada destino está disponível
Gateway iniciado -> frontend serve aplicação
```

Não existe `sleep` para presumir prontidão. Condições do Compose cobrem banco, migrator e inicializador; componentes de aplicação continuam responsáveis pela recuperação de dependência transitória.

Identity depende da conclusão do Identity Migrator; Inventory do Inventory Migrator; Billing do Billing Migrator. Inventory e Billing não dependem do RabbitMQ para iniciar HTTP. Gateway não exige saúde simultânea dos três destinos. Frontend pode servir durante recuperação do Gateway e apresenta indisponibilidade transitória conforme o SDD-09.

#### Estados operacionais

| Estado | Significado |
|---|---|
| Started | Processo iniciou |
| Live | Processo está ativo |
| Ready | Pode atender sua responsabilidade HTTP |
| Degraded | Responsabilidade principal disponível, dependência operacional indisponível |
| Unhealthy | Responsabilidade verificada não pode ser cumprida |

RabbitMQ indisponível degrada a mensageria de Inventory e Billing sem invalidar automaticamente suas superfícies HTTP.

#### Restart

PostgreSQL e RabbitMQ usam `unless-stopped`. APIs, Gateway e frontend possuem reinício limitado depois de falha inesperada. Migrators, inicializadores, runners e testes não entram em loop infinito.

Configuração inválida, seed ou migration defeituosa permanecem visíveis por exit code e log sanitizado. Restart não pode esconder erro determinístico.

#### Probe das imagens .NET

Será criado `tools/Korp.HealthProbe`, executável mínimo sem domínio, persistência ou configuração funcional. Ele consulta URL HTTP local, exige status esperado, usa timeout curto, não imprime body ou headers e retorna exit code apropriado ao Docker.

O mesmo artefato atende APIs e Gateway sem instalar `curl` ou ferramenta de desenvolvimento nas imagens finais. Nginx, PostgreSQL e RabbitMQ usam mecanismos nativos.

#### Readiness

- Identity, Inventory e Billing: configuração válida, banco acessível e schema esperado;
- Gateway: JWT, CORS, rotas, clusters e pipeline válidos;
- frontend: Nginx ativo e config público válido;
- infraestrutura: probes nativos do PostgreSQL e RabbitMQ.

Readiness de Inventory e Billing não exige RabbitMQ. A visão completa fica em `/health/dependencies`.

#### Desligamento

SIGTERM inicia desligamento gracioso. APIs param novo trabalho; Inventory e Billing interrompem entregas, permitem até 30 segundos ao handler atual, param novas reservas de Outbox, aguardam publicação em andamento e fecham channels e conexão.

Transação incompleta sofre rollback, mensagem sem ACK retorna ao broker e lease de Outbox expira. Compose concede margem superior ao prazo interno antes de SIGKILL. Migrator interrompido não informa sucesso; Nginx encerra conexões normalmente.

#### Recuperação

Banco recuperado volta a ser consultável sem migration automática na API. RabbitMQ recuperado provoca recriação de conexão, channels e topologia, seguida de retomada de Outbox e consumers. Gateway volta a encaminhar e frontend não precisa ser reconstruído.

Schema, topologia ou segredo incompatível exigem correção operacional e nunca são alterados silenciosamente.

### 7.6 Bloco 6 - Health checks e diagnóstico de dependências

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Endpoints

Gateway e microsserviços expõem anonimamente e em modo somente leitura:

```text
GET /health/live
GET /health/ready
GET /health/dependencies
```

Health não executa migration, seed, publicação, retry ou correção de estado.

`/health/live` verifica somente o processo, sem rede ou persistência. `/health/ready` confirma configuração e capacidade HTTP: APIs verificam banco e schema próprios; Gateway verifica JWT, CORS, rotas, clusters, políticas e pipeline sem exigir disponibilidade instantânea dos destinos.

`/health/dependencies` apresenta a visão operacional: Identity verifica banco; Inventory verifica banco, RabbitMQ, topologia, dispatcher e consumer; Billing acrescenta Inventory HTTP; Gateway consulta readiness de Identity, Inventory e Billing em paralelo, com timeout individual de dois segundos e sem retry.

#### Status

Health atendido retorna `200`; readiness não atendido, dependência operacional indisponível ou falha inesperada retorna `503`. Billing e Inventory podem responder `200` em ready e `503` em dependencies durante indisponibilidade do RabbitMQ, preservando sua capacidade de persistir trabalho local.

#### Representação

```json
{
  "status": "Healthy",
  "service": "billing",
  "checks": [
    { "name": "database", "status": "Healthy" },
    { "name": "rabbitmq", "status": "Healthy" }
  ]
}
```

Status permitido é `Healthy`, `Degraded` ou `Unhealthy`. Nomes pertencem a allowlist. Host, porta, connection string, usuário, vhost, tabela, fila, exception, stack trace, segredo e payload não aparecem.

#### Exposição

Nginx encaminha `/health/*` ao Gateway. Health individual dos microsserviços permanece interno; não existem rotas públicas por serviço. O probe do frontend verifica Nginx e `config.json` internamente sem ocupar a rota pública agregada.

No ambiente local Development, `/openapi/*` também é encaminhado ao Gateway conforme o SDD-08. Essa superfície não é habilitada em ambiente de entrega que não seja Development e não altera a integração funcional do Angular.

Responses usam `Cache-Control: no-store` e correlação aplicável. Parâmetros não podem escolher host ou dependência.

#### Logs e provas

Consulta saudável não gera log informativo por chamada. Apenas transições produzem eventos `dependency_health_changed` e `service_readiness_changed`.

Testes cobrem independência do liveness, banco indisponível, degradação do RabbitMQ, agregação do Gateway, timeout, ausência de retry, sanitização e roteamento Nginx.

### 7.7 Bloco 7 - Logs, correlação, tracing e métricas

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Abordagem

Observabilidade usa `ILogger<T>` com formatter JSON nativo, W3C Trace Context, `ActivitySource`, `System.Diagnostics.Metrics` e OpenTelemetry para instrumentação e exposição Prometheus. Não serão adicionados Grafana, Loki, Jaeger, collector ou plataforma externa.

As dependências OpenTelemetry estritamente necessárias para ASP.NET Core, HttpClient, runtime e Prometheus serão centralizadas e submetidas ao plano da implementação.

#### Logs

Logs seguem stdout e stderr e incluem, quando aplicável, timestamp, level, eventId, eventName, service, environment, traceId, spanId, correlationId, causationId, operation, outcome, duration e failureCode.

Body, payload, JWT, senha, hash, Idempotency-Key, ETag, connection string, e-mail, descrição, itens, query, path com UUID, headers completos e segredo não aparecem. Stack trace não é emitida em nível informativo ou devolvida ao usuário.

`Information` atende marcos e resultados esperados; `Warning`, indisponibilidade transitória, retry e atraso; `Error`, falha técnica esgotada ou inesperada; `Critical`, configuração, schema ou topologia incompatível. Rejeição funcional esperada não é automaticamente erro técnico.

#### Correlação e tracing

Gateway valida ou cria X-Correlation-ID e propaga contexto W3C. HTTP interno mantém traceparent, tracestate e correlação. Eventos mantêm correlation ID e causation ID; publicação e consumo produzem Activities próprias. Delivery tag ou trace ID não substituem a correlação funcional.

Replay idempotente preserva a correlação histórica da operação e pode registrar separadamente a requisição atual.

Entrada HTTP, chamadas internas, proxy, queries relevantes, publicação, consumo e casos de uso críticos são instrumentados. Não haverá armazenamento de traces; a entrega demonstra propagação e deixa OTLP como evolução futura.

#### Métricas

São preservadas as métricas definidas nos SDDs de Identity, Inventory, Billing, emissão e Gateway. Labels possuem conjunto finito. UUID, usuário, e-mail, IP, correlation ID, trace ID, Product ID, Invoice ID, código e path bruto são proibidos como labels.

Cada backend expõe internamente `GET /metrics` em formato Prometheus. Nginx e Gateway não publicam métricas internas dos microsserviços. O endpoint não executa ação funcional nem inclui dado sensível.

#### Retenção local

O driver de logs do Compose usa rotação limitada, inicialmente `max-size: 10m` e `max-file: 3`. Logs não usam bind mount por padrão. Evidência necessária é copiada de forma sanitizada para `artifacts/diagnostics`.

Angular não recebe analytics externo e mantém o diagnóstico aprovado no SDD-09.

#### Provas

Serão testados propagação HTTP e RabbitMQ, continuidade de trace, campos estruturados, sentinelas proibidas, incremento de métricas, cardinalidade, ausência de `/metrics` público, rotação e capacidade de reconstruir o fluxo por correlação.

### 7.8 Bloco 8 - Comandos, testes, artefatos e falhas controladas

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Arquivos

`compose.yaml` contém ambiente local, tooling e operações. `compose.test.yaml` funciona como override mínimo para isolamento, recursos efêmeros, runners e configuração de teste, sem duplicar toda a composição.

#### Execução local

```text
docker compose --profile local up --build --wait
docker compose --profile local down
```

A aplicação fica em `http://localhost:8080`. O comando normal de down preserva dados.

```text
docker compose --profile local down --volumes --remove-orphans
```

O comando acima é documentado como destrutivo e limitado ao projeto Compose local. Nunca é executado implicitamente no fluxo de uso normal.

#### Tooling

```text
docker compose --profile tooling run --rm backend-build
docker compose --profile tooling run --rm frontend-build
```

Restore, build, formatação, migrations e testes executam dentro de containers.

#### Validação isolada

A execução usa nome `korp-erp-tests-<testRunId>` e segue:

1. criar infraestrutura isolada;
2. aguardar health;
3. aplicar migrations e seed de teste;
4. iniciar APIs, Gateway e frontend;
5. executar unitários e arquitetura;
6. executar integrações;
7. executar testes sistêmicos;
8. executar Playwright;
9. consolidar cobertura;
10. coletar diagnóstico;
11. remover containers, redes e volumes efêmeros.

Scripts `.ps1` e `.sh` podem coordenar apenas comandos Docker Compose. Não executam .NET, npm, PostgreSQL ou RabbitMQ no host. Runner não recebe Docker socket.

#### Falhas controladas

O ambiente de teste pode parar e iniciar RabbitMQ, Inventory ou banco delimitado, reiniciar consumer, iniciar antes da dependência, manter Outbox pendente, provocar redelivery e fornecer topologia incompatível descartável.

A orquestração externa controla containers. Código de produção não recebe flag, endpoint ou ramo para simular falha. Cada cenário restaura dependências e usa recursos isolados.

#### Espera

Compose `--wait`, health checks e polling por condição observável substituem sleeps de prontidão. Toda espera possui timeout e último estado no diagnóstico.

#### Artefatos

Somente runners escrevem em `artifacts/test-results`, `artifacts/coverage` e `artifacts/diagnostics`. Aplicações não montam esse diretório.

Antes da limpeza são coletados resultados, cobertura, logs sanitizados relevantes, estado final, health agregado e informações das imagens.

#### Falha e limpeza

Falha preserva exit code, coleta diagnóstico, impede etapa dependente, limpa recursos efêmeros, mantém artefatos e reprova a execução. Preservar temporariamente o ambiente exige opção explícita local e não ocorre no CI.

#### Reprodutibilidade

Local e CI usam a mesma sequência. Podem variar apenas nome do projeto Compose, portas externas, localização dos secrets, retenção de artefatos e limites de recursos.

### 7.9 Bloco 9 - Critérios de aceite, provas e riscos

> Estado: Aprovado pelo engenheiro em 2026-08-20

#### Critérios e provas planejadas

| Critério | Comportamento verificável | Prova | Nível principal |
|---|---|---|---|
| `CA-OPS-01` | Todo container possui responsabilidade, profile e ciclo de vida explícitos | `TST-OPS-001` | Inspeção |
| `CA-OPS-02` | Frontend é a única porta normal e encaminha APIs somente ao Gateway | `TST-OPS-002` | Arquitetura/E2E |
| `CA-OPS-03` | Redes impedem Gateway → RabbitMQ/bancos e preservam ownership | `TST-OPS-003` | Arquitetura/integração |
| `CA-OPS-04` | Imagens são fixadas, multi-stage e não root quando aplicável | `TST-OPS-004` | Inspeção/segurança |
| `CA-OPS-05` | Segredo funcional não existe em repositório, bundle, imagem ou config público | `TST-OPS-005` | Segurança |
| `CA-OPS-06` | Config Angular de runtime contém somente apiBaseUrl público | `TST-OPS-006` | Integração |
| `CA-OPS-07` | Cada microsserviço usa PostgreSQL, credencial e volume próprios | `TST-OPS-007` | Arquitetura/integração |
| `CA-OPS-08` | Somente migrators aplicam migrations e API não inicia sobre schema incompleto | `TST-OPS-008` | Integração |
| `CA-OPS-09` | Identity Migrator cria papel e Admin idempotentemente sem redefinir senha | `TST-OPS-009` | Integração |
| `CA-OPS-10` | RabbitMQ possui vhost, usuários, permissões, volume e topologia aprovados | `TST-OPS-010` | Integração |
| `CA-OPS-11` | RabbitMQ indisponível degrada mensageria sem impedir HTTP de Inventory/Billing | `TST-OPS-011` | Recuperação/E2E |
| `CA-OPS-12` | Startup usa health e conclusão observável, sem sleep | `TST-OPS-012` | Inspeção/E2E |
| `CA-OPS-13` | Falha determinística permanece visível sem restart infinito | `TST-OPS-013` | Recuperação |
| `CA-OPS-14` | Desligamento preserva transação, ACK, Outbox e redelivery | `TST-OPS-014` | Integração/E2E |
| `CA-OPS-15` | Liveness, readiness e dependencies mantêm semânticas distintas | `TST-OPS-015` | Integração |
| `CA-OPS-16` | Gateway agrega dependências com timeout de dois segundos e sem retry | `TST-OPS-016` | Integração |
| `CA-OPS-17` | Health não expõe configuração ou diagnóstico sensível | `TST-OPS-017` | Segurança |
| `CA-OPS-18` | Logs são estruturados, correlacionáveis e sanitizados | `TST-OPS-018` | Segurança/integração |
| `CA-OPS-19` | W3C, correlation ID e causation ID atravessam HTTP e RabbitMQ | `TST-OPS-019` | Integração/E2E |
| `CA-OPS-20` | Métricas aprovadas são internas e não usam labels de alta cardinalidade | `TST-OPS-020` | Integração/inspeção |
| `CA-OPS-21` | Métricas internas e health individuais não são publicados pelo Nginx | `TST-OPS-021` | Segurança/E2E |
| `CA-OPS-22` | Ambiente completo exige somente Docker e Compose no host | `TST-OPS-022` | E2E |
| `CA-OPS-23` | Testes são isolados e induzem falhas sem ramo no código de produção | `TST-OPS-023` | Arquitetura/E2E |
| `CA-OPS-24` | Falha preserva evidência, reprova gate e limpa recursos efêmeros | `TST-OPS-024` | E2E/inspeção |
| `CA-OPS-25` | Dependências recuperadas retomam o fluxo sem reconstruir frontend | `TST-OPS-025` | Recuperação/E2E |

#### Riscos e mitigação

| Risco | Consequência | Mitigação aprovada |
|---|---|---|
| Muitas redes | Manutenção e diagnóstico difíceis | Nomes funcionais e testes arquiteturais |
| Bootstrap de secrets falhar | Ambiente não inicia | Validação antecipada e diagnóstico por nome da opção |
| Migrator divergir da API | Schema incompatível | Mesma Infrastructure e versão de imagem |
| Nginx parecer outro Gateway | Fronteira arquitetural ambígua | Proxy limitado a API, health e OpenAPI permitido |
| Imagem ficar desatualizada | Vulnerabilidade ou irreprodutibilidade | Patch e digest registrados e atualização deliberada |
| Health gerar falso positivo | Operação direcionada incorretamente | Live, ready e dependencies separados |
| Health bloquear trabalho persistível | Indisponibilidade artificial | RabbitMQ fora do readiness HTTP |
| Logs vazarem dados | Exposição de segredo ou negócio | Allowlist e testes sentinela |
| Métrica explodir cardinalidade | Memória e consulta degradadas | Labels finitos, sem identificadores |
| Volume esconder migration defeituosa | Ambiente local passa por estado antigo | Teste obrigatório em banco vazio |
| Reset atingir dados indevidos | Perda local | Project name fixo, alvo explícito e aviso destrutivo |
| Falha deixar recursos | Interferência e consumo | Cleanup garantido e projeto isolado |
| OpenTelemetry ampliar escopo | Atraso do núcleo | Instrumentação e Prometheus sem plataforma externa |
| Ambiente consumir recursos excessivos | Execução local inviável | Profiles separados e limites documentados |

#### Marcadores de acompanhamento

`[ARC] [CFG] [SEC] [DB] [MSG] [DCK] [HLT] [LOG] [TRC] [MET] [TST] [OBS] [DOC] [QA]`

---

## 8. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os nove blocos estiverem aprovados;
- cada serviço possuir rede, configuração e dependências explícitas;
- migrations, readiness e startup não dependerem de ordem temporal implícita;
- segredos e volumes possuírem tratamento seguro para o ambiente local;
- health, logs, tracing e métricas forem coerentes entre os serviços;
- comandos de execução e validação forem reproduzíveis;
- falhas controladas e recuperação possuírem provas planejadas;
- índice e matriz de rastreabilidade estiverem atualizados.

A condição foi atendida em 2026-08-20: os nove blocos foram aprovados, os 25 critérios possuem provas planejadas e a auditoria cruzada não encontrou decisão operacional pendente.

A aprovação especifica a operação local, mas não autoriza implementação antes da baseline documental conjunta.
