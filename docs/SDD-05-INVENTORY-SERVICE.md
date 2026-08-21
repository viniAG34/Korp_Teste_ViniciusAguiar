# SDD-05 - Inventory Service

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-18
> Gate A aprovado em: 2026-08-18
> Dependências: SDD-01, SDD-02, SDD-03, SDD-04, ADR-001, ADR-006, ADR-007, ADR-008, ADR-009, ADR-010, ADR-011, ADR-012 e ADR-014

---

## 1. Objetivo

Especificar o microsserviço responsável pelo cadastro e consulta de produtos, propriedade dos saldos e aplicação atômica das baixas solicitadas pelo fluxo de emissão.

Este é o primeiro SDD que detalha uma funcionalidade obrigatória do desafio. Ele transforma os contratos e o modelo aprovados em casos de uso, regras de domínio, responsabilidades por camada, persistência, falhas e testes implementáveis, ainda sem escrever código nesta macroetapa.

---

## 2. Requisitos rastreados

- `OBR-002` a `OBR-006`;
- `OBR-014` e `OBR-017`;
- `OBR-019` e `OBR-020`, no limite do comportamento do Inventory;
- `OPA-001` e `OPA-002`;
- `DIF-002` a `DIF-004`, `DIF-006`, `DIF-008` e `DIF-009`;
- `QLT-001` a `QLT-008`;
- `APR-009` e `APR-010`.

---

## 3. Escopo previsto

- propriedade do agregado `Product`, saldo e movimentos;
- responsabilidades por camada e casos de uso;
- criação e consulta pública de produtos;
- consulta interna de snapshot para Billing;
- normalização, validação e duplicidade de código;
- autoria obtida de `sub`;
- baixa atômica de múltiplos produtos;
- concorrência otimista e saldo insuficiente;
- criação de `StockMovement`;
- limites transacionais locais;
- tradução de falhas, segurança, logs e OpenAPI;
- critérios de aceite e testes.

---

## 4. Fora do escopo

- fornecedores, compras e entradas comerciais;
- ajuste manual de saldo;
- edição ou exclusão de produto;
- reserva antecipada de estoque;
- preço, unidade de medida ou categoria;
- invoices e seus estados;
- publishers, consumers, topologia RabbitMQ, retry e DLQ;
- orquestração completa da emissão, pertencente ao SDD-07;
- telas Angular;
- composição final do Docker Compose.

---

## 5. Blocos de decisão

1. responsabilidade, casos de uso e limites por camada;
2. criação de Product e invariantes;
3. consultas públicas e consulta interna;
4. baixa atômica, movimentos e concorrência;
5. persistência, transações e tradução de falhas;
6. segurança, observabilidade, OpenAPI e LINQ;
7. critérios de aceite, testes, riscos e marcadores.

Nenhuma implementação funcional será iniciada durante esta macroetapa documental.

---

## 6. Decisões herdadas

- Inventory é proprietário exclusivo de `Product`, `Balance` e `StockMovement`;
- PostgreSQL exclusivo `inventory_db` e migrations próprias;
- `Product` é Aggregate Root com concorrência por `xmin`;
- saldo é inteiro, inicia em valor maior ou igual a zero e nunca fica negativo;
- código é obrigatório, normalizado e único;
- descrição é obrigatória e normalizada;
- criação pública recebe saldo inicial, sem inventar compra ou movimentação de entrada;
- produto não pode ser editado ou excluído nesta feature;
- consulta interna retorna somente `id`, `code` e `description`;
- baixa de todos os itens é atômica;
- cada baixa confirmada cria movimentos auditáveis e imutáveis;
- JWT e políticas seguem SDD-04;
- contratos HTTP e eventos seguem SDD-03;
- mensageria usa at-least-once com Inbox e Outbox, detalhada no SDD-07;
- implementação e testes são Docker-first, com PostgreSQL real quando relevante.

---

## 7. Decisões em elaboração

Os blocos aprovados serão registrados nesta seção.

### 7.1 Bloco 1 - Responsabilidade, casos de uso e limites por camada

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Responsabilidade funcional

Inventory é a única autoridade capaz de cadastrar e consultar produtos, informar saldo, fornecer snapshot de catálogo a Billing, reduzir saldo, registrar movimentos e resolver concorrência sobre Product.

Billing pode solicitar uma baixa, mas nunca lê diretamente o banco de Inventory, calcula um saldo autoritativo ou altera Product.

