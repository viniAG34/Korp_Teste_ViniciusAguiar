# Plano de Implementação - SDD-05

> Gate: B - Plano de implementação
> Status: Aprovado pelo engenheiro
> Data: 2026-08-21
> SDD: `SDD-05-INVENTORY-SERVICE.md`
> Dependências: SDD-01 e SDD-02 validados; baselines dos SDDs 03 e 04 implementadas

---

## 1. Objetivo

Implementar o Inventory Service como proprietário exclusivo de Product, Balance e StockMovement. A entrega cobre cadastro, consulta individual, listagem paginada, snapshot interno e baixa atômica invocável pela Application, incluindo autorização JWT local, concorrência otimista e erros sanitizados.

RabbitMQ, consumer, Inbox/Outbox operacionais, retry de broker e topologia permanecem no SDD-07. A baixa será plenamente testável sem antecipar o transporte.

## 2. Auditoria da baseline

- baseline auditada no commit `cd389ca`, após conclusão do SDD-04;
- Domain já contém `Product`, `ProductCode`, `StockMovement` e invariantes principais;
- `Product.Description` ainda precisa rejeitar caracteres de controle conforme CA-INV-02/TST-INV-004;
- PostgreSQL, migration inicial, constraints, `xmin`, Inbox e Outbox estruturais já foram validados no SDD-02;
- DTOs HTTP de Product e política JSON foram estabilizados pelo SDD-03;
- Application não possui casos de uso ou portas;
- Infrastructure não possui repositórios, read service, unidade de trabalho ou composição;
- API expõe somente OpenAPI e ainda não possui autenticação, endpoints ou Problem Details;
- existem 13 testes unitários e 12 testes de integração do Inventory na baseline;
- não há publisher, consumer ou dependência RabbitMQ a preservar.

Não será criada nova migration, salvo descoberta de incompatibilidade entre o modelo aprovado e a migration existente.

## 3. Arquitetura

```text
HTTP -> Inventory.Api -> handlers da Application -> portas específicas
                                                -> Infrastructure/EF Core/PostgreSQL

SDD-07/RabbitMQ futuro -> DeductInvoiceStockHandler -> nova unidade por tentativa
```

### 3.1 Domain

- completar validação da descrição;
- preservar normalização de código e criação do produto;
- manter a mutação de saldo dentro de `Product`;
- produzir `StockMovement` somente após validação integral coordenada pela Application;
- não receber dependências de framework.

### 3.2 Application

Casos de uso:

```text
CreateProduct
GetProductById
ListProducts
GetProductSnapshot
DeductInvoiceStock
```

Portas específicas:

```text
IProductRepository
IProductReadService
IInventoryUnitOfWork
IInventoryUnitOfWorkFactory
```

Application define commands, queries, resultados discriminados, projeções próprias e exceções técnicas sanitizadas. Não expõe `IQueryable`, `DbContext`, HTTP ou RabbitMQ.

### 3.3 Infrastructure

- repositório de escrita e read service com `AsNoTracking` e projeções no banco;
- paginação determinística `Code -> Id`;
- unidade de trabalho com transação `ReadCommitted`;
- nova unidade/contexto para cada uma das três tentativas de concorrência;
- carregamento de Products em uma consulta ordenada;
- detecção de movimentos anteriores para idempotência lógica;
- tradução exclusiva da constraint `uq_products_code` para conflito conhecido;
- indisponibilidade conhecida identificada na cadeia de exceções;
- nenhuma migration ou retry mutável automático no startup.

### 3.4 API

Rotas:

```text
POST /api/v1/products                     AdminOnly
GET  /api/v1/products                     AuthenticatedUser
GET  /api/v1/products/{productId}         AuthenticatedUser
GET  /api/v1/internal/products/{productId} AdminOnly
```

A API implementará validação JWT independente, policies, extração segura de `sub`, endpoints por feature, Problem Details, logs estruturados, métricas nativas e OpenAPI. Não haverá endpoint para baixa, edição, exclusão ou ajuste de saldo.

## 4. Dependência solicitada

