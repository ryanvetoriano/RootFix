// Cada coleção sobe um host próprio; rodar em sequência mantém os testes de rate limiting
// determinísticos e evita disputa de CPU com os demais.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
