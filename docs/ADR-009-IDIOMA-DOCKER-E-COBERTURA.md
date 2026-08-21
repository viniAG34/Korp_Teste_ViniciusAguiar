# ADR-009 - Idioma, Ambiente Docker e Cobertura de Testes

> Status: Aprovada
> Data: 2026-08-16
> Dependências: ADR-005, ADR-006 e ADR-014
> Atualizada em: 2026-08-17; a política quantitativa foi refinada pelo ADR-014

---

## Decisão

- Identificadores técnicos serão escritos em inglês e terão nomes descritivos.
- Todo desenvolvimento, build, execução, migração e teste utilizará Docker e Docker Compose.
- O projeto terá testes unitários e testes de integração.
- A cobertura mínima obrigatória será de 80% das linhas de código de produção.

---

## Idioma do código

Devem ser escritos em inglês:

- namespaces, assemblies e projetos;
- classes, records, enums e interfaces;
- métodos, propriedades, parâmetros e variáveis;
- endpoints e campos JSON;
- tabelas, colunas, índices e constraints;
- comandos, consultas e eventos de integração;
- nomes de testes.

Nomes devem comunicar intenção. Abreviações não universais, nomes genéricos e traduções literais confusas são proibidos.

A documentação e as mensagens apresentadas ao usuário permanecerão em português. Termos de domínio utilizados no código terão equivalentes ingleses registrados no glossário.

---

## Desenvolvimento Docker-first

O ambiente local não dependerá de SDK, runtime, PostgreSQL ou RabbitMQ instalados diretamente na máquina, além do Docker com Docker Compose.

O Compose deverá suportar build, migrations, infraestrutura, aplicações, testes unitários, testes de integração e coleta de cobertura.

Dockerfiles utilizarão multi-stage build. Imagens finais não carregarão SDK ou ferramentas de desenvolvimento desnecessárias.

---

## Estratégia de testes

### Testes unitários

Validam sem infraestrutura externa:

- invariantes e transições do domínio;
- casos de uso isolados;
- validações;
- tratamento de resultados e falhas controladas.

### Testes de integração

Validam com infraestrutura real no ambiente Docker:

- PostgreSQL;
- Entity Framework Core e migrations;
- constraints e índices;
- concorrência otimista;
- RabbitMQ;
- Outbox e Inbox;
- endpoints HTTP;
- roteamento do Gateway quando aplicável.

Mocks não substituem a infraestrutura nos testes classificados como integração.

---

## Cobertura

- cobertura mínima: 80% de linhas em cada assembly de produção relevante;
- o relatório consolidará os projetos para visualização, sem permitir que a média esconda um assembly abaixo do gate;
- código gerado, migrations e arquivos sem lógica poderão ser excluídos mediante configuração explícita e revisável;
- exclusões não poderão ocultar regra de negócio ou infraestrutura escrita manualmente;
- atingir o percentual não substitui a cobertura dos critérios de aceite;
- todos os fluxos críticos devem possuir testes mesmo quando os gates quantitativos já tiverem sido alcançados;
- branch coverage será reportada e acompanhada, mas o gate inicial de 80% será aplicado a line coverage;
- redução de cobertura exige justificativa e aprovação.

---

## Consequências

- comandos oficiais do projeto serão comandos Docker Compose;
- CI deverá executar o mesmo fluxo utilizado localmente;
- a infraestrutura de teste precisa ser determinística e isolada;
- documentação deverá explicar como executar tudo sem instalar o SDK no host;
- o SDD de testes definirá nomes, fixtures, isolamento e evidências detalhadas.
