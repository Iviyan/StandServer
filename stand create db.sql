CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE users
(
    id serial PRIMARY KEY,
    login varchar(30) UNIQUE NOT NULL,
    password varchar(30) NOT NULL
);

CREATE TABLE refresh_tokens
(
    id uuid PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id),
    device_uid uuid,
    expires timestamptz NOT NULL
);

create table measurements
(
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
    state bool not null
);
create index measurements_sample_id_idx on measurements (sample_id);
select create_hypertable('measurements', 'time');

create table state_history
(
    time timestamptz not null,
    state boolean not null
);
select create_hypertable('state_history', 'time');

insert into users(login, password) values('admin', 'admin');

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

CREATE OR REPLACE FUNCTION get_last_measurements(count int)
    RETURNS setof measurements
AS $$ BEGIN
    return query
        select sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from
            ( select *, ROW_NUMBER() over(partition by sample_id order by time desc) as row from measurements ) as q
        where row <= count order by time asc;
END $$ LANGUAGE plpgsql;