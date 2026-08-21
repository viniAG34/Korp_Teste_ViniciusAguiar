# SDD-03 - Contratos HTTP e Eventos

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-18
> Gate A aprovado em: 2026-08-18
> Dependências: SDD-01, SDD-02, ADR-001, ADR-003, ADR-004, ADR-010, ADR-011, ADR-012 e ADR-013

---

## 1. Objetivo

Definir os contratos estáveis que conectam Angular, Gateway, Identity, Inventory e Billing, incluindo rotas, métodos, autenticação, requests, responses, paginação, erros, idempotência e eventos RabbitMQ.

O documento deve permitir implementar cada serviço sem decisões implícitas sobre exposição, semântica HTTP ou compatibilidade de mensagens.

---

## 2. Requisitos rastreados

Este SDD detalhará principalmente:

- `OBR-001`, `OBR-002` e `OBR-006` a `OBR-015`;
- `OBR-017` a `OBR-021`;
- `OPA-002`;
- `DIF-001` a `DIF-005`, `DIF-008` e `DIF-009`;
- `QLT-006` a `QLT-008`.

---

## 3. Escopo previsto

- superfície HTTP pública exposta pelo Gateway;
- rota HTTP interna entre Billing e Inventory;
- versionamento `/api/v1`;
- autenticação e políticas exigidas por rota;
- formatos de request e response;
- paginação e ordenação;
- códigos HTTP e Problem Details;
- `Idempotency-Key` de `PrintInvoice`;
- contratos de eventos entre Billing e Inventory;
- envelope, metadados, versionamento e compatibilidade de mensagens;
- semântica de correlação e causalidade;
- exemplos OpenAPI representativos;
- critérios de aceite e testes contratuais.

---

## 4. Fora do escopo

- implementação de endpoints;
- casos de uso e repositórios;
- configuração YARP;
- geração, assinatura e validação interna do JWT;
- implementação de publishers e consumers;
- topologia RabbitMQ, retry e DLQ;
- persistência já definida pelo SDD-02;
- comportamento visual e polling Angular;
- Docker Compose final.

---

## 5. Blocos de decisão

1. fronteiras HTTP públicas e internas;
2. convenções, versionamento, autenticação e erros;
3. contratos de Identity e Product;
4. contratos de Invoice e itens;
5. `PrintInvoice` e consulta do processo;
6. envelope e contratos de eventos;
7. compatibilidade, correlação e idempotência;
8. critérios de aceite, testes, riscos e marcadores.

Nenhum contrato será implementado durante a macroetapa documental.

---

## 6. Decisões herdadas

- Minimal APIs organizadas por feature;
- rotas versionadas sob `/api/v1`;
- Gateway como única entrada pública padrão;
- Gateway sem banco e sem RabbitMQ;
- Inventory e Billing comunicam-se por HTTP direto somente para consulta interna de produto;
- RabbitMQ integra exclusivamente Billing e Inventory na emissão;
- JWT validado no Gateway e novamente nas APIs;
- `ProblemDetails` para erros HTTP;
- `PrintInvoice` retorna `202 Accepted` quando a intenção e a Outbox são persistidas;
- acompanhamento do processo ocorre por polling HTTP no Billing;
- eventos usam entrega at-least-once com Inbox e Outbox;
- contratos compartilhados não expõem entidades de domínio ou persistência.

---

## 7. Decisões em elaboração

### 7.1 Bloco 1 - Fronteiras HTTP públicas e internas

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Superfície pública pelo Gateway

```text
POST   /api/v1/auth/login

POST   /api/v1/products
GET    /api/v1/products
GET    /api/v1/products/{productId}

POST   /api/v1/invoices
GET    /api/v1/invoices
GET    /api/v1/invoices/{invoiceId}

POST   /api/v1/invoices/{invoiceId}/items
PUT    /api/v1/invoices/{invoiceId}/items/{itemId}
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}

POST   /api/v1/invoices/{invoiceId}/print

GET    /api/v1/invoice-issuance-processes/{processId}
```

O Gateway preserva os caminhos publicados pelos serviços, sem prefixos adicionais ou transformações de rota desnecessárias. Cada caminho possui um único serviço proprietário.

`PUT` substitui integralmente a parte mutável do item, que nesta feature é somente a quantidade. `PrintInvoice` utiliza `/print` para manter correspondência direta com o vocabulário do desafio e continua sendo o único comando público de emissão.

O retorno `202 Accepted` de `PrintInvoice` inclui `Location` apontando para o processo criado ou recuperado. Nenhuma rota pública acessa RabbitMQ; APIs comunicam somente com casos de uso locais.

#### Superfície interna

```text
GET /api/v1/internal/products/{productId}
```

A rota existe exclusivamente no Inventory, não é configurada no Gateway e retorna somente:

```text
Id
Code
Description
```

Saldo não é exposto. A rota atende apenas à obtenção do snapshot usado por Billing em `AddInvoiceItem`.

Billing chama Inventory diretamente pela rede interna e propaga o bearer token recebido na requisição original. Inventory valida novamente assinatura, algoritmo, issuer, audience, expiração e política. Não serão criadas credenciais de serviço ou fluxo client credentials para uma única consulta síncrona iniciada por usuário.

Essa delegação é limitada ao fluxo HTTP síncrono de inclusão de item. Consumers e tarefas em background não dependem de token de usuário nem chamam essa rota.

Ausência de rota correspondente no Gateway impede exposição pública padrão de `/api/v1/internal/*`, mas não substitui autenticação e autorização no Inventory.

#### Políticas por grupo

| Grupo | Política |
|---|---|
| Login | Anônimo |
| Leituras funcionais | `AuthenticatedUser` |
| Criações e alterações | `AdminOnly` |
| `PrintInvoice` | `AdminOnly` |
| Consulta interna de produto | `AdminOnly` com bearer propagado |

