# SDD-08 - API Gateway

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-19
> Gate A aprovado em: 2026-08-19
> Dependências: SDD-01, SDD-03, SDD-04, SDD-05, SDD-06, SDD-07, ADR-004, ADR-006, ADR-009, ADR-010, ADR-013 e ADR-014

---

## 1. Objetivo

Especificar o API Gateway como entrada HTTP única do Angular, utilizando YARP para encaminhar rotas públicas aos serviços proprietários e aplicar somente responsabilidades de borda.

O documento deve tornar implementáveis roteamento, autenticação em profundidade, correlação, proteções HTTP, tratamento de indisponibilidade, exposição de OpenAPI, observabilidade e testes, sem transformar o Gateway em serviço de domínio ou participante da mensageria.

---

## 2. Requisitos rastreados

- `OBR-001`, no limite da entrada HTTP usada pelo Angular;
- `OBR-016`, como componente adicional sem descaracterizar os dois microsserviços de domínio;
- `DIF-001`, `DIF-003`, `DIF-008` e `DIF-009`;
- `QLT-001` a `QLT-008`;
- `APR-003`, `APR-008` e `APR-009`.

---

## 3. Escopo previsto

- fronteira pública e responsabilidades do Gateway;
- rotas YARP e destinos internos;
- preservação de métodos, corpos, query strings, headers e status;
- autenticação JWT e políticas por rota;
- propagação segura do bearer token;
- geração e propagação de correlation ID;
- CORS e proteções HTTP pertencentes à borda;
- limites de tamanho, timeout e eventual rate limiting aprovado;
- tratamento de destino indisponível;
- exposição separada dos documentos OpenAPI;
- health checks, logs, métricas e configuração;
- testes unitários, de integração, arquitetura e segurança.

---

## 4. Fora do escopo

- banco de dados, migrations ou persistência no Gateway;
- RabbitMQ, publishers, consumers, Inbox ou Outbox;
- autenticação de credenciais ou emissão de JWT;
- regras de Product, Invoice, estoque ou emissão;
- agregação de respostas de múltiplos serviços;
- transformação de DTOs de negócio;
- chamada do Gateway para a rota interna de Product;
- proxy entre Billing e Inventory;
- cache de respostas funcionais;
- service discovery externo ou balanceador de nuvem;
- interface Angular;
- composição final do Docker Compose.

---

## 5. Blocos de decisão

1. responsabilidades, fluxo e superfície pública;
2. rotas YARP, clusters, destinos e exclusões;
3. autenticação e autorização na borda;
4. headers, correlação e fidelidade do proxy;
5. CORS, limites e proteções HTTP;
6. falhas, OpenAPI e health checks;
7. configuração, ciclo de vida e observabilidade;
8. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- Angular acessa exclusivamente o Gateway no ambiente padrão;
- Gateway usa YARP e permanece stateless;
- Gateway possui conexão HTTP com Identity, Inventory e Billing;
- Billing acessa a rota interna de Inventory diretamente, sem passar pelo Gateway;
- Gateway não acessa bancos;
- Gateway não possui conexão com RabbitMQ;
- Gateway não coordena emissão nem baixa de estoque;
- cada rota possui um único serviço proprietário;
- caminhos públicos permanecem sob `/api/v1` e não recebem prefixo artificial;
- rota `/api/v1/internal/*` não é exposta;
- login é anônimo e pertence ao Identity;
- rotas de domínio usam `AuthenticatedUser` ou `AdminOnly` conforme SDD-03;
- Gateway valida JWT, mas Inventory e Billing repetem a validação local;
- bearer token recebido é encaminhado, não substituído;
- cada serviço preserva seu próprio documento OpenAPI;
- erros funcionais e status produzidos pelo destino permanecem sob responsabilidade do destino;
- logs não expõem token, payload sensível ou segredo;
- somente a porta do frontend/Nginx será publicada ao host no ambiente padrão; `/api`, `/health` e o OpenAPI permitido são encaminhados ao Gateway interno conforme SDD-11.

---

## 7. Decisões aprovadas

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Responsabilidades, fluxo e superfície pública

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Posição arquitetural

```text
Angular
    -> HTTP
        -> API Gateway
            -> HTTP -> Identity API
            -> HTTP -> Inventory API
            -> HTTP -> Billing API
```

As comunicações internas não passam pelo Gateway:

```text
Billing API -> HTTP interno -> Inventory API

Billing -> RabbitMQ -> Inventory
Inventory -> RabbitMQ -> Billing
```

#### Responsabilidades do Gateway

- oferecer a entrada HTTP única usada pelo Angular;
- encaminhar cada rota ao serviço proprietário;
- validar JWT nas rotas protegidas;
- aplicar políticas de autorização de borda;
- encaminhar o bearer token original;
- estabelecer e propagar o correlation ID;
- aplicar CORS e proteções HTTP aprovadas;
- expor documentos OpenAPI por caminhos distintos;
- representar indisponibilidade do destino de forma consistente;
- produzir logs, métricas e health checks da função de proxy.

#### Limites negativos

