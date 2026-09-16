(() => {
  'use strict';

  const INTERVALO_SONDEO_MS = 5000;

  const $ = (id) => document.getElementById(id);
  const botonEjecutar = $('btn-ejecutar');
  const botonDiagnostico = $('btn-diagnostico');

  const fecha = (valor) => {
    if (!valor) return '—';
    const d = new Date(valor);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('es-PE');
  };

  const numero = (valor) =>
    valor === null || valor === undefined ? '—' : Number(valor).toLocaleString('es-PE');

  function pintarIndicador(estado) {
    const indicador = $('indicador');
    const clases = ['pastilla'];
    let texto;

    if (estado.ejecucionEnCurso) {
      clases.push('pastilla--activa');
      texto = 'Sincronizando…';
    } else if (estado.ultimoError) {
      clases.push('pastilla--error');
      texto = 'Con errores';
    } else if (estado.ultimaEjecucion) {
      clases.push('pastilla--ok');
      texto = 'Operativo';
    } else {
      clases.push('pastilla--neutra');
      texto = 'En espera';
    }

    indicador.className = clases.join(' ');
    indicador.textContent = texto;
  }

  function pintarEstado(estado) {
    const ultima = estado.ultimaEjecucion;
    const activa = estado.enCurso || ultima;

    $('m-leidos').textContent = numero(activa?.leidosDelDirectorio);
    $('m-validos').textContent = numero(activa?.validos);
    $('m-actualizados').textContent = numero(ultima?.actualizados);
    $('m-yatenian').textContent = numero(ultima?.yaTenianCorreo);
    $('m-sincoincidencia').textContent = numero(ultima?.sinCoincidencia);
    $('m-ambiguos').textContent = numero(ultima?.ambiguos);

    $('d-modo').textContent = estado.modoSimulacion
      ? 'SIMULACION — no se escribe en la tabla de personas'
      : (estado.soloCorreosVacios ? 'Produccion — solo completa correos vacios' : 'Produccion — sobrescribe correos existentes');

    $('d-intervalo').textContent = `cada ${estado.intervaloMinutos} min`;
    $('d-ultima').textContent = ultima ? `${fecha(ultima.inicio)} · ${ultima.estado}` : '—';
    $('d-duracion').textContent = ultima ? `${ultima.duracionSegundos.toFixed(1)} s` : '—';
    $('d-proxima').textContent = fecha(estado.proximaEjecucion);
    $('d-inicio').textContent = fecha(estado.servicioIniciadoEn);
    $('d-directorio').textContent = estado.servidorDirectorio
      ? `${estado.servidorDirectorio} · ${estado.baseDn || 'sin BaseDN'}`
      : '—';
    $('d-mensaje').textContent = ultima?.mensaje || '—';

    const alerta = $('alerta');
    if (estado.ultimoError) {
      alerta.textContent = estado.ultimoError;
      alerta.classList.remove('oculto');
    } else {
      alerta.classList.add('oculto');
    }

    botonEjecutar.disabled = estado.ejecucionEnCurso;
    pintarIndicador(estado);
  }

  function pintarHistorial(filas) {
    const cuerpo = document.querySelector('#tabla-historial tbody');

    if (!filas.length) {
      cuerpo.innerHTML = '<tr><td colspan="9" class="vacio">Sin datos</td></tr>';
      return;
    }

    cuerpo.replaceChildren(...filas.map((fila) => {
      const tr = document.createElement('tr');
      const celdas = [
        fecha(fila.inicio),
        fila.estado ?? '—',
        fila.simulacion ? 'Simulacion' : 'Produccion',
        numero(fila.leidos),
        numero(fila.validos),
        numero(fila.actualizados),
        numero(fila.sinCoincidencia),
        numero(fila.ambiguos),
        fila.mensaje ?? ''
      ];

      celdas.forEach((valor, indice) => {
        const td = document.createElement('td');
        if (indice >= 3 && indice <= 7) td.className = 'num';
        td.textContent = valor;
        tr.appendChild(td);
      });

      return tr;
    }));
  }

  async function refrescar() {
    try {
      const respuesta = await fetch('api/estado', { cache: 'no-store' });
      if (respuesta.ok) pintarEstado(await respuesta.json());
    } catch {
      $('indicador').className = 'pastilla pastilla--error';
      $('indicador').textContent = 'Sin conexion';
    }

    try {
      const respuesta = await fetch('api/historial?top=20', { cache: 'no-store' });
      if (respuesta.ok) pintarHistorial(await respuesta.json());
    } catch {
      // El historial depende de SQL Server; su caida no debe romper el resto del tablero.
    }
  }

  botonEjecutar.addEventListener('click', async () => {
    botonEjecutar.disabled = true;
    try {
      const respuesta = await fetch('api/ejecutar', { method: 'POST' });
      if (!respuesta.ok) {
        const cuerpo = await respuesta.json().catch(() => ({}));
        alert(cuerpo.mensaje || `No se pudo ejecutar (HTTP ${respuesta.status}).`);
      }
    } finally {
      await refrescar();
    }
  });

  botonDiagnostico.addEventListener('click', async () => {
    botonDiagnostico.disabled = true;
    try {
      const respuesta = await fetch('api/diagnostico', { cache: 'no-store' });
      const cuerpo = await respuesta.json();
      alert(
        `Active Directory: ${cuerpo.activeDirectory.ok ? 'OK' : 'ERROR — ' + cuerpo.activeDirectory.error}\n\n` +
        `Base de datos: ${cuerpo.baseDatos.ok ? 'OK' : 'ERROR — ' + cuerpo.baseDatos.error}`
      );
    } catch (error) {
      alert('No se pudo ejecutar el diagnostico: ' + error.message);
    } finally {
      botonDiagnostico.disabled = false;
    }
  });

  refrescar();
  setInterval(refrescar, INTERVALO_SONDEO_MS);
})();
