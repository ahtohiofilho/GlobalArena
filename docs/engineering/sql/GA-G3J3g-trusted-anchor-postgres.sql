-- G3-J3g — apply once as a trusted PostgreSQL schema owner.
-- NEVER grant INSERT/UPDATE/DELETE or table ownership to the game service.
-- Bootstrap the initial anchor with a separately authorized admin workflow.
CREATE TABLE IF NOT EXISTS public.ga_trusted_v9_anchor (
    game_id text PRIMARY KEY
        CHECK (length(game_id) BETWEEN 1 AND 128),
    snapshot text NOT NULL
        CHECK (octet_length(snapshot) BETWEEN 1 AND 4096),
    revision bigint NOT NULL DEFAULT 0 CHECK (revision >= 0)
);

-- G3-J3j: one monotonically increasing per-game writer generation.
ALTER TABLE public.ga_trusted_v9_anchor
    ADD COLUMN IF NOT EXISTS writer_epoch bigint NOT NULL DEFAULT 0
        CHECK (writer_epoch >= 0);


CREATE TABLE IF NOT EXISTS public.ga_trusted_v9_anchor_audit (
    audit_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    game_id text NOT NULL,
    previous_snapshot text NOT NULL,
    next_snapshot text NOT NULL,
    logged_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- G3-J3h: the authenticated login is explicitly scoped to game_id.
-- Management of this table is restricted to the trusted DBA. Do not grant
-- UPDATE/INSERT/DELETE/SELECT to any runtime game login.
CREATE TABLE IF NOT EXISTS public.ga_trusted_v9_game_principal (
    game_id text NOT NULL REFERENCES public.ga_trusted_v9_anchor(game_id),
    login_role name NOT NULL,
    PRIMARY KEY (game_id, login_role)
);

-- Read only this game's canonical payload. The caller never sees the table.
CREATE OR REPLACE FUNCTION public.ga_v9_anchor_read(p_game_id text)
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    -- session_user is the authenticated LOGIN, not current_user (the
    -- SECURITY DEFINER schema owner). SET ROLE cannot impersonate a login.
    IF NOT EXISTS (
        SELECT 1 FROM public.ga_trusted_v9_game_principal
         WHERE game_id = p_game_id AND login_role = session_user
    ) THEN
        RAISE EXCEPTION 'Trusted game anchor access denied'
            USING ERRCODE = '42501';
    END IF;
    RETURN (SELECT snapshot FROM public.ga_trusted_v9_anchor
             WHERE game_id = p_game_id);
END;
$$;

-- Single-row linearizable CAS: compare the EXACT old serialized state before
-- replacing it; the row lock/atomic UPDATE serializes competing writers.
-- Audit insertion belongs to the SAME DB transaction as the state update.
CREATE OR REPLACE FUNCTION public.ga_v9_anchor_cas_fenced(
    p_game_id text, p_expected text, p_next text, p_epoch bigint)
RETURNS boolean
LANGUAGE plpgsql
VOLATILE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    old_state jsonb;
    next_state jsonb;
    candidate jsonb;
    current_anchor jsonb;
    changed integer;
BEGIN
    -- Require an explicit per-game ACL for the actual authenticated login,
    -- even if it still holds a legacy EXECUTE grant on this function.
    IF NOT EXISTS (
        SELECT 1 FROM public.ga_trusted_v9_game_principal
         WHERE game_id = p_game_id AND login_role = session_user
    ) THEN
        RAISE EXCEPTION 'Trusted game anchor access denied'
            USING ERRCODE = '42501';
    END IF;
    IF p_epoch IS NULL OR p_epoch < 0 OR
       p_game_id IS NULL OR length(p_game_id) NOT BETWEEN 1 AND 128 OR
       p_expected IS NULL OR p_next IS NULL OR
       octet_length(p_expected) NOT BETWEEN 1 AND 4096 OR
       octet_length(p_next) NOT BETWEEN 1 AND 4096 OR
       p_expected = p_next THEN
        RETURN FALSE;
    END IF;

    old_state := p_expected::jsonb;
    next_state := p_next::jsonb;
    IF jsonb_typeof(old_state) != 'object' OR
       jsonb_typeof(next_state) != 'object' OR
       old_state->'Confirmed' IS NULL OR
       old_state->'Prepared' IS NULL OR
       next_state->'Confirmed' IS NULL OR
       next_state->'Prepared' IS NULL THEN
        RETURN FALSE;
    END IF;

    current_anchor := old_state->'Confirmed';
    IF (current_anchor->>'CheckpointSha256Hex') !~
           '^[0-9A-F]{64}$' OR
       (current_anchor->>'JournalSha256Hex') !~
           '^[0-9A-F]{64}$' OR
       jsonb_typeof(current_anchor->'Sequence') != 'number' OR
       jsonb_typeof(current_anchor->'Turn') != 'number' THEN
        RETURN FALSE;
    END IF;

    IF old_state->'Prepared' = 'null'::jsonb THEN
        -- Prepare: confirmed anchor unchanged, exactly one new prepared turn.
        candidate := next_state #> '{Prepared,After}';
        IF next_state->'Confirmed' != current_anchor OR
           jsonb_typeof(next_state->'Prepared') != 'object' OR
           next_state #> '{Prepared,Before}' != current_anchor OR
           (candidate->>'CheckpointSha256Hex') !~
               '^[0-9A-F]{64}$' OR
           (candidate->>'JournalSha256Hex') !~
               '^[0-9A-F]{64}$' OR
           jsonb_typeof(candidate->'Sequence') != 'number' OR
           jsonb_typeof(candidate->'Turn') != 'number' OR
           (candidate->>'Sequence')::numeric !=
               (current_anchor->>'Sequence')::numeric + 1 OR
           (candidate->>'Turn')::numeric !=
               (current_anchor->>'Turn')::numeric + 1 OR
           candidate->>'CheckpointSha256Hex' =
               current_anchor->>'CheckpointSha256Hex' OR
           candidate->>'JournalSha256Hex' =
               current_anchor->>'JournalSha256Hex' THEN
            RETURN FALSE;
        END IF;
    ELSE
        -- Finalize: only the precise prepared After can become confirmed.
        IF jsonb_typeof(old_state->'Prepared') != 'object' OR
           old_state #> '{Prepared,Before}' != current_anchor OR
           next_state->'Prepared' != 'null'::jsonb OR
           next_state->'Confirmed' !=
               old_state #> '{Prepared,After}' THEN
            RETURN FALSE;
        END IF;
    END IF;

    UPDATE public.ga_trusted_v9_anchor
       SET snapshot = p_next, revision = revision + 1
     WHERE game_id = p_game_id AND snapshot = p_expected
       AND writer_epoch = p_epoch;
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed != 1 THEN
        RETURN FALSE;
    END IF;
    INSERT INTO public.ga_trusted_v9_anchor_audit(
        game_id, previous_snapshot, next_snapshot)
    VALUES (p_game_id, p_expected, p_next);
    RETURN TRUE;
END;
$$;


-- G3-J3k: ADMINISTRATIVE, expected-state CAS rotation. Fails when any
-- prepared transition exists. This function is never granted to gameplay
-- logins: the operator must independently verify physical fencing first.
CREATE OR REPLACE FUNCTION public.ga_v9_anchor_rotate_verified(
    p_game_id text, p_epoch bigint, p_expected_snapshot text)
RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    changed integer;
BEGIN
    IF p_epoch IS NULL OR p_epoch <= 0 OR
       p_epoch = 9223372036854775807 OR
       p_expected_snapshot IS NULL THEN RETURN FALSE; END IF;
    UPDATE public.ga_trusted_v9_anchor
       SET writer_epoch = writer_epoch + 1
     WHERE game_id = p_game_id
       AND writer_epoch = p_epoch
       AND snapshot = p_expected_snapshot
       AND snapshot::jsonb->'Prepared' = 'null'::jsonb;
    GET DIAGNOSTICS changed = ROW_COUNT;
    RETURN changed = 1;
END;
$$;

-- G3-J3j: legacy three-parameter CAS can operate ONLY before an epoch
-- has been claimed. Once an administrator rotates writer_epoch > 0, every
-- legacy writer is fenced even if it still holds a valid database password.
CREATE OR REPLACE FUNCTION public.ga_v9_anchor_cas(
    p_game_id text, p_expected text, p_next text)
RETURNS boolean
LANGUAGE sql
VOLATILE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
    SELECT public.ga_v9_anchor_cas_fenced(
        p_game_id, p_expected, p_next, 0::bigint);
$$;

-- Fenced readers also fail closed on a stale generation. Do not let an
-- old process use a new primary's snapshot to resume a previous session.
CREATE OR REPLACE FUNCTION public.ga_v9_anchor_read_fenced(
    p_game_id text, p_epoch bigint)
RETURNS text
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    IF p_epoch IS NULL OR p_epoch <= 0 OR NOT EXISTS (
        SELECT 1 FROM public.ga_trusted_v9_anchor
         WHERE game_id = p_game_id AND writer_epoch = p_epoch
    ) THEN
        RAISE EXCEPTION 'Trusted v9 writer epoch is stale or absent'
            USING ERRCODE = '42501';
    END IF;
    RETURN public.ga_v9_anchor_read(p_game_id);
END;
$$;

-- SECURITY DEFINER functions are EXECUTE-able by PUBLIC by default.
-- The administrator MUST grant only these functions to the service role.
REVOKE ALL ON TABLE public.ga_trusted_v9_anchor FROM PUBLIC;
REVOKE ALL ON TABLE public.ga_trusted_v9_anchor_audit FROM PUBLIC;
REVOKE ALL ON TABLE public.ga_trusted_v9_game_principal FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_anchor_read(text) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_anchor_cas(text, text, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_anchor_cas_fenced(text, text, text, bigint) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_anchor_read_fenced(text, bigint) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_anchor_rotate_verified(text,bigint,text) FROM PUBLIC;

-- Example, after role creation by a separately trusted DBA:
-- GRANT USAGE ON SCHEMA public TO ga_v9_game_service;
-- GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_read(text)
--     TO ga_v9_game_service;
-- GRANT EXECUTE ON FUNCTION public.ga_v9_anchor_cas(text, text, text)
--     TO ga_v9_game_service;
-- DBA must INSERT (game_id, login_role) into
-- public.ga_trusted_v9_game_principal for each authorized game/login.
-- Do not grant DML on ANY of the three tables to the service.
-- IMPORTANT: re-apply this migration before starting the game service so
-- old unrestricted function definitions are replaced with ACL checks.

-- Trusted DBA (not the game service) performs the per-game rotation AFTER
-- old writers are isolated: UPDATE ... SET writer_epoch = old + 1 WHERE
-- game_id = ... AND writer_epoch = old; verify exactly one row changed.
-- Grant both fenced functions ONLY to the game login with ACL membership.
