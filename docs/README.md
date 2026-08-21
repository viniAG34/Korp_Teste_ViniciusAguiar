# Documentação do Projeto

> Índice, ordem de leitura e acompanhamento dos documentos do desafio técnico Korp ERP.
> Última atualização: 2026-08-20

---

## 1. Objetivo

Esta pasta concentra as especificações, decisões e evidências do projeto. O desenvolvimento segue Spec-Driven Development (SDD): regras e critérios de aceite são definidos e aprovados antes do código.

O enunciado original está em `../teste tecnico KORP ERP.pdf`.

---

## 2. Estados dos documentos

| Estado | Significado |
|---|---|
| Não iniciado | Documento previsto, ainda não criado |
| Rascunho | Conteúdo em elaboração, sem autorização para implementação |
| Em revisão | Conteúdo completo aguardando avaliação do engenheiro |
| Aprovado | Fonte da verdade autorizada para orientar as próximas etapas |
| Implementado | Escopo correspondente implementado, ainda pendente de validação final |
| Validado | Implementação e evidências verificadas conforme os critérios de aceite |
| Substituído | Documento preservado para histórico, mas sucedido por decisão mais recente |

---

## 3. Documentos fundamentais

| Documento | Responsabilidade | Estado | Dependências | Aprovação |
|---|---|---|---|---|
| [`../AGENTS.md`](../AGENTS.md) | Contrato operacional dos agentes de desenvolvimento | Aprovado | Nenhuma | 2026-08-16 |
| [`VISAO-GERAL.md`](VISAO-GERAL.md) | Contexto, escopo, arquitetura macro, requisitos e trade-offs | Aprovado | Enunciado e ADR-001 a ADR-016 | 2026-08-16 |
| [`CONVENCOES-CODIGO.md`](CONVENCOES-CODIGO.md) | Arquitetura interna, nomenclatura, erros, persistência, mensageria e testes | Aprovado | Visão geral e decisões de stack | 2026-08-17 |
| [`GLOSSARIO.md`](GLOSSARIO.md) | Vocabulário canônico de domínio, integração, segurança e qualidade | Aprovado | Visão geral e ADRs | 2026-08-17 |
| [`MATRIZ-RASTREABILIDADE.md`](MATRIZ-RASTREABILIDADE.md) | Ligação entre requisitos, regras, critérios, implementação, testes e evidências | Aprovado | Visão geral e SDD-01 a SDD-13 | 2026-08-20 |
| [`AUDITORIA-DOCUMENTAL-001.md`](AUDITORIA-DOCUMENTAL-001.md) | Consistência dos ADRs, cobertura do PDF e decisões pendentes | Aprovado | Todos os documentos fundamentais e ADRs | 2026-08-17 |
| [`AUDITORIA-DOCUMENTAL-002.md`](AUDITORIA-DOCUMENTAL-002.md) | Auditoria cruzada dos 13 SDDs e baseline de implementação | Aprovado | PDF, documentos fundamentais, ADRs e SDD-01 a SDD-13 | 2026-08-20 |
| [`RELATORIO-IMPLEMENTACAO-SDD-01.md`](RELATORIO-IMPLEMENTACAO-SDD-01.md) | Evidências, ocorrências, limitações e avaliação do Gate C do setup | Aprovado | SDD-01 e plano de implementação | 2026-08-17 |
| [`PLANO-IMPLEMENTACAO-SDD-02.md`](PLANO-IMPLEMENTACAO-SDD-02.md) | Gate B da modelagem de domínio e persistência | Aprovado | Baseline aprovada e SDD-02 | 2026-08-20 |
| [`RELATORIO-IMPLEMENTACAO-SDD-02.md`](RELATORIO-IMPLEMENTACAO-SDD-02.md) | Evidências e aprovação do Gate C da modelagem e persistência | Aprovado | SDD-02 e plano de implementação | 2026-08-20 |
| [`PLANO-IMPLEMENTACAO-SDD-03.md`](PLANO-IMPLEMENTACAO-SDD-03.md) | Gate B dos contratos HTTP e eventos | Aprovado | SDD-01, SDD-02 e SDD-03 | 2026-08-20 |
| [`RELATORIO-IMPLEMENTACAO-SDD-03.md`](RELATORIO-IMPLEMENTACAO-SDD-03.md) | Evidências da baseline contratual e critérios funcionalmente diferidos | Aprovado | SDD-03 e plano de implementação | 2026-08-20 |
| [`PLANO-IMPLEMENTACAO-SDD-04.md`](PLANO-IMPLEMENTACAO-SDD-04.md) | Gate B do Identity Service | Aprovado | SDD-01 a SDD-04 e ADRs aplicáveis | 2026-08-21 |
| [`RELATORIO-IMPLEMENTACAO-SDD-04.md`](RELATORIO-IMPLEMENTACAO-SDD-04.md) | Login, lockout, JWT, segurança e evidências do Identity | Aprovado | SDD-04 e plano de implementação | 2026-08-21 |
| [`PLANO-IMPLEMENTACAO-SDD-05.md`](PLANO-IMPLEMENTACAO-SDD-05.md) | Gate B do Inventory Service | Aprovado | SDD-01 a SDD-05 e ADRs aplicáveis | 2026-08-21 |
| [`RELATORIO-IMPLEMENTACAO-SDD-05.md`](RELATORIO-IMPLEMENTACAO-SDD-05.md) | Produtos, estoque, concorrência, segurança e evidências | Aprovado | SDD-05 e plano de implementação | 2026-08-21 |
| [`PLANO-IMPLEMENTACAO-SDD-06.md`](PLANO-IMPLEMENTACAO-SDD-06.md) | Gate B do Billing Service | Aprovado | SDD-01 a SDD-06 e ADRs aplicáveis | 2026-08-21 |