#### Casos de uso

```text
CreateProduct
GetProductById
ListProducts
GetProductSnapshot
DeductInvoiceStock
```

| Caso de uso | Responsabilidade |
|---|---|
| `CreateProduct` | Criar produto com saldo inicial validado |
| `GetProductById` | Consultar representação pública com saldo |
| `ListProducts` | Listar produtos de forma paginada e determinística |
| `GetProductSnapshot` | Fornecer internamente ID, código e descrição a Billing |
| `DeductInvoiceStock` | Baixar atomicamente os produtos de uma invoice |

`DeductInvoiceStock` não possui endpoint HTTP. É um caso de uso interno da Application acionado pelo adapter RabbitMQ que será definido no SDD-07.

#### Portas de entrada

```text
HTTP público
    -> CreateProduct
    -> GetProductById
    -> ListProducts

HTTP interno
    -> GetProductSnapshot

RabbitMQ
    -> adapter de consumo
        -> DeductInvoiceStock
```

O caso de uso de baixa não conhece exchange, fila, delivery tag, retry, acknowledgment ou cliente RabbitMQ. Recebe um comando validado e retorna resultado funcional.

#### Responsabilidade por camada

```text
Inventory.Api
  -> endpoints Minimal API
  -> autenticação e autorização
  -> contratos HTTP e Problem Details
  -> OpenAPI e composição de dependências
  -> hospedagem futura do adapter RabbitMQ

Inventory.Application
  -> commands, queries e handlers
  -> validação dos casos de uso
  -> coordenação de persistência e transações
  -> paginação, projeções e resultados independentes de transporte

Inventory.Domain
  -> Product e StockMovement
  -> normalização e invariantes
  -> proteção do saldo e decisão de baixa
  -> erros de domínio

Inventory.Infrastructure
  -> InventoryDbContext e configurações EF Core
  -> repositórios específicos e migrations
  -> transações e concorrência PostgreSQL
  -> persistência futura de Inbox e Outbox
```

#### Limites arquiteturais

- Domain não referencia EF Core, ASP.NET Core, RabbitMQ ou DTOs;
- Application não conhece `HttpContext`, status HTTP, Problem Details ou delivery do broker;
- Api não altera entidades diretamente;
- Infrastructure não concentra regras de saldo pertencentes ao agregado;
- endpoints nunca serializam entidades diretamente;
- não existe repositório genérico;
- consultas podem projetar DTOs sem carregar agregados quando não houver mutação;
- alterações de saldo carregam e modificam os agregados necessários;
- o consumer traduz contrato para comando e delega o processamento.

#### Resultado da baixa

`DeductInvoiceStock` retorna resultado discriminado:

```text
Completed
Rejected
```

Rejeições funcionais previstas:

```text
ProductNotFound
InsufficientStock
InvalidRequest
```

Exceção de banco, timeout ou conflito concorrente ainda não resolvido permanece falha técnica. Ela não é convertida em rejeição de negócio e segue a política de recuperação do SDD-07.

### 7.2 Bloco 2 - Criação de Product e invariantes

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Entrada do caso de uso

```text
CreateProduct
- Code
- Description
- InitialBalance
- CreatedByUserId
```

`CreatedByUserId` não pertence ao JSON. A API o extrai de `sub` somente depois da autorização e entrega o UUID validado à Application.

#### Código

```text
entrada
    -> Trim
        -> ToUpperInvariant
            -> validar formato
```

Regras:

- obrigatório e com 1 a 50 caracteres depois da normalização;
- somente `A-Z`, `0-9`, hífen, underscore e ponto;
- sem espaços internos;
- persistido em uppercase;
- único e imutável.

Expressão canônica:

```text
^[A-Z0-9._-]+$
```

`" prod-001 "` é normalizado para `"PROD-001"`. Valores que produzem o mesmo código normalizado representam a mesma identidade funcional.

#### Descrição

- obrigatória;
- recebe somente `Trim`;
- possui 1 a 200 caracteres após o trim;
- preserva espaços internos;
- rejeita caracteres de controle;
- não recebe uppercase;
- não pode ser alterada nesta feature.

A aplicação não aplica normalização agressiva que modifique conteúdo legítimo de apresentação.

#### Saldo inicial

```text
0 <= InitialBalance <= int.MaxValue
```

Zero é válido. O contrato exige número inteiro JSON e não converte texto ou decimal. O valor é persistido diretamente em `Balance` e não cria `StockMovement`.

