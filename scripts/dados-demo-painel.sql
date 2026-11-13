-- =====================================================================
-- RootFix · API .NET · dados de demonstração do painel do gestor
--
-- Roda DEPOIS do scripts/script-bd.sql. Cria uma gestora, dois moradores,
-- uma comunidade e problemas em cada status, para testar o painel pelo
-- Swagger sem depender da API Java.
--
-- Login: SENHA_HASH abaixo NAO e um bcrypt valido, entao estes usuarios
-- NAO entram pelo login do Java. Rodando a API .NET local (Development),
-- gere o token em POST /dev/token com o ID impresso no final. Para contas
-- de demonstracao com login de verdade, cadastre pelo Java
-- (POST /auth/registro/gestor e /auth/registro/morador).
--
-- Os IDs vem da IDENTITY (nada fixo), para nao colidir com o que o Java
-- inserir depois. Rode no SQL Developer com DBMS_OUTPUT ligado.
--
-- Datas no horario de Brasilia (combinado do grupo), calculadas a partir de
-- SYSTIMESTAMP convertido explicitamente: o fuso do servidor Oracle da FIAP
-- nao importa.
-- =====================================================================

SET SERVEROUTPUT ON;

DECLARE
    v_gestora     RF_USUARIO.ID%TYPE;
    v_morador     RF_USUARIO.ID%TYPE;
    v_moradora    RF_USUARIO.ID%TYPE;
    v_comunidade  RF_COMUNIDADE.ID%TYPE;
    v_buraco      RF_PROBLEMA.ID%TYPE;
    v_lampada     RF_PROBLEMA.ID%TYPE;
    v_bueiro      RF_PROBLEMA.ID%TYPE;
    v_calcada     RF_PROBLEMA.ID%TYPE;
    v_lixo        RF_PROBLEMA.ID%TYPE;
    v_agora       TIMESTAMP := CAST(SYSTIMESTAMP AT TIME ZONE 'America/Sao_Paulo' AS TIMESTAMP);

    FUNCTION categoria(p_nome VARCHAR2) RETURN NUMBER IS
        v_id RF_CATEGORIA.ID%TYPE;
    BEGIN
        SELECT ID INTO v_id FROM RF_CATEGORIA WHERE NOME = p_nome;
        RETURN v_id;
    END;
