# Plano de Implementação - SDD-03

> Gate: B - Plano de implementação
> Status: Aprovado pelo engenheiro
> Data: 2026-08-20
> SDD: `SDD-03-CONTRATOS-HTTP-E-EVENTOS.md`
> Dependência validada: SDD-02
> Aprovação do Gate B: 2026-08-20

---

## 1. Objetivo da atividade

Materializar os contratos HTTP e de eventos já aprovados, sem antecipar endpoints, casos de uso, autenticação funcional, configuração do Gateway, publishers, consumers ou topologia RabbitMQ.

A entrega cria tipos estáveis, serialização verificável, fixtures e utilitários puros de fronteira que poderão ser consumidos pelos SDDs 04 a 08. As provas que dependem de comportamento executável permanecem explicitamente vinculadas aos SDDs proprietários.

## 2. Auditoria do estado atual

- `Korp.Shared.Contracts` existe e não contém tipos;
- os três projetos API contêm apenas o bootstrap OpenAPI do SDD-01;
- não existem DTOs HTTP, fixtures de eventos, parsers de headers ou políticas JSON explícitas;
- não existem endpoints funcionais, Gateway configurado ou RabbitMQ;
- os projetos de integração já referenciam suas APIs proprietárias;
- Inventory e Billing Infrastructure já referenciam `Korp.Shared.Contracts`, conforme a allowlist arquitetural;
- o commit `066cd54` contém a baseline implementada do SDD-02;
- o worktree estava limpo no início desta análise.

Não foi encontrada implementação legada conflitante. A ausência de endpoints é deliberada e deve ser preservada nesta fase.

## 3. Decisão de distribuição dos contratos

### 3.1 Contratos HTTP

DTOs HTTP ficam no projeto `Api` do serviço proprietário, organizados por feature. Eles não serão colocados em `Korp.Shared.Contracts`, pois Angular é um cliente externo e serviços não devem compartilhar seus modelos HTTP internos.

```text
Identity.Api   -> contratos de login
Inventory.Api  -> contratos públicos e internos de Product
Billing.Api    -> contratos de Invoice, InvoiceItem e processo de emissão
```

Responses usarão records dedicados, propriedades com tipos explícitos e nomes C# em PascalCase. A política JSON do host produzirá `camelCase`, omitirá nulos e serializará enums canônicos como strings `snake_case`. Entidades de domínio não serão expostas.

### 3.2 Contratos de eventos

Somente os quatro contratos assíncronos versionados ficam em `Korp.Shared.Contracts`, porque são a linguagem de integração entre Billing e Inventory:

- `StockDeductionRequestedV1`;
- `StockDeductionCompletedV1`;
- `StockDeductionRejectedV1`;
- `StockDeductionProcessingFailedV1`.

O envelope será genérico e não conhecerá RabbitMQ. Constantes canônicas impedirão strings mágicas para tipo, produtor e códigos de rejeição. Nenhuma entidade, `DbContext` ou lógica interna de domínio será compartilhada.

### 3.3 Tipos de fronteira

Parsing de `X-Correlation-ID`, `Idempotency-Key`, `If-Match` e paginação será implementado como lógica pura na API proprietária ou em primitivas locais da API. Esses tipos retornam resultado explícito e não acessam banco, usuário ou infraestrutura.

`ETag` permanecerá opaco no HTTP. A conversão entre token e versão persistente ficará preparada, mas sua aplicação em mutações pertence ao SDD-06.

## 4. Arquivos previstos

Os nomes abaixo podem sofrer somente ajustes mecânicos de namespace durante a implementação, sem alterar responsabilidades.

### 4.1 Shared Contracts

```text
src/Shared/Korp.Shared.Contracts/
|- Events/IntegrationEventEnvelope.cs
|- Events/IntegrationEventTypes.cs
|- Events/IntegrationEventProducers.cs
|- StockDeduction/V1/StockDeductionRequestedV1.cs
|- StockDeduction/V1/StockDeductionRequestItemV1.cs
|- StockDeduction/V1/StockDeductionCompletedV1.cs
|- StockDeduction/V1/StockDeductionRejectedV1.cs
|- StockDeduction/V1/StockDeductionFailureV1.cs
|- StockDeduction/V1/StockDeductionProcessingFailedV1.cs
|- StockDeduction/V1/StockDeductionReasonCodes.cs
```

### 4.2 Identity API

```text
src/Services/Identity/Korp.Identity.Api/Features/Auth/Contracts/
|- LoginRequest.cs
|- LoginResponse.cs
|- AuthenticatedUserResponse.cs
```

### 4.3 Inventory API

```text
src/Services/Inventory/Korp.Inventory.Api/Features/Products/Contracts/
|- CreateProductRequest.cs
|- ProductResponse.cs
|- InternalProductResponse.cs
|- ProductPageResponse.cs
```