O saldo representa somente o ponto inicial conhecido para a demonstração. Não é documentado como compra, entrada de fornecedor ou ajuste auditado.

#### Factory e estado inicial

Conceitualmente:

```text
Product.Create(
    id,
    normalizedCode,
    normalizedDescription,
    initialBalance,
    createdByUserId,
    createdAtUtc)
```

Estado produzido:

```text
Balance = InitialBalance
CreatedAtUtc = now
UpdatedAtUtc = now
CreatedByUserId = sub
```

ID e instante vêm de abstrações testáveis da Application. O agregado não consulta infraestrutura ou relógio global. A factory protege as invariantes mesmo quando a origem não for HTTP.

#### Duplicidade concorrente

Uma consulta preventiva pode melhorar o caminho comum, mas a garantia definitiva é o índice único de `products.code`:

```text
consulta preventiva
    -> tentativa de persistência
        -> constraint única
```

Somente violação identificada da constraint de código é traduzida para:

```text
409 Conflict
code: product_code_already_exists
```

Outras violações ou falhas de banco não são classificadas genericamente como duplicidade.

#### Resposta e defesas

```text
201 Created
Location: /api/v1/products/{productId}
```

O corpo usa `ProductResponse` do SDD-03 com valores normalizados. Cliente nunca envia ID, saldo atual separado, autoria, versão, datas ou movimentos.

- Api/Application validam contrato e produzem mensagens por campo;
- Domain protege invariantes independentemente da origem;
- PostgreSQL é segunda defesa para unicidade, formato e saldo não negativo.

### 7.3 Bloco 3 - Consultas públicas e consulta interna

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Consulta pública individual

```text
GET /api/v1/products/{productId}
Política: AuthenticatedUser
```

A consulta valida o UUID e projeta diretamente `ProductResponse`. Inclui ID, código, descrição, saldo e timestamps; não inclui autoria, versão persistente, movimentos ou dados do Identity.

| Situação | Status | `code` |
|---|---:|---|
| UUID inválido | 400 | `invalid_product_id` |
| Produto inexistente | 404 | `product_not_found` |
| Banco indisponível | 503 | `inventory_unavailable` |

Como Product não possui edição pública, a representação não precisa fornecer `ETag`.

#### Listagem pública

```text
GET /api/v1/products?pageNumber=1&pageSize=20
Política: AuthenticatedUser
```

- `pageNumber` mínimo 1;
- `pageSize` entre 1 e 100;
- valores padrão 1 e 20;
- parâmetros inválidos retornam `400`;
- ordenação fixa por `code` e depois `id`;
- página além do último resultado retorna `200` com `items` vazio;
- `totalPages` vale zero quando `totalCount` for zero.

```text
totalPages = ceiling(totalCount / pageSize)
```

Não existem pesquisa, filtro de saldo, ordenação fornecida pelo cliente ou paginação por cursor.

A contagem e a página podem usar consultas separadas sem isolamento elevado. Criação concorrente pode produzir pequena diferença temporal entre `totalCount` e `items`; isso é aceitável para a listagem administrativa e não afeta invariantes de estoque.

#### Consulta interna

```text
GET /api/v1/internal/products/{productId}
Política: AdminOnly com bearer propagado
Exposição no Gateway: proibida
```

Retorna exclusivamente:

```text
Id
Code
Description
```

Não contém saldo, movimentos, autoria, timestamps ou versão. A rota serve apenas para Billing criar o snapshot do item, sem verificar disponibilidade ou reservar estoque.

Timeout e repetição da chamada são responsabilidades de Billing. Inventory autentica, autoriza, valida o identificador, consulta sua fonte de verdade e retorna seu contrato ou erro sanitizado.

#### Estratégia de leitura e LINQ

As consultas usam `AsNoTracking`, projeção direta, cancellation token e nunca carregam `StockMovement`. A listagem aplica paginação no banco:

```text
OrderBy(Code)
    -> ThenBy(Id)
        -> Skip(...)
            -> Take(...)
                -> Select(...)
```

Esse fluxo constitui uso real e justificável de LINQ para consulta, ordenação, paginação e projeção, sem materializar a tabela antes de `Skip` e `Take`.

### 7.4 Bloco 4 - Baixa atômica, movimentos e concorrência

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Comando interno

```text
DeductInvoiceStock
- EventId
- IssuanceProcessId
- InvoiceId
- Items
    - ProductId
    - Quantity
```

