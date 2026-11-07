-- Cancela las órdenes de compra y ofertas de venta activas creadas antes de agosto de 2026
-- (datos de prueba de julio que quedaron en la base) y devuelve el saldo reservado,
-- igual que la cancelación de la app (CancelacionService):
--   * Orden de compra: devuelve TotalComprometido - TotalEjecutado en la moneda de origen.
--   * Oferta de venta: devuelve CantidadPendiente en la moneda de destino.
-- Solo se devuelve saldo si existe el movimiento de reserva original; las filas "espejo"
-- que nunca reservaron saldo solo se cancelan (mismo criterio que 002_desactivar_espejos_fisicos.sql).
-- No toca al usuario mercado_demo.
--
-- Paso 1 (opcional, solo lectura): ver qué se va a cancelar.
--
--   select 'Orden de compra' as tipo, o.ordencompraid as id, u.nombreusuario,
--          mo.codigoiso || ' -> ' || md.codigoiso as par, o.preciounitario,
--          o.totalcomprometido - o.totalejecutado as reembolso, mo.codigoiso as moneda,
--          o.fechacreacion
--   from ordenescompra o
--   join usuarios u on u.usuarioid = o.usuarioid
--   join paresmoneda p on p.parmonedaid = o.parmonedaid
--   join monedas mo on mo.monedaid = p.monedaorigenid
--   join monedas md on md.monedaid = p.monedadestinoid
--   where o.estado in ('Activa', 'Parcialmente ejecutada')
--     and o.fechacreacion < '2026-08-01' and u.nombreusuario <> 'mercado_demo'
--   union all
--   select 'Oferta de venta', v.ofertaventaid, u.nombreusuario,
--          mo.codigoiso || ' -> ' || md.codigoiso, v.preciounitario,
--          v.cantidadpendiente, md.codigoiso, v.fechacreacion
--   from ofertasventa v
--   join usuarios u on u.usuarioid = v.usuarioid
--   join paresmoneda p on p.parmonedaid = v.parmonedaid
--   join monedas mo on mo.monedaid = p.monedaorigenid
--   join monedas md on md.monedaid = p.monedadestinoid
--   where v.estado in ('Activa', 'Parcialmente ejecutada')
--     and v.fechacreacion < '2026-08-01' and u.nombreusuario <> 'mercado_demo'
--   order by 1, 2;
--
-- Paso 2: ejecutar el bloque DO de abajo (todo o nada).

do $$
declare
  r record;
  v_billetera int;
  v_anterior numeric;
  v_canceladas int := 0;
  v_reembolsos int := 0;
begin
  for r in
    select 'OrdenCompra' as tipo, o.ordencompraid as id, o.usuarioid,
           p.monedaorigenid as monedaid,
           o.totalcomprometido - o.totalejecutado as monto,
           exists (select 1 from movimientosbilletera m
                   where m.referenciaid = o.ordencompraid
                     and m.referenciatipo in ('OrdenCompra', 'ordenescompra')) as tiene_reserva
    from ordenescompra o
    join paresmoneda p on p.parmonedaid = o.parmonedaid
    join usuarios u on u.usuarioid = o.usuarioid
    where o.estado in ('Activa', 'Parcialmente ejecutada')
      and o.fechacreacion < '2026-08-01'
      and u.nombreusuario <> 'mercado_demo'
    union all
    select 'OfertaVenta', v.ofertaventaid, v.usuarioid,
           p.monedadestinoid,
           v.cantidadpendiente,
           exists (select 1 from movimientosbilletera m
                   where m.referenciaid = v.ofertaventaid
                     and m.referenciatipo in ('OfertaVenta', 'ofertasventa'))
    from ofertasventa v
    join paresmoneda p on p.parmonedaid = v.parmonedaid
    join usuarios u on u.usuarioid = v.usuarioid
    where v.estado in ('Activa', 'Parcialmente ejecutada')
      and v.fechacreacion < '2026-08-01'
      and u.nombreusuario <> 'mercado_demo'
  loop
    if r.tipo = 'OrdenCompra' then
      update ordenescompra
      set estado = 'Cancelada', cantidadpendiente = 0,
          fechacancelacion = now(), fechaactualizacion = now()
      where ordencompraid = r.id;
    else
      update ofertasventa
      set estado = 'Cancelada', cantidadpendiente = 0,
          fechacancelacion = now(), fechaactualizacion = now()
      where ofertaventaid = r.id;
    end if;
    v_canceladas := v_canceladas + 1;

    if r.tiene_reserva and r.monto > 0 then
      select billeteraid into v_billetera from billeteras where usuarioid = r.usuarioid;

      if v_billetera is null then
        insert into billeteras (usuarioid, fechacreacion)
        values (r.usuarioid, now())
        returning billeteraid into v_billetera;
      end if;

      select saldodisponible into v_anterior
      from saldosbilletera
      where billeteraid = v_billetera and monedaid = r.monedaid
      for update;

      if v_anterior is null then
        v_anterior := 0;
        insert into saldosbilletera (billeteraid, monedaid, saldodisponible, fechaactualizacion)
        values (v_billetera, r.monedaid, r.monto, now());
      else
        update saldosbilletera
        set saldodisponible = saldodisponible + r.monto, fechaactualizacion = now()
        where billeteraid = v_billetera and monedaid = r.monedaid;
      end if;

      insert into movimientosbilletera
        (usuarioid, monedaid, tipomovimiento, monto, saldoanterior, saldoposterior,
         fechamovimiento, referenciatipo, referenciaid)
      values
        (r.usuarioid, r.monedaid, 'Reembolso', r.monto, v_anterior, v_anterior + r.monto,
         now(), 'Cancelacion', null);
      v_reembolsos := v_reembolsos + 1;
    end if;

    v_billetera := null;
    v_anterior := null;
  end loop;

  raise notice 'Canceladas: %, con reembolso de saldo: %', v_canceladas, v_reembolsos;
end $$;

-- Comprobación: no deben quedar órdenes ni ofertas activas anteriores a agosto (sin contar mercado_demo)
select 'ordenescompra activas antiguas' as revision, count(*)
from ordenescompra o join usuarios u on u.usuarioid = o.usuarioid
where o.estado in ('Activa', 'Parcialmente ejecutada') and o.fechacreacion < '2026-08-01'
  and u.nombreusuario <> 'mercado_demo'
union all
select 'ofertasventa activas antiguas', count(*)
from ofertasventa v join usuarios u on u.usuarioid = v.usuarioid
where v.estado in ('Activa', 'Parcialmente ejecutada') and v.fechacreacion < '2026-08-01'
  and u.nombreusuario <> 'mercado_demo';
