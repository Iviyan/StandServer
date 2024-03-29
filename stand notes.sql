-- Init db by postgres

create user stand with password 'standserver';

create database stand;

grant all privileges on database stand to stand;

\c stand -- Connect to stand database (psql)
   
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO stand;

alter default privileges in schema public grant all privileges on tables to stand;
alter default privileges in schema public grant all privileges on sequences to stand;
alter default privileges in schema public grant all privileges on functions to stand;
alter default privileges in schema public grant all privileges on types to stand;
alter default privileges in schema public grant all privileges on routines to stand;

CREATE EXTENSION IF NOT EXISTS timescaledb;

create table users
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

create table refresh_tokens
(
    id uuid PRIMARY KEY,
    user_id integer NOT NULL REFERENCES users (id) on delete cascade,
    device_uid uuid NOT NULL,
    expires timestamptz NOT NULL
);

create index refresh_tokens_device_uid_index on refresh_tokens(device_uid);

/*
select * from refresh_tokens;
delete from refresh_tokens;

insert into refresh_tokens(id, user_id, device_uid, expires)
SELECT gen_random_uuid(), 1, gen_random_uuid(), now() + '2 day'
FROM generate_series(1, 100000) AS t(i);
*/

create type sample_state AS ENUM ('off', 'work', 'relax');

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

/*
insert into measurements values (1, '01.01.2022 12:05', 60, 20, 45, 50, 7167, 1000, 50, 10, 10000, true);
select * from measurements order by time;
delete from measurements where sample_id = 90522;

SELECT m.sample_id, m.time, m.seconds_from_start, m.duty_cycle, m.t, m.tu, m.i, m.period, m.work, m.relax, m.frequency, m.state
	FROM measurements AS m
	WHERE ((m.sample_id = 90522) AND (m.time >= '01.01.2022')) AND (m.time <= '01.01.2023')


insert into users(login, password) values('admin', 'admin');

select * from measurements where sample_id = 1 and time > '01.01.2022';

select distinct sample_id from measurements;

WITH RECURSIVE t AS (
	(SELECT sample_id FROM measurements ORDER BY sample_id LIMIT 1)
	UNION ALL
	SELECT (SELECT sample_id FROM measurements WHERE sample_id > t.sample_id ORDER BY sample_id LIMIT 1)
		FROM t WHERE t.sample_id IS NOT NULL
   )
SELECT sample_id FROM t WHERE sample_id IS NOT NULL; */

create table configuration
(
    key text primary key,
    value text
);

/*
select * from configuration;
insert into configuration values ('Test',null)
on conflict (key) do update set value = excluded.value;
*/

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

-- select get_unique_sample_ids();

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

-- select * from get_sample_period(90522);

/*
select oldest.time as first,
	   newest.time as last  
from
  (select time from measurements where sample_id = 90522 order by time limit 1) as oldest,
  (select time from measurements where sample_id = 90522 order by time desc limit 1) as newest;

select (select time from measurements where sample_id = 90522 order by time limit 1) as first,
	   (select time from measurements where sample_id = 90522 order by time desc limit 1) as last

with t as (select distinct sample_id from measurements)
select 
	  (select time from measurements where sample_id = t.sample_id order by time limit 1) as oldest,
	  (select time from measurements where sample_id = t.sample_id order by time desc limit 1) as newest
from  t;

select sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from
( select *, ROW_NUMBER() OVER(PARTITION BY sample_id ORDER BY time) AS row from measurements ) as a
where row <= 2;
*/

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

-- select * from get_last_measurements(5);
-- select * from get_last_measurements(20, array[80822])

-- VACUUM ANALYZE measurements;

/*
ALTER TABLE measurements ALTER COLUMN state TYPE sample_state
using case
	when state = false then 'off'::sample_state
	when state = true and i > 100 then 'work'::sample_state
	else 'relax'::sample_state
end;
 */

/*
ALTER TABLE refresh_tokens
    DROP CONSTRAINT refresh_tokens_user_id_fkey,
    ADD CONSTRAINT refresh_tokens_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES users (id) ON DELETE CASCADE;
*/

/*
create table measurements_new
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

create index measurements_new_sample_id_idx on measurements_new (sample_id);
select create_hypertable('measurements_new', 'time');

insert into measurements_new select 1 as stand_id, sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from measurements;
*/

/*
select sample_id from measurements where time = (select time from measurements where stand_id = 1 order by time desc limit 1)
union all
select sample_id from measurements where time = (select time from measurements where stand_id = 2 order by time desc limit 1);
*/