Health checks serão definidos no SDD-11. Documentos OpenAPI podem ser anônimos somente em Development, sem tornar operações protegidas anônimas.

### 7.2 Bloco 2 - Convenções, versionamento, autenticação e erros

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Formato JSON

- media type `application/json`;
- propriedades em `camelCase`;
- datas em ISO 8601 UTC com sufixo `Z`;
- UUIDs no formato textual canônico;
- enums como strings canônicas em `snake_case`;
- DTOs dedicados para request e response;
- entidades de domínio e persistência nunca serializadas diretamente;
- respostas de sucesso sem envelope genérico `data`;
- propriedades opcionais nulas omitidas quando sua ausência já representa o estado corretamente.

Exemplo:

```json
{
  "id": "15b08872-efca-4142-a148-d0f69d4ae64c",
  "number": 42,
  "status": "open",
  "createdAtUtc": "2026-08-18T14:30:00Z"
}
```

#### Paginação

Listagens utilizam paginação numerada:

```text
?pageNumber=1&pageSize=20
```

- primeira página: 1;
- tamanho padrão: 20;
- tamanho máximo: 100;
- valores fora do intervalo retornam `400`;
- Products: `code` ascendente e depois `id`;
- Invoices: `createdAtUtc` descendente e depois `id`.

Resposta comum:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

Não será usada paginação por cursor porque o volume e a experiência do desafio não justificam sua complexidade.

#### Concorrência HTTP

Criação e consulta de Invoice retornam `ETag` opaco derivado da versão persistente. As operações abaixo exigem `If-Match`:

```text
POST   /api/v1/invoices/{invoiceId}/items
PUT    /api/v1/invoices/{invoiceId}/items/{itemId}
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}
POST   /api/v1/invoices/{invoiceId}/print
```

- ausência: `428 Precondition Required`;
- ETag malformado: `400 Bad Request`;
- versão desatualizada: `412 Precondition Failed`;
- resposta com representação atualizada retorna o novo `ETag`;
- o cliente armazena e devolve o token, mas não interpreta seu conteúdo.

ETag controla concorrência e intenção baseada em uma versão observada. Ele não substitui idempotência.

#### Problem Details

Erros utilizam `ProblemDetails` ou `ValidationProblemDetails`:

```json
{
  "type": "urn:korp:problem:invoice-not-open",
  "title": "Operação não permitida",
  "status": 409,
  "detail": "A nota fiscal precisa estar aberta.",
  "instance": "/api/v1/invoices/15b08872-efca-4142-a148-d0f69d4ae64c",
  "code": "invoice_not_open",
  "traceId": "00-..."
}
```

- `type` é uma URN estável derivada do código;
- `code` é estável, em inglês e `snake_case`;
- `title` e `detail` apresentados ao usuário são em português;
- `traceId` acompanha todos os erros;
- validações incluem dicionário `errors` por campo;
- SQL, stack trace, hosts, filas, payloads e segredos não aparecem.

#### Correlação

- request pode enviar `X-Correlation-ID` como UUID;
- ausência gera novo UUID;
- valor malformado retorna `400` com `invalid_correlation_id`;
- response devolve o valor efetivo no mesmo header;
- chamadas HTTP internas e mensagens propagam o identificador;
- `traceId` pertence ao tracing técnico e não substitui `correlationId`.

#### Status HTTP comuns

| Situação | Status |
|---|---|
| Recurso criado | `201 Created` |
| Consulta bem-sucedida | `200 OK` |
| Processamento aceito | `202 Accepted` |
| Exclusão sem representação | `204 No Content` |
| Request, header ou formato inválido | `400 Bad Request` |
| Credencial ausente, inválida ou expirada | `401 Unauthorized` |
| Identidade sem permissão | `403 Forbidden` |
| Recurso inexistente | `404 Not Found` |
| ETag desatualizado | `412 Precondition Failed` |
| Duplicidade ou estado incompatível | `409 Conflict` |
| `If-Match` obrigatório ausente | `428 Precondition Required` |
| Dependência indisponível antes da persistência | `503 Service Unavailable` |
| Falha inesperada | `500 Internal Server Error` |

#### Autenticação

Bearer token utiliza o header padrão `Authorization`. `401` não informa se e-mail, senha, assinatura ou claim específica estava incorreta. `403` pressupõe identidade válida, mas sem a política exigida.

OpenAPI de cada serviço descreve o esquema bearer e as políticas aplicáveis. O Gateway encaminha o header recebido; não cria identidade nem substitui claims.

### 7.3 Bloco 3 - Contratos de Identity e Product

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Login

```text
POST /api/v1/auth/login
Política: Anonymous
```

Request:

```json
{
  "email": "admin@example.com",
  "password": "senha-fornecida-pelo-ambiente"
}
```

Validações de fronteira:

- `email` obrigatório, formato válido e máximo 254 caracteres;
- espaços externos do e-mail removidos antes da autenticação;
- `password` obrigatória e máximo 128 caracteres;
- senha não sofre trim, uppercase ou outra normalização.