Antes de consultar saldo, o comando exige IDs válidos e não vazios, ao menos um item, quantidades inteiras positivas e `ProductId` não repetido. Violação determinística produz `Rejected` com `invalid_stock_deduction_request`.

#### Ordem de processamento

```text
validar comando
    -> verificar baixa lógica anterior da invoice
        -> carregar todos os produtos
            -> validar existência de todos
                -> validar todos os saldos
                    -> aplicar todas as baixas
                        -> criar todos os movimentos
                            -> confirmar transação
```

Nenhum saldo é modificado antes da validação completa da solicitação.

#### Produto inexistente

Se qualquer produto não existir, nenhum saldo ou movimento é alterado. O resultado é `product_not_found` e identifica de forma segura os produtos ausentes. Suficiência dos demais saldos não é apresentada simultaneamente, pois a solicitação ainda não possui referências integralmente válidas.

#### Saldo insuficiente

Com todos os produtos existentes, todos os saldos são comparados. Se qualquer um for insuficiente, nenhum produto é alterado e nenhum movimento é criado. O resultado `insufficient_stock` pode listar todos os produtos insuficientes com ID, quantidade solicitada e saldo disponível.

#### Operação de domínio

```text
Product.Deduct(quantity, occurredAtUtc)
    -> validar quantity positiva
    -> validar Balance suficiente
    -> capturar BalanceBefore
    -> reduzir Balance
    -> atualizar UpdatedAtUtc
    -> retornar dados para o movimento
```

Cada produto produz `StockMovement` com ID, ProductId, InvoiceId, quantidade, saldos anterior e posterior, tipo `InvoiceDeduction`, EventId e instante. Todos os movimentos da solicitação usam o mesmo instante lógico obtido por `TimeProvider`.

#### Atomicidade local

Produtos, movimentos e as estruturas Inbox e Outbox acrescentadas pelo SDD-07 são confirmados na mesma transação PostgreSQL:

```text
commit   -> todos os produtos baixados
rollback -> nenhum produto baixado
```

O isolamento permanece `ReadCommitted`. Não existe transação distribuída nem lock pessimista como caminho padrão.

#### Concorrência otimista

Cada atualização verifica `xmin`. Em `DbUpdateConcurrencyException`:

1. a tentativa atual é revertida;
2. contexto e transação são descartados;
3. novo escopo carrega novamente todos os produtos;
4. existência e saldos são reavaliados;
5. uma nova decisão funcional pode ser produzida.

São permitidas no máximo três tentativas locais, sem espera artificial. Conflito persistente após o limite é falha técnica e pode receber retry do consumer conforme SDD-07. Ele não é convertido em saldo insuficiente sem leitura atualizada.

No cenário de duas notas disputando a última unidade, uma confirma saldo zero. A outra encontra conflito, recarrega o saldo e retorna `insufficient_stock`, sem saldo negativo ou segundo movimento.

#### Duplicidade lógica

Antes da baixa, Inventory compara movimentos existentes da invoice:

- mesmos produtos e quantidades: retorna `Completed` sem nova baixa;
- conjunto ou quantidades divergentes: inconsistência técnica;
- conjunto parcial de movimentos: inconsistência técnica;
- nenhum movimento: processamento normal.

A repetição do mesmo `messageId` normalmente é interceptada pela Inbox no adapter do SDD-07. As constraints únicas em `(event_id, product_id)` e `(invoice_id, product_id)` permanecem como defesas persistentes adicionais.

### 7.5 Bloco 5 - Persistência, transações e tradução de falhas

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Portas específicas

Application pode depender de:

```text
IProductRepository
IProductReadService
IInventoryUnitOfWork
IInventoryUnitOfWorkFactory
```

- `IProductRepository` carrega e adiciona agregados;
- `IProductReadService` executa projeções das consultas;
- `IInventoryUnitOfWork` representa contexto e transação de uma tentativa;
- `IInventoryUnitOfWorkFactory` produz unidade nova para cada reavaliação concorrente.

Não existem repositório genérico, `IQueryable` atravessando Infrastructure, referência a `DbContext` pela Application ou abstração de transação compartilhada entre microsserviços.

#### Unidade por tentativa

```text
para attempt de 1 até 3
    -> criar IInventoryUnitOfWork novo
    -> iniciar transação ReadCommitted
    -> executar validação e baixa
    -> tentar commit
    -> descartar unidade
```

