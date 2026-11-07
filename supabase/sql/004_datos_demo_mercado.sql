-- Datos de demostración del mercado para una base nueva (sin pares, precios ni órdenes).
-- Crea:
--   1. Todos los pares de monedas activas (origen -> destino).
--   2. Historial de precios para los gráficos: 90 días (diario), 7 días (cada 6 h) y 24 h (cada hora).
--   3. Un usuario "mercado_demo" (no puede iniciar sesión) con billetera y saldos.
--   4. Una orden de compra y una oferta de venta activas en cada par que involucra USD,
--      para que el mercado muestre mejor precio de compra, de venta y margen.
--
-- Convención del backend: en el par origen -> destino, PrecioUnitario = unidades de la moneda
-- de origen por 1 unidad de la moneda de destino (ej. par PEN -> USD: 3.75).
-- Las tasas son valores aproximados de referencia, solo para pruebas.
--
-- Se puede ejecutar más de una vez: cada paso omite lo que ya existe.
-- Ejecutar en Supabase > SQL Editor.

begin;

create temp table tasas (codigo text primary key, por_usd numeric) on commit drop;
insert into tasas (codigo, por_usd) values
  ('USD', 1),      ('EUR', 0.92),   ('GBP', 0.78),   ('CHF', 0.88),   ('JPY', 150),
  ('HKD', 7.8),    ('CAD', 1.37),   ('CNY', 7.2),    ('AUD', 1.52),   ('RUB', 92),
  ('ARS', 1000),   ('BOB', 6.91),   ('BRL', 5.4),    ('CLP', 940),    ('COP', 4000),
  ('CRC', 510),    ('CUP', 24),     ('DOP', 59),     ('GTQ', 7.75),   ('HNL', 24.7),
  ('HTG', 132),    ('MXN', 18),     ('NIO', 36.6),   ('PAB', 1),      ('PEN', 3.75),
  ('PYG', 7400),   ('SRD', 36),     ('UYU', 39.5),   ('VES', 36.5);

-- 1. Pares de monedas
insert into paresmoneda (monedaorigenid, monedadestinoid, activo)
select o.monedaid, d.monedaid, true
from monedas o
join monedas d on d.monedaid <> o.monedaid
where o.activa and d.activa
  and not exists (
    select 1 from paresmoneda p
    where p.monedaorigenid = o.monedaid and p.monedadestinoid = d.monedaid);

-- Precio medio de referencia de cada par
create temp table precio_par on commit drop as
select p.parmonedaid,
       o.codigoiso as origen,
       d.codigoiso as destino,
       t_o.por_usd / t_d.por_usd as medio
from paresmoneda p
join monedas o on o.monedaid = p.monedaorigenid
join monedas d on d.monedaid = p.monedadestinoid
join tasas t_o on t_o.codigo = o.codigoiso
join tasas t_d on t_d.codigo = d.codigoiso;

-- 2. Historial de precios (solo para pares que aún no tienen historial)
insert into historicopreciospar
  (parmonedaid, mayorpreciocompra, menorprecioventa, volumencompra, volumenventa, fecharegistro, snapshotminuto)
select pp.parmonedaid,
       round(pp.medio * f.factor * 0.997, 8),
       round(pp.medio * f.factor * 1.003, 8),
       (50 + abs(hashtext(pp.parmonedaid::text || 'c' || f.ts::text)) % 950)::numeric,
       (50 + abs(hashtext(pp.parmonedaid::text || 'v' || f.ts::text)) % 950)::numeric,
       f.ts,
       date_trunc('minute', f.ts)
from precio_par pp
cross join lateral (
  select s.ts,
         (1 + 0.02 * sin(extract(epoch from s.ts)::float8 / 432000.0 + pp.parmonedaid)
            + 0.005 * sin(extract(epoch from s.ts)::float8 / 3600.0 + pp.parmonedaid))::numeric as factor
  from (
    select generate_series(date_trunc('day', now()) - interval '90 days',
                           date_trunc('day', now()) - interval '8 days',
                           interval '1 day') as ts
    union all
    select generate_series(date_trunc('hour', now()) - interval '7 days',
                           date_trunc('hour', now()) - interval '25 hours',
                           interval '6 hours')
    union all
    select generate_series(date_trunc('hour', now()) - interval '24 hours',
                           date_trunc('hour', now()),
                           interval '1 hour')
  ) s
) f
where not exists (select 1 from historicopreciospar h where h.parmonedaid = pp.parmonedaid);