Adicionar `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 somente ao projeto `Korp.Inventory.Api`. É o handler oficial ASP.NET Core necessário para validar assinatura HS256, algoritmo, issuer, audience, lifetime e principal localmente. A versão acompanha os demais pacotes ASP.NET Core 10.0.11 já fixados.

`Microsoft.AspNetCore.Mvc.Testing` já está aprovado e centralizado; será apenas referenciado no projeto de integração do Inventory.

Nenhum pacote de validação, mediator, mock, métricas ou retry será adicionado.

## 5. Arquivos previstos

### Domain

```text
Products/Product.cs                         (validação ampliada)
Products/ProductErrors.cs                   (erro de controle, se necessário)
```

### Application

```text
Common/
|- IGuidGenerator.cs
|- InventoryServiceUnavailableException.cs
|- InventoryConsistencyException.cs

Products/Create/
|- CreateProductCommand.cs
|- CreateProductHandler.cs
|- CreateProductResult.cs

Products/GetById/
|- GetProductByIdQuery.cs
|- GetProductByIdHandler.cs

Products/List/
|- ListProductsQuery.cs
|- ListProductsHandler.cs
|- ProductPage.cs

Products/GetSnapshot/
|- GetProductSnapshotQuery.cs
|- GetProductSnapshotHandler.cs

Products/Common/
|- ProductDetails.cs
|- ProductSnapshot.cs
|- IProductRepository.cs
|- IProductReadService.cs

Stock/DeductInvoiceStock/
|- DeductInvoiceStockCommand.cs
|- DeductInvoiceStockItem.cs
|- DeductInvoiceStockHandler.cs
|- DeductionResult.cs
|- DeductionFailure.cs
|- IInventoryUnitOfWork.cs
|- IInventoryUnitOfWorkFactory.cs
```

Os nomes finais poderão ser compactados quando dois tipos representarem o mesmo conceito, sem criar arquivos artificiais apenas para reproduzir esta árvore.

### Infrastructure

```text
DependencyInjection.cs
Persistence/Repositories/ProductRepository.cs
Persistence/Queries/ProductReadService.cs
Persistence/UnitsOfWork/InventoryUnitOfWork.cs
Persistence/UnitsOfWork/InventoryUnitOfWorkFactory.cs
Persistence/DatabaseErrorClassifier.cs
```

### API

```text
Program.cs                                  (alterado)
Security/
|- JwtValidationOptions.cs
|- JwtValidationOptionsValidator.cs
|- AuthenticationExtensions.cs
|- AuthorizationPolicies.cs
|- CurrentUser.cs

Errors/
|- ApiProblemDetails.cs
|- InventoryExceptionHandler.cs

Features/Products/
|- CreateProductEndpoint.cs
|- GetProductByIdEndpoint.cs
|- ListProductsEndpoint.cs
|- GetInternalProductEndpoint.cs
|- ProductRequestValidator.cs
|- ProductResponseMapper.cs

Observability/InventoryMetrics.cs
```

### Testes

```text
tests/Inventory/Korp.Inventory.UnitTests/
|- Products/                         (ampliado)
|- Application/Products/
|- Application/Stock/

tests/Inventory/Korp.Inventory.IntegrationTests/
|- AssemblyInfo.cs
|- Authentication/
|- Products/
|- Stock/
|- Persistence/                      (ampliado)
|- OpenApi/