Em conflito de `xmin`, a unidade inteira é descartada. Entidades rastreadas não são reutilizadas e o token não é substituído manualmente. Cada tentativa observa novamente o estado persistido.

#### Limites por operação

`CreateProduct` utiliza uma gravação e a atomicidade de `SaveChanges`, sem transação explícita adicional. O índice único resolve a disputa real de código.

Consultas usam `AsNoTracking`, projeção no banco e nenhuma transação explícita.

`DeductInvoiceStock` utiliza transação explícita contendo Products, StockMovements e, após o SDD-07, Inbox e resposta na Outbox, com um único commit por tentativa.

#### Carregamento em lote

IDs são validados, tornam-se distintos por regra e são ordenados antes de uma única consulta dos Products:

```text
OrderBy(ProductId)
    -> consulta única
```

Não existe uma chamada ao banco por item. A ordem determinística facilita comparação, testes e reduz a chance de ciclos de disputa.

#### Mapeamento

Infrastructure respeita integralmente o SDD-02:

- tabelas `products` e `stock_movements` em `snake_case`;
- `xmin` mapeado para `Product.Version`;
- índice único de código e constraints de formato e saldo;
- FK local dos movimentos;
- índices únicos de idempotência dos movimentos;
- enum como string canônica em `snake_case`;
- timestamps UTC em `timestamptz`.

Migrations pertencem exclusivamente a Inventory e não são aplicadas no startup da API.

#### Tradução de falhas

| Origem | Tratamento |
|---|---|
| Constraint única conhecida de código | `409 product_code_already_exists` |
| Produto inexistente em consulta HTTP | `404 product_not_found` |
| Saldo insuficiente na baixa | Rejeição funcional |
| Produto inexistente na baixa | Rejeição funcional |
| Conflito de `xmin` | Nova tentativa completa, limitada a três |
| Conflito persistente | Falha técnica |
| Constraint de movimento com repetição equivalente | Reavaliar idempotência sem repetir efeito |
| Movimento existente com conteúdo divergente | Inconsistência técnica |
| Banco indisponível ou timeout | `503` no HTTP ou falha técnica no consumer |
| Constraint inesperada | Falha técnica, sem inventar erro do usuário |
| Cancelamento solicitado | Propagado sem conversão para falha inesperada |

#### Retentativas técnicas

Não será habilitado retry automático e cego de comandos mutáveis:

- criação HTTP pode ser refeita conscientemente e a unicidade do código impede duplicidade lógica;
- baixa trata localmente somente concorrência otimista com reavaliação integral;
- falhas transitórias do consumer usam o mecanismo do SDD-07;
- Billing controla timeout e repetição limitada da consulta HTTP interna.

A política evita retries empilhados e repetição de mutações sem nova avaliação das regras.

### 7.6 Bloco 6 - Segurança, observabilidade, OpenAPI e LINQ

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Segurança HTTP

```text
POST /api/v1/products
    -> AdminOnly

GET /api/v1/products
GET /api/v1/products/{productId}
    -> AuthenticatedUser

GET /api/v1/internal/products/{productId}
    -> AdminOnly com bearer propagado
    -> não exposto pelo Gateway
```

Inventory valida localmente assinatura HS256, algoritmo, issuer, audience, validade, claims mínimas e papel. A API não presume que a passagem pelo Gateway seja suficiente.

`CreatedByUserId` vem de `sub`. Ausência, vazio ou UUID inválido impede a operação antes de persistência.

#### Segurança do consumer

O consumer RabbitMQ não usa token do usuário para autorizar a baixa. Confia nas credenciais próprias do serviço e da topologia, contrato reconhecido, envelope validado, Inbox, regras de domínio e transação local.

`requestedByUserId` transportado no evento representa correlação da intenção original; não é credencial nem fonte de autorização no Inventory.

#### Tratamento de texto

- descrição recebe somente a normalização aprovada;
- caracteres de controle são rejeitados;
- domínio não aplica sanitização HTML destrutiva;
- API nunca interpreta descrição como HTML;
- frontend apresenta texto pelo escape normal do Angular;
- erros não retornam SQL, constraints, hosts ou stack trace.

#### Logs

Criação bem-sucedida pode registrar:

```text
event = product_created
productId
productCode
createdByUserId
correlationId
duration
```

Baixa pode registrar:

```text
event = stock_deduction_completed|rejected|technical_failure
eventId
invoiceId
issuanceProcessId
productCount
attemptCount
reasonCode
correlationId
duration
```