-- 3. Usuario creador de mercado (su contraseña es un hash aleatorio: nadie puede iniciar sesión con él)
insert into usuarios (rolid, paisid, nombreusuario, correoelectronico, passwordhash,
                      temavisual, estado, fecharegistro)
select r.rolid,
       (select min(paisid) from paises),
       'mercado_demo',
       'mercado.demo@xchang.local',
       md5(random()::text || clock_timestamp()::text),
       'Oscuro', 'Activo', now()
from roles r
where r.nombre = 'USU'
  and not exists (select 1 from usuarios where nombreusuario = 'mercado_demo');

insert into billeteras (usuarioid, fechacreacion)
select u.usuarioid, now()
from usuarios u
where u.nombreusuario = 'mercado_demo'
  and not exists (select 1 from billeteras b where b.usuarioid = u.usuarioid);

-- Saldo equivalente a 1 000 000 USD en cada moneda
insert into saldosbilletera (billeteraid, monedaid, saldodisponible, fechaactualizacion)
select b.billeteraid, m.monedaid, round(1000000 * t.por_usd, 2), now()
from usuarios u
join billeteras b on b.usuarioid = u.usuarioid
join monedas m on m.activa
join tasas t on t.codigo = m.codigoiso
where u.nombreusuario = 'mercado_demo'
  and not exists (
    select 1 from saldosbilletera s
    where s.billeteraid = b.billeteraid and s.monedaid = m.monedaid);

-- 4. Libro de órdenes: compra 0,5 % bajo el precio medio y venta 0,5 % sobre él,
--    por el equivalente a 1 000 USD de la moneda de destino, en los pares con USD.
insert into ordenescompra
  (usuarioid, parmonedaid, cantidadoriginal, cantidadobtenida, cantidadpendiente,
   preciounitario, totalcomprometido, totalejecutado, estado, fechacreacion, fechaactualizacion)
select u.usuarioid, pp.parmonedaid,
       q.cantidad, 0, q.cantidad,
       q.precio, round(q.cantidad * q.precio, 8), 0,
       'Activa', now(), now()
from precio_par pp
join tasas t_d on t_d.codigo = pp.destino
join usuarios u on u.nombreusuario = 'mercado_demo'
cross join lateral (
  select round(1000 * t_d.por_usd, 2) as cantidad, round(pp.medio * 0.995, 8) as precio
) q
where 'USD' in (pp.origen, pp.destino)
  and not exists (
    select 1 from ordenescompra o
    where o.usuarioid = u.usuarioid and o.parmonedaid = pp.parmonedaid);

insert into ofertasventa
  (usuarioid, parmonedaid, cantidadoriginal, cantidadvendida, cantidadpendiente,
   preciounitario, totalesperado, totalrecibido, estado, fechacreacion, fechaactualizacion)
select u.usuarioid, pp.parmonedaid,
       q.cantidad, 0, q.cantidad,
       q.precio, round(q.cantidad * q.precio, 8), 0,
       'Activa', now(), now()
from precio_par pp
join tasas t_d on t_d.codigo = pp.destino
join usuarios u on u.nombreusuario = 'mercado_demo'
cross join lateral (
  select round(1000 * t_d.por_usd, 2) as cantidad, round(pp.medio * 1.005, 8) as precio
) q
where 'USD' in (pp.origen, pp.destino)
  and not exists (
    select 1 from ofertasventa o
    where o.usuarioid = u.usuarioid and o.parmonedaid = pp.parmonedaid);

-- Resumen de lo cargado
select 'paresmoneda' as tabla, count(*) as filas from paresmoneda
union all select 'historicopreciospar', count(*) from historicopreciospar
union all select 'ordenescompra activas', count(*) from ordenescompra where estado = 'Activa'
union all select 'ofertasventa activas', count(*) from ofertasventa where estado = 'Activa';

commit;
