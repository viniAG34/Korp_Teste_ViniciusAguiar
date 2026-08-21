# ADR-001 - Escopo da Nota Fiscal Simplificada

> Status: Aprovada
> Data: 2026-08-16
> Observação: a possibilidade de reimpressão descrita neste documento foi substituída pelo ADR-003.

---

## Contexto

Em um ERP completo, o ciclo de mercadorias pode envolver compra de fornecedor, recebimento, entrada em estoque, venda, emissão fiscal, devolução, cancelamento e outras operações comerciais e tributárias.

O desafio técnico, porém, solicita somente:

- cadastro de produtos com saldo disponível;
- criação de notas fiscais com múltiplos produtos e quantidades;
- impressão da nota;
- fechamento da nota após o processamento;
- baixa do saldo dos produtos utilizados.

O enunciado não fornece requisitos para fornecedores, clientes, compras, entradas de mercadoria, preços, valores, impostos ou integração com autoridades fiscais.

---

## Decisão

O sistema implementará exclusivamente a feature de **emissão de nota fiscal simplificada de saída**, representando o trecho do processo em que produtos previamente disponíveis em estoque são incluídos em uma nota e têm seus saldos reduzidos após a emissão.

Fluxo contemplado:

```text
Produto cadastrado com saldo inicial
    -> produto incluído em uma nota de saída
        -> nota emitida e disponibilizada para impressão
            -> estoque baixado
                -> nota fechada
```

O saldo informado no cadastro do produto será considerado o estoque inicial disponível. A origem desse saldo não será modelada.

A nota produzida pelo sistema será um documento interno e simplificado. Ela não representa uma nota fiscal eletrônica com validade jurídica ou integração fiscal externa.

---

## Terminologia no código

Os nomes do domínio seguirão os conceitos presentes no desafio, com identificadores técnicos em inglês conforme o ADR-009:

- `Invoice`: agregado que representa o documento simplificado;
- `InvoiceItem`: produto e quantidade incluídos na nota;
- `InvoiceStatus`: estados `Open` e `Closed`;
- `DeductInvoiceStock`: operação solicitada ao Serviço de Estoque;
- `StockDeductionRequested`: evento ou comando de integração;
- `StockDeductionCompleted`: resultado positivo da operação;
- `StockDeductionRejected`: resultado negativo por regra de negócio;
- `PrintInvoice`: único caso de uso público que inicia a emissão de uma nota aberta, a baixa e o fechamento; o ADR-003 proíbe reimpressão de nota fechada.

A implementação deverá distinguir explicitamente:

- **emissão/fechamento**, que produz efeitos de negócio e baixa o estoque;
- **impressão**, que inicia o processamento exigido pelo desafio e só é permitida para nota aberta.

Os nomes definitivos dos contratos e casos de uso serão validados nos SDDs correspondentes antes da implementação.

---

## Fora do escopo

Não serão implementados nesta feature:

- fornecedores;
- clientes;
- pedidos de compra ou venda;
- entrada de mercadoria;
- nota fiscal de entrada;
- diferentes tipos, modelos ou séries de documentos fiscais;
- preços, descontos ou valores totais;
- impostos ou regras tributárias;
- integração com serviços fiscais externos;
- cancelamento, devolução ou carta de correção;
- contas a pagar ou receber;
- origem contábil do saldo inicial.

Esses itens são reconhecidos como partes possíveis de um ERP real, mas não serão antecipados na modelagem nem gerarão abstrações especulativas.

---

## Consequências

### Positivas

- mantém aderência ao enunciado;
- concentra o esforço em microsserviços, consistência, falhas e estoque;
- evita implementar parcialmente um domínio fiscal muito mais amplo;
- facilita explicar os limites da solução ao avaliador;
- permite evolução posterior sem apresentar a feature como sistema fiscal completo.

### Limitações assumidas

- o estoque inicial não possui documento de entrada associado;
- a nota não identifica cliente ou operação comercial;
- o documento não possui validade fiscal externa;
- o sistema demonstra somente uma etapa do processo de compra, estoque e venda existente em um ERP completo.

---

## Documentação e apresentação em vídeo

O README, o detalhamento técnico e o vídeo deverão informar explicitamente que:

1. a equipe reconhece que um ERP real possui um ciclo comercial e fiscal mais amplo;
2. o desafio foi interpretado como uma feature isolada de saída de estoque;
3. o saldo inicial representa produtos previamente disponíveis, sem modelagem de sua origem;
4. a emissão fecha a nota e provoca a baixa de estoque;
5. uma nota fechada não pode ser impressa novamente;
6. regras fiscais reais foram deixadas de fora conscientemente, e não por desconhecimento.

---

## Regra para os próximos SDDs

Nenhum SDD poderá introduzir compra, fornecedor, cliente, entrada fiscal ou tributação sem uma nova decisão aprovada que altere formalmente este escopo.