Payload completo, bearer token, descrição do produto, connection string e detalhes internos do broker não são registrados. Diagnósticos técnicos ficam em logs internos correlacionados, sem serem devolvidos ao cliente.

#### Métricas

```text
products_created_total
stock_deductions_total{outcome="completed|rejected|technical_failure"}
stock_deduction_concurrency_conflicts_total
stock_deduction_duration_seconds
```

Produto, invoice, evento, usuário e código não são labels de métricas.

#### OpenAPI

```text
/openapi/v1.json
```

O documento descreve bearer, políticas, contratos, paginação, Problem Details, limites dos campos e exemplos não secretos. A rota interna é marcada como interna e pode aparecer no documento técnico em Development, mas permanece sem rota correspondente no Gateway.

#### LINQ justificável

```text
Paginação:
OrderBy -> ThenBy -> Skip -> Take -> Select

Carga da baixa:
Select(ProductId) -> OrderBy -> ToArray

Detecção de ausentes:
requestedIds.Except(loadedIds)

Saldo insuficiente:
Where -> Select

Comparação idempotente:
OrderBy(ProductId) -> SequenceEqual
```

LINQ serve a consulta, transformação e comparação. Alterações de estado usam laços explícitos e não escondem efeitos colaterais em pipelines LINQ. Materialização precoce sem necessidade não será usada apenas para demonstrar sintaxe.

### 7.7 Bloco 7 - Critérios de aceite, testes, riscos e marcadores

> Estado: Aprovado pelo engenheiro em 2026-08-18

---

## 8. Critérios de aceite

### CA-INV-01 - Propriedade do estoque

**Dado** o conjunto de serviços e bancos,  
**quando** dependências e modelos forem inspecionados,  
**então** somente Inventory persistirá Product, Balance e StockMovement.

### CA-INV-02 - Criação normalizada

**Dado** um cadastro válido,  
**quando** Product for criado,  
**então** código e descrição serão normalizados, autoria e timestamps serão consistentes e o response refletirá o estado persistido.

### CA-INV-03 - Saldo inicial

**Dado** o saldo inicial informado,  
**quando** o produto for criado,  
**então** zero será aceito, negativo será rejeitado e nenhuma movimentação fictícia será criada.

### CA-INV-04 - Código único

**Dado** dois códigos equivalentes após normalização,  
**quando** cadastros sequenciais ou concorrentes forem tentados,  
**então** somente um produto será persistido e o outro receberá conflito estável.

### CA-INV-05 - Consultas públicas

**Dado** o catálogo persistido,  
**quando** consultas públicas forem executadas,  
**então** campos, ordenação, paginação e erros obedecerão ao SDD-03 e a este documento.

### CA-INV-06 - Snapshot interno

**Dado** a consulta interna autenticada,  
**quando** um produto for encontrado,  
**então** somente ID, código e descrição serão retornados e a rota não estará exposta pelo Gateway.

### CA-INV-07 - Baixa atômica

**Dado** uma solicitação válida com múltiplos produtos,  
**quando** a baixa for confirmada,  
**então** todos os saldos e movimentos serão persistidos na mesma transação.

### CA-INV-08 - Produto ausente

**Dado** uma solicitação contendo produto inexistente,  
**quando** for processada,  
**então** será rejeitada com código funcional correspondente e nenhum saldo ou movimento será alterado.

### CA-INV-09 - Saldo insuficiente

**Dado** uma solicitação com qualquer saldo insuficiente,  
**quando** for processada,  
**então** todos os produtos insuficientes poderão ser informados e nenhuma baixa parcial será aplicada.

### CA-INV-10 - Movimento consistente

**Dado** uma baixa confirmada,  
**quando** seus movimentos forem inspecionados,  
**então** quantidade, saldos anterior e posterior, invoice, evento, tipo e instante serão consistentes e imutáveis.

### CA-INV-11 - Disputa da última unidade

**Dado** saldo um e duas notas concorrentes solicitando uma unidade,  
**quando** ambas forem processadas,  
**então** uma concluirá, outra será rejeitada após reavaliação e o saldo final será zero.

### CA-INV-12 - Repetição sem novo efeito

**Dado** uma baixa técnica ou logicamente repetida,  
**quando** o conteúdo equivaler ao já confirmado,  
**então** Inventory retornará conclusão sem reduzir novamente o saldo ou criar movimentos.

### CA-INV-13 - Duplicidade divergente