### 4.4 Billing API

```text
src/Services/Billing/Korp.Billing.Api/Features/Invoices/Contracts/
|- AddInvoiceItemRequest.cs
|- UpdateInvoiceItemRequest.cs
|- InvoiceItemResponse.cs
|- InvoiceResponse.cs
|- InvoiceSummaryResponse.cs
|- InvoicePageResponse.cs

src/Services/Billing/Korp.Billing.Api/Features/Issuance/Contracts/
|- InvoiceIssuanceProcessResponse.cs
```

### 4.5 Convenções HTTP locais

```text
src/Services/Identity/Korp.Identity.Api/Http/ApiJsonOptions.cs
src/Services/Inventory/Korp.Inventory.Api/Http/ApiJsonOptions.cs
src/Services/Billing/Korp.Billing.Api/Http/ApiJsonOptions.cs
src/Services/Billing/Korp.Billing.Api/Http/EntityTag.cs
src/Services/Billing/Korp.Billing.Api/Http/IdempotencyKey.cs
```

A correlação e paginação comuns somente serão extraídas se houver uso real idêntico nas três APIs durante esta atividade. Não será criada uma biblioteca HTTP compartilhada especulativa.

### 4.6 Testes e fixtures

```text
tests/Shared/Korp.Shared.Contracts.UnitTests/
|- Korp.Shared.Contracts.UnitTests.csproj
|- Fixtures/stock-deduction-requested-v1.json
|- Fixtures/stock-deduction-completed-v1.json
|- Fixtures/stock-deduction-rejected-v1.json
|- Fixtures/stock-deduction-processing-failed-v1.json
|- Events/StockDeductionContractTests.cs

tests/Identity/Korp.Identity.IntegrationTests/Contracts/
|- IdentityHttpContractTests.cs

tests/Inventory/Korp.Inventory.IntegrationTests/Contracts/
|- ProductHttpContractTests.cs

tests/Billing/Korp.Billing.IntegrationTests/Contracts/
|- InvoiceHttpContractTests.cs
|- HttpPrimitiveTests.cs
```

O novo projeto de testes de contratos será adicionado a `Korp.Erp.sln`. Ele referencia somente `Korp.Shared.Contracts`.

## 5. Mapeamento critério → implementação → prova

| Critério | Entrega neste SDD | Prova nesta atividade | Situação após esta atividade |
|---|---|---|---|
| CA-CON-01 | Catálogo documental preservado; nenhuma rota interna mapeada no Gateway | Teste arquitetural negativo sobre bootstrap | Parcial; roteamento real no SDD-08 |
| CA-CON-02 | DTOs, JSON, paginação e primitivas de headers | Serialização e validações puras | Parcial; status e políticas nos SDDs 04–06 |
| CA-CON-03 | Request/response de login sem campos sensíveis | Inspeção e serialização contratual | Parcial; indistinguibilidade no SDD-04 |
| CA-CON-04 | Product público/interno e eventos sem alteração externa de saldo | Inspeção de campos e fixtures | Atendido no nível contratual |
| CA-CON-05 | Requests de item e ETag opaco | Testes dos contratos e parser | Parcial; mutação no SDD-06 |
| CA-CON-06 | Contratos de processo e evento inicial | Fixture e inspeção | Diferido funcionalmente ao SDD-06/07 |
| CA-CON-07 | Idempotency Key tipada | Parsing e igualdade | Diferido funcionalmente ao SDD-06 |
| CA-CON-08 | Idempotency Key tipada | Parsing e igualdade | Diferido funcionalmente ao SDD-06 |
| CA-CON-09 | DTO e enum público de todos os estados, opcionais omitidos | Serialização por estado | Atendido no nível contratual; polling no SDD-06/09 |
| CA-CON-10 | Envelope e quatro mensagens V1 | Round-trip das quatro fixtures | Atendido no nível contratual |
| CA-CON-11 | Identidade estável no envelope | Round-trip preserva `messageId` | Diferido funcionalmente ao SDD-07 |
| CA-CON-12 | Identidade e corpo serializável | Fixture/hash de referência | Diferido funcionalmente ao SDD-07 |
| CA-CON-13 | Request com coleção tipada e falhas seguras | Fixtures e inspeção de payload | Diferido funcionalmente ao SDD-05/07 |
| CA-CON-14 | `correlationId` e `causationId` explícitos | Fixtures verificam propagação estrutural | Diferido ponta a ponta ao SDD-07/08 |
| CA-CON-15 | Versão explícita e desserialização tolerante a campo opcional desconhecido | Teste contratual | Atendido para V1 |
| CA-CON-16 | Tipos que alimentarão schemas OpenAPI | Inspeção dos DTOs | Diferido ao mapeamento de endpoints nos SDDs 04–06 |