Response `200 OK`:

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresInSeconds": 900,
  "expiresAtUtc": "2026-08-18T15:15:00Z",
  "user": {
    "id": "6dab8c4c-2bb8-46cc-a865-0e992aaeb443",
    "email": "admin@example.com",
    "roles": ["Admin"]
  }
}
```

O contrato não contém refresh token. `expiresInSeconds` auxilia temporização do cliente e `expiresAtUtc` fornece instante absoluto verificável.

Erros:

| Situação | Status | `code` |
|---|---:|---|
| Request inválido | 400 | `validation_failed` |
| Usuário inexistente ou senha inválida | 401 | `invalid_credentials` |
| Persistência indisponível | 503 | `identity_unavailable` |
| Falha inesperada | 500 | `unexpected_error` |

Usuário inexistente e senha incorreta produzem resposta indistinguível. Senha, hash e token nunca aparecem em logs ou Problem Details.

#### Criar Product

```text
POST /api/v1/products
Política: AdminOnly
```

Request:

```json
{
  "code": "prod-001",
  "description": "Produto demonstrativo",
  "initialBalance": 10
}
```

Response `201 Created`:

```json
{
  "id": "37e52de4-b892-4418-8abd-9799ee95cb38",
  "code": "PROD-001",
  "description": "Produto demonstrativo",
  "balance": 10,
  "createdAtUtc": "2026-08-18T15:00:00Z",
  "updatedAtUtc": "2026-08-18T15:00:00Z"
}
```

Header:

```text
Location: /api/v1/products/{productId}
```

O response apresenta código e descrição após normalização do domínio. O contrato não aceita `id`, saldo atual separado ou campos de auditoria fornecidos pelo cliente.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| Campos inválidos | 400 | `validation_failed` |
| Código já existente | 409 | `product_code_already_exists` |
| Inventory sem persistir a solicitação | 503 | `inventory_unavailable` |

#### Consultar Product

```text
GET /api/v1/products/{productId}
Política: AuthenticatedUser
```

Response `200 OK` utiliza o mesmo DTO da criação.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| UUID malformado | 400 | `invalid_product_id` |
| Produto inexistente | 404 | `product_not_found` |

#### Listar Products

```text
GET /api/v1/products?pageNumber=1&pageSize=20
Política: AuthenticatedUser
```

Response `200 OK`:

```json
{
  "items": [
    {
      "id": "37e52de4-b892-4418-8abd-9799ee95cb38",
      "code": "PROD-001",
      "description": "Produto demonstrativo",
      "balance": 10,
      "createdAtUtc": "2026-08-18T15:00:00Z",
      "updatedAtUtc": "2026-08-18T15:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Não existem pesquisa ou filtros neste contrato. Inclusão futura depende de requisito de experiência, consulta e índice correspondente.

#### Consultar Product internamente

```text
GET /api/v1/internal/products/{productId}
Política: AdminOnly com bearer propagado
Exposição pelo Gateway: proibida
```

Response `200 OK`:

```json
{
  "id": "37e52de4-b892-4418-8abd-9799ee95cb38",
  "code": "PROD-001",
  "description": "Produto demonstrativo"
}
```

O contrato não expõe saldo, autoria, timestamps, versão ou movimentos.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| UUID malformado | 400 | `invalid_product_id` |
| Produto inexistente | 404 | `product_not_found` |
| Inventory indisponível | 503 | `inventory_unavailable` |

Billing traduz indisponibilidade e timeout para seu erro público contextual. Problem Details internos não são repassados cegamente ao cliente.

### 7.4 Bloco 4 - Contratos de Invoice e itens

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### InvoiceResponse

```json
{
  "id": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
  "number": 42,
  "status": "open",
  "isIssuanceInProgress": false,
  "items": [
    {
      "id": "bf83f91a-93b5-4e8b-9800-58618224436a",
      "productId": "37e52de4-b892-4418-8abd-9799ee95cb38",
      "productCode": "PROD-001",
      "productDescription": "Produto demonstrativo",
      "quantity": 2
    }
  ],
  "createdAtUtc": "2026-08-18T15:00:00Z",
  "updatedAtUtc": "2026-08-18T15:05:00Z"
}
```

`closedAtUtc` é incluído somente quando preenchido. A versão não é exposta no JSON e utiliza o header `ETag`. Itens são ordenados por `productCode` e depois `id`.

#### Criar Invoice

```text
POST /api/v1/invoices
Política: AdminOnly
Body: ausente
```

Response `201 Created` contém `InvoiceResponse` vazia, aberta e desbloqueada.

```text
Location: /api/v1/invoices/{invoiceId}
ETag: "<opaque-version>"
```

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| Billing não persiste a criação | 503 | `billing_unavailable` |

#### Consultar Invoice

```text
GET /api/v1/invoices/{invoiceId}
Política: AuthenticatedUser
```

Response `200 OK` contém `InvoiceResponse` e `ETag`.

| Situação | Status | `code` |
|---|---:|---|
| UUID malformado | 400 | `invalid_invoice_id` |
| Invoice inexistente | 404 | `invoice_not_found` |

#### Listar Invoices

```text
GET /api/v1/invoices?pageNumber=1&pageSize=20
Política: AuthenticatedUser
```

Cada item utiliza resumo sem carregar a coleção:

```json
{
  "id": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
  "number": 42,
  "status": "open",
  "isIssuanceInProgress": false,
  "itemCount": 1,
  "createdAtUtc": "2026-08-18T15:00:00Z",
  "updatedAtUtc": "2026-08-18T15:05:00Z"
}
```

Não existem filtros nesta versão.

#### Adicionar InvoiceItem

```text
POST /api/v1/invoices/{invoiceId}/items
Política: AdminOnly
Header: If-Match obrigatório
```

Request:

```json
{
  "productId": "37e52de4-b892-4418-8abd-9799ee95cb38",
  "quantity": 2
}
```

Billing consulta Inventory, cria snapshots exclusivamente a partir da resposta interna e persiste a alteração. Response `200 OK` contém a Invoice completa e o novo `ETag`. A operação é tratada como mutação do agregado, não como criação pública de recurso independente.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| Campos inválidos | 400 | `validation_failed` |
| Invoice inexistente | 404 | `invoice_not_found` |
| Product inexistente | 404 | `product_not_found` |
| Product já presente | 409 | `product_already_added` |
| Invoice fechada | 409 | `invoice_not_open` |
| Emissão ativa | 409 | `invoice_issuance_in_progress` |
| ETag desatualizado | 412 | `invoice_version_mismatch` |
| `If-Match` ausente | 428 | `invoice_version_required` |
| Inventory indisponível ou timeout esgotado | 503 | `product_catalog_unavailable` |

Falha da consulta interna não persiste item parcial. Saldo não é consultado nessa operação.

#### Atualizar quantidade

```text
PUT /api/v1/invoices/{invoiceId}/items/{itemId}
Política: AdminOnly
Header: If-Match obrigatório
```

Request:

```json
{
  "quantity": 5
}
```

Response `200 OK` contém Invoice completa e novo `ETag`. `ProductId`, código e descrição não podem ser enviados ou alterados.

Erros adicionais:

| Situação | Status | `code` |
|---|---:|---|
| UUID de item malformado | 400 | `invalid_invoice_item_id` |
| Item não pertence à invoice | 404 | `invoice_item_not_found` |

Não será revelado se um `itemId` existe sob outra invoice.

#### Remover item

```text
DELETE /api/v1/invoices/{invoiceId}/items/{itemId}
Política: AdminOnly
Header: If-Match obrigatório
```

Response `204 No Content` devolve o novo `ETag`. A invoice pode permanecer aberta e vazia.

Utiliza os mesmos erros de identificação, estado e concorrência da atualização.

#### Regras comuns

- API não aceita snapshots enviados pelo cliente;
- não existe alteração direta do status;
- não existe exclusão, cancelamento ou reabertura de invoice;
- invoice fechada é imutável;
- invoice com emissão ativa permanece consultável, mas não editável;
- toda mutação de item atualiza a versão da raiz.

### 7.5 Bloco 5 - `PrintInvoice` e consulta do processo

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Iniciar emissão

```text
POST /api/v1/invoices/{invoiceId}/print
Política: AdminOnly
Headers: If-Match e Idempotency-Key obrigatórios
Body: ausente
```

`Idempotency-Key` utiliza UUID textual canônico. `X-Correlation-ID` segue a convenção geral e permanece opcional.

A validação obedece à seguinte ordem:

1. validar sintaticamente o identificador da invoice e os headers obrigatórios;
2. procurar globalmente a chave de idempotência antes de comparar a versão ou o estado atual da invoice;
3. se a chave estiver associada a outra invoice, rejeitar seu reaproveitamento;
4. se a chave estiver associada à mesma invoice, retornar o processo original sem repetir efeitos;
5. somente para chave nova, carregar a invoice, validar `If-Match`, estado, itens e ausência de processamento ativo;
6. bloquear a invoice, criar o processo em `pending` e persistir `StockDeductionRequested` na Outbox, atomicamente.

Uma repetição com a mesma chave e a mesma invoice é uma recuperação da intenção original, não uma nova emissão nem uma reimpressão. O `If-Match` deve continuar presente e sintaticamente válido, mas sua versão desatualizada não invalida essa recuperação. Isso permite ao cliente repetir uma requisição cuja resposta tenha sido perdida.

Response de processo:

```json
{
  "id": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
  "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
  "status": "pending",
  "isDelayed": false,
  "createdAtUtc": "2026-08-18T18:30:00Z",
  "updatedAtUtc": "2026-08-18T18:30:00Z"
}
```

Para uma nova solicitação aceita:

```text
Status: 202 Accepted
Location: /api/v1/invoice-issuance-processes/{processId}
Retry-After: 1
ETag: "<current-invoice-version>"
```

O mesmo contrato é usado em repetições idempotentes:

- processo ainda ativo: `202 Accepted`, com o mesmo `id`, `Location` e `Retry-After`;
- processo terminal: `200 OK`, com o resultado original;
- nenhuma repetição cria processo, Outbox ou baixa adicional;
- o response devolve o `ETag` atual da invoice.

RabbitMQ indisponível não impede `202` quando processo, bloqueio e Outbox já foram confirmados no banco do Billing. Se essa persistência local não for confirmada, nenhuma solicitação é considerada aceita.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| UUID da invoice malformado | 400 | `invalid_invoice_id` |
| `Idempotency-Key` malformada | 400 | `invalid_idempotency_key` |
| `If-Match` malformado | 400 | `invalid_if_match` |
| Invoice inexistente | 404 | `invoice_not_found` |
| Invoice sem itens | 409 | `invoice_has_no_items` |
| Invoice fechada | 409 | `invoice_not_open` |
| Emissão ativa com outra chave | 409 | `invoice_issuance_in_progress` |
| Chave vinculada a outra invoice | 409 | `idempotency_key_reused` |
| ETag desatualizado para chave nova | 412 | `invoice_version_mismatch` |
| `If-Match` ausente | 428 | `invoice_version_required` |
| `Idempotency-Key` ausente | 400 | `idempotency_key_required` |
| Billing não confirma a persistência | 503 | `billing_unavailable` |

Após um processo `rejected`, uma nova tentativa funcional exige uma nova chave. Invoice em `manual_intervention` permanece bloqueada e não admite nova tentativa pública.

#### Consultar processo de emissão

```text
GET /api/v1/invoice-issuance-processes/{processId}
Política: AuthenticatedUser
```

Response `200 OK`:

```json
{
  "id": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
  "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
  "status": "completed",
  "isDelayed": false,
  "createdAtUtc": "2026-08-18T18:30:00Z",
  "updatedAtUtc": "2026-08-18T18:30:02Z",
  "finishedAtUtc": "2026-08-18T18:30:02Z"
}
```

`outcomeCode` e `outcomeDescription` existem somente em `rejected` ou `manual_intervention`. `finishedAtUtc` existe nos três estados terminais. Esses campos são omitidos enquanto não existirem. `isDelayed` é calculado e vale `true` quando `pending` ou `awaiting_stock` durar mais de cinco segundos; não é estado persistido nem confirmação de falha.

Estados e efeitos observáveis:

| Estado | Situação da invoice | Significado público |
|---|---|---|
| `pending` | Aberta e bloqueada | Solicitação persistida, ainda sem confirmação de publicação |
| `awaiting_stock` | Aberta e bloqueada | Solicitação publicada, aguardando Inventory |
| `completed` | Fechada | Estoque baixado e fluxo de impressão liberado |
| `rejected` | Aberta e desbloqueada | Nenhuma baixa aplicada; invoice pode ser corrigida |
| `manual_intervention` | Aberta e bloqueada | Processamento automático encerrado sem conclusão segura |

Enquanto o processo estiver ativo, o response recomenda polling pelo header `Retry-After`: um segundo nos dez primeiros segundos e três segundos posteriormente. Estados terminais não retornam esse header.

Erros específicos:

| Situação | Status | `code` |
|---|---:|---|
| UUID do processo malformado | 400 | `invalid_issuance_process_id` |
| Processo inexistente | 404 | `invoice_issuance_process_not_found` |

Não existe endpoint público para alterar diretamente o processo. O backend inicia e acompanha a emissão; somente depois de `completed` o frontend abre a visualização de impressão do navegador.

### 7.6 Bloco 6 - Envelope e contratos de eventos

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Envelope comum

Todas as mensagens usam um envelope explícito e versionado:

```json
{
  "messageId": "02b61d75-942a-4962-ab1c-d212d88c8548",
  "messageType": "stock_deduction_requested",
  "messageVersion": 1,
  "occurredAtUtc": "2026-08-18T18:30:00Z",
  "correlationId": "cd39c905-d8a5-427f-b12b-e90f2303bf98",
  "causationId": null,
  "producer": "billing",
  "payload": {}
}
```

| Campo | Regra |
|---|---|
| `messageId` | UUID único da mensagem, utilizado por Inbox, diagnóstico e detecção de inconsistência |
| `messageType` | Discriminador estável em `snake_case` |
| `messageVersion` | Versão inteira positiva do contrato |
| `occurredAtUtc` | Instante UTC de criação da mensagem |
| `correlationId` | UUID propagado durante todo o fluxo iniciado pela requisição HTTP |
| `causationId` | UUID da mensagem causadora; nulo no evento iniciado diretamente por HTTP |
| `producer` | Nome canônico `billing` ou `inventory` |
| `payload` | Objeto específico do tipo e da versão da mensagem |

Os nomes de classes C# permanecem em PascalCase, enquanto os valores serializados de `messageType` e `producer` obedecem aos valores canônicos do contrato JSON.

Na persistência técnica definida pelo SDD-02, `messageId` corresponde ao `Id` da Outbox e `messageVersion` corresponde a `SchemaVersion`. Tipo, versão e metadados ficam em colunas próprias para seleção e diagnóstico; o dispatcher monta o envelope publicado sem alterar o payload funcional persistido. `producer` é determinado pelo serviço proprietário da Outbox.

#### `StockDeductionRequested`

Direção: Billing para Inventory.

```json
{
  "messageId": "02b61d75-942a-4962-ab1c-d212d88c8548",
  "messageType": "stock_deduction_requested",
  "messageVersion": 1,
  "occurredAtUtc": "2026-08-18T18:30:00Z",
  "correlationId": "cd39c905-d8a5-427f-b12b-e90f2303bf98",
  "causationId": null,
  "producer": "billing",
  "payload": {
    "issuanceProcessId": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
    "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
    "invoiceNumber": 42,
    "requestedByUserId": "6dab8c4c-2bb8-46cc-a865-0e992aaeb443",
    "items": [
      {
        "productId": "37e52de4-b892-4418-8abd-9799ee95cb38",
        "quantity": 2
      }
    ]
  }
}
```

Regras do payload:

- `items` possui ao menos um elemento;
- `productId` não se repete, como consequência da unicidade por produto na invoice;
- `quantity` é inteira e positiva;
- saldo, código e descrição não são transportados;
- Inventory consulta e altera somente sua própria fonte de verdade;
- todos os itens são processados em uma única transação local.

#### `StockDeductionCompleted`

Direção: Inventory para Billing.

```json
{
  "messageId": "68bd3ae8-0eb5-4e14-aa32-fb767df49b42",
  "messageType": "stock_deduction_completed",
  "messageVersion": 1,
  "occurredAtUtc": "2026-08-18T18:30:02Z",
  "correlationId": "cd39c905-d8a5-427f-b12b-e90f2303bf98",
  "causationId": "02b61d75-942a-4962-ab1c-d212d88c8548",
  "producer": "inventory",
  "payload": {
    "issuanceProcessId": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
    "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7"
  }
}
```

O evento confirma que todos os saldos foram baixados atomicamente. Novos saldos e movimentos permanecem sob propriedade de Inventory e não são replicados para Billing.

#### `StockDeductionRejected`

Direção: Inventory para Billing.

```json
{
  "messageId": "13d503e1-48d7-4585-a3fa-586f8c77b144",
  "messageType": "stock_deduction_rejected",
  "messageVersion": 1,
  "occurredAtUtc": "2026-08-18T18:30:02Z",
  "correlationId": "cd39c905-d8a5-427f-b12b-e90f2303bf98",
  "causationId": "02b61d75-942a-4962-ab1c-d212d88c8548",
  "producer": "inventory",
  "payload": {
    "issuanceProcessId": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
    "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
    "reasonCode": "insufficient_stock",
    "reasonDescription": "Estoque insuficiente para um ou mais produtos.",
    "failures": [
      {
        "productId": "37e52de4-b892-4418-8abd-9799ee95cb38",
        "requestedQuantity": 2,
        "availableBalance": 1
      }
    ]
  }
}
```

Códigos determinísticos previstos:

| `reasonCode` | Significado |
|---|---|
| `insufficient_stock` | Ao menos um produto não possui saldo suficiente |
| `product_not_found` | Ao menos um identificador não existe em Inventory |
| `invalid_stock_deduction_request` | Payload válido como JSON, mas contrário às invariantes do contrato |

`failures` contém somente informações seguras e relevantes ao diagnóstico funcional. A rejeição significa que nenhuma baixa parcial foi confirmada.

Cada falha identifica `productId` e `requestedQuantity`. `availableBalance` existe somente quando conhecido, especialmente em `insufficient_stock`, e é omitido para produto inexistente. Em `invalid_stock_deduction_request`, a coleção pode ser omitida quando o erro não estiver associado a um produto específico.

#### `StockDeductionProcessingFailed`

Direção: Inventory para Billing.

```json
{
  "messageId": "a9bb57e4-afc6-4b18-b0da-8e2bf37062e5",
  "messageType": "stock_deduction_processing_failed",
  "messageVersion": 1,
  "occurredAtUtc": "2026-08-18T18:31:00Z",
  "correlationId": "cd39c905-d8a5-427f-b12b-e90f2303bf98",
  "causationId": "02b61d75-942a-4962-ab1c-d212d88c8548",
  "producer": "inventory",
  "payload": {
    "issuanceProcessId": "b6f53891-056a-4cf5-a113-70f8c53ceef5",
    "invoiceId": "771ff4e5-1b47-4fb3-a7c4-fcb678b29fe7",
    "reasonCode": "stock_processing_failed",
    "reasonDescription": "Não foi possível concluir o processamento automaticamente."
  }
}
```

Esse evento técnico conduz o processo a `manual_intervention` quando Inventory consegue persistir e publicar com segurança o esgotamento do processamento automático. Exceções, hosts, filas, stack traces e outros detalhes internos permanecem exclusivamente nos logs.

Uma DLQ isolada não produz esse evento e não altera diretamente o banco de Billing. Caso a própria falha impeça Inventory de persistir ou publicar o resultado técnico, o processo pode continuar em `awaiting_stock` até diagnóstico; o sistema não alegará reconciliação automática inexistente.

Os nomes físicos de exchanges, filas, routing keys, retries e DLQs pertencem ao SDD-07. Este bloco estabiliza somente os contratos transportados.

### 7.7 Bloco 7 - Compatibilidade, correlação e idempotência

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Evolução compatível

- consumidores ignoram propriedades JSON adicionais desconhecidas;
- uma propriedade nova e opcional pode permanecer na mesma versão;
- remoção, renomeação, mudança de tipo, mudança semântica incompatível ou nova propriedade obrigatória exige incremento de `messageVersion`;
- uma combinação publicada de tipo e versão é imutável;
- durante migrações, consumidores aceitam simultaneamente as versões ainda produzidas;
- uma versão antiga só é removida depois de todos os produtores correspondentes serem atualizados e não existirem mensagens pendentes dessa versão.

Tipo ou versão desconhecida não representa rejeição de negócio. É incompatibilidade técnica determinística e segue diretamente para DLQ, pois repetir o mesmo conteúdo não o torna compatível. O SDD-07 definirá a topologia física desse encaminhamento.

#### Classificação de entradas inválidas

| Entrada | Classificação | Tratamento |
|---|---|---|
| JSON ou envelope ilegível | Falha técnica determinística | Nenhum efeito funcional; DLQ direta |
| Tipo ou versão não suportada | Incompatibilidade técnica determinística | Nenhum efeito funcional; DLQ direta |
| Envelope reconhecido e payload contrário às invariantes | Rejeição determinística | `StockDeductionRejected` com `invalid_stock_deduction_request` |
| Produto inexistente ou saldo insuficiente | Rejeição funcional | Resposta determinística e nenhum efeito parcial |
| Exceção transitória de banco ou infraestrutura | Falha técnica recuperável | Retry, sem rejeição funcional prematura |

Somente uma solicitação reconhecida, correlacionável e semanticamente validável pode originar evento de resposta funcional.

#### Inbox e identidade de conteúdo

O consumidor calcula SHA-256 sobre os bytes UTF-8 do corpo recebido e registra, conforme SDD-02:

```text
MessageId
MessageType
MessageVersion
PayloadHash
CorrelationId
ProcessedAtUtc
```

O tratamento é:

- `messageId` novo: processar efeitos, registrar Inbox e, quando aplicável, resposta na Outbox na mesma transação;
- mesmo `messageId` e mesmo hash: confirmar a entrega sem repetir efeitos;
- mesmo `messageId` e hash diferente: violação de integridade, sem efeitos, registrada com severidade crítica e encaminhada para diagnóstico e DLQ.

A republicação de uma Outbox preserva exatamente o `messageId` e o corpo originalmente persistidos. Nenhuma tentativa de publicação cria uma nova identidade lógica.

#### Duplicidade lógica com outro `messageId`

Inbox protege a repetição da mesma mensagem, mas não basta quando mensagens distintas representam a mesma intenção. Inventory aplica também as restrições lógicas definidas no SDD-02:

- uma invoice já baixada não sofre nova redução de saldo;
- se uma nova mensagem para a mesma invoice corresponder aos mesmos produtos e quantidades já registrados, Inventory registra seu consumo sem nova baixa e pode produzir novamente uma confirmação;
- se produtos ou quantidades divergirem, a entrada é uma inconsistência técnica e não uma segunda operação funcional;
- unicidade de `StockMovement` por evento/produto e por invoice/produto permanece como defesa persistente adicional.

A resposta recriada para outro `messageId` possui identidade própria, usa esse novo request como `causationId` e não modifica os movimentos existentes.

#### Propagação de correlação e causalidade

```text
Requisição HTTP
  correlationId = X-Correlation-ID efetivo

StockDeductionRequested
  correlationId = identificador da requisição
  causationId = null

StockDeductionCompleted, StockDeductionRejected ou StockDeductionProcessingFailed
  correlationId = o mesmo identificador
  causationId = messageId de StockDeductionRequested
```

Cada mensagem nova recebe seu próprio `messageId`. `correlationId` agrupa a operação distribuída, enquanto `causationId` representa a relação direta entre entrada e resposta; nenhum deles substitui a identidade idempotente da mensagem.

#### Limites transacionais e confirmação ao broker

Inventory confirma em uma única transação local:

- alterações de todos os saldos envolvidos;
- respectivos `StockMovement`;
- registro da Inbox;
- evento de resultado na Outbox.

Billing confirma em uma única transação local:

- registro da Inbox;
- transição do `InvoiceIssuanceProcess`;
- fechamento ou desbloqueio da invoice.

O consumer envia `ack` ao RabbitMQ somente depois da confirmação da transação local. Falha antes do commit permite nova entrega; falha depois do commit e antes do `ack` produz redelivery neutralizado pela Inbox. O sistema assume entrega at-least-once com efeitos idempotentes e não declara exactly-once.

### 7.8 Bloco 8 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-18

---

## 8. Critérios de aceite

### CA-CON-01 - Fronteira pública

**Dado** o conjunto de rotas dos serviços,  
**quando** a superfície do Gateway for verificada,  
**então** somente as rotas públicas aprovadas estarão expostas e `/api/v1/internal/*` permanecerá inacessível externamente.

### CA-CON-02 - Convenções HTTP

**Dado** qualquer endpoint público,  
**quando** requests e responses forem exercitados,  
**então** autenticação, autorização, JSON, paginação, status HTTP, headers e Problem Details seguirão as convenções deste SDD.

### CA-CON-03 - Login seguro

**Dado** credenciais inválidas,  
**quando** o login for solicitado,  
**então** usuário inexistente e senha incorreta produzirão respostas indistinguíveis, sem exposição de senha, hash, token ou detalhe interno.

### CA-CON-04 - Propriedade de Product e saldo

**Dado** os contratos públicos, internos e assíncronos,  
**quando** seus campos forem inspecionados,  
**então** Inventory permanecerá como único proprietário de Product, Balance e StockMovement, sem permitir alteração externa de saldo.

### CA-CON-05 - Mutação concorrente de itens

**Dado** uma tentativa de incluir, atualizar ou remover item,  
**quando** a operação for validada,  
**então** somente invoice aberta, desbloqueada e com versão correspondente será alterada.

### CA-CON-06 - Aceite durável da emissão

**Dado** um `PrintInvoice` válido com chave nova,  
**quando** Billing retornar `202 Accepted`,  
**então** processo, bloqueio da invoice e Outbox terão sido persistidos atomicamente.

### CA-CON-07 - Repetição HTTP idempotente

**Dado** uma chave já associada à mesma invoice,  
**quando** `PrintInvoice` for repetido durante ou depois do processamento,  
**então** o processo original será retornado sem criar processo, Outbox ou baixa adicional.

### CA-CON-08 - Escopo da chave idempotente

**Dado** uma chave associada a uma invoice,  
**quando** ela for enviada para outra invoice,  
**então** a solicitação será rejeitada com `409` e `idempotency_key_reused`.

### CA-CON-09 - Representação do processo

**Dado** qualquer estado permitido do processo,  
**quando** ele for consultado,  
**então** status, campos opcionais, situação da invoice, `Retry-After` e atraso calculado corresponderão às regras aprovadas.

### CA-CON-10 - Contratos de eventos

**Dado** qualquer mensagem de emissão,  
**quando** for serializada ou consumida,  
**então** envelope, direção, tipo, versão e payload corresponderão a um dos quatro contratos aprovados.

### CA-CON-11 - Redelivery seguro

**Dado** uma mensagem já confirmada localmente,  
**quando** o mesmo ID e conteúdo forem entregues novamente,  
**então** nenhuma baixa, movimentação ou transição será repetida.

### CA-CON-12 - Identidade adulterada

**Dado** um `messageId` já registrado,  
**quando** outro conteúdo for recebido com o mesmo identificador,  
**então** nenhum efeito será aplicado e a violação de integridade será encaminhada para diagnóstico.

### CA-CON-13 - Atomicidade da baixa

**Dado** uma solicitação com múltiplos produtos,  
**quando** Inventory concluir ou rejeitar a operação,  
**então** todos os saldos e movimentos serão confirmados ou nenhum deles será aplicado.

### CA-CON-14 - Correlação distribuída

**Dado** um fluxo iniciado por HTTP,  
**quando** chamadas internas, eventos e logs forem observados,  
**então** o mesmo `correlationId` será preservado e cada resposta apontará para sua causa direta.

### CA-CON-15 - Compatibilidade versionada

**Dado** uma evolução de contrato,  
**quando** sua compatibilidade for avaliada,  
**então** mudanças incompatíveis criarão nova versão e propriedades opcionais adicionais não quebrarão consumidores existentes.

### CA-CON-16 - OpenAPI fiel

**Dado** o documento OpenAPI gerado,  
**quando** ele for comparado aos endpoints implementados,  
**então** rotas, autenticação, headers, requests, responses e erros relevantes refletirão este SDD.

---

## 9. Estratégia de testes planejada

| ID | Teste planejado | Nível | Critérios |
|---|---|---|---|
| TST-CON-001 | Verificar rotas públicas e bloqueio de `/internal/*` | Integração/arquitetura | CA-CON-01 |
| TST-CON-002 | Exercitar políticas anônima, autenticada e administrativa | Integração | CA-CON-02, CA-CON-03 |
| TST-CON-003 | Validar serialização, UTC, enums e paginação | Unitário/integração | CA-CON-02 |
| TST-CON-004 | Validar Problem Details e sanitização | Integração | CA-CON-02, CA-CON-03 |
| TST-CON-005 | Comparar respostas para credenciais inválidas | Integração | CA-CON-03 |
| TST-CON-006 | Exercitar contratos de Product e duplicidade de código | Integração | CA-CON-04 |
| TST-CON-007 | Exercitar criação e consultas de Invoice | Integração | CA-CON-02 |
| TST-CON-008 | Incluir, atualizar e remover itens em estados permitidos e proibidos | Integração | CA-CON-05 |
| TST-CON-009 | Exercitar `ETag`, `If-Match`, `412` e `428` | Integração | CA-CON-05 |
| TST-CON-010 | Executar primeira chamada válida de `PrintInvoice` | Integração | CA-CON-06 |
| TST-CON-011 | Repetir a mesma chave durante e depois do processo | Integração | CA-CON-07 |
| TST-CON-012 | Reutilizar a chave em outra invoice | Integração | CA-CON-08 |
| TST-CON-013 | Consultar todos os estados, atraso e polling | Unitário/integração | CA-CON-09 |
| TST-CON-014 | Serializar e desserializar fixtures dos quatro eventos | Contrato | CA-CON-10 |
| TST-CON-015 | Consumir propriedade opcional desconhecida | Contrato | CA-CON-15 |
| TST-CON-016 | Receber tipo e versão não suportados | Integração | CA-CON-10, CA-CON-15 |
| TST-CON-017 | Reentregar o mesmo ID e hash | Integração | CA-CON-11 |
| TST-CON-018 | Reentregar o mesmo ID com conteúdo adulterado | Integração | CA-CON-12 |
| TST-CON-019 | Entregar intenção lógica equivalente com outro ID | Integração | CA-CON-11, CA-CON-13 |
| TST-CON-020 | Propagar correlação e causalidade ponta a ponta | Integração | CA-CON-14 |
| TST-CON-021 | Comparar OpenAPI gerado aos endpoints | Snapshot/arquitetura | CA-CON-16 |
| TST-CON-022 | Rejeitar solicitação com múltiplos produtos sem confirmar baixa parcial | Integração | CA-CON-13 |

Os exemplos JSON aprovados serão preservados como fixtures contratuais. Testes de integração usam PostgreSQL e, quando a garantia depender do transporte, RabbitMQ reais em containers. Testes unitários cobrem serialização, validações e regras puras; o código relevante permanece sujeito ao gate mínimo de 80% definido pelo ADR-014.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| OpenAPI divergir do comportamento | Integração incorreta e documentação enganosa | Teste automatizado e revisão dos DTOs e endpoints |
| Retry HTTP criar outra emissão | Processos e baixas duplicados | Chave persistida, ordem de validação definida e testes de repetição |
| Evento duplicado baixar estoque novamente | Saldo incorreto | Inbox, hash e restrições persistentes adicionais |
| Evento evoluir quebrando consumidor | Interrupção do fluxo | Versionamento explícito e testes de compatibilidade |
| Detalhes técnicos vazarem em erros | Exposição de implementação ou dados | Problem Details e descrições de eventos sanitizados |
| Polling excessivo | Carga desnecessária | `Retry-After` progressivo e intervalo mínimo controlado pelo frontend |
| Gateway expor rota interna | Violação da fronteira de serviço | Rotas explícitas e teste negativo de exposição |
| DTO compartilhado acoplar domínios | Dependência indevida entre serviços | Compartilhamento restrito aos eventos de integração |
| Fluxo parecer exactly-once | Expectativa operacional falsa | Documentar at-least-once e demonstrar efeitos idempotentes |
| Evento técnico não puder ser publicado | Processo permanecer aguardando | Estado atrasado, DLQ, logs correlacionados e limitação operacional explícita |

---

## 11. Marcadores de qualidade

| Marcador | Exigência neste SDD |
|---|---|
| ESP | Todos os contratos aprovados antes de endpoints, DTOs ou mensagens |
| RAS | Cada contrato ligado a requisito, critério e teste planejado |
| ARC | Gateway, HTTP interno e RabbitMQ respeitam suas fronteiras |
| DOM | Nenhum contrato permite contornar invariantes dos agregados |
| ERR | Erros funcionais, rejeições e falhas técnicas permanecem distintos e sanitizados |
| SEG | Autenticação e autorização cobrem toda rota protegida e dados sensíveis não são expostos |
| TST | Contratos relevantes possuem testes e integram o gate mínimo de 80% |
| INT | PostgreSQL e RabbitMQ reais comprovam as garantias dependentes de infraestrutura |
| OBS | Correlação, causalidade e identificadores permitem diagnóstico distribuído |
| DOC | OpenAPI, fixtures e documentação permanecem sincronizados |
| QA | Cenários positivos, negativos, concorrentes, incompatíveis e idempotentes são revisados |

---

## 12. Limites para a futura implementação

Uma implementação deste SDD poderá criar:

- DTOs HTTP exclusivos por operação;
- mapeamento e validação de headers e requests;
- configuração OpenAPI dos contratos;
- contratos de eventos versionados em `Korp.Shared.Contracts`;
- testes unitários, contratuais, de integração e arquitetura descritos neste documento.

Não poderá, por antecipação deste SDD isolado, definir:

- regras de aplicação completas dos serviços;
- configuração final do Gateway;
- publishers, consumers ou topologia RabbitMQ;
- política física de retry e DLQ;
- polling e impressão no frontend;
- composição final do Docker Compose.

Essas responsabilidades dependem dos SDDs próprios. A existência de um contrato não autoriza inverter propriedade de dados nem adicionar integração não aprovada.

---

## 13. Condição para Gate A

O SDD-03 estará apto ao Gate A quando:

- os oito blocos de decisão estiverem aprovados;
- revisão integral não encontrar contradição material com SDD-01, SDD-02 ou ADRs vigentes;
- todo contrato possuir proprietário, consumidor e comportamento de erro explícitos;
- cada critério de aceite estiver ligado a pelo menos um teste planejado;
- a matriz de rastreabilidade estiver atualizada;
- dependências dos SDDs posteriores estiverem identificadas;
- nenhuma garantia exatamente uma vez ou reconciliação automática inexistente for declarada.

A aprovação do Gate A estabilizará os contratos para os SDDs de serviços, segurança, emissão e frontend. Conforme a macroetapa documental, não autorizará implementação funcional antes da baseline conjunta dos SDDs.