**Dado** uma invoice já movimentada,  
**quando** outro conteúdo for apresentado para ela,  
**então** nenhuma nova baixa será executada e a entrada será classificada como inconsistência técnica.

### CA-INV-14 - Reavaliação concorrente

**Dado** um conflito de `xmin`,  
**quando** nova tentativa for segura,  
**então** contexto e transação anteriores serão descartados, o estado será relido e não haverá retry cego.

### CA-INV-15 - Falhas classificadas

**Dado** erro HTTP, rejeição funcional, conflito persistente ou indisponibilidade,  
**quando** o resultado for produzido,  
**então** a classificação corresponderá à causa sem exposição de infraestrutura.

### CA-INV-16 - Autorização e autoria

**Dado** qualquer rota de Inventory,  
**quando** for acessada,  
**então** a política aprovada será aplicada e a autoria de criação virá exclusivamente de `sub` válido.

### CA-INV-17 - Observabilidade segura

**Dado** criação, conclusão, rejeição ou falha técnica,  
**quando** logs e métricas forem inspecionados,  
**então** haverá correlação suficiente sem payload, segredo ou label de alta cardinalidade.

### CA-INV-18 - OpenAPI fiel

**Dado** o documento OpenAPI de Inventory,  
**quando** comparado aos endpoints,  
**então** contratos, políticas, erros e natureza interna da rota de snapshot estarão descritos corretamente.

### CA-INV-19 - Camadas independentes

**Dado** Domain e Application,  
**quando** referências e tipos públicos forem analisados,  
**então** não existirão dependências de EF Core, ASP.NET Core ou RabbitMQ nem exposição de `IQueryable`.

---

## 9. Estratégia de testes planejada

| ID | Teste planejado | Nível | Critérios |
|---|---|---|---|
| TST-INV-001 | Validar dependências e propriedade do Inventory | Arquitetura | CA-INV-01, CA-INV-19 |
| TST-INV-002 | Criar Product válido pela factory | Unitário | CA-INV-02 |
| TST-INV-003 | Validar normalização e formatos de código | Unitário | CA-INV-02, CA-INV-04 |
| TST-INV-004 | Validar descrição, caracteres de controle e limites | Unitário | CA-INV-02 |
| TST-INV-005 | Aceitar saldo zero e rejeitar saldo negativo | Unitário | CA-INV-03 |
| TST-INV-006 | Criar produto pela API e verificar autoria | Integração | CA-INV-02, CA-INV-16 |
| TST-INV-007 | Disputar criação do mesmo código normalizado | Integração | CA-INV-04 |
| TST-INV-008 | Confirmar ausência de movimento no saldo inicial | Integração | CA-INV-03 |
| TST-INV-009 | Consultar produto existente, inexistente e ID inválido | Integração | CA-INV-05 |
| TST-INV-010 | Paginar, ordenar e consultar página além do final | Integração | CA-INV-05 |
| TST-INV-011 | Validar snapshot interno e ausência de saldo | Integração | CA-INV-06 |
| TST-INV-012 | Provar que `/internal/*` não é exposto pelo Gateway | Integração/arquitetura | CA-INV-06 |
| TST-INV-013 | Baixar um produto e validar movimento | Unitário/integração | CA-INV-07, CA-INV-10 |
| TST-INV-014 | Baixar múltiplos produtos atomicamente | Integração | CA-INV-07 |
| TST-INV-015 | Rejeitar produto inexistente sem efeito parcial | Integração | CA-INV-08 |
| TST-INV-016 | Rejeitar saldo insuficiente sem efeito parcial | Integração | CA-INV-09 |
| TST-INV-017 | Disputar a última unidade em dois contextos reais | Integração | CA-INV-11, CA-INV-14 |
| TST-INV-018 | Forçar conflito repetido até o limite técnico | Integração | CA-INV-14, CA-INV-15 |
| TST-INV-019 | Repetir baixa equivalente com o mesmo evento | Integração | CA-INV-12 |
| TST-INV-020 | Repetir intenção equivalente com outro evento | Integração | CA-INV-12 |
| TST-INV-021 | Enviar conteúdo divergente para invoice movimentada | Integração | CA-INV-13 |
| TST-INV-022 | Forçar constraints de saldo e movimento | Integração | CA-INV-10, CA-INV-15 |
| TST-INV-023 | Verificar consulta única em lote, sem N+1 | Integração | CA-INV-07, CA-INV-19 |
| TST-INV-024 | Diferenciar `401`, `403`, `404`, `409` e `503` | Integração | CA-INV-15, CA-INV-16 |
| TST-INV-025 | Inspecionar logs e métricas com valores sentinela | Segurança/integração | CA-INV-17 |
| TST-INV-026 | Comparar OpenAPI aos endpoints | Snapshot/arquitetura | CA-INV-18 |
| TST-INV-027 | Propagar cancelamento | Unitário/integração | CA-INV-15 |
| TST-INV-028 | Aplicar migrations em PostgreSQL vazio | Integração | CA-INV-01 |