BEGIN
    INSERT INTO RF_USUARIO (NOME, EMAIL, SENHA_HASH, PERFIL, ORGANIZACAO, CRIADO_EM)
    VALUES ('Ana Gestora (demo)', 'demo.gestora@rootfix.local', 'nao-e-bcrypt-use-dev-token', 'GESTOR',
            'Associação de Moradores da Vila Esperança', v_agora)
    RETURNING ID INTO v_gestora;

    INSERT INTO RF_USUARIO (NOME, EMAIL, SENHA_HASH, PERFIL, ENDERECO, LATITUDE, LONGITUDE, CRIADO_EM)
    VALUES ('Carlos Morador (demo)', 'demo.carlos@rootfix.local', 'nao-e-bcrypt-use-dev-token', 'MORADOR',
            'Rua das Flores, 120', -23.5614000, -46.6559000, v_agora)
    RETURNING ID INTO v_morador;

    INSERT INTO RF_USUARIO (NOME, EMAIL, SENHA_HASH, PERFIL, ENDERECO, LATITUDE, LONGITUDE, CRIADO_EM)
    VALUES ('Dora Moradora (demo)', 'demo.dora@rootfix.local', 'nao-e-bcrypt-use-dev-token', 'MORADOR',
            'Rua dos Ipês, 45', -23.5620000, -46.6570000, v_agora)
    RETURNING ID INTO v_moradora;

    INSERT INTO RF_COMUNIDADE (NOME, DESCRICAO, LATITUDE, LONGITUDE, ID_GESTOR, CRIADA_EM)
    VALUES ('Vila Esperança (demo)', 'Comunidade de demonstração do painel', -23.5617000, -46.6562000, v_gestora, v_agora)
    RETURNING ID INTO v_comunidade;

    INSERT INTO RF_PARTICIPACAO (ID_USUARIO, ID_COMUNIDADE, ENTROU_EM) VALUES (v_morador, v_comunidade, v_agora);
    INSERT INTO RF_PARTICIPACAO (ID_USUARIO, ID_COMUNIDADE, ENTROU_EM) VALUES (v_moradora, v_comunidade, v_agora);

    -- PENDENTE, recém-adotado, muito apoiado
    INSERT INTO RF_PROBLEMA (TITULO, DESCRICAO, LATITUDE, LONGITUDE, ID_AUTOR, ID_COMUNIDADE, ID_CATEGORIA,
                             ADOTADO, ADOTADO_EM, STATUS, GRAVIDADE_IA, JUSTIFICATIVA_IA, CRIADO_EM)
    VALUES ('Buraco grande na Rua das Flores', 'Buraco perto do número 120, carros desviando pela calçada.',
            -23.5615000, -46.6558000, v_morador, v_comunidade, categoria('Buraco ou pavimento'),
            1, v_agora - INTERVAL '2' DAY, 'PENDENTE', 'ALTA',
            'Buraco em via de tráfego com risco de acidente.', v_agora - INTERVAL '5' DAY)
    RETURNING ID INTO v_buraco;

    -- EM_ANDAMENTO, com prioridade
    INSERT INTO RF_PROBLEMA (TITULO, DESCRICAO, LATITUDE, LONGITUDE, ID_AUTOR, ID_COMUNIDADE, ID_CATEGORIA,
                             ADOTADO, ADOTADO_EM, STATUS, PRIORIDADE, GRAVIDADE_IA, JUSTIFICATIVA_IA, CRIADO_EM)
    VALUES ('Poste apagado na praça', 'Poste 14 da praça central está apagado há uma semana.',
            -23.5619000, -46.6565000, v_moradora, v_comunidade, categoria('Iluminação pública'),
            1, v_agora - INTERVAL '4' DAY, 'EM_ANDAMENTO', 'URGENTE', 'CRITICA',
            'Praça escura à noite aumenta o risco para pedestres.', v_agora - INTERVAL '7' DAY)
    RETURNING ID INTO v_lampada;

    -- RESOLVIDO, para o tempo médio de resolução
    INSERT INTO RF_PROBLEMA (TITULO, DESCRICAO, LATITUDE, LONGITUDE, ID_AUTOR, ID_COMUNIDADE, ID_CATEGORIA,
                             ADOTADO, ADOTADO_EM, STATUS, PRIORIDADE, GRAVIDADE_IA, CRIADO_EM)
    VALUES ('Bueiro entupido na esquina', 'Água acumula na esquina com a Rua dos Ipês quando chove.',
            -23.5621000, -46.6568000, v_morador, v_comunidade, categoria('Bueiro e drenagem'),
            1, v_agora - INTERVAL '10' DAY, 'RESOLVIDO', 'ALTA', 'MEDIA', v_agora - INTERVAL '12' DAY)
    RETURNING ID INTO v_bueiro;

    -- ARQUIVADO
    INSERT INTO RF_PROBLEMA (TITULO, DESCRICAO, LATITUDE, LONGITUDE, ID_AUTOR, ID_COMUNIDADE, ID_CATEGORIA,
                             ADOTADO, ADOTADO_EM, STATUS, GRAVIDADE_IA, CRIADO_EM)
    VALUES ('Calçada irregular (duplicado)', 'Mesmo trecho já reportado em outro problema.',
            -23.5613000, -46.6561000, v_moradora, v_comunidade, categoria('Calçada e acessibilidade'),
            1, v_agora - INTERVAL '6' DAY, 'ARQUIVADO', 'BAIXA', v_agora - INTERVAL '8' DAY)
    RETURNING ID INTO v_calcada;

    -- Ainda NÃO adotado (fica no feed do Java, não aparece no painel)
    INSERT INTO RF_PROBLEMA (TITULO, DESCRICAO, LATITUDE, LONGITUDE, ID_AUTOR, ID_COMUNIDADE, ID_CATEGORIA,
                             GRAVIDADE_IA, CRIADO_EM)
    VALUES ('Lixo acumulado no terreno baldio', 'Sacos de lixo há dias no terreno da Rua 3.',
            -23.5611000, -46.6566000, v_morador, v_comunidade, categoria('Lixo e limpeza'),
            'MEDIA', v_agora - INTERVAL '1' DAY)
    RETURNING ID INTO v_lixo;

    -- Apoios ("também sofri com isso")
    INSERT INTO RF_APOIO (ID_USUARIO, ID_PROBLEMA, CRIADO_EM) VALUES (v_moradora, v_buraco, v_agora);
    INSERT INTO RF_APOIO (ID_USUARIO, ID_PROBLEMA, CRIADO_EM) VALUES (v_morador, v_lampada, v_agora);
    INSERT INTO RF_APOIO (ID_USUARIO, ID_PROBLEMA, CRIADO_EM) VALUES (v_moradora, v_bueiro, v_agora);
    INSERT INTO RF_APOIO (ID_USUARIO, ID_PROBLEMA, CRIADO_EM) VALUES (v_moradora, v_lixo, v_agora);

    -- Linha do tempo como a API .NET gravaria
    INSERT INTO RF_HISTORICO_STATUS (ID_PROBLEMA, ID_USUARIO, STATUS_ANTERIOR, STATUS_NOVO, OBSERVACAO, DATA_HORA)
    VALUES (v_lampada, v_gestora, 'PENDENTE', 'EM_ANDAMENTO', 'Chamado aberto na concessionária de energia.',
            v_agora - INTERVAL '3' DAY);

    INSERT INTO RF_HISTORICO_STATUS (ID_PROBLEMA, ID_USUARIO, STATUS_ANTERIOR, STATUS_NOVO, OBSERVACAO, DATA_HORA)
    VALUES (v_bueiro, v_gestora, 'PENDENTE', 'EM_ANDAMENTO', 'Equipe de limpeza agendada.',
            v_agora - INTERVAL '9' DAY);
    INSERT INTO RF_HISTORICO_STATUS (ID_PROBLEMA, ID_USUARIO, STATUS_ANTERIOR, STATUS_NOVO, OBSERVACAO, DATA_HORA)
    VALUES (v_bueiro, v_gestora, 'EM_ANDAMENTO', 'RESOLVIDO', 'Bueiro desentupido.',
            v_agora - INTERVAL '8' DAY);

    INSERT INTO RF_HISTORICO_STATUS (ID_PROBLEMA, ID_USUARIO, STATUS_ANTERIOR, STATUS_NOVO, OBSERVACAO, DATA_HORA)
    VALUES (v_calcada, v_gestora, 'PENDENTE', 'ARQUIVADO', 'Duplicado: o trecho já está em outro problema.',
            v_agora - INTERVAL '5' DAY);

    COMMIT;

    DBMS_OUTPUT.PUT_LINE('Gestora (POST /dev/token, perfil GESTOR): ' || v_gestora);
    DBMS_OUTPUT.PUT_LINE('Morador membro (perfil MORADOR):          ' || v_morador);
    DBMS_OUTPUT.PUT_LINE('Comunidade:                               ' || v_comunidade);
    DBMS_OUTPUT.PUT_LINE('Problema PENDENTE (teste o PATCH):        ' || v_buraco);
END;
/