O Gateway não autentica credenciais, emite JWT, consulta usuários, valida regras de Product ou Invoice, transforma DTOs funcionais, coordena emissão, executa baixa, acessa bancos, participa do RabbitMQ, conhece Inbox, Outbox ou DLQ, agrega respostas, chama a rota interna de Inventory ou substitui a validação das APIs.

Validação JWT na borda é defesa adicional. Inventory e Billing continuam validando localmente o bearer encaminhado.

#### Superfície pública funcional

Identity:

```text
POST /api/v1/auth/login
```

Inventory:

```text
POST /api/v1/products
GET  /api/v1/products
GET  /api/v1/products/{productId}
```

Billing:

```text
POST   /api/v1/invoices
GET    /api/v1/invoices
GET    /api/v1/invoices/{invoiceId}
POST   /api/v1/invoices/{invoiceId}/items
PUT    /api/v1/invoices/{invoiceId}/items/{itemId}
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}
POST   /api/v1/invoices/{invoiceId}/print
GET    /api/v1/invoice-issuance-processes/{processId}
```

Caminhos externos e caminhos publicados pelo serviço são iguais. Não existem prefixos artificiais por serviço.

#### Allowlist

Rotas públicas são declaradas explicitamente por caminho e método. Não existe rota abrangente como `/api/{**catch-all}` e `/api/v1/internal/{**catch-all}` nunca é configurada.

Uma nova rota de uma API não se torna pública automaticamente. Inclusão exige decisão documental e alteração explícita da configuração do Gateway.

#### Fidelidade do proxy

Salvo decisões específicas dos blocos seguintes, o Gateway preserva método, path, query string, corpo, status, corpo da resposta, `Content-Type`, `ETag`, `Location`, `Retry-After` e headers de correlação permitidos.

`202 Accepted`, Problem Details, paginação, concorrência e erros funcionais continuam sendo produzidos pelo serviço proprietário.

Rota fora da allowlist não é encaminhada nem descoberta dinamicamente e recebe resposta de rota inexistente na borda.

#### Limite de sanitização

O Gateway valida e normaliza somente elementos pertencentes à borda, como correlation ID e headers permitidos. Não altera código ou descrição de produto, invoice, itens, DTOs ou mensagens funcionais.

APIs validam seus contratos e o domínio protege invariantes. O proxy não cria uma segunda versão das regras de negócio.

### 7.2 Bloco 2 - Rotas YARP, clusters, destinos e exclusões

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Clusters

| Cluster | Destino inicial no Docker |
|---|---|
| `identity-cluster` | `http://identity-api:8080` |
| `inventory-cluster` | `http://inventory-api:8080` |
| `billing-cluster` | `http://billing-api:8080` |

Cada cluster possui inicialmente um destino. A abstração permite futura replicação sem introduzir service discovery externo nesta entrega. Endereços são fornecidos por configuração e validados no startup; o código não depende diretamente dos nomes do Docker.

#### Rotas funcionais

| Route ID | Método | Caminho | Cluster | Política |
|---|---|---|---|---|
| `identity-login` | `POST` | `/api/v1/auth/login` | Identity | `Anonymous` |
| `products-create` | `POST` | `/api/v1/products` | Inventory | `AdminOnly` |
| `products-list` | `GET` | `/api/v1/products` | Inventory | `AuthenticatedUser` |
| `products-get` | `GET` | `/api/v1/products/{productId}` | Inventory | `AuthenticatedUser` |
| `invoices-create` | `POST` | `/api/v1/invoices` | Billing | `AdminOnly` |
| `invoices-list` | `GET` | `/api/v1/invoices` | Billing | `AuthenticatedUser` |
| `invoices-get` | `GET` | `/api/v1/invoices/{invoiceId}` | Billing | `AuthenticatedUser` |
| `invoice-items-add` | `POST` | `/api/v1/invoices/{invoiceId}/items` | Billing | `AdminOnly` |
| `invoice-items-update` | `PUT` | `/api/v1/invoices/{invoiceId}/items/{itemId}` | Billing | `AdminOnly` |
| `invoice-items-delete` | `DELETE` | `/api/v1/invoices/{invoiceId}/items/{itemId}` | Billing | `AdminOnly` |
| `invoice-print` | `POST` | `/api/v1/invoices/{invoiceId}/print` | Billing | `AdminOnly` |
| `issuance-process-get` | `GET` | `/api/v1/invoice-issuance-processes/{processId}` | Billing | `AuthenticatedUser` |

Método integra a correspondência da rota. Operação com método não permitido não é encaminhada como outra operação.

#### Precedência

Padrões literais e mais específicos, especialmente `/print` e `/items`, prevalecem sobre segmentos parametrizados. A configuração usa ordem inequívoca e testes comprovam que a rota de detalhe da invoice não captura comandos ou sub-recursos.

#### Transformações funcionais

Nas rotas funcionais:

- path não recebe transformação ou prefixo;
- query string é preservada;
- corpo é encaminhado sem desserialização ou nova serialização;
- forwarded headers técnicos aprovados são adicionados;
- headers hop-by-hop não são encaminhados;
- `Host` é ajustado para o destino conforme o comportamento seguro do proxy.

#### OpenAPI

Somente em `Development`:

