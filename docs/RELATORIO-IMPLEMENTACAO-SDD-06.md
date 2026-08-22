# Relatório de Implementação - SDD-06

> Gate: C - Conclusão
> Status: Aprovado pelo engenheiro
> Data: 2026-08-21
> Gate C aprovado em: 2026-08-21
> SDD: `SDD-06-BILLING-SERVICE.md`
> Plano: `PLANO-IMPLEMENTACAO-SDD-06.md`

---

## 1. Resultado

O Billing Service implementa criação, consulta e paginação de invoices, gestão de itens com snapshot autoritativo do Inventory, concorrência HTTP por ETag e aceite durável de `PrintInvoice`. Billing permanece proprietário exclusivo de Invoice, InvoiceItem e InvoiceIssuanceProcess.

O aceite de emissão confirma atomicamente Invoice bloqueada, processo Pending e mensagem `StockDeductionRequested` na Outbox. Nenhum publisher, consumer ou acesso direto ao banco do Inventory foi antecipado.

## 2. Funcionalidades entregues

- `POST /api/v1/invoices`, protegido por `AdminOnly` e com autoria por `sub`;
- `GET /api/v1/invoices`, protegido por `AuthenticatedUser`, paginado e ordenado;
- `GET /api/v1/invoices/{invoiceId}`, com itens ordenados e ETag;
- inclusão, alteração de quantidade e remoção de itens, protegidas por `If-Match`;
- snapshot de Product obtido pela rota interna do Inventory;
- bearer e `X-Correlation-ID` propagados somente na fronteira HTTP;
- timeout de três segundos por tentativa e uma repetição seletiva sem Polly;
- `PrintInvoice` com Idempotency-Key global, bloqueio e Outbox transacional;
- replay ativo em `202` e terminal em `200`, sem novo efeito;
- consulta de InvoiceIssuanceProcess com atraso e Retry-After derivados;
- transições internas para AwaitingStock, Completed, Rejected e ManualIntervention;
- JWT HS256 validado localmente com issuer, audience, lifetime, algoritmo e claims mínimas;
- Problem Details sanitizado, logs estruturados, correlação e métricas nativas;
- OpenAPI das oito rotas aprovadas;
- ausência de preço, tributo, cliente, pagamento, cancelamento, PDF e alteração de saldo.

## 3. Arquitetura

Application contém casos de uso e portas específicas, sem conhecer ASP.NET Core, EF Core ou RabbitMQ. Infrastructure implementa repositórios, projeções SQL, sequence, transações, Outbox e cliente HTTP. Domain protege estados, bloqueio, itens e transições. API concentra JWT, headers HTTP, DTOs, Problem Details e composição.

`PrintInvoice` não chama Inventory, não aguarda RabbitMQ e não fecha a invoice. O fechamento ocorre somente pelo caso interno `CompleteInvoiceIssuance`, que será conectado ao consumer no SDD-07.

Não foi criada nova migration. O único ajuste de mapeamento, `InvoiceItem.Id.ValueGeneratedNever`, explicita o comportamento já previsto: UUID é fornecido pela aplicação e não pelo banco.

## 4. Dependências

