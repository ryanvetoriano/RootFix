-- =====================================================================
-- RootFix · API .NET · ajuste na RF_IDEMPOTENCIA (para o Raul aplicar)
--
-- Use SO se o script-bd.sql oficial ja foi rodado sem a coluna
-- IMPRESSAO_DIGITAL. Quem rodar o scripts/script-bd.sql deste repositorio
-- ja recebe a coluna e nao precisa deste arquivo.
--
-- Por que: a API .NET guarda um hash (SHA-256) de metodo + rota + corpo de
-- cada Idempotency-Key. Sem ele, a mesma chave reaproveitada no mesmo
-- problema com outro status (ex.: EM_ANDAMENTO e depois ARQUIVADO) receberia
-- silenciosamente a resposta da primeira, em vez de 422.
--
-- A tabela so guarda chaves temporarias (validade de 24 h): limpar antes do
-- ALTER nao perde nada e permite a coluna NOT NULL.
-- =====================================================================

DELETE FROM RF_IDEMPOTENCIA;

ALTER TABLE RF_IDEMPOTENCIA ADD (IMPRESSAO_DIGITAL VARCHAR2(64 CHAR) NOT NULL);

COMMIT;