---

## 4. Decisões arquiteturais e de escopo

| Documento | Decisão | Estado | Data |
|---|---|---|---|
| [`ADR-001-ESCOPO-NOTA-FISCAL-SIMPLIFICADA.md`](ADR-001-ESCOPO-NOTA-FISCAL-SIMPLIFICADA.md) | Implementar somente a feature de emissão simplificada de saída de estoque | Aprovado | 2026-08-16 |
| [`ADR-002-BACKEND-E-REQUISITOS-OPCIONAIS.md`](ADR-002-BACKEND-E-REQUISITOS-OPCIONAIS.md) | Backend C#/.NET; concorrência e idempotência obrigatórias no projeto; IA excluída | Aprovado | 2026-08-16 |
| [`ADR-003-IMPRESSAO-SOMENTE-NOTA-ABERTA.md`](ADR-003-IMPRESSAO-SOMENTE-NOTA-ABERTA.md) | Permitir impressão somente para nota aberta e proibir reimpressão | Aprovado | 2026-08-16 |
| [`ADR-004-FRONTEIRAS-GATEWAY-E-MENSAGERIA.md`](ADR-004-FRONTEIRAS-GATEWAY-E-MENSAGERIA.md) | Gateway roteia HTTP para três serviços; RabbitMQ integra somente Inventory e Billing | Aprovado | 2026-08-16 |
| [`ADR-005-VERSAO-DOTNET-E-LEGADO.md`](ADR-005-VERSAO-DOTNET-E-LEGADO.md) | Adotar .NET 10 LTS e demonstrar conhecimento de legado sem usar runtime fora de suporte | Aprovado | 2026-08-16 |
| [`ADR-006-CLEAN-ARCHITECTURE-POR-SERVICO.md`](ADR-006-CLEAN-ARCHITECTURE-POR-SERVICO.md) | Quatro camadas por serviço, Gateway simples e contratos de integração isolados | Aprovado | 2026-08-16 |
| [`ADR-007-PADROES-DE-PROJETO.md`](ADR-007-PADROES-DE-PROJETO.md) | Catálogo de padrões aprovados, condicionais e rejeitados inicialmente | Aprovado | 2026-08-16 |
| [`ADR-008-PERSISTENCIA-E-CONCORRENCIA.md`](ADR-008-PERSISTENCIA-E-CONCORRENCIA.md) | PostgreSQL, EF Core 10, migrations controladas, transações locais e concorrência via `xmin` | Aprovado | 2026-08-16 |
| [`ADR-009-IDIOMA-DOCKER-E-COBERTURA.md`](ADR-009-IDIOMA-DOCKER-E-COBERTURA.md) | Código em inglês, desenvolvimento Docker-first e cobertura mínima de 80% | Aprovado | 2026-08-16 |
| [`ADR-010-APIS-ERROS-E-BIBLIOTECAS-BACKEND.md`](ADR-010-APIS-ERROS-E-BIBLIOTECAS-BACKEND.md) | Minimal APIs, ProblemDetails, RabbitMQ.Client, OpenAPI e contratos de erro | Aprovado | 2026-08-16 |
| [`ADR-011-REGRAS-DE-DOMINIO.md`](ADR-011-REGRAS-DE-DOMINIO.md) | Regras de Product, StockMovement, Invoice, InvoiceItem e PrintInvoice | Aprovado | 2026-08-16 |
| [`ADR-012-ESTADOS-RECUPERACAO-E-ACOMPANHAMENTO-DA-EMISSAO.md`](ADR-012-ESTADOS-RECUPERACAO-E-ACOMPANHAMENTO-DA-EMISSAO.md) | Estados técnicos, recuperação, retry, DLQ e acompanhamento da emissão | Aprovado | 2026-08-16 |
| [`ADR-013-AUTENTICACAO-E-SERVICO-DE-IDENTIDADE.md`](ADR-013-AUTENTICACAO-E-SERVICO-DE-IDENTIDADE.md) | Serviço de Identidade, JWT e autorização com defesa em profundidade | Aprovado | 2026-08-17 |
| [`ADR-014-COBERTURA-DE-TESTES-E-EVIDENCIAS.md`](ADR-014-COBERTURA-DE-TESTES-E-EVIDENCIAS.md) | Coverlet, gate de 80% por assembly e relatórios auxiliares | Aprovado | 2026-08-17 |
| [`ADR-015-BIBLIOTECA-VISUAL-ANGULAR.md`](ADR-015-BIBLIOTECA-VISUAL-ANGULAR.md) | Angular Material, tema personalizado, SCSS e biblioteca visual única | Aprovado | 2026-08-17 |
| [`ADR-016-IMPRESSAO-VIA-NAVEGADOR.md`](ADR-016-IMPRESSAO-VIA-NAVEGADOR.md) | HTML imprimível e diálogo nativo, sem geração de PDF | Aprovado | 2026-08-17 |