| Caminho externo | Cluster e path de destino |
|---|---|
| `/openapi/identity/v1.json` | Identity `/openapi/v1.json` |
| `/openapi/inventory/v1.json` | Inventory `/openapi/v1.json` |
| `/openapi/billing/v1.json` | Billing `/openapi/v1.json` |

As rotas aplicam transformação explícita de path, permanecem separadas e não produzem documento agregado. Fora de `Development`, não são registradas.

#### Exclusões

Não existem rotas para:

```text
/api/v1/internal/*
/health/* dos serviços internos
/metrics dos serviços internos
RabbitMQ Management
PostgreSQL
```

O Gateway possui seus próprios endpoints operacionais. Dependências podem ser verificadas internamente sem republicar endpoints administrativos dos destinos.

#### Validação da configuração

A seção `ReverseProxy` admite sobrescrita dos endereços por variáveis de ambiente. O startup valida presença dos três clusters, URI HTTP interna absoluta, associação correta, route IDs únicos, políticas conhecidas e ausência de rota abrangente ou interna.

Configuração inválida impede o Gateway de ficar pronto. Não existe fallback silencioso para outro destino.

### 7.3 Bloco 3 - Autenticação e autorização na borda

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Responsabilidade

Identity permanece como único componente que verifica e-mail e senha, aplica lockout, consulta usuários e papéis e emite JWT. O Gateway apenas valida tokens emitidos, sem consultar Identity a cada request, possuir banco ou armazenar sessão.

#### Validação JWT

```text
ValidateIssuerSigningKey = true
ValidateIssuer = true
ValidateAudience = true
ValidateLifetime = true
RequireSignedTokens = true
RequireExpirationTime = true
ClockSkew = 30 segundos
ValidAlgorithms = HS256
MapInboundClaims = false
```

O Gateway recebe `Jwt:SigningKey`, `Jwt:Issuer` e `Jwt:Audience` pelo ambiente. A chave deve ser Base64 válida e possuir ao menos 256 bits depois da decodificação. Não existem defaults funcionais; chave, issuer ou audience ausente ou incompatível impedem readiness.

Gateway, Identity, Inventory e Billing recebem valores compatíveis sem compartilhar banco ou consultar configuração entre processos.

#### Claims mínimas

Depois da validação criptográfica, o principal exige:

- `sub` como UUID válido e não vazio;
- `email` preenchido;
- `jti` como UUID válido e não vazio;
- ao menos uma claim `role` preenchida;
- claims temporais, issuer e audience aceitos pelo bearer handler.

```text
NameClaimType = email
RoleClaimType = role
```

Token criptograficamente válido, mas incompleto, não constitui a identidade esperada e recebe `401`.

#### Políticas

```text
AuthenticatedUser
    -> identidade autenticada
    -> sub válido
    -> email presente
    -> jti válido
    -> ao menos uma role preenchida

AdminOnly
    -> todos os requisitos de AuthenticatedUser
    -> role contém exatamente Admin
```

`Admin` usa comparação ordinal sensível a maiúsculas. O Gateway não cria permissões dinâmicas ou interpreta autorização além das duas políticas.

#### Respostas da borda

| Situação | Resultado |
|---|---|
| Bearer ausente em rota protegida | `401 Unauthorized` |
| Token expirado, adulterado ou malformado | `401 Unauthorized` |
| Algoritmo, issuer ou audience inválido | `401 Unauthorized` |
| Claims mínimas ausentes | `401 Unauthorized` |
| Identidade válida sem `Admin` | `403 Forbidden` |
| Identidade e política válidas | Encaminhamento ao destino |

`401` inclui `WWW-Authenticate: Bearer`. Respostas usam Problem Details sanitizado, `traceId`, `correlationId` e código estável, sem indicar assinatura, algoritmo, expiração ou claim específica. Falha na borda não alcança o destino.

#### Login anônimo

`POST /api/v1/auth/login` não exige bearer e permanece utilizável sem token ou depois da expiração da sessão anterior. Antes do encaminhamento, o Gateway remove `Authorization` dessa chamada.

Bearer expirado não impede novo login e token desnecessário não chega ao endpoint de credenciais. O Gateway não lê, registra ou transforma e-mail e senha.

#### Propagação do bearer

Após autorização, o header original é encaminhado a Inventory ou Billing. O Gateway não cria ou renova token, adiciona claims, troca papel, converte bearer em cookie, aceita token por query string ou armazena JWT.

Inventory e Billing validam novamente o mesmo token. A passagem pelo Gateway não substitui a proteção do serviço proprietário.

#### OpenAPI e health

Rotas OpenAPI somente em `Development` e health checks próprios do Gateway são anônimos. Essa permissão não se propaga às operações funcionais descritas pelos documentos.

### 7.4 Bloco 4 - Headers, correlação e fidelidade do proxy

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Correlation ID na entrada

O Gateway estabelece o identificador efetivo por `X-Correlation-ID`:

- ausência: gera novo UUID;
- UUID válido: preserva o valor em formato canônico;
- valor vazio, múltiplo ou malformado: retorna `400 invalid_correlation_id`;
- valor inválido não é reutilizado como identificador de log.