tests/Architecture/Korp.ArchitectureTests/
|- ProjectReferenceRulesTests.cs     (ampliado se necessário)
```

## 6. Critério → implementação → prova

| Critérios | Implementação | Provas |
|---|---|---|
| CA-INV-01, CA-INV-19 | camadas e portas específicas | TST-INV-001, TST-INV-023 |
| CA-INV-02 a CA-INV-04 | Domain, CreateProduct e índice único | TST-INV-002 a TST-INV-008 |
| CA-INV-05 | read service e endpoints públicos | TST-INV-009, TST-INV-010 |
| CA-INV-06 | projeção e endpoint interno | TST-INV-011; exposição pelo Gateway acumula no TST-INV-012/SDD-08 |
| CA-INV-07 a CA-INV-10 | handler de baixa, transação e movimentos | TST-INV-013 a TST-INV-016, TST-INV-022 |
| CA-INV-11, CA-INV-14 | `xmin`, nova UoW por tentativa e limite três | TST-INV-017, TST-INV-018 |
| CA-INV-12, CA-INV-13 | comparação de movimentos da invoice | TST-INV-019 a TST-INV-021 |
| CA-INV-15 | resultados funcionais e exception handler | TST-INV-024, TST-INV-027 |
| CA-INV-16 | JwtBearer, policies e `sub` | TST-INV-006, TST-INV-024 |
| CA-INV-17 | logs e `System.Diagnostics.Metrics` | TST-INV-025 |
| CA-INV-18 | metadata e documento gerado | TST-INV-026 |

## 7. Estratégia de testes

### Unitários

- descrição, código, saldo e movimento;
- handlers de criação e consultas;
- validação da baixa antes de qualquer mutação;
- produto ausente e todos os saldos insuficientes;
- idempotência equivalente e divergência técnica;
- repetição de concorrência com descarte de cada unidade;
- cancelamento e classificação de resultados.

### Integração PostgreSQL/API

- migration e constraints existentes;
- criação válida, saldo zero, autoria e ausência de movimento inicial;
- duplicidade sequencial e concorrente;
- consultas, ordenação, paginação e projeção interna;
- baixa unitária e múltipla em uma transação;
- rollback em ausência ou insuficiência;
- disputa real da última unidade;
- repetição lógica sem novo efeito;
- JWT válido, ausente, adulterado, sem role e sem claims mínimas;
- `400`, `401`, `403`, `404`, `409`, `503` e cancelamento;
- OpenAPI e inexistência das rotas excluídas;
- logs/respostas sem sentinelas sensíveis.

Os testes que limpam o mesmo `inventory_db` serão serializados somente no assembly de integração do Inventory.

## 8. Ordem de implementação

1. aprovar dependência e este plano;
2. completar invariantes e testes do Domain;
3. implementar tipos, portas e handlers da Application;
4. implementar read service, repositório e unidades de trabalho;
5. validar baixa, atomicidade, idempotência e concorrência em PostgreSQL;
6. implementar JWT, policies, Problem Details e endpoints;
7. validar HTTP, OpenAPI, logs e métricas;
8. executar regressão e cobertura Docker-first;
9. atualizar matriz, índice e relatório Gate C.

## 9. Riscos e contenções

| Risco | Contenção |
|---|---|
| Baixa parcial | validar tudo antes da mutação e confirmar uma transação |
| Entidade permanecer alterada após rejeição | descartar a UoW completa em qualquer tentativa não confirmada |
| Retry cego | somente `DbUpdateConcurrencyException`, com novo contexto e reavaliação |
| Duplicidade genérica virar `409` | conferir constraint conhecida do código |
| N+1 | carregamento único e teste por comandos emitidos |
| JWT divergir do Identity | mesmas chaves de configuração e algoritmo fixo HS256 |
| Rota interna parecer pública | metadata explícita e deferimento da prova de Gateway ao SDD-08 |
| Antecipar mensageria | nenhuma referência ou adapter RabbitMQ nesta fase |
| Código gerado reduzir cobertura artificialmente | exclusões somente conforme ADR-014, com valores brutos publicados |

## 10. Gate C previsto

- build Release com 0 erros e sem novo warning injustificado;
- 28 testes planejados com prova ou deferimento cumulativo explícito;
- PostgreSQL real comprova constraints, transação, `xmin` e idempotência lógica;
- autenticação e policies são validadas localmente;
- OpenAPI contém exatamente as quatro rotas aprovadas;
- nenhuma rota de baixa, edição, exclusão ou ajuste existe;
- line coverage manual aplicável mínima de 80% por assembly;
- branch coverage publicada;
- regressão integral permanece aprovada;
- documentação e matriz refletem implementação e limites reais.

## 11. Decisões solicitadas

Aprovar ou ajustar:

1. `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 somente na API;
2. baixa completa na Application/Infrastructure, porém sem endpoint ou consumer até o SDD-07;
3. três tentativas de concorrência com UoW/contexto novo, sem espera artificial;
4. métricas via API nativa `System.Diagnostics.Metrics`, sem pacote adicional;
5. TST-INV-012 como prova cumulativa concluída somente na implementação do Gateway.

Plano aprovado pelo engenheiro antes do início da implementação funcional.