Novas decisões que alterem arquitetura, limites de domínio ou escopo devem ser registradas em um ADR antes de modificar os SDDs ou o código.

---

## 5. SDDs planejados

A lista abaixo é uma proposta inicial. Títulos, divisão e dependências podem ser ajustados durante a elaboração da visão geral, antes da aprovação do primeiro SDD.

| Ordem | Documento | Responsabilidade prevista | Estado | Dependências | Aprovação |
|---|---|---|---|---|---|
| 01 | [`SDD-01-SETUP-E-ARQUITETURA.md`](SDD-01-SETUP-E-ARQUITETURA.md) | Estrutura da solução, serviços, projetos e dependências | Validado | Visão geral, convenções | 2026-08-17 |
| 02 | [`SDD-02-MODELAGEM-DE-DADOS.md`](SDD-02-MODELAGEM-DE-DADOS.md) | Entidades, invariantes, bancos, índices e auditoria | Validado | SDD-01 | 2026-08-20 |
| 03 | [`SDD-03-CONTRATOS-HTTP-E-EVENTOS.md`](SDD-03-CONTRATOS-HTTP-E-EVENTOS.md) | APIs e contratos de integração | Implementado | SDD-01, SDD-02 | Gate C contratual em revisão |
| 04 | [`SDD-04-IDENTITY-SERVICE.md`](SDD-04-IDENTITY-SERVICE.md) | Usuários, login, JWT e políticas de autorização | Validado | SDD-02, SDD-03 | Gate C aprovado em 2026-08-21 |
| 05 | [`SDD-05-INVENTORY-SERVICE.md`](SDD-05-INVENTORY-SERVICE.md) | Produtos, saldos e movimentações | Validado | SDD-02, SDD-03, SDD-04 | Gate C aprovado em 2026-08-21 |
| 06 | [`SDD-06-BILLING-SERVICE.md`](SDD-06-BILLING-SERVICE.md) | Notas, itens, estados e impressão | Aprovado | SDD-02, SDD-03, SDD-04, SDD-05 | Gate A aprovado |
| 07 | [`SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md`](SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md) | Emissão, baixa, falhas, idempotência e concorrência | Aprovado | SDD-05, SDD-06 | Gate A aprovado |
| 08 | [`SDD-08-API-GATEWAY.md`](SDD-08-API-GATEWAY.md) | Entrada única, roteamento, autenticação e políticas transversais | Aprovado | SDD-03, SDD-04, SDD-05, SDD-06, SDD-07 | Gate A aprovado |
| 09 | [`SDD-09-FRONTEND-ANGULAR.md`](SDD-09-FRONTEND-ANGULAR.md) | Telas, sessão, estado, feedback e integração | Aprovado | SDD-03 a SDD-08 | Gate A aprovado |
| 10 | [`SDD-10-TESTES.md`](SDD-10-TESTES.md) | Estratégia e infraestrutura de testes | Aprovado | SDD-01 a SDD-09 | Gate A aprovado |
| 11 | [`SDD-11-DOCKER-COMPOSE-E-OBSERVABILIDADE.md`](SDD-11-DOCKER-COMPOSE-E-OBSERVABILIDADE.md) | Execução local, saúde, logs e métricas | Aprovado | SDD-01 a SDD-10 | Gate A aprovado |
| 12 | [`SDD-12-DOCUMENTACAO-VIDEO-E-ENTREGA.md`](SDD-12-DOCUMENTACAO-VIDEO-E-ENTREGA.md) | README final, detalhamento técnico, vídeo e entrega | Aprovado | SDD-01 a SDD-11 e SDD-13 | Gate A aprovado |
| 13 | [`SDD-13-QA-E-VALIDACAO-FINAL.md`](SDD-13-QA-E-VALIDACAO-FINAL.md) | Auditoria cruzada, regressão e evidências finais | Aprovado | SDD-01 a SDD-11 para elaboração; SDD-12 para execução final | Gate A aprovado |