A rejeição possui Problem Details, `traceId` e novo `correlationId` diagnóstico. Depois da validação, somente um header canônico é encaminhado e devolvido.

Se o destino omitir ou devolver valor divergente, o Gateway garante na resposta pública o valor efetivo estabelecido na entrada.

#### Distributed tracing

`traceparent` e `tracestate` seguem W3C e o contexto é propagado pelo pipeline ASP.NET Core e YARP. `traceId` representa spans técnicos; `correlationId` atravessa HTTP e mensagens. Nenhum transporta dados de usuário ou negócio.

#### Headers funcionais

Requests preservam, quando aplicável:

```text
Authorization
Content-Type
Accept
If-Match
Idempotency-Key
X-Correlation-ID
traceparent
tracestate
```

Responses preservam:

```text
Content-Type
ETag
Location
Retry-After
WWW-Authenticate
X-Correlation-ID
```

`If-Match`, `Idempotency-Key` e `ETag` permanecem opacos; `Retry-After` não é recalculado; corpos não são reserializados.

#### Forwarded headers confiáveis

O Gateway remove valores de cliente para `X-Forwarded-For`, `X-Forwarded-Host`, `X-Forwarded-Proto` e `Forwarded` e cria valores confiáveis a partir da conexão observada. O destino pode usá-los para URLs públicas, nunca para substituir autenticação.

#### Headers removidos

Não são encaminhados:

- headers hop-by-hop;
- `Proxy-Authorization`;
- `Cookie` e `Set-Cookie`;
- headers de cliente com prefixo reservado `X-Korp-Internal-*`;
- `Server` e divulgações de implementação controláveis;
- `Authorization` na rota de login.

CORS da borda é a fonte pública dos respectivos headers; valores CORS do destino não são duplicados.

#### Location

Contratos usam caminhos relativos, como `/api/v1/invoice-issuance-processes/{processId}`, que são preservados. Forwarded headers confiáveis ajudam o destino a construir URL pública quando necessário.

Host interno absoluto nunca pode ser exposto. Testes procuram `identity-api`, `inventory-api` e `billing-api` em `Location`. Resposta incompatível não é encaminhada silenciosamente; seu tratamento pertence ao bloco de falhas.

#### Fidelidade de status e corpo

O Gateway não converte status funcionais, adiciona corpo a `204`, substitui Problem Details do destino ou transforma DTOs. Somente falhas originadas na borda recebem Problem Details produzido pelo Gateway.

### 7.5 Bloco 5 - CORS, limites, rate limiting e proteções HTTP

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### CORS

`Gateway:Cors:AllowedOrigins` exige ao menos uma origem HTTP ou HTTPS absoluta e explícita. Não aceita wildcard, combinação de wildcard com credenciais ou inferência pelo `Host`. Valor inválido impede readiness. O endereço local final pertence ao SDD-11.

Métodos permitidos:

```text
GET
POST
PUT
DELETE
OPTIONS
```

Request headers permitidos:

```text
Authorization
Content-Type
Accept
If-Match
Idempotency-Key
X-Correlation-ID
```

Response headers expostos:

```text
ETag
Location
Retry-After
X-Correlation-ID
WWW-Authenticate
```

Credenciais CORS permanecem desabilitadas. Preflight `OPTIONS` é anônimo, termina no Gateway e não concede origem, método ou header fora da allowlist. CORS não substitui autenticação.

#### Tamanho da requisição

O limite global é 64 KiB. A aplicação não possui upload ou payload funcional grande. Excesso retorna `413 request_body_too_large` com Problem Details.

`Content-Length` excessivo permite rejeição anterior ao encaminhamento. Corpo sem tamanho declarado continua limitado durante a recepção pelo servidor. O Gateway não desserializa JSON para aplicar o limite.

#### Rate limiting

Políticas nativas do ASP.NET Core operam em memória e por instância:

| Grupo | Limite | Partição | Fila |
|---|---:|---|---:|
| Login | 10 por minuto | IP observado | 0 |
| Rotas funcionais | 120 por minuto | `sub` validado | 0 |
| OpenAPI em Development | 60 por minuto | IP observado | 0 |
| Health próprio | Sem rate limit | Não aplicável | Não aplicável |

O limite de login complementa o lockout do Identity. O limite funcional comporta o polling aprovado. Requisição sem identidade válida não recebe partição funcional.

Excesso retorna `429 rate_limit_exceeded`, `Retry-After`, Problem Details, `correlationId` e `traceId`, sem expor IP, usuário, contador ou regra interna.

Não existe Redis ou coordenação distribuída. Em futura replicação, o limite permanece por instância até nova decisão.

#### Ordem do pipeline

```text
tratamento de exceções
    -> correlation ID
        -> forwarded headers confiáveis
            -> limite de tamanho
                -> CORS
                    -> autenticação
                        -> rate limiting
                            -> autorização
                                -> reverse proxy
```

Login recebe rate limit por IP apesar de anônimo. Preflight termina antes de autenticação e proxy.

#### Headers de segurança

```text
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
```

Respostas de login recebem também `Cache-Control: no-store` e `Pragma: no-cache`. O Gateway publica API e documentos JSON, sem necessidade de scripts ou frames.

