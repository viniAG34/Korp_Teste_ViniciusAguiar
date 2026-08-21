# Auditoria Documental 001 - Preparação para os SDDs

> Status: Aprovada
> Aprovação: 2026-08-17
> Data: 2026-08-17
> Escopo: enunciado, Visão Geral, Convenções de Código e ADR-001 a ADR-016

---

## 1. Objetivo

Esta auditoria verifica se o planejamento fundamental pode orientar os SDDs sem depender de decisões dispersas na conversa. Foram avaliados:

- aderência ao desafio original;
- coerência entre os ADRs;
- atualização de decisões anteriores por decisões posteriores;
- propriedade de dados e limites de comunicação;
- consistência de nomes e referências;
- destino de requisitos obrigatórios, opcionais e diferenciais;
- decisões ainda necessárias antes da implementação.

A auditoria não aprova os SDDs e não autoriza código. Ela fornece a linha de base documental para escrevê-los.

---

## 2. Fontes e precedência

Foram utilizadas, nesta ordem:

1. decisões explícitas mais recentes do engenheiro;
2. ADRs aprovados;
3. Visão Geral;
4. Convenções de Código;
5. PDF do desafio.

O PDF permanece a fonte dos requisitos da avaliação. ADRs podem acrescentar diferenciais ou restringir interpretações ambíguas, mas não remover requisito obrigatório.

---

## 3. Resultado executivo

- todos os requisitos funcionais e técnicos do desafio possuem destino em pelo menos um SDD;
- concorrência e idempotência, originalmente opcionais, foram convertidas em compromissos do projeto;
- inteligência artificial permanece conscientemente excluída;
- autenticação, Gateway, confiabilidade distribuída, Docker-first, cobertura e OpenAPI estão separados como diferenciais ou decisões técnicas, sem serem apresentados como exigências do PDF;
- a impressão foi limitada ao comportamento realmente solicitado, sem PDF;
- não foi encontrada contradição de negócio que exija revogar um ADR aprovado;
- foram encontradas inconsistências documentais decorrentes de decisões posteriores, todas consolidadas nesta auditoria;
- duas decisões de segurança permanecem abertas e precisam ser fechadas nos SDDs correspondentes.

A matriz inicial contém:

- 22 requisitos obrigatórios normalizados;
- 11 requisitos de apresentação e detalhamento;
- 2 requisitos opcionais adotados;
- 1 requisito opcional conscientemente excluído;
- 10 diferenciais acrescentados;
- 8 gates internos de qualidade.

---

## 4. Inconsistências encontradas e tratamento

| ID | Inconsistência | Risco | Tratamento |
|---|---|---|---|
| AUD-001 | ADR-004 descrevia Gateway somente para Inventory e Billing após a inclusão de Identity | Arquitetura macro incompleta | Identity e login adicionados; RabbitMQ permanece exclusivo de Inventory/Billing |
| AUD-002 | ADR-004 ainda exemplificava rotas sem `/api/v1` | Contratos divergentes | Exemplos atualizados para o prefixo aprovado no ADR-010 |
| AUD-003 | ADR-006 usava nomes de projetos em português e não continha Identity | Estrutura incompatível com ADR-009 e ADR-013 | Estrutura convertida para `Identity`, `Inventory`, `Billing` e `Gateway` |
| AUD-004 | ADR-008 definia apenas dois bancos | Identity sem propriedade persistente formal | `identity_db` e credencial isolada adicionados |
| AUD-005 | ADR-009 descrevia cobertura global combinada | Média poderia contrariar o gate por assembly do ADR-014 | ADR-009 alinhado ao refinamento posterior |
| AUD-006 | ADR-010 omitia `401`, `403` e OpenAPI de Identity | Contratos de segurança incompletos | Status e rota OpenAPI adicionados |
| AUD-007 | ADR-007 citava dois formatos de impressão como exemplo de Strategy | Exemplo obsoleto após ADR-016 | Exemplo removido; Strategy continua condicional |
| AUD-008 | Convenções continham identificadores como `IProdutoRepository` | Violação da regra de nomes em inglês | Exemplos substituídos por identificadores canônicos em inglês |
| AUD-009 | Convenções ainda condicionavam Decorator à decisão sobre Mediator | MediatR já havia sido rejeitado inicialmente | Texto tornado independente de Mediator |
| AUD-010 | Visão Geral atribuía publicação e consumo RabbitMQ a “cada serviço”, incluindo Identity | Responsabilidade indevida para Identity | Regra restringida a Inventory e Billing |
| AUD-011 | OpenAPI consolidado omitia Identity | Documentação incompleta da API | Gateway passa a rotear três documentos separados |

