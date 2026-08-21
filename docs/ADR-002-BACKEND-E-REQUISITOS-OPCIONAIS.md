# ADR-002 - Backend e Requisitos Opcionais Adotados

> Status: Aprovada
> Data: 2026-08-16

---

## Contexto

O desafio permite implementar o backend em Golang ou C# e apresenta concorrência, inteligência artificial e idempotência como requisitos opcionais.

Para reduzir alternativas durante o planejamento e concentrar a qualidade no fluxo distribuído de emissão, é necessário definir a tecnologia do backend e quais diferenciais passam a fazer parte do compromisso do projeto.

---

## Decisão

- Todo o backend será desenvolvido exclusivamente em C#/.NET.
- Tratamento de concorrência será implementado.
- Idempotência será implementada.
- Inteligência artificial não será implementada.

Concorrência e idempotência deixam de ser opcionais para este projeto depois desta decisão. Seus comportamentos e critérios de aceite serão definidos nos SDDs correspondentes.

A versão foi posteriormente definida como .NET 10 LTS no ADR-005. Frameworks complementares, bibliotecas e estratégia interna ainda dependem de decisões posteriores.

---

## Consequências

- O detalhamento técnico deverá informar os frameworks utilizados no C#.
- O uso de LINQ deverá ser documentado com exemplos reais da solução.
- Gerenciamento de dependências em Golang será registrado como não aplicável.
- O cenário concorrente mínimo terá duas notas disputando a última unidade de um produto.
- Requisições e mensagens repetidas não poderão duplicar notas, processos ou baixas de estoque.
- A solução terá maior complexidade de testes e persistência, aceita por proteger o fluxo crítico.

---

## Regra para os próximos SDDs

Nenhum SDD deverá apresentar Golang como alternativa de implementação. Concorrência e idempotência deverão possuir regras, critérios de aceite, testes e evidências explícitas.