Foi referenciado `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 somente na API. `Microsoft.AspNetCore.Mvc.Testing` e `Microsoft.IdentityModel.JsonWebTokens` são usados somente na integração.

Nenhum pacote de retry, mediator, validação, mocks, métricas ou mensageria foi adicionado.

## 5. Critérios de aceite

| Critérios | Resultado |
|---|---|
| CA-BIL-01 a CA-BIL-04 | Atendidos por ownership, sequence, criação e projeções reais |
| CA-BIL-05 a CA-BIL-08 | Atendidos por snapshot validado, falhas classificadas e mutações de itens |
| CA-BIL-09 e CA-BIL-10 | Atendidos por invariantes, ETag Base64Url, `xmin` e conflitos HTTP |
| CA-BIL-11 a CA-BIL-14 | Atendidos por transação local, Outbox, replay e disputas concorrentes reais |
| CA-BIL-15 | Transição interna atendida; publisher confirm real é prova cumulativa do SDD-07 |
| CA-BIL-16 a CA-BIL-20 | Regras e coordenação interna atendidas; atomicidade com Inbox/consumer real acumula no SDD-07 |
| CA-BIL-21 e CA-BIL-22 | Atendidos por cálculo com relógio controlado e ausência de timeout mutável |
| CA-BIL-23 e CA-BIL-24 | Atendidos por JWT, policies, autoria, bearer e erros sanitizados |
| CA-BIL-25 | Logs HTTP, correlação e métricas atendidos; logs do transporte acumulam no SDD-07 |
| CA-BIL-26 | Rotas e contratos principais do OpenAPI verificados |
| CA-BIL-27 e CA-BIL-28 | Atendidos por referências de camadas e ausência do escopo fiscal excluído |

## 6. Testes e validações

### Billing

- unitários: 25 aprovados;
- integração, contratos, HTTP e PostgreSQL: 42 aprovados;
- total: 67 aprovados, 0 falhas e 0 ignorados.

Foram exercitados sequence, migrations, `xmin`, itens, estados, constraints, cliente HTTP, retry, JWT, ETag, idempotência sequencial e concorrente, chaves diferentes concorrentes, Outbox, consulta do processo, OpenAPI e escopo negativo.

### Regressão

A regressão integral final executada no Docker aprovou 151 testes, com 0 falhas e 0 ignorados: 67 do Billing e 84 dos demais projetos. O assembly de testes do Gateway ainda não possui casos descobertos, conforme o planejamento do SDD-08.

### Cobertura final

| Assembly | Linhas manuais aplicáveis | Branches publicadas |
|---|---:|---:|
| `Korp.Billing.Domain` | 96,86% | 89,74% na suíte unitária |
| `Korp.Billing.Application` | 89,86% | 64,22% na suíte unitária |
| `Korp.Billing.Infrastructure` | 80,44% | 54,00% na suíte de integração |
| `Korp.Billing.Api` | 85,60% | 44,56% na suíte de integração |

OpenAPI gerado, migrations, snapshot e factory de design time foram excluídos somente do cálculo manual aplicável, conforme ADR-014. Percentuais brutos da suíte de integração antes da última prova foram: API 68,78%, Application 73,55%, Domain 69,63% e Infrastructure 50,20%. A suíte unitária publicou Application 77,17% e Domain 93,71%. A consolidação por arquivo e linha produz os percentuais manuais da tabela.

Branch coverage é publicada sem gate percentual, conforme decisão aprovada.

## 7. Ocorrências técnicas

1. O EF Core interpretava UUID novo de InvoiceItem como chave gerada e marcava o dependente como `Modified`; `ValueGeneratedNever` tornou explícita a geração pela aplicação.
2. Registrar `AddDbContext` e `AddDbContextFactory` separadamente produzia lifetimes incompatíveis; um único factory singleton passou a criar o contexto escopado e unidades transacionais independentes.
3. Filtrar após projetar `PersistedIssuanceProcess` não era traduzível para SQL; filtros agora são aplicados às entidades antes da projeção.
4. Conflito de `xmin` em impressões concorrentes precisava ser reavaliado para distinguir ETag obsoleto de processo vencedor; a leitura posterior agora preserva replay e retorna `invoice_issuance_in_progress` quando aplicável.
5. O ETag anterior usava Base64 com padding; o codec foi alinhado ao Base64Url forte sem padding definido no SDD.

As correções não alteraram escopo nem introduziram retry cego.

## 8. Limitações e deferimentos

- publisher, publisher confirm, consumers, Inbox operacional, acknowledgment, retry, DLQ e reconciliador pertencem ao SDD-07;
- transições internas foram implementadas e testadas diretamente, mas sua ativação por RabbitMQ permanece no SDD-07;
- validação conjunta com Inventory real em containers será acumulada no fluxo distribuído do SDD-07; neste SDD, o adapter HTTP foi validado com servidor controlado e o contrato interno já é provado pelo Inventory;
- logs e métricas do transporte ainda não existem porque não há adapter de mensageria;
- composição executável completa dos serviços pertence ao SDD-11;
- nenhuma garantia exactly-once é declarada.

## 9. Avaliação do Gate C

Recomendação: **aprovar o SDD-06 com deferimentos cumulativos explícitos para o transporte do SDD-07 e a composição do SDD-11**.

O Billing atende ao escopo local aprovado, mantém isolamento de dados, possui concorrência e idempotência verificadas e está pronto para receber os adapters RabbitMQ sem mover regras de negócio para o transporte.

## 10. Próximo passo

Após aprovação e commit deste ponto-chave, iniciar o Gate B do SDD-07 - Emissão e Consistência Distribuída.