#### HTTPS e ambiente local

Containers usam HTTP na rede isolada local e o Gateway não exige certificado artificial ou redirecionamento HTTPS nessa execução. Exposição real exige TLS na borda, proxies confiáveis, secrets externos e revisão de CORS e rate limiting distribuído.

#### Limites da proteção

O Gateway rejeita header, origem, tamanho, frequência, token ou política invávidos. Não sanitiza HTML em campos, corrige JSON, normaliza DTOs ou executa regra de domínio.

### 7.6 Bloco 6 - Falhas de proxy, OpenAPI e health checks

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Timeouts

```text
Login:             10 segundos
Rotas funcionais:  15 segundos
OpenAPI:            5 segundos
Health dependency: 2 segundos
```

O timeout cobre a espera pelo destino e cancelamento do cliente é propagado. `PrintInvoice` aguarda somente a persistência local necessária ao `202`, nunca RabbitMQ ou Inventory.

#### Ausência de retry HTTP

O Gateway não repete automaticamente requisições, inclusive `GET`. A regra evita tentativas empilhadas e resultados desconhecidos em comandos que podem ter sido confirmados antes de uma desconexão.

Idempotência funcional pertence ao contrato e ao serviço proprietário. O proxy não utiliza esse conhecimento para executar retry invisível.

#### Falhas originadas no proxy

| Situação | Status | Código |
|---|---:|---|
| DNS, conexão recusada ou destino indisponível | `503` | `upstream_service_unavailable` |
| Timeout antes de resposta válida | `504` | `upstream_service_timeout` |
| Resposta ou protocolo HTTP incompatível | `502` | `invalid_upstream_response` |
| `Location` absoluto com host interno | `502` | `invalid_upstream_response` |
| Cancelamento pelo cliente | Sem resposta sintética adicional | Não aplicável |
| Erro inesperado na borda | `500` | `gateway_internal_error` |

Problem Details da borda inclui `type`, `title`, `status`, `detail`, `instance`, `code`, `traceId` e `correlationId`. Não inclui URI, container, IP, DNS, socket, exception message, stack trace ou configuração do destino.

#### Respostas originadas no serviço

Qualquer resposta HTTP válida do destino, inclusive `400`, `401`, `403`, `404`, `409`, `412`, `428`, `500` ou `503`, é preservada. Falha de proxy somente é produzida quando não existe resposta válida.

Se a falha ocorrer depois de os headers públicos iniciarem, o Gateway encerra o stream, registra diagnóstico sanitizado e incrementa métrica. Não tenta substituir resposta parcial por Problem Details nem declara sucesso.

#### OpenAPI

Em `Development`, os três documentos são anônimos, separados, encaminhados como JSON e recebem `Cache-Control: no-store`. Ausência de um serviço afeta apenas seu documento. Não existe combinação ou Swagger UI agregado.

Fora de `Development`, as rotas não são registradas e recebem `404`.

#### Health checks próprios

```text
GET /health/live
GET /health/ready
GET /health/dependencies
```

Os três são anônimos e não passam pelo YARP.

`/health/live` verifica somente o processo, sem acessar dependências.

`/health/ready` verifica configurações JWT e CORS, clusters, destinos, rotas, políticas e pipeline. Não exige disponibilidade instantânea de todos os serviços, preservando as rotas independentes.

`/health/dependencies` consulta internamente o readiness de Identity, Inventory e Billing, com timeout individual de dois segundos e sem retry. O resultado público expõe somente o estado agregado e o nome funcional das três dependências. URI, porta e diagnóstico interno não aparecem. Qualquer dependência indisponível torna esse endpoint `503`, sem alterar o readiness próprio do Gateway.

A representação comum e os caminhos internos definitivos de health serão consolidados no SDD-11 sem mudar essa semântica.

#### Não exposição administrativa

O Gateway consulta saúde pela rede interna, mas não publica rotas individuais como `/identity/health`, `/inventory/health` ou `/billing/health`.

#### Recuperação

Não existe circuit breaker externo nesta entrega. Quando um serviço retorna, a próxima requisição pode ser encaminhada normalmente, sem recarregar configuração ou limpar estado. O Gateway não participa da recuperação dos bancos ou da mensageria.

### 7.7 Bloco 7 - Configuração, ciclo de vida e observabilidade

> Estado: Aprovado pelo engenheiro em 2026-08-19

#### Configuração

```text
ReverseProxy:Routes
ReverseProxy:Clusters
Jwt:SigningKey
Jwt:Issuer
Jwt:Audience
Gateway:Cors:AllowedOrigins
Gateway:Limits:MaximumRequestBodyBytes = 65536
Gateway:RateLimiting:LoginPermitLimit = 10
Gateway:RateLimiting:FunctionalPermitLimit = 120
Gateway:RateLimiting:OpenApiPermitLimit = 60
Gateway:RateLimiting:WindowSeconds = 60
Gateway:Timeouts:LoginSeconds = 10
Gateway:Timeouts:FunctionalSeconds = 15
Gateway:Timeouts:OpenApiSeconds = 5
Gateway:Timeouts:DependencyHealthSeconds = 2
Gateway:ShutdownTimeoutSeconds = 30
```

