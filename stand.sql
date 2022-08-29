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

/*
insert into measurements values (1, '01.01.2022 12:05', 60, 20, 45, 50, 7167, 1000, 50, 10, 10000, true);
select * from measurements order by time;
delete from measurements where sample_id = 90522;
select * from state_history;

SELECT m.sample_id, m.time, m.seconds_from_start, m.duty_cycle, m.t, m.tu, m.i, m.period, m.work, m.relax, m.frequency, m.state
	FROM measurements AS m
	WHERE ((m.sample_id = 90522) AND (m.time >= '01.01.2022')) AND (m.time <= '01.01.2023')
*/

create table state_history
(
    time timestamptz not null,
    state boolean not null
);
select create_hypertable('state_history', 'time');

/*
delete from state_history;
insert into state_history values ('09.06.2022 19:18:00', true), ('22.06.2022 09:36:00',false);

insert into users(login, password) values('admin', 'admin');

select * from measurements where sample_id = 1 and time > '01.01.2022';

select distinct sample_id from measurements;
*/
/* WITH RECURSIVE t AS (
	(SELECT sample_id FROM measurements ORDER BY sample_id LIMIT 1)
	UNION ALL
	SELECT (SELECT sample_id FROM measurements WHERE sample_id > t.sample_id ORDER BY sample_id LIMIT 1)
		FROM t WHERE t.sample_id IS NOT NULL
   )
SELECT sample_id FROM t WHERE sample_id IS NOT NULL; */

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

CREATE OR REPLACE FUNCTION get_last_measurements(count int)
    RETURNS setof measurements
AS $$ BEGIN
    return query
        select sample_id, time, seconds_from_start, duty_cycle, t, tu, i, period, work, relax, frequency, state from
            ( select *, ROW_NUMBER() over(partition by sample_id order by time desc) as row from measurements ) as q
        where row <= count order by time asc;
END $$ LANGUAGE plpgsql;

-- select * from get_last_measurements(5);

-- VACUUM ANALYZE measurements;