Essas alterações apenas propagam decisões já aprovadas. Nenhuma cria comportamento novo.

---

## 5. Pontos analisados que não são contradições

### 5.1 Impressão depois do fechamento

`PrintInvoice` só é aceito enquanto a invoice está `Open`. A baixa e o fechamento fazem parte dessa mesma operação. O diálogo do navegador é aberto após `Completed`, como continuação da solicitação já aceita. Isso não autoriza outro comando nem reimpressão de uma invoice `Closed`.

### 5.2 Status da invoice e status do processo

`Open` e `Closed` são estados de negócio da invoice. `Pending`, `AwaitingStock`, `Completed`, `Rejected` e `ManualIntervention` pertencem ao processo técnico. A separação evita inventar estados fiscais e permite feedback distribuído.

### 5.3 RabbitMQ e bancos

RabbitMQ nunca consulta banco. Outbox e Inbox pertencem aos serviços e são persistidas por eles. O broker recebe e entrega mensagens entre aplicações.

### 5.4 Gateway e RabbitMQ

O Gateway interage com o usuário por HTTP e não publica nem consome mensagens. A mensageria existe somente entre Billing e Inventory.

### 5.5 Recuperação e DLQ

Serviço parado antes do consumo não gasta retry: a mensagem permanece na fila principal. DLQ trata falhas após entrega ou mensagens incompatíveis. Uma DLQ isolada não garante que Billing consiga persistir `ManualIntervention`; esse limite já está explícito no ADR-012.

### 5.6 Autenticação como diferencial

O PDF não exige usuários ou JWT. A funcionalidade foi adicionada por decisão explícita e está identificada como diferencial, sem substituir o fluxo principal.

---

## 6. Decisões ainda necessárias

### DEC-P01 - Autenticação da chamada Billing para Inventory

Ao adicionar um item, Billing consulta a rota interna de Inventory. Ainda precisa ser decidido como essa chamada será autenticada:

- propagação controlada do token do usuário;
- credencial própria de serviço;
- política interna baseada apenas na rede Docker.

A terceira alternativa é a mais simples, mas oferece defesa menor. A decisão afeta contratos, configuração JWT e testes e deve ser tomada no `SDD-03-CONTRATOS-HTTP-E-EVENTOS.md` ou no `SDD-04-IDENTITY-SERVICE.md` antes da implementação da rota.

### DEC-P02 - Armazenamento do access token no Angular

O ADR-013 deixou para o frontend a escolha entre memória, session storage ou outro mecanismo. A decisão afeta recarregamento da página, exposição a XSS e experiência de login. Deve ser fechada no `SDD-09-FRONTEND-ANGULAR.md` antes da implementação da sessão.

---

## 7. Decisões próprias dos SDDs, não bloqueios atuais

Os itens abaixo são esperados durante a especificação detalhada e não representam contradição:

- versão exata do Angular, Node e Angular Material;
- gerenciador de pacotes e lockfile do frontend;
- framework e organização dos testes;
- tempo de validade, clock skew e claims exatas do JWT;
- paginação, filtros e ordenação das listagens;
- schemas completos de requests, responses e mensagens;
- topologia e nomes físicos do RabbitMQ;
- retenção e limpeza de Outbox, Inbox e processos;
- health checks e critérios de prontidão;
- detalhes do CSS de impressão;
- ferramenta de análise estática e formato final do pipeline.

Essas decisões precisam aparecer no SDD adequado antes do código correspondente.

---

## 8. Escopo do PDF e orientação de entrega

Requisitos de produto, arquitetura, falha, banco, detalhamento técnico e vídeo foram rastreados. As instruções logísticas de nome do repositório, destinatário de e-mail e hospedagem dos links não fazem parte dos SDDs por decisão explícita do engenheiro.

Elas continuam visíveis no PDF original, mas não orientam a arquitetura nem a implementação.

---

## 9. Conclusão

A base documental está apta a iniciar os SDDs depois de:

1. revisão e aprovação desta auditoria;
2. aprovação da versão consolidada das Convenções de Código;
3. ciência das decisões DEC-P01 e DEC-P02, que serão fechadas nos SDDs indicados.

Não é necessário criar outro ADR antes do `SDD-01`, salvo se o engenheiro desejar antecipar uma das duas decisões pendentes.