Rotas, políticas, tamanhos e limites devem corresponder a esta versão. Endereços e origens variam por ambiente dentro das validações aprovadas.

#### Validação e imutabilidade durante a execução

Antes de aceitar tráfego, o processo valida opções obrigatórias, JWT, CORS, rotas, clusters, políticas, limites e ausência de rota interna ou abrangente.

Configuração estática inválida causa fail-fast com mensagem sanitizada e exit code diferente de zero. Readiness representa o processo depois de startup válido e não substitui essa barreira.

Não existe reload dinâmico nesta entrega. Destino, origem, rota, política, timeout ou limite exige reinício controlado e nova validação integral.

#### Segredos

`Jwt:SigningKey` é secreto. Issuer, audience, destinos e origens são configuração operacional, mas também não são exibidos integralmente em logs, health ou erros. O repositório contém apenas exemplos não secretos; fornecimento Docker pertence ao SDD-11.

#### Cliente do proxy

O YARP gerencia conexões HTTP reutilizáveis por cluster. A implementação não cria `HttpClient` por request, abre socket manual, mantém sessão, armazena resposta funcional em cache ou substitui o pooling nativo sem necessidade aprovada.

#### Ciclo de vida

```text
validar configuração
    -> construir pipeline
        -> mapear health checks
            -> mapear rotas permitidas
                -> iniciar escuta HTTP
```

No encerramento, o servidor deixa de aceitar novas conexões, drena requisições por até 30 segundos e cancela as restantes. Requisição interrompida pode ter resultado desconhecido no destino; não existe retry ou sucesso artificial. Reinício não exige recuperação de estado local.

#### Logs estruturados

Eventos mínimos:

```text
gateway_request_completed
gateway_authentication_failed
gateway_authorization_denied
gateway_rate_limit_rejected
gateway_request_body_rejected
gateway_upstream_failed
gateway_invalid_upstream_response
gateway_dependency_health_changed
gateway_configuration_invalid
```

Campos permitidos incluem `routeId`, `clusterId`, `httpMethod`, `statusCode`, `outcome`, `duration`, `correlationId`, `traceId` e `failureCode`.

O log usa route ID ou template, não path bruto. Query string, corpo, bearer, claims, `sub`, e-mail, IP, headers completos, senha, chave, endereço interno e mensagem de exceção não aparecem em logs informativos. Exceção completa permanece restrita ao diagnóstico técnico.

#### Métricas

```text
gateway_requests_total{route_id,method,status_class,outcome}
gateway_request_duration_seconds{route_id,method}
gateway_active_requests{route_id}
gateway_authentication_failures_total
gateway_authorization_denials_total{policy}
gateway_rate_limit_rejections_total{policy}
gateway_upstream_failures_total{cluster,reason}
gateway_dependency_health{dependency}
```

Labels pertencem a conjuntos finitos. URL, UUID, usuário, IP, correlation ID, trace ID e status individual não são labels quando a classe HTTP for suficiente.

#### Tracing

Cada request inicia ou continua `Activity` W3C e o encaminhamento produz span HTTP do destino. Logs carregam `traceId` e `correlationId`. Instrumentação usa recursos da plataforma; exportadores e stack final pertencem ao SDD-11.

Telemetria é evidência operacional, não estado funcional. Sua indisponibilidade não modifica autorização, roteamento ou resposta.

### 7.8 Bloco 8 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-19

As seções 8 a 11 transformam as decisões anteriores em provas objetivas. Destinos HTTP controlados comprovam o comportamento exato do proxy, enquanto os testes finais exercitam os serviços reais no Docker.

---

## 8. Critérios de aceite

### CA-GTW-01 - Fronteira stateless

**Dado** o conjunto de componentes,  
**quando** dependências e referências forem inspecionadas,  
**então** o Gateway não possuirá banco, mensageria, domínio ou referência aos serviços.

### CA-GTW-02 - Rotas públicas

**Dado** cada método e caminho aprovado,  
**quando** uma requisição válida alcançar o Gateway,  
**então** será encaminhada ao único serviço proprietário sem transformação funcional.

### CA-GTW-03 - Rota interna protegida

**Dado** qualquer caminho `/api/v1/internal/*`,  
**quando** for chamado pelo endereço público,  
**então** não será encaminhado a Inventory ou outro serviço.

### CA-GTW-04 - Allowlist de método

**Dado** um caminho conhecido com método não aprovado,  
**quando** a chamada alcançar a borda,  
**então** nenhum destino funcional será acionado.

### CA-GTW-05 - Validação JWT estrita

**Dado** token expirado, adulterado, incompleto ou com algoritmo, issuer ou audience incorretos,  
**quando** alcançar rota protegida,  
**então** será rejeitado com `401` sanitizado antes do proxy.

### CA-GTW-06 - Autorização diferenciada

**Dado** identidade válida sem papel `Admin`,  
**quando** acessar rota `AdminOnly`,  
**então** receberá `403`, enquanto leituras `AuthenticatedUser` continuarão permitidas.

### CA-GTW-07 - Login recuperável

**Dado** bearer ausente, expirado ou desnecessário,  
**quando** o login for chamado,  
**então** a chamada permanecerá anônima e `Authorization` não chegará ao Identity.

