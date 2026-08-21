# ADR-005 - Versão do .NET e Estratégia para Legado

> Status: Aprovada
> Data: 2026-08-16

---

## Contexto

Foi considerada a adoção do .NET 6 para demonstrar familiaridade com sistemas C#/.NET mais antigos e evitar uma tecnologia percebida como nova demais.

Durante a análise, foi verificado que o .NET 6 encerrou seu suporte oficial em 12 de novembro de 2024 e teve como último patch a versão 6.0.36. Em agosto de 2026, utilizá-lo em um projeto novo significaria iniciar a solução sobre um runtime sem atualizações de segurança ou suporte técnico.

O desafio é um projeto greenfield e não apresenta código legado, dependência incompatível ou restrição de infraestrutura que exija .NET 6.

Referências oficiais:

- [Política de suporte do .NET](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Política de suporte do .NET e .NET Core](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Versões e ciclo de vida do .NET](https://dotnet.microsoft.com/en-us/download/dotnet)

---

## Decisão

O backend será desenvolvido em **C# com .NET 10 LTS**.

A solução utilizará recursos estáveis e convencionais da plataforma. A adoção do .NET 10 não autoriza dependência desnecessária de APIs novas, experimentais ou sem benefício mensurável para o desafio.

O conhecimento sobre sistemas legados será demonstrado por decisões de arquitetura, manutenibilidade, testes e estratégia de migração, e não pela criação de um sistema novo em uma versão fora de suporte.

---

## Motivos

- .NET 10 é uma versão LTS ativa e adequada para um projeto novo.
- O suporte oficial está previsto até novembro de 2028.
- A plataforma recebe correções de segurança e manutenção.
- Bibliotecas e imagens de container atuais possuem melhor compatibilidade.
- A escolha reduz risco operacional e de avaliação.
- Não existe requisito do desafio que dependa de uma versão antiga.

---

## Alternativa rejeitada - .NET 6

O .NET 6 não foi rejeitado por incapacidade técnica de trabalhar com a versão. Ele foi rejeitado porque:

- está fora de suporte desde novembro de 2024;
- não recebe correções de segurança;
- a Microsoft recomenda migração para versões suportadas;
- o desafio não é uma manutenção de sistema existente;
- sua adoção exigiria justificar um risco sem benefício funcional.

Se o contexto fosse a manutenção de um produto existente, a versão não seria alterada automaticamente. Antes de migrar seriam analisados:

- dependências e compatibilidade;
- mudanças incompatíveis do runtime e framework;
- cobertura de testes e regressão;
- acesso a dados e migrations;
- mensageria e serialização de contratos;
- imagens e ambiente de execução;
- implantação gradual e rollback.

---

## Demonstração de conhecimento de legado

O detalhamento técnico e o vídeo deverão explicar que:

1. um projeto novo deve iniciar em versão suportada;
2. um sistema legado exige diagnóstico antes de qualquer atualização;
3. domínio e aplicação serão mantidos desacoplados de detalhes do framework;
4. contratos e testes reduzem o risco de migração;
5. atualizações de runtime devem ser graduais, verificáveis e reversíveis;
6. recursos exclusivos da versão mais nova só serão usados quando houver justificativa.

---

## Consequências

- Todos os projetos backend terão como target framework o .NET 10.
- ASP.NET Core e bibliotecas integradas deverão ser compatíveis com .NET 10.
- Imagens Docker deverão utilizar tags compatíveis com .NET 10.
- CI, ambiente local e documentação deverão exigir SDK/runtime compatíveis.
- O uso de uma biblioteca sem suporte ao .NET 10 exigirá reavaliação, não rebaixamento automático do runtime.

---

## Regra para os próximos SDDs

Nenhum SDD poderá alterar a versão do .NET ou introduzir dependência exclusiva de runtime antigo sem novo ADR aprovado. O SDD de setup deverá verificar a versão efetiva do SDK, target framework, imagens de container e pipeline.

