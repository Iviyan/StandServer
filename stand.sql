-- Init db by postgres using command line tool psql

create user stand with password 'standserver';

create database stand;

grant all privileges on database stand to stand;

-- Connect to stand database (psql command)
\c stand 

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO stand;

alter default privileges in schema public grant all privileges on tables to stand;
alter default privileges in schema public grant all privileges on sequences to stand;
alter default privileges in schema public grant all privileges on functions to stand;
alter default privileges in schema public grant all privileges on types to stand;
alter default privileges in schema public grant all privileges on routines to stand;


CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE users
(
    id serial PRIMARY KEY,
    login varchar(30) UNIQUE NOT NULL,
    password text NOT NULL,
    is_admin boolean NOT NULL
);

create table telegram_bot_users
(
    telegram_user_id bigint PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id) on delete cascade,
    username varchar(32) NULL
);

CREATE TABLE refresh_tokens
(
    id uuid PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id) on delete cascade,
    device_uid uuid NOT NULL,
    expires timestamptz NOT NULL
);

create index refresh_tokens_device_uid_index on refresh_tokens(device_uid);

CREATE TYPE sample_state AS ENUM ('off', 'work', 'relax');

create table measurements
(
    stand_id smallint not null default 1,
    sample_id int not null,
    time timestamptz not null,
    seconds_from_start int not null,
    duty_cycle smallint not null,
    t smallint not null,
    tu smallint not null,
    i smallint not null,
    period smallint not null,
    work smallint not null,
    relax smallint not null,
    frequency smallint not null,
    state sample_state not null
);

create index measurements_sample_id_idx on measurements (sample_id);
select create_hypertable('measurements', 'time');

create table configuration
(
    key text primary key,
    value text
);

CREATE OR REPLACE FUNCTION get_unique_sample_ids() RETURNS table(sample_id int)
AS $$ BEGIN
    return query
        WITH RECURSIVE t AS (
            (SELECT m.sample_id FROM measurements m ORDER BY sample_id LIMIT 1)
            UNION ALL
            SELECT (SELECT m.sample_id FROM measurements m WHERE m.sample_id > t.sample_id ORDER BY m.sample_id LIMIT 1)
            FROM t
            WHERE t.sample_id IS NOT NULL
        )
        SELECT t.sample_id FROM t WHERE t.sample_id IS NOT NULL;
END $$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_sample_period(p_sample_id int)
    RETURNS table ( "from" timestamptz, "to" timestamptz )
AS $$ BEGIN
    return query select
                     (select time from measurements where sample_id = p_sample_id order by time limit 1) as "from",
                     (select time from measurements where sample_id = p_sample_id order by time desc limit 1) as "to";
END $$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_sample_period()
    RETURNS table ( sample_id int, "from" timestamptz, "to" timestamptz )
AS $$ BEGIN
    return query
        with t as (select * from get_unique_sample_ids())
        select
            (select t.sample_id) as sample_id,
            (select m.time from measurements m where m.sample_id = t.sample_id order by m.time limit 1) as oldest,
            (select m.time from measurements m where m.sample_id = t.sample_id order by m.time desc limit 1) as newest
        from  t;
END $$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_last_measurements(p_count int)
    RETURNS setof measurements
AS $$ BEGIN
    return query
        select stand_id, sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from
            ( select *, ROW_NUMBER() over(partition by sample_id order by time desc) as row from measurements ) as q
        where row <= p_count order by time asc;
END $$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_last_measurements(p_count int, p_sample_ids int[])
    RETURNS setof measurements
AS $$ BEGIN
    return query
        select stand_id, sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from
            (
                select *, ROW_NUMBER() over(partition by sample_id order by time desc) as row from measurements
                where sample_id = any(p_sample_ids)
            ) as q
        where row <= p_count order by time asc;
END $$ LANGUAGE plpgsql;