### CA-GTW-08 - Defesa em profundidade

**Dado** token válido em rota protegida,  
**quando** o Gateway autorizar e encaminhar,  
**então** o bearer original chegará à API, que realizará sua própria validação.

### CA-GTW-09 - Correlação

**Dado** correlation ID ausente, válido ou malformado,  
**quando** a requisição for processada,  
**então** o Gateway gerará, preservará ou rejeitará conforme a regra e devolverá um identificador efetivo.

### CA-GTW-10 - Forwarded headers confiáveis

**Dado** cliente tentando fornecer forwarded headers,  
**quando** o request for encaminhado,  
**então** os valores do cliente serão removidos e recriados pela borda.

### CA-GTW-11 - Fidelidade HTTP

**Dado** resposta válida do destino,  
**quando** atravessar o Gateway,  
**então** status, corpo, `ETag`, `Location`, `Retry-After`, `WWW-Authenticate` e correlation ID permanecerão coerentes.

### CA-GTW-12 - CORS restritivo

**Dado** origem, método ou header não aprovado,  
**quando** o navegador executar preflight,  
**então** o Gateway não concederá acesso nem encaminhará a chamada.

### CA-GTW-13 - Limite de corpo

**Dado** payload acima de 64 KiB,  
**quando** alcançar a borda,  
**então** receberá `413 request_body_too_large` sem atingir o destino.

### CA-GTW-14 - Rate limiting

**Dado** limite de login, rota funcional ou OpenAPI excedido,  
**quando** nova chamada ocorrer na janela,  
**então** receberá `429 rate_limit_exceeded` com `Retry-After`.

### CA-GTW-15 - Falha do destino

**Dado** um serviço indisponível ou lento,  
**quando** sua rota for chamada,  
**então** o Gateway retornará `503` ou `504` sanitizado sem retry automático.

### CA-GTW-16 - Resposta inválida

**Dado** protocolo incompatível ou `Location` expondo host interno,  
**quando** o Gateway validar a resposta,  
**então** retornará `502 invalid_upstream_response` sem revelar infraestrutura.

### CA-GTW-17 - OpenAPI separado

**Dado** ambiente `Development`,  
**quando** os caminhos OpenAPI forem consultados,  
**então** cada documento será encaminhado separadamente; fora desse ambiente, as rotas não existirão.

### CA-GTW-18 - Health isolado

**Dado** uma API de domínio indisponível,  
**quando** os health checks forem consultados,  
**então** liveness e readiness próprios continuarão coerentes e dependencies mostrará degradação sem expor detalhes internos.

### CA-GTW-19 - Configuração inválida

**Dado** segredo, origem, cluster, rota ou limite inválido,  
**quando** o Gateway iniciar,  
**então** falhará antes de aceitar tráfego, sem fallback silencioso.

### CA-GTW-20 - Shutdown seguro

**Dado** encerramento solicitado,  
**quando** houver requests em andamento,  
**então** serão drenados por até 30 segundos e nenhum retry ou sucesso artificial será criado.

### CA-GTW-21 - Observabilidade segura

**Dado** sucesso, rejeição ou falha do proxy,  
**quando** logs e métricas forem inspecionados,  
**então** haverá correlação e classificação sem token, corpo, path bruto, usuário, IP ou labels de alta cardinalidade.

---

## 9. Plano de testes

| ID | Evidência | Tipo | Critérios |
|---|---|---|---|
| TST-GTW-001 | Inspecionar referências e dependências proibidas | Arquitetura | CA-GTW-01 |
| TST-GTW-002 | Exercitar todas as rotas contra destinos controlados | Integração | CA-GTW-02 |
| TST-GTW-003 | Tentar acessar `/api/v1/internal/*` | Segurança | CA-GTW-03 |
| TST-GTW-004 | Combinar caminhos conhecidos com métodos inválidos | Integração | CA-GTW-04 |
| TST-GTW-005 | Rejeitar assinatura, algoritmo, issuer e audience inváidos | Segurança | CA-GTW-05 |
| TST-GTW-006 | Rejeitar expiração e claims mínimas ausentes | Segurança | CA-GTW-05 |
| TST-GTW-007 | Diferenciar `401` e `403` por política | Integração | CA-GTW-06 |
| TST-GTW-008 | Fazer login com bearer expirado e verificar remoção | Integração | CA-GTW-07 |
| TST-GTW-009 | Capturar bearer no destino e validar novamente na API | Integração | CA-GTW-08 |
| TST-GTW-010 | Gerar, preservar e rejeitar correlation IDs | Unitário/integração | CA-GTW-09 |
| TST-GTW-011 | Tentar falsificar forwarded headers | Segurança | CA-GTW-10 |
| TST-GTW-012 | Preservar query, corpo, status e headers funcionais | Integração | CA-GTW-11 |
| TST-GTW-013 | Verificar ausência de host interno em `Location` | Segurança | CA-GTW-11, CA-GTW-16 |
| TST-GTW-014 | Exercitar preflight permitido e negado | Integração | CA-GTW-12 |
| TST-GTW-015 | Enviar corpo no limite e acima dele | Integração | CA-GTW-13 |
| TST-GTW-016 | Exceder cada política de rate limiting | Integração | CA-GTW-14 |
| TST-GTW-017 | Simular conexão recusada e timeout | Resiliência | CA-GTW-15 |
| TST-GTW-018 | Provar ausência de retry com destino contador | Integração | CA-GTW-15 |
| TST-GTW-019 | Simular resposta inválida e URL interna | Segurança/integração | CA-GTW-16 |
| TST-GTW-020 | Consultar OpenAPI em Development e outro ambiente | Integração | CA-GTW-17 |
| TST-GTW-021 | Verificar live, ready e dependencies por combinação | Integração | CA-GTW-18 |
| TST-GTW-022 | Inicializar com configurações inválidas | Unitário/integração | CA-GTW-19 |
| TST-GTW-023 | Encerrar durante request rápido e bloqueado | Integração | CA-GTW-20 |
| TST-GTW-024 | Inspecionar logs e métricas com sentinelas | Segurança | CA-GTW-21 |
| TST-GTW-025 | Executar Angular, Gateway e APIs no Docker | E2E | CA-GTW-02, CA-GTW-08, CA-GTW-11, CA-GTW-18 |
| TST-GTW-026 | Confirmar ausência de banco e RabbitMQ no Gateway | Arquitetura | CA-GTW-01, CA-GTW-03 |