Não será declarado Gate C integral do SDD-03 enquanto os critérios funcionalmente diferidos não possuírem as provas dos SDDs proprietários. Esta atividade entrega e valida a baseline contratual necessária para iniciar esses SDDs.

## 6. Testes previstos agora

- serialização `camelCase`, UTC, UUID e omissão de nulos;
- round-trip exato das quatro fixtures V1;
- propriedades JSON opcionais desconhecidas não quebram consumidores;
- nomes canônicos de tipo, produtor, status e reason code;
- presença e ausência corretas dos campos opcionais de rejeição e processo;
- contratos públicos de Product contêm saldo, contrato interno não contém;
- requests não aceitam IDs, snapshots, timestamps ou saldo atual indevidos;
- InvoiceResponse não expõe versão no JSON;
- parser rejeita UUID vazio ou não canônico para idempotência;
- parser de ETag diferencia ausência, formato inválido e token válido;
- paginação aplica página inicial 1, padrão 20 e máximo 100;
- arquitetura preserva Shared Contracts sem dependência sobre serviços;
- build Release e regressão dos 50 testes existentes.

Testes de status HTTP, políticas, persistência do comando, OpenAPI final, redelivery, atomicidade e RabbitMQ não serão simulados com mocks apenas para marcar critérios como atendidos.

## 7. Dependências

Não há nova biblioteca de produção prevista. Serão usados `System.Text.Json`, ASP.NET Core e xUnit já disponíveis.

O novo projeto de teste reutiliza as versões centralizadas de:

- `Microsoft.NET.Test.Sdk`;
- `xunit.v3`;
- `xunit.runner.visualstudio`;
- `coverlet.collector`.

## 8. Ordem de implementação

1. estabilizar os contratos e fixtures de eventos em `Korp.Shared.Contracts`;
2. criar e executar os testes contratuais dos eventos;
3. criar DTOs HTTP de Identity, Inventory e Billing;
4. adicionar política JSON e primitivas puras aprovadas;
5. criar testes de forma, serialização e parsing por API;
6. ampliar os testes arquiteturais para proteger as fronteiras;
7. executar build e regressão Docker-first;
8. coletar cobertura dos assemblies manuais aplicáveis;
9. atualizar matriz, índice e relatório parcial do Gate C contratual.

## 9. Riscos e contenções

| Risco | Contenção |
|---|---|
| Implementar endpoints sem casos de uso | Nenhum `MapGet`, `MapPost`, `MapPut` ou `MapDelete` funcional nesta fase |
| Transformar Shared Contracts em biblioteca genérica | Somente os quatro eventos e suas constantes entram no projeto compartilhado |
| Acoplar DTO HTTP ao domínio | DTOs ficam na API e não referenciam entidades |
| Marcar prova funcional como concluída por teste estrutural | Matriz distingue atendimento contratual de prova diferida |
| Duplicar abstrações prematuramente | Utilitários permanecem locais até existir repetição real comprovada |
| OpenAPI vazio parecer contrato entregue | CA-CON-16 continua diferido até os endpoints dos SDDs 04–06 |
| Alterar migrations do SDD-02 | Nenhuma entidade ou configuração EF será modificada |
| Cobertura das APIs bootstrap cair artificialmente | Gate considera somente código manual introduzido e aplicável nesta atividade |

## 10. Validações do Gate contratual

- solution Release compila sem erro e sem novo warning;
- todos os testes existentes permanecem aprovados;
- fixtures são compatíveis com os exemplos aprovados do SDD;
- contratos compartilhados não referenciam serviços ou infraestrutura;
- nenhuma entidade, migration, endpoint, publisher ou consumer é criado;
- cobertura mínima de 80% é aplicada ao código manual novo relevante;
- resultados e critérios diferidos são registrados sem pendência oculta.

## 11. Arquivos documentais afetados ao concluir

- `docs/RELATORIO-IMPLEMENTACAO-SDD-03.md`;
- `docs/MATRIZ-RASTREABILIDADE.md`;
- `docs/README.md`;
- `docs/SDD-03-CONTRATOS-HTTP-E-EVENTOS.md` somente se uma descoberta exigir mudança comportamental previamente aprovada.

## 12. Decisão solicitada ao engenheiro

Aprovar ou ajustar:

1. DTOs HTTP locais em cada projeto `Api`;
2. somente eventos em `Korp.Shared.Contracts`;
3. criação do projeto `Korp.Shared.Contracts.UnitTests`;
4. implementação apenas da baseline contratual agora;
5. manutenção explícita dos critérios funcionais como diferidos aos SDDs 04–08.

Após aprovação deste Gate B, a implementação seguirá exatamente a ordem da seção 8.
