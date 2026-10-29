-- G3-J3j: run ONLY on a separately administered PostgreSQL service,
-- not either member of the primary/standby anchor cluster.
-- Every game is initialized exactly once by a trusted DBA with the
-- independently authenticated genesis (canonical anchor JSON) and epoch 1.
CREATE TABLE IF NOT EXISTS public.ga_v9_witness (
    game_id text PRIMARY KEY CHECK (length(game_id) BETWEEN 1 AND 128),
    writer_epoch bigint NOT NULL CHECK (writer_epoch > 0),
    acknowledged text NOT NULL CHECK (octet_length(acknowledged) BETWEEN 1 AND 1024),
    revision bigint NOT NULL DEFAULT 0 CHECK (revision >= 0)
);
CREATE TABLE IF NOT EXISTS public.ga_v9_witness_acl (
    game_id text NOT NULL REFERENCES public.ga_v9_witness(game_id),
    login_role name NOT NULL,
    PRIMARY KEY(game_id, login_role)
);
CREATE TABLE IF NOT EXISTS public.ga_v9_witness_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    game_id text NOT NULL,
    event_kind text NOT NULL CHECK (event_kind IN ('ack','rotate')),
    old_epoch bigint NOT NULL,
    new_epoch bigint NOT NULL,
    old_ack text NOT NULL,
    new_ack text NOT NULL,
    logged_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE OR REPLACE FUNCTION public.ga_v9_witness_read(p_game_id text)
RETURNS TABLE(writer_epoch bigint, acknowledged text)
LANGUAGE plpgsql STABLE SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.ga_v9_witness_acl
        WHERE game_id = p_game_id AND login_role = session_user) THEN
        RAISE EXCEPTION 'Independent v9 witness access denied'
            USING ERRCODE = '42501';
    END IF;
    RETURN QUERY SELECT w.writer_epoch, w.acknowledged
        FROM public.ga_v9_witness AS w WHERE w.game_id = p_game_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ga_v9_witness_ack(
    p_game_id text, p_epoch bigint, p_before text, p_after text)
RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    old_anchor jsonb;
    new_anchor jsonb;
    changed integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.ga_v9_witness_acl
        WHERE game_id = p_game_id AND login_role = session_user) THEN
        RAISE EXCEPTION 'Independent v9 witness access denied'
            USING ERRCODE = '42501';
    END IF;
    IF p_epoch IS NULL OR p_epoch <= 0 OR p_before IS NULL OR
       p_after IS NULL OR p_before = p_after OR
       octet_length(p_before) NOT BETWEEN 1 AND 1024 OR
       octet_length(p_after) NOT BETWEEN 1 AND 1024 THEN
        RETURN FALSE;
    END IF;
    old_anchor := p_before::jsonb;
    new_anchor := p_after::jsonb;
    IF jsonb_typeof(old_anchor) != 'object' OR
       jsonb_typeof(new_anchor) != 'object' OR
       (new_anchor->>'CheckpointSha256Hex') !~ '^[0-9A-F]{64}$' OR
       (new_anchor->>'JournalSha256Hex') !~ '^[0-9A-F]{64}$' OR
       jsonb_typeof(new_anchor->'Sequence') != 'number' OR
       jsonb_typeof(new_anchor->'Turn') != 'number' OR
       (new_anchor->>'Sequence')::numeric !=
            (old_anchor->>'Sequence')::numeric + 1 OR
       (new_anchor->>'Turn')::numeric !=
            (old_anchor->>'Turn')::numeric + 1 OR
       new_anchor->>'CheckpointSha256Hex' =
            old_anchor->>'CheckpointSha256Hex' OR
       new_anchor->>'JournalSha256Hex' =
            old_anchor->>'JournalSha256Hex' THEN
        RETURN FALSE;
    END IF;
    UPDATE public.ga_v9_witness
        SET acknowledged = p_after, revision = revision + 1
        WHERE game_id = p_game_id AND writer_epoch = p_epoch
          AND acknowledged = p_before;
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed != 1 THEN RETURN FALSE; END IF;
    INSERT INTO public.ga_v9_witness_audit(
        game_id,event_kind,old_epoch,new_epoch,old_ack,new_ack)
    VALUES (p_game_id,'ack',p_epoch,p_epoch,p_before,p_after);
    RETURN TRUE;
END;
$$;

-- DBA-ONLY function (not GRANTED to game service): rotate one generation.
-- Requires physical fencing of old primary and old application sessions.
CREATE OR REPLACE FUNCTION public.ga_v9_witness_rotate(
    p_game_id text, p_expected_epoch bigint)
RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    changed integer;
    old_ack text;
BEGIN
    IF p_expected_epoch IS NULL OR p_expected_epoch <= 0 OR
       p_expected_epoch = 9223372036854775807 THEN RETURN FALSE; END IF;
    UPDATE public.ga_v9_witness
       SET writer_epoch = writer_epoch + 1, revision = revision + 1
     WHERE game_id = p_game_id AND writer_epoch = p_expected_epoch
     RETURNING acknowledged INTO old_ack;
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed != 1 THEN RETURN FALSE; END IF;
    INSERT INTO public.ga_v9_witness_audit(
        game_id,event_kind,old_epoch,new_epoch,old_ack,new_ack)
    VALUES (p_game_id,'rotate',p_expected_epoch,p_expected_epoch+1,
            old_ack,old_ack);
    RETURN TRUE;
END;
$$;

-- G3-J3k: operator-only CAS rotation also checks the exact acknowledged
-- anchor. This catches an acknowledgment in flight between preflight and
-- witness rotation. The old rotate function remains for existing DBA tools
-- but cannot be executed by application roles.
CREATE OR REPLACE FUNCTION public.ga_v9_witness_rotate_verified(
    p_game_id text, p_epoch bigint, p_expected_ack text)
RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    changed integer;
BEGIN
    IF p_epoch IS NULL OR p_epoch <= 0 OR
       p_epoch = 9223372036854775807 OR
       p_expected_ack IS NULL THEN RETURN FALSE; END IF;
    UPDATE public.ga_v9_witness
       SET writer_epoch = writer_epoch + 1, revision = revision + 1
     WHERE game_id = p_game_id
       AND writer_epoch = p_epoch
       AND acknowledged = p_expected_ack;
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed != 1 THEN RETURN FALSE; END IF;
    INSERT INTO public.ga_v9_witness_audit(
        game_id,event_kind,old_epoch,new_epoch,old_ack,new_ack)
    VALUES (p_game_id,'rotate',p_epoch,p_epoch+1,
            p_expected_ack,p_expected_ack);
    RETURN TRUE;
END;
$$;

REVOKE ALL ON TABLE public.ga_v9_witness FROM PUBLIC;
REVOKE ALL ON TABLE public.ga_v9_witness_acl FROM PUBLIC;
REVOKE ALL ON TABLE public.ga_v9_witness_audit FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_witness_read(text) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_witness_ack(text,bigint,text,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_witness_rotate(text,bigint) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.ga_v9_witness_rotate_verified(text,bigint,text) FROM PUBLIC;
-- The witness game login gets EXECUTE only on read/ack, scoped by ACL.
-- The rotate function is exclusively for an independent DBA/operator.
-- Do NOT grant direct SELECT/INSERT/UPDATE/DELETE to game logins.