Destinos controlados comprovam o request recebido e a resposta devolvida. As provas finais também utilizam Identity, Inventory e Billing reais no Docker.

Conforme ADR-014, cada assembly de produção relevante deve atingir ao menos 80% de line coverage. Branch coverage é coletada e publicada, inicialmente sem gate percentual, mas todos os ramos críticos destes critérios devem possuir testes. Mocks não substituem provas de proxy e segurança.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Catch-all publicar endpoint interno | Exposição indevida | Allowlist e teste negativo |
| Validação apenas no Gateway | Contorno pela rede interna | APIs validam novamente |
| Header falsificado alterar origem | Spoofing | Remoção e recriação na borda |
| Retry duplicar comando | Efeito funcional repetido | Ausência de retry automático |
| Gateway esconder status funcional | Contrato incorreto | Fidelidade de resposta |
| Host interno aparecer em `Location` | Vazamento de infraestrutura | Forwarded headers e teste negativo |
| Rate limiting bloquear demonstração | Fluxo indisponível | Limites acima do uso normal e testes |
| Limite por instância parecer distribuído | Garantia falsa | Limitação documentada |
| Uma API parada derrubar toda a borda | Falha ampliada | Readiness próprio separado de dependencies |
| Logs exporem token ou identificadores | Vazamento | Allowlist de campos e sentinelas |
| Configuração dinâmica alterar superfície | Exposição sem validação | Configuração estática e restart |
| HTTP local ser confundido com produção | Modelo de segurança incorreto | Limite local e exigência de TLS documentados |

---

## 11. Marcadores de qualidade

| Marcador | Condição |
|---|---|
| `ARC` | Gateway sem domínio, banco, RabbitMQ ou referências proibidas |
| `API` | Métodos, caminhos, clusters e respostas fiéis |
| `AUT` | JWT e políticas testados na borda e nas APIs |
| `SEC` | CORS, spoofing, tamanho, rate limit e vazamentos verificados |
| `RES` | Timeout, indisponibilidade e ausência de retry comprovados |
| `TST` | Gate de line coverage, branch coverage publicada e critérios rastreados |
| `INT` | Destinos controlados e serviços reais utilizados |
| `OBS` | Logs, métricas, tracing e health verificados |
| `DOC` | Configuração e limites de produção documentados |
| `QA` | Fluxo Docker completo e negativas de exposição executados |

---

## 12. Limites para implementação futura

Uma implementação deste SDD poderá criar ou alterar:

- composition root e configuração do `Korp.Gateway.Api`;
- rotas, clusters, transforms e filtros YARP aprovados;
- autenticação, políticas e respostas de borda;
- correlation ID, forwarded headers, CORS, limites e rate limiting;
- erros de proxy, OpenAPI, health checks, logs, métricas e tracing;
- testes unitários, de integração, arquitetura, segurança e E2E descritos.

Não autoriza:

- banco, migration, EF Core ou persistência no Gateway;
- RabbitMQ ou qualquer adapter de mensageria;
- regra ou DTO intermediário de negócio;
- exposição de rota interna;
- retry, cache, circuit breaker ou agregação funcional;
- service discovery externo ou rate limiting distribuído;
- implementação antes da baseline documental conjunta.

---

## 13. Condição para Gate A

O SDD poderá atingir o Gate A quando:

- os oito blocos estiverem aprovados;
- toda rota pública possuir destino e política inequívocos;
- nenhuma rota interna estiver exposta;
- indisponibilidade e respostas do proxy estiverem definidas;
- cada critério possuir evidência planejada;
- matriz de rastreabilidade e índice estiverem atualizados;
- não houver regra de negócio, persistência ou mensageria atribuída ao Gateway.

A aprovação estabiliza o comportamento do Gateway, mas não autoriza implementação antes da baseline documental conjunta.