PostgreSQL real é obrigatório para `xmin`, constraints, transações e índices. Testes unitários cobrem factories e decisões puras. Código relevante permanece sujeito ao gate mínimo de 80%.

---

## 10. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Baixa parcial de múltiplos produtos | Saldos e invoice inconsistentes | Uma transação e testes de rollback |
| Conflitos frequentes gerarem trabalho repetido | Latência e carga | Três tentativas locais e recuperação externa limitada |
| Retries em camadas multiplicarem tentativas | Pressão e efeitos inesperados | Responsabilidades separadas e ausência de retry automático do EF |
| Saldo inicial parecer compra real | Interpretação funcional incorreta | Limitação explícita e ausência de movimento fictício |
| Nova mensagem baixar novamente a invoice | Saldo duplicadamente reduzido | Inbox e unicidade por invoice/produto |
| Movimento parcial preexistente | Estado anterior inconsistente | Intervenção técnica, sem continuação automática |
| Consulta por item causar N+1 | Desempenho degradado | Carregamento em lote e teste de comandos |
| Rota interna ser publicada | Exposição indevida | Configuração explícita e teste negativo do Gateway |
| Abstrações se tornarem genéricas | Complexidade e acoplamento | Interfaces exclusivas do Inventory |
| Crescimento do histórico | Armazenamento crescente | Índices aprovados e retenção futura documentada |

---

## 11. Marcadores de qualidade

| Marcador | Exigência neste SDD |
|---|---|
| ESP | Sete blocos aprovados antes do código funcional |
| RAS | Requisitos do desafio ligados a critérios e testes |
| ARC | Inventory mantém propriedade exclusiva e camadas isoladas |
| DOM | Product protege código, descrição e saldo |
| ERR | Rejeição funcional não é confundida com falha técnica |
| SEG | JWT, autoria, rota interna e dados de log protegidos |
| TST | Regras puras e integrações reais cobertas em pelo menos 80% |
| INT | PostgreSQL real comprova concorrência e atomicidade |
| OBS | Criação e baixa possuem correlação suficiente |
| DOC | OpenAPI, LINQ e limites do saldo inicial documentados |
| QA | Concorrência, duplicidade, rollback e ausência de N+1 verificados |

---

## 12. Limites para a futura implementação

Uma implementação deste SDD poderá criar:

- agregado Product, operação de baixa e StockMovement;
- casos de uso, endpoints e projeções aprovados;
- portas específicas e implementações EF Core do Inventory;
- `InventoryDbContext`, configurações e migrations;
- tratamento local de concorrência e falhas HTTP;
- autenticação, políticas, OpenAPI, logs e métricas do Inventory;
- testes descritos neste documento.

Não poderá criar antecipadamente:

- modelos de Billing ou acesso ao banco de invoices;
- publishers, consumers ou topologia RabbitMQ completos;
- política física de retry e DLQ;
- fornecedor, compra, entrada ou ajuste manual de saldo;
- endpoints de edição ou exclusão de produto;
- transação distribuída;
- telas Angular ou composição final do ambiente.

Inbox, Outbox e o adapter RabbitMQ somente serão integrados ao mesmo limite transacional depois da especificação do SDD-07.

---

## 13. Condição para Gate A

O SDD-05 estará apto ao Gate A quando:

- os sete blocos estiverem aprovados;
- não houver contradição material com SDD-02, SDD-03, SDD-04 e ADRs vigentes;
- cada critério possuir ao menos um teste planejado;
- matriz de rastreabilidade estiver atualizada;
- baixa, concorrência e idempotência lógica forem implementáveis sem decisão implícita;
- limites entre Inventory e SDD-07 estiverem explícitos;
- nenhuma operação de suprimentos fora do desafio tiver sido inventada.

A aprovação estabiliza o comportamento do Inventory, mas não autoriza implementação antes da baseline documental conjunta.