---

## 6. Marcadores de qualidade

Cada SDD e sua implementação serão acompanhados pelos marcadores abaixo:

| Marcador | Verificação |
|---|---|
| ESP | SDD aprovado antes do código |
| RAS | Requisitos, regras e critérios rastreáveis |
| ARC | Limites arquiteturais respeitados |
| DOM | Invariantes protegidos no domínio |
| ERR | Erros e falhas tratados conforme especificação |
| SEG | Entradas, dados e segredos tratados com segurança |
| TST | Testes derivados dos critérios de aceite |
| INT | Integrações verificadas com infraestrutura relevante |
| OBS | Logs, correlação e diagnóstico suficientes |
| DOC | Documentação e decisões atualizadas |
| QA | Validação manual e regressão concluídas |

Os marcadores não substituem critérios de aceite. Eles funcionam como gates transversais de qualidade.

---

## 7. Ordem de leitura

### Macroetapa atual

O projeto está na **implementação orientada pelos SDDs**. Os 13 SDDs concluíram o Gate A e a baseline documental foi aprovada em 2026-08-20. Cada SDD ainda exige plano Gate B aprovado antes do código e evidências Gate C antes de ser considerado validado.

Durante esta macroetapa, alterações no repositório ficam limitadas à documentação Markdown e às correções documentais necessárias. O setup validado no SDD-01 permanece preservado.

### Planejamento e decisões

1. `../AGENTS.md`
2. este `README.md`
3. `VISAO-GERAL.md`
4. ADRs aplicáveis
5. `GLOSSARIO.md`
6. `CONVENCOES-CODIGO.md`
7. auditoria documental mais recente
8. `MATRIZ-RASTREABILIDADE.md`

### Implementação de uma fase

1. documentos fundamentais;
2. SDD da fase;
3. SDDs declarados como dependência;
4. código e testes existentes da área afetada.

---

## 8. Regra de atualização

Este índice deve ser atualizado quando:

- um documento for criado;
- seu estado mudar;
- uma aprovação ocorrer;
- uma dependência for alterada;
- um SDD for adicionado, removido ou dividido;
- uma decisão substituir outra.

Nenhum documento pode ser marcado como `Implementado` ou `Validado` sem as evidências exigidas pelo `AGENTS.md`.
